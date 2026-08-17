using Bindrune.Portals;
using Bindrune.UI;
using HarmonyLib;
using UnityEngine;

namespace Bindrune.Patches
{
    /// <summary>
    /// Makes a portal honour our one-way target instead of the vanilla connection.
    /// <para>
    /// All three patches are inert on a portal nobody has re-aimed, which is what keeps vanilla tag
    /// pairing working as the fallback (DESIGN.md §5) — an unmodded save behaves normally because we
    /// genuinely do nothing to it.
    /// </para>
    /// <para>
    /// Every one of them takes <c>ZNetView ___m_nview</c> rather than reading
    /// <c>__instance.m_nview</c>. The field is private, and Jotunn's publicised assemblies only make
    /// it look public to the compiler — reading it directly builds fine and then throws
    /// <c>FieldAccessException</c> at runtime on this game build. Harmony's triple-underscore
    /// injection hands us the real field with no access check and no reflection. See DESIGN.md §12.
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(TeleportWorld))]
    internal static class TeleportWorldPatches
    {
        /// <summary>
        /// Interact re-aims the portal (DESIGN.md §5); <b>alt</b>-interact renames it.
        /// <para>
        /// Splitting them that way round costs nothing, because vanilla's alt branch does literally
        /// nothing — it returns immediately. Renaming has to stay reachable rather than being merely
        /// displaced: the selector identifies destinations by name, so a mod that made portals hard
        /// to name would undermine its own map.
        /// </para>
        /// <para>
        /// The alt modifier is <c>AltPlace</c>, which <c>Player.Update</c> reads to build this
        /// argument — <b>Left Shift</b> by default, not the Alt key, and <c>JoyAltKeys</c> on a
        /// gamepad. Worth stating because guessing it wrong looks exactly like a broken patch.
        /// </para>
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(nameof(TeleportWorld.Interact))]
        private static bool InteractReaims(TeleportWorld __instance, Humanoid human, bool alt, ref bool __result)
        {
            // Skipping the original means we owe it a return value: true says "this interaction was
            // handled", and without it the game goes looking for something else to interact with.
            __result = true;

            // Vanilla's own gate on interacting with a portal at all. Ours is about who may re-aim,
            // and applies on top of it rather than instead.
            if (!PrivateArea.CheckAccess(__instance.transform.position, 0f, true, false))
            {
                human?.Message(MessageHud.MessageType.Center, "$piece_noaccess");
                return false;
            }

            if (alt)
            {
                TextInput.instance.RequestText(__instance, "$piece_portal_tag", 10);
                return false;
            }

            DestinationSelector.Open(__instance, human);
            return false;
        }

        /// <summary>
        /// The travel gate. DESIGN.md §6 names this prefix as where the check lives, and Phase 2
        /// puts the destination's clearance mask into it; for now it resolves our target and
        /// otherwise reproduces vanilla's own guards.
        /// <para>
        /// It has to take the whole method rather than nudge it, because vanilla reads the
        /// destination straight out of <c>GetConnectionZDOID(Portal)</c> — the one thing we
        /// deliberately do not write. The guards below are a transcription of vanilla's
        /// <c>Teleport</c> as verified in DESIGN.md §12; if a game update changes them, that section
        /// and this method move together.
        /// </para>
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(nameof(TeleportWorld.Teleport))]
        private static bool TravelToOurTarget(TeleportWorld __instance, Player player, ZNetView ___m_nview)
        {
            if (player == null || ___m_nview == null || !___m_nview.IsValid())
            {
                return true;
            }

            ZDO zdo = ___m_nview.GetZDO();
            long targetPid = PortalTarget.GetDestination(zdo);
            if (targetPid == PortalTarget.NoPid)
            {
                // Never re-aimed. Vanilla's tag pairing owns this portal entirely.
                return true;
            }

            ZoneSystem zones = ZoneSystem.instance;
            if (zones != null)
            {
                if (zones.GetGlobalKey(GlobalKeys.NoPortals))
                {
                    player.Message(MessageHud.MessageType.Center, "$msg_blocked");
                    return false;
                }

                if (zones.GetGlobalKey(GlobalKeys.NoBossPortals) && IsBossActive(zones))
                {
                    player.Message(MessageHud.MessageType.Center, "$msg_blockedbyboss");
                    return false;
                }
            }

            // Phase 2 replaces this with the destination's clearance mask and a refusal that names
            // the resource and the missing bindrune (R6). Until then the vanilla rule stands, and
            // m_allowAllItems is honoured either way — a portal the base game lets everything
            // through must not start refusing cargo because we are in the path.
            if (!__instance.m_allowAllItems && !player.IsTeleportable())
            {
                player.Message(MessageHud.MessageType.Center, "$msg_noteleport");
                return false;
            }

            // The pid survives relogs; the ZDOID it resolves to does not, so the registry is asked
            // fresh every time rather than anything caching the answer.
            if (!PortalRegistry.TryGet(targetPid, out PortalRecord target))
            {
                player.Message(MessageHud.MessageType.Center, "This portal points at somewhere that no longer exists.");
                return false;
            }

            ZDO destination = ZDOMan.instance?.GetZDO(target.Id);
            if (destination == null)
            {
                // Normal, not exceptional: the destination is usually kilometres away and not in
                // this client's ZDO set. Ask for it — the TargetFound patch below is already asking
                // every frame the player stands here, so this is the rare case of walking straight
                // in before it arrived.
                ZDOMan.instance?.RequestZDO(target.Id);
                player.Message(MessageHud.MessageType.Center, "The far side has not answered yet.");
                return false;
            }

            Vector3 position = destination.GetPosition();
            Quaternion rotation = destination.GetRotation();
            Vector3 exit = position + rotation * Vector3.forward * __instance.m_exitDistance + Vector3.up;

            player.TeleportTo(exit, rotation, true);
            Game.instance?.IncrementPlayerStat(PlayerStatType.PortalsUsed, 1f);
            return false;
        }

        /// <summary>
        /// Vanilla blocks portal travel while a boss event is running, or while the world says
        /// bosses are active. Both halves, in vanilla's order.
        /// </summary>
        private static bool IsBossActive(ZoneSystem zones)
        {
            if (RandEventSystem.instance != null && RandEventSystem.instance.GetBossEvent() != null)
            {
                return true;
            }

            return zones.GetGlobalKey(GlobalKeys.activeBosses, out float activeBosses) && activeBosses > 0f;
        }

        /// <summary>
        /// Tells the truth about what the keys now do.
        /// <para>
        /// Vanilla's hover text ends with "[E] Set tag", which stopped being true the moment Interact
        /// started opening the selector. A prompt that names the wrong action is worse than none —
        /// it is the first thing a player reads, and it teaches them something false about a control
        /// scheme this mod has changed out from under them.
        /// </para>
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(nameof(TeleportWorld.GetHoverText))]
        private static void DescribeOurKeys(ref string __result)
        {
            if (string.IsNullOrEmpty(__result))
            {
                return;
            }

            // Vanilla builds this from "$piece_portal_settag" and localises it before we see it, so
            // the line is matched on the localised text rather than the token.
            string settag = Localization.instance.Localize("$piece_portal_settag");
            if (!__result.Contains(settag))
            {
                // Another mod rewrote the hover text, or the token moved. Leave it alone rather than
                // appending a second, contradictory prompt.
                return;
            }

            string use = Localization.instance.Localize("$KEY_Use");

            // Read the modifier rather than naming Shift: it is rebindable, and a prompt that is
            // confidently wrong about a key is exactly what this patch exists to stop.
            string alt = ZInput.instance?.GetBoundKeyString("AltPlace", true);
            if (string.IsNullOrEmpty(alt))
            {
                alt = Localization.instance.Localize("$KEY_AltPlace");
            }

            __result = __result.Replace(settag, Localization.instance.Localize("$bindrune_hover_aim"))
                       + $"\n[<color=yellow><b>{alt}+{use}</b></color>] {settag}";
        }

        /// <summary>
        /// A re-aimed portal has a target whether or not vanilla thinks it is connected. Without
        /// this it would read as unconnected and never light up.
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch("HaveTarget")]
        private static void HaveOurTarget(ref bool __result, ZNetView ___m_nview)
        {
            if (__result || ___m_nview == null || !___m_nview.IsValid())
            {
                return;
            }

            __result = PortalTarget.GetDestination(___m_nview.GetZDO()) != PortalTarget.NoPid;
        }

        /// <summary>
        /// The stricter question: is the destination actually reachable from here yet?
        /// <para>
        /// This runs from <c>UpdatePortal</c> while a player stands nearby, which makes it the right
        /// place to pull a distant destination's ZDO across — the same trick vanilla uses, and the
        /// reason walking into a re-aimed portal usually just works.
        /// </para>
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch("TargetFound")]
        private static void OurTargetFound(ref bool __result, ZNetView ___m_nview)
        {
            if (__result || ___m_nview == null || !___m_nview.IsValid())
            {
                return;
            }

            long targetPid = PortalTarget.GetDestination(___m_nview.GetZDO());
            if (targetPid == PortalTarget.NoPid || !PortalRegistry.TryGet(targetPid, out PortalRecord target))
            {
                return;
            }

            if (ZDOMan.instance?.GetZDO(target.Id) != null)
            {
                __result = true;
                return;
            }

            ZDOMan.instance?.RequestZDO(target.Id);
        }
    }
}
