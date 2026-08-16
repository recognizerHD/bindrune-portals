using System.Reflection;
using BepInEx;
using Bindrune.Compat;
using Bindrune.Config;
using Bindrune.Portals;
using HarmonyLib;
using Jotunn.Utils;

namespace Bindrune
{
    /// <summary>
    /// Entry point. Binds config, warns about conflicting mods, and installs Harmony patches.
    /// <para>
    /// The mod must be installed on the server and on every client: the server owns clearance and
    /// computes the masks, and clients need the destination panel and the travel gate. Jotunn
    /// enforces that handshake through <see cref="NetworkCompatibilityAttribute"/>.
    /// </para>
    /// </summary>
    [BepInPlugin(BuildInfo.Guid, BuildInfo.Name, BuildInfo.Version)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal sealed class Plugin : BaseUnityPlugin
    {
        private Harmony _harmony;

        internal static Plugin Instance { get; private set; }

        private void Awake()
        {
            Instance = this;

            BindruneConfig.Bind(base.Config);

            // Before any world exists: Jotunn wires the RPC into its own Game.Start hook, and the
            // join-time synchronisation has to be registered before anyone can join.
            PortalRegistry.Register();

            _harmony = new Harmony(BuildInfo.Guid);
            _harmony.PatchAll(Assembly.GetExecutingAssembly());

            Jotunn.Logger.LogInfo($"{BuildInfo.Name} {BuildInfo.Version} loaded.");
        }

        private void Start()
        {
            // Deliberately not in Awake — see ConflictDetector.WarnAboutKnownConflicts.
            ConflictDetector.WarnAboutKnownConflicts();
        }

        private void OnDestroy()
        {
            _harmony?.UnpatchSelf();
            _harmony = null;
            Instance = null;
        }
    }
}
