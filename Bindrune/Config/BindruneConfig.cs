using System.Collections.Generic;
using BepInEx.Configuration;
using Bindrune.Tiers;

namespace Bindrune.Config
{
    /// <summary>
    /// Which portals an anchor grants its clearance to. See DESIGN.md R2.
    /// <para>
    /// Only the anchor-to-portal step is configurable; bindrunes always have to stand within the
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
        /// Inside a guard stone's protected area, only the players it permits; outside any guard
        /// stone, anyone. Reuses the guard stone's existing permitted-players list rather than
        /// inventing a second access-control system. The guard stone is the vanilla piece the game
        /// labels "Ward" — it is unrelated to bindrunes, which carry clearance and have no player list.
        /// </summary>
        GuardStonePermitted,

        /// <summary>Admins only.</summary>
        Admin,
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
        private const string SectionDiagnostics = "6 - Diagnostics";

        // -- Travel ----------------------------------------------------------------------------

        // There is no SelectionMode entry: rewire is the only travel model being built. Station is
        // recorded in DESIGN.md §13 as a future idea, and a config switch with one working value is
        // just a trap for whoever flips it.
        internal static ConfigEntry<bool> DiscoveredPortalsOnly { get; private set; }
        internal static ConfigEntry<bool> HidePortalNames { get; private set; }
        internal static ConfigEntry<ReaimPermission> Reaim { get; private set; }

        // -- Clearance -------------------------------------------------------------------------

        // There is no EnforceAtSource entry. Checking the source as well would mean an outpost with
        // no bindrunes could not send ore anywhere, which kills the one-way outpost the whole design is
        // built on (R3). A setting that can switch off the central mechanic is not worth having.
        internal static ConfigEntry<bool> StrictLadder { get; private set; }
        internal static ConfigEntry<float> AnchorRadius { get; private set; }
        internal static ConfigEntry<PortalBinding> Binding { get; private set; }

        // Which blocked item belongs to which bindrune. The *list* of blocked items is never
        // configured — it is read from ObjectDB at runtime (§4) — but the mapping has to be, because
        // only a human can decide that a new ore belongs with iron rather than with silver.
        internal static ConfigEntry<string> ElderItems { get; private set; }
        internal static ConfigEntry<string> BonemassItems { get; private set; }
        internal static ConfigEntry<string> ModerItems { get; private set; }
        internal static ConfigEntry<string> YagluthItems { get; private set; }
        internal static ConfigEntry<string> AshenItems { get; private set; }

        /// <summary>The tier lists, paired with the tier they grant. Read by <c>TierMap</c>.</summary>
        internal static IEnumerable<KeyValuePair<Clearance, string>> TierPrefabs()
        {
            yield return new KeyValuePair<Clearance, string>(Clearance.Elder, ElderItems.Value);
            yield return new KeyValuePair<Clearance, string>(Clearance.Bonemass, BonemassItems.Value);
            yield return new KeyValuePair<Clearance, string>(Clearance.Moder, ModerItems.Value);
            yield return new KeyValuePair<Clearance, string>(Clearance.Yagluth, YagluthItems.Value);
            yield return new KeyValuePair<Clearance, string>(Clearance.Ashen, AshenItems.Value);
        }

        // -- Cargo preview ---------------------------------------------------------------------

        internal static ConfigEntry<bool> ShowBlockedCargoOverlay { get; private set; }
        internal static ConfigEntry<float> CargoPreviewRange { get; private set; }

        // -- Compatibility ---------------------------------------------------------------------

        internal static ConfigEntry<bool> WarnOnConflictingMods { get; private set; }
        internal static ConfigEntry<string> IgnoredConflictGuids { get; private set; }

        // -- Diagnostics -------------------------------------------------------------------------

        internal static ConfigEntry<bool> LogNetworkSync { get; private set; }

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
                Synced("Who may change where a portal points. Anyone: no restriction. " +
                       "GuardStonePermitted: inside a guard stone's area, only the players it permits. " +
                       "Admin: admins only."));

            // -- Clearance ---------------------------------------------------------------------

            StrictLadder = config.Bind(
                SectionClearance,
                "StrictLadder",
                false,
                Synced("Require the lower bindrunes before a higher one can be built. Off by default: " +
                       "per-tier flags are independent (R1), so a site can accept silver but refuse iron."));

            AnchorRadius = config.Bind(
                SectionClearance,
                "AnchorRadius",
                10f,
                Synced("Metres from a Wayfarer's Anchor within which bindrunes count toward the site, " +
                       "and the range the anchor searches for the portal(s) it grants clearance to (R2).",
                    new AcceptableValueRange<float>(2f, 64f)));

            Binding = config.Bind(
                SectionClearance,
                "PortalBinding",
                PortalBinding.Nearest,
                Synced("Nearest: an anchor grants its clearance to the single closest portal in range. " +
                       "Re-aiming reaches everywhere from one portal, so a site only needs one. " +
                       "AllInRadius: every portal in range, for a base spread across more than one."));

            // Prefab names, not display names, because prefab names are what ObjectDB is keyed on and
            // what survives a language change. These defaults are a starting guess: whatever they get
            // wrong shows up as an "unclassified" warning naming the prefab, which is the intended way
            // to find out rather than a failure.
            ElderItems = config.Bind(
                SectionClearance,
                "ElderItems",
                "CopperOre,Copper,TinOre,Tin,Bronze,CopperScrap,BronzeScrap,chest_hildir3",
                Synced("Blocked items an Elder's Bindrune permits. Comma-separated prefab names."));

            BonemassItems = config.Bind(
                SectionClearance,
                "BonemassItems",
                "IronScrap,Iron,IronOre,Ironpit",
                Synced("Blocked items a Bonemass's Bindrune permits. Comma-separated prefab names."));

            ModerItems = config.Bind(
                SectionClearance,
                "ModerItems",
                "SilverOre,Silver,DragonEgg,chest_hildir2",
                Synced("Blocked items a Moder's Bindrune permits. Comma-separated prefab names."));

            YagluthItems = config.Bind(
                SectionClearance,
                "YagluthItems",
                "BlackMetalScrap,BlackMetal,chest_hildir1,DvergrNeedle,MechanicalSpring",
                Synced("Blocked items a Yagluth's Bindrune permits. Comma-separated prefab names. " +
                       "Carries the Mistlands' two blocked items as well: the ladder has no Mistlands " +
                       "rune, and the alternative was gating them behind the Ashlands - see DESIGN.md §4."));

            AshenItems = config.Bind(
                SectionClearance,
                "AshenItems",
                "FlametalOre,Flametal,FlametalOreNew,FlametalNew,CharredCogwheel",
                Synced("Blocked items an Ashen Bindrune permits. Comma-separated prefab names. " +
                       "Anything blocked and unlisted lands here anyway, by design."));

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

            // -- Diagnostics -------------------------------------------------------------------

            // Defaults ON while the portal registry is still being proven against a real server.
            // Turn it off before release: it is a development aid, and a shipped mod that narrates
            // itself into everyone's log is a nuisance rather than a help.
            LogNetworkSync = config.Bind(
                SectionDiagnostics,
                "LogNetworkSync",
                true,
                new ConfigDescription("Log every step of the portal registry's sync - sweeps, broadcasts, " +
                                      "joins and receives - so a multiplayer problem can be read off one " +
                                      "log instead of reproduced. Local to you."));
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
