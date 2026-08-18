using System;

namespace Bindrune.Tiers
{
    /// <summary>
    /// What a site permits through its portal — independent per-tier flags, not a level (R1).
    /// <para>
    /// A site with an Elder's and a Moder's Bindrune accepts copper and silver and still refuses
    /// iron. Nothing forces you up the ladder in order, which is the point: you build the bindrune
    /// for the haul you actually make, not the one that comes next.
    /// </para>
    /// <para>
    /// Stored as an int on a ZDO and mirrored onto every portal at the site, so the values here are
    /// part of the save format. <b>Never renumber them</b> — a flag's meaning is written into worlds.
    /// </para>
    /// </summary>
    [Flags]
    internal enum Clearance
    {
        /// <summary>No bindrunes. A Wayfarer's Anchor alone grants exactly this (tier 0).</summary>
        None = 0,

        /// <summary>Elder's Bindrune — copper ore and bar, tin ore and bar, bronze.</summary>
        Elder = 1 << 0,

        /// <summary>Bonemass's Bindrune — iron scrap and iron. The rule the whole idea started from.</summary>
        Bonemass = 1 << 1,

        /// <summary>Moder's Bindrune — silver ore, silver, dragon eggs.</summary>
        Moder = 1 << 2,

        /// <summary>Yagluth's Bindrune — black metal scrap and black metal.</summary>
        Yagluth = 1 << 3,

        /// <summary>
        /// Queen's Bindrune — the Mistlands' guarded things. Added after the ObjectDB scan proved the
        /// Mistlands does block resources, which §4 had assumed it did not.
        /// </summary>
        Queen = 1 << 4,

        /// <summary>Ashen Bindrune — flametal, and whatever else the Ashlands blocks.</summary>
        Ashen = 1 << 5,
    }

    internal static class ClearanceExtensions
    {
        /// <summary>
        /// The tier an unrecognised blocked item is assigned to. Deliberately the most restrictive
        /// one: a game update or another mod adding a blocked resource should make Bindrune
        /// conservative, never permissive. Being wrong here means someone cannot carry a new ore
        /// until a line of config is edited; the other way round means the mod silently stops
        /// enforcing the thing it exists to enforce. See DESIGN.md §4.
        /// </summary>
        internal const Clearance Highest = Clearance.Ashen;

        /// <summary>Every tier, for the ladder-complete case.</summary>
        internal const Clearance All = Clearance.Elder | Clearance.Bonemass | Clearance.Moder |
                                       Clearance.Yagluth | Clearance.Queen | Clearance.Ashen;

        /// <summary>Does this mask permit <paramref name="required"/> through?</summary>
        internal static bool Permits(this Clearance mask, Clearance required)
        {
            // None is not a tier any item needs, so an unblocked item is permitted everywhere.
            return required == Clearance.None || (mask & required) == required;
        }

        /// <summary>
        /// The bindrune a player would have to build, named as they would recognise it. Used in
        /// refusals, where naming the missing piece is the whole difference between R6 and vanilla's
        /// "you cannot teleport with that".
        /// </summary>
        internal static string BindruneName(this Clearance tier)
        {
            switch (tier)
            {
                case Clearance.Elder: return "Elder's Bindrune";
                case Clearance.Bonemass: return "Bonemass's Bindrune";
                case Clearance.Moder: return "Moder's Bindrune";
                case Clearance.Yagluth: return "Yagluth's Bindrune";
                case Clearance.Queen: return "Queen's Bindrune";
                case Clearance.Ashen: return "Ashen Bindrune";
                default: return "no bindrune";
            }
        }
    }
}
