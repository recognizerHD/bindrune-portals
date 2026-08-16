using BepInEx.Configuration;

namespace Bindrune.Config
{
    /// <summary>
    /// Which portals an anchor grants its clearance to. See DESIGN.md R2.
    /// <para>
    /// Only the anchor-to-portal step is configurable; wards always have to stand within the
    /// anchor's radius. Both options are resolved from position on the server's sweep, so neither
    /// stores a reference that can go stale when a portal is rebuilt.
    /// </para>
    /// </summary>
    internal enum PortalBinding
    {
        /// <summary>
        /// The single closest portal within the anchor's radius. Re-aiming reaches every destination
        /// from one portal, so a site only ever needs one and there is nothing to disambiguate — and
        /// two portals at one location can carry different clearance.
        /// </summary>
        Nearest,

        /// <summary>
        /// Every portal within the anchor's radius, for a base spread across more than one portal.
        /// </summary>
        AllInRadius,
    }

    /// <summary>Who is allowed to change where a portal points. See DESIGN.md §5.</summary>
    internal enum ReaimPermission
    {
        /// <summary>Any player may re-aim any portal.</summary>
        Anyone,

        /// <summary>
        /// Inside a vanilla ward's protected area, only players that ward permits; outside any ward,
        /// anyone. Reuses the ward's existing permitted-players list rather than inventing a second
        /// access-control system. This is Valheim's ward, not a Bindrune ward stone — ours carry
        /// clearance and have no player list.
        /// </summary>
        WardPermitted,

        /// <summary>Only the player who placed the portal.</summary>
        Builder,

        /// <summary>Admins only.</summary>
        Admin,
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
        private const string SectionCargoPreview = "3 - Cargo preview";
        private const string SectionCompatibility = "4 - Compatibility";

        // -- Travel ----------------------------------------------------------------------------

        // There is no SelectionMode entry: rewire is the only travel model being built. Station is
        // recorded in DESIGN.md §13 as a future idea, and a config switch with one working value is
        // just a trap for whoever flips it.
        internal static ConfigEntry<bool> DiscoveredPortalsOnly { get; private set; }
        internal static ConfigEntry<bool> HidePortalNames { get; private set; }
        internal static ConfigEntry<ReaimPermission> Reaim { get; private set; }

        // -- Clearance -------------------------------------------------------------------------

        // There is no EnforceAtSource entry. Checking the source as well would mean an un-warded
        // outpost could not send ore anywhere, which kills the one-way outpost the whole design is
        // built on (R3). A setting that can switch off the central mechanic is not worth having.
        internal static ConfigEntry<bool> StrictLadder { get; private set; }
        internal static ConfigEntry<float> AnchorRadius { get; private set; }
        internal static ConfigEntry<PortalBinding> Binding { get; private set; }
        internal static ConfigEntry<WardMode> Wards { get; private set; }

        // -- Cargo preview ---------------------------------------------------------------------

        internal static ConfigEntry<bool> ShowBlockedCargoOverlay { get; private set; }
        internal static ConfigEntry<float> CargoPreviewRange { get; private set; }

        // -- Compatibility ---------------------------------------------------------------------

        internal static ConfigEntry<bool> WarnOnConflictingMods { get; private set; }
        internal static ConfigEntry<string> IgnoredConflictGuids { get; private set; }

        internal static void Bind(ConfigFile config)
        {
            DiscoveredPortalsOnly = config.Bind(
                SectionTravel,
                "DiscoveredPortalsOnly",
                false,
                Synced("A portal can only be selected as a destination once you have stood at it."));

            HidePortalNames = config.Bind(
                SectionTravel,
                "HidePortalNames",
                false,
                new ConfigDescription("Hide portal names in the map selector. Local to you."));

            Reaim = config.Bind(
                SectionTravel,
                "ReaimPermission",
                ReaimPermission.Anyone,
                Synced("Who may change where a portal points. Anyone: no restriction. WardPermitted: " +
                       "inside a vanilla ward's area, only players that ward permits. Builder: only " +
                       "whoever placed the portal. Admin: admins only."));

            // -- Clearance ---------------------------------------------------------------------

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
                       "and the range the anchor searches for the portal(s) it grants clearance to (R2).",
                    new AcceptableValueRange<float>(2f, 64f)));

            Binding = config.Bind(
                SectionClearance,
                "PortalBinding",
                PortalBinding.Nearest,
                Synced("Nearest: an anchor grants its clearance to the single closest portal in range. " +
                       "Re-aiming reaches everywhere from one portal, so a site only needs one. " +
                       "AllInRadius: every portal in range, for a base spread across more than one."));

            Wards = config.Bind(
                SectionClearance,
                "WardMode",
                WardMode.Permanent,
                Synced("Permanent: a built ward stands forever (R7). Fuelled: wards consume fuel to " +
                       "hold their clearance, so a large network keeps costing something."));

            // -- Cargo preview -----------------------------------------------------------------

            ShowBlockedCargoOverlay = config.Bind(
                SectionCargoPreview,
                "ShowBlockedCargoOverlay",
                true,
                new ConfigDescription("Mark inventory stacks the nearby portal's destination will refuse. " +
                                      "Purely visual — it reads the tier map, never item data. Local to you."));

            CargoPreviewRange = config.Bind(
                SectionCargoPreview,
                "CargoPreviewRange",
                8f,
                new ConfigDescription("How close to a portal the overlay switches on. Kept short on purpose: " +
                                      "showing it everywhere would paint your ore red all game and teach you " +
                                      "to ignore it. Local to you.",
                    new AcceptableValueRange<float>(2f, 32f)));

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
