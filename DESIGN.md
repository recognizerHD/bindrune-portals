# Bindrune — Design Spec

> A Valheim mod. Travel to any portal by name; what you may **carry** through is decided by the
> bindrunes standing at the **destination**, and every bindrune is bought with a boss trophy.

Status: **Draft 2.** Scaffold, config and conflict detection exist; no portal or bindrune behaviour yet.
Phase 1 not started.

---

## 1. The pitch

Two features, one system.

1. **Any-portal travel.** Interact with a portal, pick any portal in the world off the map, and it
   points there — for everyone — until someone re-aims it. Walking in travels.
2. **Destination clearance.** Each portal site has a clearance mask built out of physical
   bindrunes. When you travel, the game checks the mask of the portal you are *arriving at* against
   what you are carrying.

The second one is the reason the mod exists. Build an Elder's Bindrune at your base and from then
on *every* portal in the world can send copper, tin and bronze **to** your base — and none of them
can receive it back until you go build a bindrune there too.

That asymmetry is the whole design: **ore flows inward** toward places you have invested in, and
outposts stay cheap, disposable and one-way.

### Words, used precisely

The word *ward* is banned from this document, because Valheim already has a piece called Ward and the
collision made everything ambiguous.

| Term | Means |
|---|---|
| **Bindrune** | One of our five clearance pieces — Elder's, Bonemass's, Moder's, Yagluth's, Ashen. Built from a boss trophy plus the metal it unlocks. |
| **Wayfarer's Anchor** | The tier-0 piece that defines a site. Not itself a bindrune: it opens nothing and only marks where a site is. |
| **Site** | An anchor, the bindrunes standing around it, and the portal it binds to. |
| **Clearance mask** | The per-tier flags a site grants, mirrored onto its portal. |
| **Guard stone** | Valheim's *vanilla* piece — the one the game labels "Ward". Ours never touch it, except that `ReaimPermission` can read its permitted-players list (§5). |

---

## 2. Lineage

