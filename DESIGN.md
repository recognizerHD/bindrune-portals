# Bindrune — Design Spec

> A Valheim mod. Travel to any portal by name; what you may **carry** through is decided by the
> ward stones standing at the **destination**, and every ward is bought with a boss trophy.

Status: **design only, no code yet.** Draft 2.

---

## 1. The pitch

Two features, one system.

1. **Any-portal travel.** Interact with a portal, get a list of every portal in the world, pick one, go.
2. **Destination clearance.** Each portal site has a clearance mask built out of physical ward
   stones. When you travel, the game checks the mask of the portal you are *arriving at* against
   what you are carrying.

The second one is the reason the mod exists. Build an Elder's Bindrune at your base and from then
on *every* portal in the world can send copper, tin and bronze **to** your base — and none of them
can receive it back until you go build a ward there too.

That asymmetry is the whole design: **ore flows inward** toward places you have invested in, and
outposts stay cheap, disposable and one-way.

---

## 2. Lineage

| Mod | Nexus | What we take |
|---|---|---|
| AnyPortal (+ XPortal, its maintained rewrite) | [170](https://www.nexusmods.com/valheim/mods/170) / [2239](https://www.nexusmods.com/valheim/mods/2239) | The destination list. Non-negotiable feature. |
| Handy portals | [471](https://www.nexusmods.com/valheim/mods/471) | The **station** interaction model (pick-and-go, nothing persisted) + gamepad support + optional name hiding. |
| Advanced Portals | [2187](https://www.nexusmods.com/valheim/mods/2187) | Per-portal cargo permissions — the *idea*, not the delivery. |
| Immersive Portals | [268](https://www.nexusmods.com/valheim/mods/268) | Seamless no-loading-screen transit. Optional, last phase. |

Note: Immersive Portals is **not** a see-through render of the far side (that's the Minecraft mod of
the same name). It removes the loading screen, preloads the destination and places you facing the
way you walked in, with a configurable fade.

### Prior art that already exists — don't reinvent it

- **XPortal** (GPLv3) already solves registry + sync + panel + map ping for the rewire model.
- **Progression Portals** ([2659](https://www.nexusmods.com/valheim/mods/2659)) and
  **Gate of Ore-thority** already gate blocked items on boss kills, globally per player.
- **ProperPortals / PortalPreload / QuickTeleport** already do loading-screen reduction.

**The gap nobody has filled:** clearance as a property of the *destination*, and as a thing you
*build* and can lose. Lead with that on the mod page; everything else is table stakes.

---

## 3. Rules

| # | Rule |
|---|---|
| **R1** | Each portal carries a **clearance mask** — independent per-tier flags, not a single level. A site with Elder's + Moder's wards accepts copper and silver but still refuses iron. Nothing forces you up the ladder in order. |
| **R2** | A site is defined by a **Wayfarer's Anchor**. Wards must stand within the anchor's radius (default 10 m); the anchor grants its mask to every portal in that radius. Portal hubs upgrade as one — that's what a hub is for. |
| **R3** | Only the **destination** is checked. `EnforceAtSource = false` by default. |
| **R4** | Every ward costs that biome boss's **trophy** plus a little of **the metal it unlocks**. You always earn the shortcut by making the haul the hard way once. |
| **R5** | Trophies are farmable by re-summoning, so the ladder is a **cost curve, not a wall**. |
| **R6** | Refusals **name the reason**: not "you cannot teleport with that" but `Iron cannot enter "Copper Mine" — no Bonemass Bindrune at that site.` |
| **R7** | Wards are **permanent** once built. Consumable-charge mode is a config option, not the default. |

---

## 4. The ward ladder

Costs are **placeholders** — they need real play to settle.

| # | Ward | Cost | Opens | Why that boss |
|---|---|---|---|---|
| 0 | **Wayfarer's Anchor** | Eikthyr trophy · 20 stone · 4 core wood | Nothing. Prerequisite for every ward at the site. | Gives Eikthyr a job; makes founding a warded site deliberate. |
| 1 | **Elder's Bindrune** | Elder trophy · 10 bronze · 20 stone | Copper ore & bar, tin ore & bar, bronze | Black Forest metals. |
| 2 | **Bonemass Bindrune** | Bonemass trophy · 10 iron · 20 stone | Iron scrap, iron | Swamp iron — the rule the idea started from. |
| 3 | **Moder's Bindrune** | Moder trophy · 10 silver · 20 stone | Silver ore, silver, dragon eggs | Mountain hauls are the worst ones; this is the ward people want most. |
| 4 | **Yagluth's Bindrune** | Yagluth trophy · 10 black metal · 20 stone | Black metal scrap, black metal | Plains fulings. |
| 5 | **Ashen Bindrune** | Fader trophy · Queen trophy · 10 flametal · 20 stone | Flametal ore & bar, Ashlands blocked items | The Mistlands has no blocked resources, so the Queen folds in here rather than being skipped. |

### Do not hand-maintain the item list

Build it at runtime:

1. Scan `ObjectDB` for items with `m_shared.m_teleportable == false`.
2. Map known prefab names → tier from a config file.
3. Anything unrecognised → **highest tier**, and **log it by name** so a user can classify it.

That way a game update (Deep North, new content, other mods adding blocked items) never breaks the
mod; it just gets conservative until someone edits one line.

---

## 5. Interaction model — the biggest structural choice

The two reference mods sit on opposite sides of this.

### Station (recommended default)

Handy portals' model. Interact → pick a destination → travel immediately. Per-player, per-trip,
**nothing persisted**.

- No target ZDO field, no rewire RPC, no two players fighting over one portal's destination.
- Each trip is a fresh question, which is exactly the shape a destination-clearance check wants.
- Cost: the network flattens to hub-and-spoke and portals stop being pairs.
- **Upside for this mod specifically:** once travel is one-click, the ward network becomes the
  *only* friction left in the logistics game. That puts the core mechanic at the centre of the mod
  instead of beside it.

### Rewire (config alternative)

AnyPortal / XPortal's model. A target ZDOID stored on the portal, synced to everyone. Portals stay
pairs and vanilla's mental model survives — but it's shared world state, with contention and a
bigger sync story.

Ship `SelectionMode = Station | Rewire`.

### Panel requirements

- **Gamepad navigation from the first commit.** Handy portals binds A to travel; retrofitting Unity
  UI navigation later is miserable.
- Sort/filter: distance, name, favourites, and one **"only destinations that accept my cargo"**
  toggle that most people leave on.
- Per-row **clearance chips** (Cu / Fe / Ag / Bm / Fl) — granted filled, missing dashed — plus a
  verdict column and a footer showing what you're carrying and how many destinations will take it.
- **Map ping** on the selected row. The only way to make sense of fourteen names.
- `DiscoveredPortalsOnly` — a portal appears in your list only once you've stood at it.
- `HidePortalNames` option, as Handy portals has.
- In rewire mode, keep vanilla tag pairing as the fallback when no explicit target is set, so an
  unmodded save behaves normally.

---

## 6. Architecture

| Concern | Approach |
|---|---|
| **Stack** | BepInEx 5 + HarmonyX. Jotunn for custom pieces, localisation, config sync. The anchor and five wards are custom build pieces — exactly `PieceManager`'s job. |
| **Where clearance lives** | An int bitmask on the anchor's ZDO, **mirrored onto every portal ZDO in radius** (`bindrune_mask`). The mirror is **not optional**: a traveling client can read the destination portal's ZDO but cannot see ward pieces 2 km away. |
| **Who computes it** | The **server**. It holds every ZDO, so it recomputes a site's mask on ward place/destroy and on a slow sweep, then writes the portals. Clients never author a mask. This also self-heals staleness when a ward is destroyed while the site is unloaded. |
| **Portal registry** | Server-authoritative list from `ZDOMan.GetPortals()`, pushed to clients on join and on change. |
| **The check** | Prefix on the travel path: resolve destination → read mask → walk `Inventory.GetAllItems()` for `m_shared.m_teleportable == false` → allow, or refuse with a named reason. Suppress vanilla's `Player.IsTeleportable()` via a scoped context flag. |
| **What not to do** | Do **not** flip `m_shared.m_teleportable` on shared item data to let ore through. It's shared state — it leaks into tooltips, other mods, and anything else that asks. Several existing portal mods take that shortcut and it's why they conflict. |
| **Trust model** | Player inventories are client-side in Valheim, so cargo checks are client-trusting — same as vanilla. The server can authoritatively own **clearance**, never **cargo**. Put that in the readme: this is a rule system for a co-op server, not anti-cheat. |
| **Install** | Server **and** every client. Config syncs from the server so tiers can't be edited locally. |
| **Uninstalling** | Extra ZDO keys are harmless to a vanilla client. Custom pieces are not — remove the mod and anchors/wards vanish. Normal for custom-piece mods; warn anyway. |
| **Known conflicts** | Anything that rewrites teleport rules: Valheim Plus, Advanced Portals, Progression Portals, Gate of Ore-thority, unrestricted-portal mods. Detect by GUID at startup and log a loud warning rather than fighting over patches. |

---

## 7. The seamless-transit layer (Phase 4, default off)

One real collision: seamless transit removes the **Interact** moment, and the interact moment is
where the cargo check lives. If you walk in and the destination refuses your ore, there's no dialog
to refuse you in.

Fix — and it improves the base experience too:

- Resolve the destination's mask when the player comes within ~4 m of an open portal, not at transit.
- On refusal: flare the runes red, drop a rune-curtain collider across the doorway, HUD line naming
  the item and the missing ward.
- You get told **before** you commit, which beats vanilla's silent stone wall.

Scope call: ship last, default off, treat as replaceable. Maintained mods already do preloading
well — depend on one rather than owning that code.

Cheap 80% of the *feeling*: a **destination thumbnail** in the panel, snapshotted the last time any
player stood at that portal and cached with the portal record. Phase 3 nicety, not a renderer.

---

## 8. Build order

| # | Phase | Ships | Standalone? |
|---|---|---|---|
| 1 | **Destination list** | Portal registry, server sync, panel with keyboard *and* gamepad nav, map ping. Station mode. | Yes — and it's the must-have. |
| 2 | **Anchors & wards** | Six pieces, the mask, server recompute, the destination check, named refusals. | Yes. The mod's reason to exist. |
| 3 | **Fusion & polish** | Clearance chips in the panel, cargo filter, portal rune tinting by tier, destination thumbnails. | Needs 1 + 2. |
| 4 | **Seamless transit** | Approach-time gating, rune curtain, preload, fade. Default off. | Optional — cut without regret if it fights the game. |

---

## 9. Open decisions

| Question | Lean |
|---|---|
| Station or rewire? | Station default, rewire as a mode. |
| Own the destination list, or build on XPortal (GPLv3, would bind the whole mod)? | Own it under station mode; companion if we go rewire. |
| Independent per-tier flags, or a strict ladder (tier 3 requires 1 + 2)? | Independent flags, ladder as a config switch. |
| Anchor-and-radius, or bind each ward to one portal explicitly? | Anchor + 10 m radius, radius configurable. |
| Does the source ever matter? | Destination-only default, `EnforceAtSource` for the rest. |
| Permanent wards, or an ongoing sink? | Permanent default, optional fuelled mode. |

---

## 10. The main balance risk

The cost curve is the only brake, and trophies are farmable. On a server that has killed Yagluth, a
determined group can ward every site they own in an afternoon of boss re-summons — at which point
Bindrune quietly becomes an unrestricted-portals mod with extra steps.

Two honest answers: make the metal component large enough that warding a site is a real project,
and offer the fuelled mode so the network keeps costing something. Neither needs deciding before
Phase 2, but the ladder numbers are placeholders and this is the part that needs real play.

---

## 11. Licensing

**Recommendation: MIT for the code**, with two carve-outs below.

Why MIT: it's the de-facto default across the Valheim/BepInEx ecosystem, it imposes nothing on
users who bundle the mod into modpacks, and — the real reason — Valheim mods get abandoned
constantly. A permissive licence means that when you stop updating this, someone can legally fork
and keep it alive for the next game patch instead of doing an unlicensed reupload. Apache-2.0 is a
fine alternative if you want an explicit patent grant and a formal attribution requirement; it's
strictly more paperwork for the same practical outcome here.

Two things constrain the choice:

1. **If you fork XPortal, you must ship GPLv3.** XPortal is GPL-3.0, so any derivative is too, and
   that relicenses your whole assembly. This is a strong argument for the station model — writing
   the registry and panel yourself keeps the licence choice in your hands. You can still *read*
   XPortal for approach; copying code is what triggers it.
2. **A code licence does not cover art or game assets.** Keep those separate:
   - **Never commit Valheim's DLLs, publicised assemblies, or ripped meshes/textures** to the repo.
     They're Iron Gate's, redistribution isn't yours to grant, and it's the fastest way to get a
     repo taken down. Reference game assemblies from a local install path via an env var and
     `.gitignore` them.
   - Prefer **cloning existing in-game prefabs at runtime** (a rune stone, a standing stone) for the
     anchor and wards over shipping custom models. Cheaper, always matches the art style, and there
     is nothing to license.
   - If you do ship original models/icons in an asset bundle, license the art separately in the
     README — CC BY 4.0 is the usual pick — because MIT's "software" wording maps badly onto art.

Practical setup: `LICENSE` (MIT, your name), plus a short `NOTICE` or README section reading roughly
*"Code: MIT. Art assets: <licence>. Valheim and its assets are property of Iron Gate Studio; no game
files are redistributed."* Then set the Nexus permission fields and the Thunderstore manifest to
match — a repo that says MIT next to a mod page that says "do not reupload" is the single most
common licensing mistake in this ecosystem.

---

## 12. Facts to verify before trusting them

Every API name below came from mod sources and recollection, **not** from reading the current game
assemblies. Confirm each in a decompiler (ILSpy / dnSpy on `assembly_valheim.dll`) before building
on it:

- `TeleportWorld` — the portal component. Method names and signature of its teleport entry point,
  `Interact`, `GetHoverText`, `HaveTarget`, `TargetFound`, `GetConnectedPortal`.
- `Player.IsTeleportable()`, `Inventory.IsTeleportable()`, `Inventory.GetAllItems()`,
  `ItemDrop.ItemData.m_shared.m_teleportable`, `Player.TeleportTo(...)`.
- `ZDOMan.GetPortals()` — confirmed present via XPortal's source; confirm the signature and whether
  it's server-only.
- `Game.ConnectPortals()` — the server-side tag-matching pass (relevant only in rewire mode).
- ZDO custom-field API (`Set`/`GetInt` and how keys are hashed in the current version).
- Boss trophy prefab names, especially **The Queen** and **Fader** — verify in `ObjectDB`.
- The authoritative non-teleportable item list — get it from the `ObjectDB` scan, not from a wiki
  and not from this document.

Also worth reading directly: [XPortal's source](https://github.com/SpikeHimself/XPortal) (GPLv3 —
read for approach, don't copy code unless you're licensing GPLv3), which uses ZDO keys
`XPortal_TargetId` / `XPortal_PreviousId` and a client `KnownPortalsManager` resynced from the
server.
