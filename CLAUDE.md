# CLAUDE.md — Bindrune

Context for Claude Code sessions in this repo.

## What this is

**Bindrune** — a Valheim BepInEx mod. Two features, one system:

1. **Any-portal travel.** Interact with a portal, get a list of every portal in the world, pick one, go.
2. **Destination clearance.** Each portal site has a clearance mask built from physical ward stones
   bought with boss trophies. Travel is checked against the mask of the portal you're **arriving
   at**, not the one you're leaving — so ore flows *inward* toward places you've invested in, and
   un-warded outposts are one-way.

Feature 2 is the reason the mod exists. Feature 1 is table stakes (several mods already do it).

**Read `DESIGN.md` first.** It is the authoritative spec: rules R1–R7, the ward ladder, the
architecture table, phases, open decisions. This file only covers how to work in the repo.

## Current state

Design only — no code yet. Phase 1 (destination list) has not been started.

## Non-negotiable invariants

Violating any of these means rewriting a lot, so check against them before proposing a change:

- **Clearance is read from the destination**, never the source (unless `EnforceAtSource` is on).
- **The server computes clearance masks; clients only read them.** Masks live on the anchor's ZDO and
  are *mirrored* onto every portal ZDO in radius — required, because a traveling client can read the
  destination portal's ZDO but cannot see ward pieces kilometres away.
- **Never mutate `m_shared.m_teleportable`.** It's shared item data; changes leak into tooltips,
  other mods, and everything else that asks. Gate travel with a scoped context flag instead. This is
  the single most common bug in existing portal mods.
- **The blocked-item list is generated at runtime** by scanning `ObjectDB` for
  `m_teleportable == false`, mapped to tiers by config, with unknown items defaulting to the highest
  tier and being logged by name. Do not hardcode item lists.
- **Cargo checks are client-trusting** (player inventories are client-side in Valheim). The server
  owns *clearance*, never *cargo*. This is a co-op rule system, not anti-cheat — don't add
  complexity pretending otherwise.
- **Gamepad navigation in the panel from the first commit.** Retrofitting Unity UI navigation is
  miserable.

## Before writing code that touches game internals

API names in `DESIGN.md` came from other mods' sources and recollection, **not** from the current
game assemblies. Verify in a decompiler before building on any of them — see DESIGN.md §12 for the
list. If a name doesn't match, fix `DESIGN.md` in the same commit.

## Licensing rules for this repo

- Code is **MIT** (see DESIGN.md §11). Do **not** copy code from XPortal — it's GPL-3.0 and would
  relicense this whole assembly. Reading it for approach is fine.
- **Never commit game DLLs, publicised assemblies, or extracted game assets.** Reference the game
  install through an env var and keep those paths in `.gitignore`.
- Prefer cloning existing in-game prefabs at runtime over shipping custom models.

## Intended layout (when Phase 1 starts)

```
Bindrune.sln
Bindrune/
  Bindrune.csproj
  Plugin.cs                 # BepInPlugin entry, Harmony bootstrap
  Config/                   # ServerSync'd config
  Tiers/                    # ObjectDB scan, prefab -> tier map
  Portals/                  # registry + server sync
  Wards/                    # anchor + ward pieces, site resolution
  Clearance/                # mask type, server recompute
  Travel/                   # the teleport gate + refusal messages
  UI/                       # destination panel (keyboard + gamepad)
  Patches/
Assets/                     # asset bundle, only if we ship original art
DESIGN.md  CLAUDE.md  README.md  LICENSE
```

Toolchain to confirm at setup time: BepInEx 5, HarmonyX, Jotunn (`JotunnLib` NuGet) for pieces /
localisation / config sync, and `BepInEx.AssemblyPublicizer.MSBuild` for publicising game
assemblies at build time. Target framework and Jotunn version should match whatever the current
Jotunn project template ships with rather than being guessed.

## Working notes

- Suggested plugin GUID: `com.recognizerhd.bindrune`.
- Detect conflicting mods by GUID at startup (Valheim Plus, Advanced Portals, Progression Portals,
  Gate of Ore-thority, unrestricted-portal mods) and log a loud warning — don't fight over patches.
- Costs in the ward ladder are **placeholders**. Don't treat them as balanced.