| Mod | Nexus | What we take |
|---|---|---|
| AnyPortal (+ XPortal, its maintained rewrite) | [170](https://www.nexusmods.com/valheim/mods/170) / [2239](https://www.nexusmods.com/valheim/mods/2239) | The destination list. Non-negotiable feature. |
| Handy portals | [471](https://www.nexusmods.com/valheim/mods/471) | Gamepad support and optional name hiding. Its station interaction model is *not* what we build — see §5, and §13 for why it was set aside. |
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
| **R1** | Each portal carries a **clearance mask** — independent per-tier flags, not a single level. A site with Elder's + Moder's bindrunes accepts copper and silver but still refuses iron. Nothing forces you up the ladder in order. |
| **R2** | A site is defined by a **Wayfarer's Anchor**. Two separate relationships, and only the second is configurable: bindrunes must stand within the anchor's radius (default 10 m), and the anchor grants its mask to the **nearest portal** within that radius. `PortalBinding = AllInRadius` instead grants it to every portal in range, for a base spread across two portals. |
| **R3** | Only the **destination** is checked — always, with no setting to change it. You need a bindrune at every site you want to send resources *to*; where you set out from is never asked about. |
| **R4** | Every bindrune costs that biome boss's **trophy** plus a little of **the metal it unlocks**. You always earn the shortcut by making the haul the hard way once. |
| **R5** | Trophies are farmable by re-summoning, so the ladder is a **cost curve, not a wall**. |
| **R6** | Refusals **name the reason**: not "you cannot teleport with that" but `Iron cannot enter "Copper Mine" — no Bonemass Bindrune at that site.` |
| **R7** | Bindrunes are **permanent** once built. There is no fuelled or consumable mode: the gate is the boss kill, not upkeep. |

---

## 4. The bindrune ladder

Costs are **placeholders** — they need real play to settle.

| # | Piece | Cost | Opens | Why that boss |
|---|---|---|---|---|
| 0 | **Wayfarer's Anchor** | Eikthyr trophy · 20 stone · 4 core wood | Nothing. Prerequisite for every bindrune at the site. | Gives Eikthyr a job; makes founding a site deliberate. |
| 1 | **Elder's Bindrune** | Elder trophy · 10 bronze · 20 stone | Copper ore & bar, tin ore & bar, bronze | Black Forest metals. |
| 2 | **Bonemass Bindrune** | Bonemass trophy · 10 iron · 20 stone | Iron scrap, iron | Swamp iron — the rule the idea started from. |
| 3 | **Moder's Bindrune** | Moder trophy · 10 silver · 20 stone | Silver ore, silver, dragon eggs | Mountain hauls are the worst ones; this is the bindrune people want most. |
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

## 5. Interaction model

Settled: **rewire, selected on the map.** A portal's destination is a property of the portal and
applies to everyone.

### Two verbs

- **Walk in** — travel to wherever the portal currently points. No menu, no dialog.
- **Interact** — open the map selector and re-aim the portal. The new target stands for everyone
  until someone changes it again.

Splitting the verbs is what makes a shared destination tolerable. Travelling is the frequent action
and costs nothing; re-aiming is deliberate and rare.

### Selection is a map, with a list beside it

Choose the destination by picking a portal on the world map. Names stop carrying meaning at about a
dozen portals; position never does. This subsumes what was previously listed as a "map ping on the
selected row" — the map *is* the selector.

The list is shown **alongside** the map rather than instead of it, and the two are not alternative
selectors: there is one highlighted destination, and the map and the list are two renderings of it.
Moving the highlight moves both. That is why the interaction model is a highlight rather than a click
target — it costs nothing to add a second view of a highlight, and a great deal to reconcile two
independent ones.

They answer different questions, which is why neither replaces the other. The map answers *where is
it, and is that near the thing I care about*. The list answers *what are my options, in an order I
chose* — nearest first, or by name — and it is the only one of the two that stays usable when the
destination is off the visible map or has no name worth recognising.

### Pointers are one-way

Each portal independently stores where *it* sends you. A points at B; B may point at C rather than
back at A; C may point at B, which makes B and C a working pair — but only because two independent
pointers happen to face each other. Consequence: this does **not** require multi-portal rooms, since
you re-aim a portal instead of building another beside it.

### What it costs

A target is shared world state, so re-aiming changes everyone's route — including someone mid-haul.
That contention is a shipped property of the design now rather than something avoided. It is what
`ReaimPermission` below exists to bound, and the reason `PortalBinding = AllInRadius` is kept: if
re-aiming fights push players into building hubs after all, a per-portal charge would sting.

It also moves the clearance check. With a per-trip picker, choosing a destination and being told "no"
are the same moment. Here you commit by walking, so a refusal lands at the threshold with no dialog to
deliver it. The next two sections answer both problems: who is allowed to re-aim at all, and how a
player learns a destination will refuse them.

### Who may re-aim

`ReaimPermission`, server-synced, **default `Anyone`**:

| Value | Rule |
|---|---|
| `Anyone` | Any player may re-aim any portal. Default. |
| `GuardStonePermitted` | Inside a **guard stone**'s protected area, only players it permits. Outside any guard stone, anyone. |
| `Admin` | Admins only. |

`GuardStonePermitted` reuses the guard stone's existing permitted-players list rather than inventing
a second access-control system — players already understand it, and it already means "this is my
base, these are my crew". The guard stone is the vanilla piece the game labels *Ward*; it has nothing
to do with bindrunes, which carry clearance and have no player list.

A `Builder` option was considered and dropped: it needs the portal to record who placed it, and that
is not worth a shippable-or-not dependency for a rule two other values already cover.

### Telling the player before they commit

Two layers, and the first is the one that matters:

1. **A blocked marker on the inventory slot.** Near a portal, any stack that the portal's *current
   destination* will refuse gets a blocked overlay on its icon. You find out while packing, not after
   walking — which is better than any doorway warning, because at that point you can still do
   something about it.
2. **A named message on entry.** If you walk in anyway, R6's refusal names the offending resource and
   the missing bindrune rather than saying "you cannot teleport with that".

Three constraints on the first layer, all of them load-bearing:

- **Proximity-gated.** The overlay is live only within `CargoPreviewRange` of a portal, reading that
  portal's target. Always-on would paint red across your ore for the entire game and train everyone
  to ignore it.
- **It reads the registry, not the far side's ZDO.** The destination is normally unloaded on this
  client — see §6.
- **It never touches item data.** The overlay is computed from the prefab→tier map against the
  destination mask. Flipping `m_shared.m_teleportable` to drive a UI state would leak into tooltips
  and every other mod, which is the failure mode §6 calls out.

Where two portals are close together, "the portal you are near" resolves the same way an anchor picks
its portal: nearest within range.

### Selector requirements

- **Gamepad navigation from the first commit.** Retrofitting Unity UI navigation later is miserable.
- Per-portal **clearance chips** (Cu / Fe / Ag / Bm / Fl) — granted filled, missing dashed — plus a
  verdict and a footer showing what you're carrying and how many destinations will take it.
- Filter by **"only destinations that accept my cargo"**. Still meaningful under a shared target: you
  re-aim because you intend to travel with what you are holding.
- Sort/filter by distance, name and favourites for the list view that backs the map.
- `DiscoveredPortalsOnly` — a portal is selectable only once you've stood at it.
- `HidePortalNames` option, as Handy portals has.
- Keep vanilla tag pairing as the fallback when no explicit target is set, so an unmodded save
  behaves normally.

### Build-mode feedback — required, not polish

Auto-binding removes the binding UI, but it also removes the player's certainty about what the game
just decided for them. Two questions need answering without a menu:

- **Range** — which bindrunes count toward this anchor. Otherwise a bindrune planted 11 m out silently does
  nothing, and the player has no way to find out except by hauling ore and being refused.
- **Connection** — which portal the anchor bound to. With two portals 30 m apart, nothing on screen
  says which one just became iron-capable.

Reuse the existing in-game guard stone effect for both rather than authoring new art — that keeps this
inside the §11 rule about cloning prefabs instead of shipping assets. The prefab and its component
names are unverified; see §12.

---

## 6. Architecture

| Concern | Approach |
|---|---|
| **Stack** | BepInEx 5 + HarmonyX. Jotunn for custom pieces, localisation, config sync. The anchor and five bindrunes are custom build pieces — exactly `PieceManager`'s job. |
| **Where clearance lives** | An int bitmask on the anchor's ZDO, **mirrored onto every portal ZDO in radius** (`bindrune_mask`). The mirror is **not optional**: a traveling client can read the destination portal's ZDO but cannot see bindrunes 2 km away. |
| **Who computes it** | The **server**. It holds every ZDO, so it recomputes a site's mask on bindrune place/destroy and on a slow sweep, then writes the portals. Clients never author a mask. This also self-heals staleness when a bindrune is destroyed while the site is unloaded. |
| **Portal registry** | Server-authoritative list from `ZDOMan.GetPortals()`, pushed to clients on join and on change. **Each record carries that portal's clearance mask**, not just name, id and position — see the row below for why. `GetPortals()` hands back the live list, so read it and never mutate it. |
| **Where the target lives** | Our own `bindrune_target` ZDOID key on the portal's ZDO, with a prefix on `TeleportWorld.Teleport` preferring it over the vanilla destination. **Not** the vanilla `ConnectionType.Portal` connection: the server rebuilds those from tag matches every 5 seconds and would erase a one-way target almost immediately (§12). Leaving that pass alone is also what gives §5's vanilla-tag fallback for free — a portal we never re-aimed still pairs the way it always did. |
| **Why the registry carries masks** | A client standing at A needs the mask of A's *target*, which is usually kilometres away and not in the client's ZDO set at all. The ZDO mirror alone cannot answer it. So the mask travels in the registry record, which is what makes both the travel gate and the inventory preview possible without loading the far side. |
| **The check** | Prefix on `TeleportWorld.Teleport(Player)`: resolve destination → read mask → walk `Inventory.GetAllItems()` for `m_shared.m_teleportable == false` → allow, or refuse with a named reason through `Character.Message`. Suppress vanilla's `Humanoid.IsTeleportable()` via a scoped context flag, and honour the portal's own `m_allowAllItems`. |
| **What not to do** | Do **not** flip `m_shared.m_teleportable` on shared item data to let ore through. It's shared state — it leaks into tooltips, other mods, and anything else that asks. Several existing portal mods take that shortcut and it's why they conflict. |
| **Trust model** | Player inventories are client-side in Valheim, so cargo checks are client-trusting — same as vanilla. The server can authoritatively own **clearance**, never **cargo**. Put that in the readme: this is a rule system for a co-op server, not anti-cheat. |
| **Install** | Server **and** every client. Config syncs from the server so tiers can't be edited locally. |
| **Uninstalling** | Extra ZDO keys are harmless to a vanilla client. Custom pieces are not — remove the mod and anchors and bindrunes vanish. Normal for custom-piece mods; warn anyway. |
| **Known conflicts** | Anything that rewrites teleport rules: Valheim Plus, Advanced Portals, Progression Portals, Gate of Ore-thority, unrestricted-portal mods. Detect by GUID at startup and log a loud warning rather than fighting over patches. |

---

## 7. The seamless-transit layer (Phase 4, default off)

One real collision: seamless transit removes the **Interact** moment, and the interact moment is
where the cargo check lives. If you walk in and the destination refuses your ore, there's no dialog
to refuse you in.

**Since §5 settled on rewire, this is no longer only a Phase 4 problem.** Interact now re-aims rather
than travels, so *every* trip is a walk-in and there is never a dialog to carry the refusal. The
approach-time warning below is the answer to the base game loop, not just to seamless transit — which
is an argument for pulling some of it earlier than Phase 4.

Fix — and it improves the base experience too:

- Resolve the destination's mask when the player comes within ~4 m of an open portal, not at transit.
- On refusal: flare the runes red, drop a rune-curtain collider across the doorway, HUD line naming
  the item and the missing bindrune.
- You get told **before** you commit, which beats vanilla's silent stone wall.

Scope call: ship last, default off, treat as replaceable. Maintained mods already do preloading
well — depend on one rather than owning that code.

Cheap 80% of the *feeling*: a **destination thumbnail** in the panel, snapshotted the last time any
player stood at that portal and cached with the portal record. Phase 3 nicety, not a renderer.

---

## 8. Build order

| # | Phase | Ships | Standalone? |
|---|---|---|---|
| 1 | **Destination selector** | Portal registry, server sync, the one-way target on the portal ZDO, and the map selector with keyboard *and* gamepad nav. | Yes — and it's the must-have. |
| 2 | **Anchors & bindrunes** | Six pieces, the mask, server recompute, the destination check, named refusals on entry. | Yes. The mod's reason to exist. |
| 3 | **Fusion & polish** | The blocked-cargo overlay on inventory icons, clearance chips in the selector, cargo filter, portal rune tinting by tier, destination thumbnails. | Needs 1 + 2. |
| 4 | **Seamless transit** | Approach-time gating, rune curtain, preload, fade. Default off. | Optional — cut without regret if it fights the game. |

Phase 2 is playable on the entry message alone, but it is the *worse* half of §5's two layers — you
learn at the threshold instead of while packing. If Phase 3 slips, pull the overlay forward out of it
rather than shipping Phase 2 as the long-term state.

---

## 9. Decisions

### Settled

| Question | Decision |
|---|---|
| Station or rewire? | **Rewire, selected on the map** (§5). A portal's destination belongs to the portal and applies to everyone; walk in to travel, interact to re-aim. Station is deferred to §13 and is not being built. |
| Independent per-tier flags, or a strict ladder (tier 3 requires 1 + 2)? | **Independent flags.** `StrictLadder` opts into requiring the lower bindrunes first. |
| Anchor-and-radius, or bind each bindrune to one portal? | **Auto-bind to the nearest portal in range** (`PortalBinding = Nearest`), no manual binding UI. `AllInRadius` covers every portal at the site. |
| Does the source ever matter? | **No — never, and there is no setting.** R3 rewritten to say so. |
| Own the destination list, or build on XPortal (GPLv3)? | **Own it.** Inspiration only, no copied code, MIT preserved. §11 spells out where the line sits. |
| Who may re-aim a portal? | **`ReaimPermission`, default `Anyone`**, with `GuardStonePermitted` / `Admin` — see §5. |
| How is a player warned *before* they commit? | **A blocked overlay on inventory icons near a portal**, plus R6's named message on entry — see §5. |
| Permanent bindrunes, or an ongoing sink? | **Permanent, with no fuelled mode at all.** The gate is the boss kill, not upkeep — see §10. |

One portal per site is what makes the binding default reasonable, and that survives the move to
rewire: re-aiming reaches every destination from a single portal, so a site needs exactly one and
there is nothing to disambiguate. Binding also dissolves the overlapping-anchor question — nearest
wins, deterministically — and gives back the "loading dock" pattern (two portals at one location with
different clearance) that a site-wide radius cannot express.

The caveat is contention: if re-aiming fights push players into building hubs after all, `Nearest`
would charge a full bindrune set per portal in the hub. `AllInRadius` is the escape hatch, and that is
now its main justification rather than the large-base case it was kept for.

Dropping source enforcement outright, rather than shipping it off by default, is worth being precise
about — because the two are not equivalent, and the difference *is* the mod. Checking the source too
would mean an outpost with no bindrunes could no longer send ore anywhere, since it has none of its
own to authorise the departure. That kills the one-way outpost in §1: ore would need bindrunes at
both ends, the network would become symmetric, and "ore flows inward toward places you invested in"
would just become "build bindrunes everywhere". A setting that can switch off the central mechanic is not a setting worth
having.

Explicit manual binding was rejected on cost: it needs a bind interaction, gamepad navigation for it,
a stored ZDOID per bindrune, and dangling-reference handling when either end is destroyed while the chunk
is unloaded. Auto-binding is recomputed from positions on the server's sweep, so it is self-healing
and stores nothing that can rot.

### Still open

None of the structural questions. What is left is numbers, not shape: the ladder costs in §4 are
placeholders and the anchor radius default is a guess, and both want real play rather than more
argument. §10 is where that lands.

---

## 10. The main balance risk

The cost curve is the only brake, and trophies are farmable. On a server that has killed Yagluth, a
determined group can build bindrunes at every site they own in an afternoon of boss re-summons — at
which point Bindrune quietly becomes an unrestricted-portals mod with extra steps.

**A fuelled mode was the other available answer, and it has been rejected** (R7). The gate is meant
to be the boss kill, not upkeep: once you have beaten a biome you have moved on to the next one, and
the trips back to earlier zones for resources are exactly the drudgery this mod exists to remove.
Charging rent on a network you already earned would put that friction back in the wrong place.

So the cost curve is now the *only* brake, and it has to carry the whole load alone. That makes the
metal component the lever — building out a site should read as a real project, not an errand. The numbers
in §4 are placeholders and this is the part that most needs real play.

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
   that relicenses your whole assembly. This got harder once §5 settled on rewire, because rewire is
   precisely what XPortal already implements — the overlap is no longer theoretical. Writing the
   registry, the one-way target and the map selector ourselves is what keeps the licence choice in
   our hands, and it is a few days of work against permanently binding the assembly. You can still
   *read* XPortal for approach; copying code is what triggers it.

   **Settled: own it. Inspiration only.** Copyright protects expression, not ideas — it does not
   reach "any idea, procedure, process, system, method of operation, concept, principle, or
   discovery" (17 U.S.C. §102(b)). GPL-3.0 is a copyright licence, so if nothing is copied it never
   attaches. In practice:

   | Free to take | Not free to take |
   |---|---|
   | The mechanic — a portal stores a target and syncs it to everyone | Source, whole or partial |
   | The architecture — server-authoritative registry, client cache resynced on join | Copy-then-rename, which is still a derivative |
   | That a problem exists and roughly how it is solved | Paraphrase close enough to be recognisably the same code |
   | Facts about the **game's** API — that `ZDOMan.GetPortals()` exists is a fact about Iron Gate's code, not XPortal's expression | |

   The working rule: read it to understand *what* and *why*, then close the file and write ours from
   this document. The failure mode is not reading — it is having their implementation open in the
   other window while typing the equivalent function, which drifts into transcription without anyone
   deciding to.

   Use our own ZDO keys (`bindrune_*`). The legal case against reusing theirs is weak — short
   identifiers are barely copyrightable — but the functional one is decisive: two mods writing the
   same key on the same world would corrupt each other.

   We start from a good position here. This document was written from the problem, not reverse
   engineered from their code, so the spec we implement against already exists independently — which
   is most of what a formal clean-room process buys, without the ceremony.

   Credit AnyPortal and XPortal as inspiration in the README at ship time. Not required if nothing is
   copied; good manners, and free.

   *(Not legal advice — this is the standard understanding in the modding ecosystem.)*
2. **A code licence does not cover art or game assets.** Keep those separate:
   - **Never commit Valheim's DLLs, publicised assemblies, or ripped meshes/textures** to the repo.
     They're Iron Gate's, redistribution isn't yours to grant, and it's the fastest way to get a
     repo taken down. Reference game assemblies from a local install path via an env var and
     `.gitignore` them.
   - Prefer **cloning existing in-game prefabs at runtime** (a rune stone, a standing stone) for the
     anchor and bindrunes over shipping custom models. Cheaper, always matches the art style, and there
     is nothing to license.
   - If you do ship original models/icons in an asset bundle, license the art separately in the
     README — CC BY 4.0 is the usual pick — because MIT's "software" wording maps badly onto art.

Practical setup: `LICENSE` (MIT, your name), plus a short `NOTICE` or README section reading roughly
*"Code: MIT. Art assets: <licence>. Valheim and its assets are property of Iron Gate Studio; no game
files are redistributed."* Then set the Nexus permission fields and the Thunderstore manifest to
match — a repo that says MIT next to a mod page that says "do not reupload" is the single most
common licensing mistake in this ecosystem.

---

## 12. Game API — verified and still unverified

Everything here originally came from mod sources and recollection. The first half has since been read
off the shipped assemblies; the second half still hasn't.

### Verified

Read from `assembly_valheim.dll` at game build **5.4.23.2+3** (Aug 2026) with Mono.Cecil. Anything
below is what the game actually contains, not what the spec assumed.

**`TeleportWorld`** — fields `m_activationRange`, `m_exitDistance`, `m_allowAllItems`, `m_proximityRoot`;
methods `GetHoverText()`, `GetHoverName()`, `Interact(Humanoid, bool, bool)`, `UseItem(Humanoid, ItemData)`,
`Teleport(Player)`, `GetText()` / `SetText(string)` (it is a `TextReceiver`), and private
`HaveTarget()`, `TargetFound()`, `UpdatePortal()`, `SetConnectedPortal(ZDOID)`, `RPC_SetConnected`,
`GetTagSignature(out string tagRaw, out string authorId)`.

- **`GetConnectedPortal` does not exist.** It was never a method; earlier drafts invented it.
- `Teleport(Player)` is the travel entry point, and it is where the checks live: `GlobalKeys.NoPortals`,
  then `GlobalKeys.NoBossPortals`, then — unless the portal's own `m_allowAllItems` is set —
  `Humanoid.IsTeleportable()`, refusing with `Character.Message(MessageHud.MessageType.Center, "$msg_noteleport")`.
  That last branch is the one **R6** replaces with a named reason.
- `Interact` gates on `PrivateArea.CheckAccess(transform.position, 0f, flash: true, wardCheck: false)`
  and then opens `TextInput.RequestText(this, "$piece_portal_tag", 10)`. That `RequestText` call is
  precisely what the §5 map selector replaces.

**The destination is a ZDO *connection*, not a tag lookup.** `Teleport` resolves it as
`zdo.GetConnectionZDOID(ZDOExtraData.ConnectionType.Portal)` → `ZDOMan.GetZDO(...)` → position/rotation →
`Character.TeleportTo(pos, rot, distantTeleport: true)`. `TargetFound()` calls `ZDOMan.RequestZDO(id)`
when the destination ZDO isn't loaded locally — the precedent for reaching a portal kilometres away.

**`Game.ConnectPortals()` will tear down anything we write into that connection.** It runs on the
server only (`Game.Start` starts `ConnectPortalsCoroutine` behind `ZNet.instance.IsServer()`) and
repeats **every 5 seconds**. Two passes:

1. For every portal with a connection: if the target ZDO is gone, **or the target's `ZDOVars.s_tag`
   differs from this portal's**, clear the connection.
2. For every unconnected portal: find a random unconnected portal with the same tag and connect
   **both ends** to each other.

So the vanilla connection is by construction symmetric and tag-derived, and a one-way target written
into it would survive at most five seconds. §6 records the consequence: our target lives in our own
ZDO key, `Teleport` is patched to prefer it, and vanilla's pass is left running untouched — which is
what makes "vanilla tag pairing as the fallback" in §5 fall out for free rather than needing code.

`ZDOMan.ConvertPortals()` migrates pre-connection saves off a legacy `ZDOVars.s_toRemoveTarget` ZDO
key. That key is dead; don't reuse the name or the pattern.

**Teleportability** — `Player.IsTeleportable()` **does not exist**. It is `Humanoid.IsTeleportable()`,
which forwards to `Inventory.IsTeleportable()`, which returns true immediately if
`GlobalKeys.TeleportAll` is set and otherwise scans `m_inventory` for
`ItemDrop.ItemData.m_shared.m_teleportable == false`. `Inventory.GetAllItems()` and
`Player.TeleportTo(Vector3, Quaternion, bool)` both exist as assumed.

**`ZDOMan.GetPortals()`** — public instance method on `ZDOMan.instance`, returning `List<ZDO>`. It
returns the **live `m_portalObjects` list, not a copy**; read it, never mutate it. Server-side
population, consistent with the server-authoritative registry in §6.

**ZDO custom fields** — `Set(string, ZDOID)` / `GetZDOID(string)` / `RemoveZDOID(string)`, with
hash-pair overloads via the cacheable `ZDO.GetHashZDOID(string)`; ints via `Set(string, int)` /
`GetInt(string, int)`. String keys hash through `StringExtensionMethods.GetStableHashCode` in
`assembly_utils`. Vanilla caches its own hashes as statics on `ZDOVars` (`s_tag`, `s_tagauthor`, …);
ours do the same with `bindrune_` names.

**`PrivateArea.CheckAccess(Vector3 point, float radius, bool flash, bool wardCheck)`** — public
static, and the call behind `ReaimPermission = GuardStonePermitted`. Vanilla already gates portal
interaction on it, so that setting is mostly a matter of not weakening what is already there.

**A ZDOID is not a persistent reference.** Every ZDO in the world is renumbered on every save and
load. Measured across one logout to the main menu and back:

| Portal | Before | After |
|---|---|---|
| built earlier, already saved | `1:20372` | `1:20375` |
| built earlier, already saved | `1:27015` | `1:27018` |
| built during that session | `2261713014:42343` | `1:32429` |

So both halves move: a ZDO created in a session carries that session's id and comes back under a
different one, and even long-persisted ZDOs have their numeric id shifted. A stored ZDOID therefore
points at nothing after a relog — or, worse, at whatever object inherited its number.

This is why vanilla persists portal connections as `ZDOConnectionHashData` in
`ZDOExtraData.s_saveConnections` and rebuilds the real ids after load, and why `ZDOMan.ConvertPortals`
migrates old saves off the ZDOID-valued `ZDOVars.s_toRemoveTarget` key. **Anything of ours that must
outlive a session refers to a portal by its own `bindrune_pid`**, a 64-bit id the server mints once
and the registry resolves to a live ZDOID on demand. ZDOIDs are still the right handle *within* a
session — they are how you reach the object — but they may never be written down.

**Publicised assemblies are a compile-time fiction, and this game build enforces that.** Jotunn's
prebuild publicises the game assemblies and every reference resolves against those, so
`portal.m_nview` compiles without complaint. At runtime the *real* `assembly_valheim.dll` is loaded,
the field is private again, and this build's Mono raises the access check:

```
FieldAccessException: Field `TeleportWorld:m_nview' is inaccessible from method
`Bindrune.Patches.TeleportWorldPatches:HaveOurTarget (TeleportWorld,bool&)'
```

It throws on every call, and a field read from a per-frame path such as `GetHoverText` produces five
figures of log spam in a couple of minutes. Nothing warns at build time, so the rule has to be held
by hand: **never read or write a private game member directly.** Patch methods take Harmony's
`___fieldName` injected parameter; everything else goes through a cached
`AccessTools.FieldRefAccess`, which is emitted once and costs about what the field access would have.
Public members — and most of what we need is public — are fine as they are.

Two things the game has grown that the spec didn't know about:

- **`TeleportWorld.m_allowAllItems`** — a per-prefab flag that skips the teleportable check entirely.
  Our gate has to respect it or we will refuse cargo at a portal the base game lets through.
- **`ZDOVars.s_tagauthor`** — vanilla now records who set a portal's tag. The `Builder` value dropped
  from `ReaimPermission` in §5 was dropped because nothing recorded the placer; this records the
  *tagger*, which is close but not the same thing. Noted, not reopened.

### Still unverified

These need the game running or the asset database loaded, so the assemblies can't answer them:

- Boss trophy prefab names, especially **The Queen** and **Fader** — verify in `ObjectDB`.
- The authoritative non-teleportable item list — from the `ObjectDB` scan at runtime, not from a wiki
  and not from this document.
- The **in-game guard stone effect** reused for the build-mode range and connection indicators in §5 —
  the prefab name, the component that drives it, and whether its radius can be driven at runtime.
- The **inventory slot UI** for the blocked-cargo overlay in §5: how `InventoryGui` / the inventory
  grid builds and refreshes slot elements, and where a child image can be attached so it survives a
  refresh. Vanilla already draws quality stars and durability bars on those elements, so the hook
  exists.
- Which vanilla prefabs ship with `m_allowAllItems` set.
- **Plugin GUIDs of the conflicting mods** in §6. `Compat/ConflictDetector.cs` currently holds only
  the two confirmed from source (Valheim Plus `org.bepinex.plugins.valheim_plus`, XPortal
  `SpikeHimself.XPortal`); Advanced Portals, Progression Portals, Gate of Ore-thority and AnyPortal
  are covered only by a `portal`/`teleport` keyword heuristic until someone reads their GUIDs off
  the actual plugins. A wrong GUID in that list fails silently, which is why guesses stay out of it.

Also worth reading directly: [XPortal's source](https://github.com/SpikeHimself/XPortal) (GPLv3 —
read for approach, don't copy code unless you're licensing GPLv3), which uses ZDO keys
`XPortal_TargetId` / `XPortal_PreviousId` and a client `KnownPortalsManager` resynced from the
server.

---

## 13. Future ideas — noted, not planned

Things deliberately set aside. Nothing here is scheduled; they are recorded so the reasoning is not
lost and so nobody re-derives them from scratch.

### Station mode — per-player, per-trip destinations

Handy portals' model. Interact, pick a destination, travel immediately; the choice is yours alone and
nothing is written to the portal.

It gives up the thing rewire is built around — a portal that means the same to everyone — but it has
two real advantages worth remembering if the shared-target model turns out to chafe:

- **No contention.** No two players ever fight over one portal's destination, and re-aiming cannot
  strand somebody mid-haul.
- **The clearance check lands in the right place.** Each trip is a fresh question, so choosing a
  destination and being told "no" are the same moment — no walking into a wall, no need for
  approach-time warnings to carry the refusal.

It would slot in as a `SelectionMode` alongside rewire rather than replacing it. The cost is a second
travel path to maintain and test, which is why it is not being built now.
