using System.Collections.Generic;

namespace Bindrune.Portals
{
    /// <summary>
    /// The ZDO keys this mod owns, hashed once at load.
    /// <para>
    /// Every key is prefixed <c>bindrune_</c>. That is a functional requirement, not a courtesy:
    /// two mods writing the same key on the same world corrupt each other's state, and unlike a
    /// name collision it fails silently. See DESIGN.md §11.
    /// </para>
    /// <para>
    /// Vanilla caches its own key hashes as statics on <c>ZDOVars</c> for the same reason we do —
    /// <see cref="StringExtensionMethods.GetStableHashCode"/> runs over the whole string, and these
    /// are read in loops over every portal in the world.
    /// </para>
    /// </summary>
    internal static class ZdoKeys
    {
        /// <summary>
        /// Where this portal sends you: our one-way target, as the destination portal's ZDOID.
        /// <para>
        /// Deliberately <em>not</em> vanilla's <c>ConnectionType.Portal</c> connection. The server
        /// rebuilds those from tag matches every five seconds and clears any whose ends disagree,
        /// so a one-way target written there survives seconds at most — see DESIGN.md §12. Keeping
        /// ours separate also leaves vanilla's tag pairing running underneath as the fallback §5
        /// asks for, at no cost: a portal nobody has re-aimed still behaves exactly as it always did.
        /// </para>
        /// <para>
        /// ZDOIDs are stored as a pair of hashes rather than a single one, which is why this is a
        /// <see cref="KeyValuePair{TKey,TValue}"/> and not an int.
        /// </para>
        /// </summary>
        internal static readonly KeyValuePair<int, int> Target = ZDO.GetHashZDOID("bindrune_target");

        /// <summary>
        /// The clearance mask granted to this portal by the anchor bound to it — per-tier flags, not
        /// a level (R1). Written by the server only, mirrored from the anchor onto every portal in
        /// radius, and read by clients that cannot see the bindrunes themselves. Phase 2 fills it;
        /// Phase 1 carries it through the registry so the wire format does not change later.
        /// </summary>
        internal static readonly int ClearanceMask = "bindrune_mask".GetStableHashCode();
    }
}
