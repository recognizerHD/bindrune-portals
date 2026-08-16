using System.Collections;
using System.Collections.Generic;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Bindrune.Portals
{
    /// <summary>
    /// Every portal in the world, and the one place the rest of the mod asks about them.
    /// <para>
    /// Server-authoritative by necessity rather than by taste. A client's <c>ZDOMan</c> only holds
    /// the ZDOs near it, so <c>ZDOMan.GetPortals()</c> on a client returns whatever happens to be
    /// loaded — never the world. Only the server has the full set, so the server builds the list and
    /// pushes it; clients only ever read what they were sent. See DESIGN.md §6.
    /// </para>
    /// <para>
    /// On a single-player or client-hosted game the local instance <em>is</em> the server, so the
    /// sweep fills this directly and no packet is ever sent.
    /// </para>
    /// </summary>
    internal static class PortalRegistry
    {
        /// <summary>
        /// Guards against a wire-format change between builds that Jotunn's version check lets
        /// through. <c>VersionStrictness.Minor</c> only pins major.minor, so two patch releases can
        /// meet on one server; a mismatched package is then refused loudly instead of being read as
        /// garbage.
        /// </summary>
        private const byte WireVersion = 2;

        /// <summary>
        /// How often the server rechecks the world for portals placed, destroyed, renamed or
        /// re-aimed. Portals change on a human timescale, so this is deliberately lazy — nothing
        /// waits on it, because whoever changes a portal writes the ZDO themselves and only other
        /// players need telling.
        /// </summary>
        private const float ServerSweepSeconds = 2f;

        private static readonly List<PortalRecord> Ordered = new List<PortalRecord>();
        private static readonly Dictionary<long, PortalRecord> ByPid = new Dictionary<long, PortalRecord>();

        /// <summary>Scratch list for the server sweep, reused so a 2 s timer doesn't allocate forever.</summary>
        private static readonly List<PortalRecord> SweepBuffer = new List<PortalRecord>();

        /// <summary>
        /// Pids handed out during the sweep in progress, so a duplicate is caught the moment it is
        /// seen rather than after both portals have been published.
        /// </summary>
        private static readonly HashSet<long> SweepPids = new HashSet<long>();

        private static CustomRPC _rpc;
        private static Coroutine _sweep;

        /// <summary>
        /// Every known portal. Ordered as the server found them, which is stable enough to page
        /// through and is not meant to be relied on beyond that — sort it for display.
        /// </summary>
        internal static IReadOnlyList<PortalRecord> All => Ordered;

        /// <summary>
        /// Look up a single portal by its permanent id — resolving a stored destination, usually.
        /// This is the step that turns a reference that survives relogs into a ZDOID you can reach
        /// this session.
        /// </summary>
        internal static bool TryGet(long pid, out PortalRecord record) => ByPid.TryGetValue(pid, out record);

        /// <summary>
        /// Registers the sync RPC. Called once from <see cref="Plugin"/>, before any world exists.
        /// </summary>
        internal static void Register()
        {
            _rpc = NetworkManager.Instance.AddRPC("bindrune_portals", OnServerReceive, OnClientReceive);

            // Fires on the server once a client has logged in but before it loads into the world, so
            // a joining player has the full list in hand before they can walk into anything.
            SynchronizationManager.Instance.AddInitialSynchronization(_rpc, BuildSnapshotForJoiningClient);

            CommandManager.Instance.AddConsoleCommand(new PortalRegistryCommand());
            CommandManager.Instance.AddConsoleCommand(new PortalAimCommand());
        }

        /// <summary>Called when a world starts, from the <c>Game.Start</c> patch.</summary>
        internal static void OnWorldStart()
        {
            Clear();

            if (!IsServer())
            {
                // Clients wait to be told. AddInitialSynchronization has already sent, or is about
                // to send, the snapshot for this login.
                return;
            }

            _sweep = Plugin.Instance.StartCoroutine(SweepRoutine());
        }

        /// <summary>Called when a world ends, from the <c>Game.OnDestroy</c> patch.</summary>
        internal static void OnWorldEnd()
        {
            if (_sweep != null)
            {
                // Plugin outlives the world; if it is being torn down too, the coroutine dies with it.
                if (Plugin.Instance != null)
                {
                    Plugin.Instance.StopCoroutine(_sweep);
                }

                _sweep = null;
            }

            Clear();
        }

        private static void Clear()
        {
            Ordered.Clear();
            ByPid.Clear();
            SweepBuffer.Clear();
            SweepPids.Clear();
        }

        private static bool IsServer() => ZNet.instance != null && ZNet.instance.IsServer();

        // -- Server side ---------------------------------------------------------------------------

        private static IEnumerator SweepRoutine()
        {
            var wait = new WaitForSeconds(ServerSweepSeconds);

            while (true)
            {
                if (RebuildFromWorld())
                {
                    Jotunn.Logger.LogDebug($"Portal registry changed: {Ordered.Count} portal(s).");
                    Broadcast();
                }

                yield return wait;
            }
        }

        /// <summary>
        /// Rereads every portal ZDO the server holds. Returns true when the result differs from what
        /// we last published, which is the only thing that puts a packet on the wire.
        /// </summary>
        private static bool RebuildFromWorld()
        {
            SweepBuffer.Clear();
            SweepPids.Clear();

            // The live list, not a copy — read it, never mutate it (DESIGN.md §12).
            List<ZDO> portals = ZDOMan.instance?.GetPortals();
            if (portals == null)
            {
                return false;
            }

            foreach (ZDO zdo in portals)
            {
                if (zdo == null || !zdo.IsValid())
                {
                    continue;
                }

                // The server is the only thing that hands out permanent ids, which is what keeps
                // them unique without any coordination.
                long pid = PortalTarget.EnsurePid(zdo, SweepPids.Contains);
                if (pid == PortalTarget.NoPid)
                {
                    continue;
                }

                SweepPids.Add(pid);

                SweepBuffer.Add(new PortalRecord(
                    pid,
                    zdo.m_uid,
                    zdo.GetString(ZDOVars.s_tag, string.Empty),
                    zdo.GetPosition(),
                    PortalTarget.GetDestination(zdo),
                    zdo.GetInt(ZdoKeys.ClearanceMask, 0)));
            }

            if (Matches(SweepBuffer))
            {
                return false;
            }

            Apply(SweepBuffer);
            return true;
        }

        private static bool Matches(List<PortalRecord> candidate)
        {
            if (candidate.Count != Ordered.Count)
            {
                return false;
            }

            for (int i = 0; i < candidate.Count; i++)
            {
                if (!candidate[i].Equals(Ordered[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static void Broadcast()
        {
            List<ZNetPeer> peers = ZNet.instance?.GetConnectedPeers();
            if (peers == null || peers.Count == 0)
            {
                // Single player, or a dedicated server with nobody on it.
                return;
            }

            _rpc?.SendPackage(peers, BuildSnapshot());
        }

        private static ZPackage BuildSnapshotForJoiningClient()
        {
            // Cheap insurance against the sweep not having run since the last change: the joining
            // client's copy is the one nobody gets to correct until the next change lands.
            RebuildFromWorld();
            return BuildSnapshot();
        }

        private static ZPackage BuildSnapshot()
        {
            var package = new ZPackage();
            package.Write(WireVersion);
            package.Write(Ordered.Count);

            foreach (PortalRecord record in Ordered)
            {
                record.WriteTo(package);
            }

            return package;
        }

        // -- Client side ---------------------------------------------------------------------------

        private static IEnumerator OnClientReceive(long sender, ZPackage package)
        {
            byte version = package.ReadByte();
            if (version != WireVersion)
            {
                Jotunn.Logger.LogError(
                    $"Ignoring a portal list in wire format {version}; this build speaks {WireVersion}. " +
                    "The server and this client are running different Bindrune builds — portal " +
                    "destinations will not work until they match.");
                yield break;
            }

            int count = package.ReadInt();
            var received = new List<PortalRecord>(count);
            for (int i = 0; i < count; i++)
            {
                received.Add(PortalRecord.ReadFrom(package));
            }

            Apply(received);
            Jotunn.Logger.LogDebug($"Portal registry updated from the server: {count} portal(s).");
        }

        private static IEnumerator OnServerReceive(long sender, ZPackage package)
        {
            // Nothing legitimate travels client-to-server on this RPC: the server is the only author
            // of the list, and re-aiming a portal writes the portal's own ZDO rather than asking for
            // the registry to be edited. Jotunn's Initiate() sends an empty package, so an empty one
            // is not worth a word.
            if (package != null && package.Size() > 0)
            {
                Jotunn.Logger.LogWarning($"Discarding an unexpected portal-registry package from peer {sender}.");
            }

            yield break;
        }

        // -- Shared --------------------------------------------------------------------------------

        private static void Apply(List<PortalRecord> records)
        {
            Ordered.Clear();
            ByPid.Clear();

            foreach (PortalRecord record in records)
            {
                Ordered.Add(record);

                // The server re-mints on collision, so duplicates should never reach here. Indexing
                // rather than adding keeps a bad server from taking the client down anyway.
                ByPid[record.Pid] = record;
            }
        }
    }
}
