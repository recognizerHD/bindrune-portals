using System;
using UnityEngine;

namespace Bindrune.Portals
{
    /// <summary>
    /// One portal, as everything outside <see cref="PortalRegistry"/> sees it.
    /// <para>
    /// A record carries the portal's <see cref="ClearanceMask"/> as well as its identity, because a
    /// client standing at portal A needs the mask of A's <em>target</em> — which is normally
    /// kilometres away and not in that client's ZDO set at all. The ZDO mirror alone cannot answer
    /// that question, which is what makes this field the thing the travel gate and the inventory
    /// overlay both rest on. See DESIGN.md §6.
    /// </para>
    /// </summary>
    internal readonly struct PortalRecord : IEquatable<PortalRecord>
    {
        internal PortalRecord(ZDOID id, string name, Vector3 position, ZDOID target, int clearanceMask)
        {
            Id = id;
            Name = name ?? string.Empty;
            Position = position;
            Target = target;
            ClearanceMask = clearanceMask;
        }

        /// <summary>The portal ZDO's id. Stable for the life of the placed piece.</summary>
        internal ZDOID Id { get; }

        /// <summary>The portal's tag — what the player named it. May be empty.</summary>
        internal string Name { get; }

        /// <summary>World position, used to sort by distance and to place the map marker.</summary>
        internal Vector3 Position { get; }

        /// <summary>
        /// Where this portal currently sends you, or <see cref="ZDOID.None"/> if it has never been
        /// re-aimed and is still following vanilla tag pairing. Pointers are one-way: that the
        /// target points back here is a coincidence, not an invariant (DESIGN.md §5).
        /// </summary>
        internal ZDOID Target { get; }

        /// <summary>
        /// Per-tier clearance flags granted at this portal's site. Always zero until Phase 2 builds
        /// anchors and bindrunes.
        /// </summary>
        internal int ClearanceMask { get; }

        internal void WriteTo(ZPackage package)
        {
            package.Write(Id);
            package.Write(Name);
            package.Write(Position);
            package.Write(Target);
            package.Write(ClearanceMask);
        }

        internal static PortalRecord ReadFrom(ZPackage package)
        {
            ZDOID id = package.ReadZDOID();
            string name = package.ReadString();
            Vector3 position = package.ReadVector3();
            ZDOID target = package.ReadZDOID();
            int clearanceMask = package.ReadInt();
            return new PortalRecord(id, name, position, target, clearanceMask);
        }

        /// <summary>
        /// Value equality, used by the server sweep to decide whether anything actually changed and
        /// a broadcast is worth sending.
        /// <para>
        /// Position compares with Unity's approximate <c>==</c> on purpose. A portal does not move,
        /// but its position round-trips through float storage, and an exact comparison would let
        /// last-bit noise report a change every sweep and push the whole list to every client
        /// forever.
        /// </para>
        /// </summary>
        public bool Equals(PortalRecord other)
        {
            return Id == other.Id
                   && Target == other.Target
                   && ClearanceMask == other.ClearanceMask
                   && string.Equals(Name, other.Name, StringComparison.Ordinal)
                   && Position == other.Position;
        }

        public override bool Equals(object obj) => obj is PortalRecord other && Equals(other);

        public override int GetHashCode() => Id.GetHashCode();

        public override string ToString()
        {
            return string.IsNullOrEmpty(Name)
                ? $"(unnamed portal {Id})"
                : $"\"{Name}\" ({Id})";
        }
    }
}
