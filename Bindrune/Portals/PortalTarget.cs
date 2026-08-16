using HarmonyLib;
using UnityEngine;

namespace Bindrune.Portals
{
    /// <summary>
    /// Reads and writes a portal's one-way target — the single piece of state the rewire model
    /// stands on.
    /// <para>
    /// Ours lives in <see cref="ZdoKeys.Target"/> rather than vanilla's portal connection, and
    /// DESIGN.md §12 explains why at length: the server rebuilds every vanilla connection from tag
    /// matches every five seconds and clears the ones whose ends disagree, so a one-way target
    /// written there would not survive a sneeze. The happy side effect is that a portal nobody has
    /// re-aimed still pairs by tag exactly as it always did, which is the vanilla fallback §5 asks
    /// for, delivered by leaving well alone.
    /// </para>
    /// </summary>
    internal static class PortalTarget
    {
        /// <summary>
        /// <c>TeleportWorld.m_nview</c> is private, and Jotunn's publicised assemblies only make it
        /// look otherwise to the compiler. At runtime the real assembly is loaded and this game
        /// build's Mono enforces the access check, so reading the field directly compiles cleanly
        /// and then throws <c>FieldAccessException</c> on every call — see DESIGN.md §12.
        /// <para>
        /// Harmony's field accessor is emitted once and costs about what the field access would
        /// have. Patch methods get the same field injected as a <c>___m_nview</c> parameter instead,
        /// which is cheaper still; this exists for the callers that are not patches.
        /// </para>
        /// </summary>
        private static readonly AccessTools.FieldRef<TeleportWorld, ZNetView> NetView =
            AccessTools.FieldRefAccess<TeleportWorld, ZNetView>("m_nview");

        /// <summary>
        /// Where this portal sends you, or <see cref="ZDOID.None"/> if it has never been re-aimed.
        /// </summary>
        internal static ZDOID Get(ZDO zdo)
        {
            return zdo == null ? ZDOID.None : zdo.GetZDOID(ZdoKeys.Target);
        }

        /// <summary>Convenience for the common case of asking a live portal component.</summary>
        internal static ZDOID Get(TeleportWorld portal) => Get(ZdoOf(portal));

        /// <summary>
        /// The ZDO behind a loaded portal, or null if it has none yet — which happens for a frame or
        /// two after the piece spawns.
        /// </summary>
        internal static ZDO ZdoOf(TeleportWorld portal)
        {
            if (portal == null)
            {
                return null;
            }

            ZNetView view = NetView(portal);
            return view != null && view.IsValid() ? view.GetZDO() : null;
        }

        /// <summary>
        /// Points a portal at another one. Pointers are one-way by design: this writes nothing to
        /// the destination, so B may well still point somewhere else entirely (DESIGN.md §5).
        /// </summary>
        internal static void Set(ZDO zdo, ZDOID target)
        {
            if (zdo == null)
            {
                return;
            }

            Claim(zdo);
            zdo.Set(ZdoKeys.Target, target);
            Publish(zdo);
        }

        /// <summary>
        /// Drops the explicit target, handing the portal back to vanilla tag pairing rather than
        /// leaving it pointing nowhere.
        /// </summary>
        internal static void Clear(ZDO zdo)
        {
            if (zdo == null)
            {
                return;
            }

            Claim(zdo);
            zdo.RemoveZDOID(ZdoKeys.Target);
            Publish(zdo);
        }

        /// <summary>
        /// A ZDO can only be written by whoever owns it. Taking ownership of a portal you are
        /// standing at is ordinary Valheim behaviour — players take ownership of nearby objects
        /// constantly — and re-aiming is deliberate and rare enough that a last-writer-wins race
        /// between two players at the same portal is the contention §5 already accepts, not a bug
        /// to engineer around.
        /// </summary>
        private static void Claim(ZDO zdo)
        {
            if (!zdo.IsOwner())
            {
                zdo.SetOwner(ZDOMan.GetSessionID());
            }
        }

        /// <summary>
        /// Pushes the change out now instead of waiting for the next routine sync. Re-aiming is a
        /// thing a player does and then immediately walks into, so the usual lazy propagation is
        /// exactly the wrong trade here. Vanilla does the same after rewriting portal connections.
        /// </summary>
        private static void Publish(ZDO zdo)
        {
            ZDOMan.instance?.ForceSendZDO(zdo.m_uid);
        }

        /// <summary>
        /// The loaded portal nearest a point, within <paramref name="range"/> metres.
        /// <para>
        /// Only finds portals that are loaded, which is fine for its two callers: you are standing
        /// at the portal you mean. This is also the rule §5 settles on for "the portal you are near"
        /// when two are close together — nearest within range, the same way an anchor picks the
        /// portal it grants clearance to.
        /// </para>
        /// </summary>
        internal static TeleportWorld FindNearest(Vector3 point, float range)
        {
            TeleportWorld nearest = null;
            float nearestDistance = range * range;

            // Unsorted: we are picking the minimum ourselves, so paying for InstanceID ordering
            // would be pure waste.
            foreach (TeleportWorld candidate in Object.FindObjectsByType<TeleportWorld>(FindObjectsSortMode.None))
            {
                if (ZdoOf(candidate) == null)
                {
                    continue;
                }

                float distance = (candidate.transform.position - point).sqrMagnitude;
                if (distance > nearestDistance)
                {
                    continue;
                }

                nearestDistance = distance;
                nearest = candidate;
            }

            return nearest;
        }
    }
}
