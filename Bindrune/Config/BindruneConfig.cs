using BepInEx.Configuration;

namespace Bindrune.Config
{
    /// <summary>How a player picks where a portal sends them. See DESIGN.md §5.</summary>
    internal enum SelectionMode
    {
        /// <summary>
        /// Interact, pick a destination, travel. Per-player, per-trip, nothing persisted —
        /// no target field on the portal and no two players fighting over one portal's target.
        /// </summary>
        Station,

        /// <summary>
        /// Vanilla's mental model: a portal is aimed at another portal and stays that way for
        /// everyone. Shared world state, so it carries contention and a bigger sync story.
        /// </summary>
        Rewire,
    }

    /// <summary>Whether a built ward stays built. See DESIGN.md R7.</summary>
    internal enum WardMode
    {
        /// <summary>Built once, stands forever.</summary>
        Permanent,

        /// <summary>Consumes fuel to keep its clearance, so the network keeps costing something.</summary>
        Fuelled,
    }

    /// <summary>
    /// Every config entry the mod owns, bound once from <see cref="Plugin"/>.
    /// <para>
    /// Entries marked synced are admin-only: Jotunn pushes the server's value to every client and
    /// locks the local one, so tiers and clearance rules cannot be edited client-side. Entries that
    /// only change what a player sees stay local, because forcing a display preference across a
    /// server is rude and pointless.
    /// </para>
    /// </summary>
    internal static class BindruneConfig
    {
        private const string SectionTravel = "1 - Travel";
        private const string SectionClearance = "2 - Clearance";
        private const string SectionCompatibility = "3 - Compatibility";

        // -- Travel ----------------------------------------------------------------------------

        internal static ConfigEntry<SelectionMode> Selection { get; private set; }
        internal static ConfigEntry<bool> DiscoveredPortalsOnly { get; private set; }
        internal static ConfigEntry<bool> HidePortalNames { get; private set; }

        // -- Clearance -------------------------------------------------------------------------

        internal static ConfigEntry<bool> EnforceAtSource { get; private set; }
        internal static ConfigEntry<bool> StrictLadder { get; private set; }
        internal static ConfigEntry<float> AnchorRadius { get; private set; }
        internal static ConfigEntry<WardMode> Wards { get; private set; }

        // -- Compatibility ---------------------------------------------------------------------

        internal static ConfigEntry<bool> WarnOnConflictingMods { get; private set; }
        internal static ConfigEntry<string> IgnoredConflictGuids { get; private set; }

        internal static void Bind(ConfigFile config)
        {
            Selection = config.Bind(
                SectionTravel,
                "SelectionMode",
                SelectionMode.Station,
                Synced("Station: pick a destination each time you use a portal, nothing is stored. " +
                       "Rewire: aim a portal at another portal for everyone, vanilla-style."));

            DiscoveredPortalsOnly = config.Bind(
                SectionTravel,
                "DiscoveredPortalsOnly",
                false,
                Synced("A portal only appears in your destination list once you have stood at it."));

            HidePortalNames = config.Bind(
                SectionTravel,
                "HidePortalNames",
                false,
                new ConfigDescription("Hide portal names in the destination list. Local to you."));

            // -- Clearance ---------------------------------------------------------------------

            EnforceAtSource = config.Bind(
                SectionClearance,
                "EnforceAtSource",
                false,
                Synced("Also check the clearance of the portal you are leaving from. Off by default: " +
                       "the whole design is that clearance belongs to the destination (R3), so ore " +
                       "flows inward toward sites you have invested in."));

            StrictLadder = config.Bind(
                SectionClearance,
                "StrictLadder",
                false,
                Synced("Require the lower wards before a higher one can be built. Off by default: " +
                       "per-tier flags are independent (R1), so a site can accept silver but refuse iron."));

            AnchorRadius = config.Bind(
                SectionClearance,
                "AnchorRadius",
                10f,
                Synced("Metres from a Wayfarer's Anchor within which ward stones count toward the site, " +
                       "and within which portals inherit the site's clearance (R2).",
                    new AcceptableValueRange<float>(2f, 64f)));

            Wards = config.Bind(
                SectionClearance,
                "WardMode",
                WardMode.Permanent,
                Synced("Permanent: a built ward stands forever (R7). Fuelled: wards consume fuel to " +
                       "hold their clearance, so a large network keeps costing something."));

            // -- Compatibility -----------------------------------------------------------------

            WarnOnConflictingMods = config.Bind(
                SectionCompatibility,
                "WarnOnConflictingMods",
                true,
                new ConfigDescription("Log a warning at startup when another installed mod also rewrites " +
                                      "portal or teleport rules. Local to you."));

            IgnoredConflictGuids = config.Bind(
                SectionCompatibility,
                "IgnoredConflictGuids",
                string.Empty,
                new ConfigDescription("Comma-separated plugin GUIDs to leave out of the conflict warning, " +
                                      "for when the check flags something harmless. Local to you."));
        }

        /// <summary>
        /// Marks an entry admin-only, which is what makes Jotunn synchronise it from the server and
        /// lock the client's copy.
        /// </summary>
        private static ConfigDescription Synced(string description, AcceptableValueBase acceptableValues = null)
        {
            return new ConfigDescription(
                description,
                acceptableValues,
                new ConfigurationManagerAttributes { IsAdminOnly = true });
        }
    }
}
