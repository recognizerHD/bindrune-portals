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

Scaffold in place, no game behaviour yet. Phase 1 (destination list) has not been started.

What exists: solution + project, the Jotunn/BepInEx build setup, `Plugin.cs` (BepInPlugin entry,
Harmony bootstrap, network compatibility), the ServerSync'd config, and mod-conflict detection.
None of it touches a game API, so all of it is verified — it compiles and the build guards were
tested. Everything from `Portals/` onward is still unwritten.

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

## Layout

`✓` exists, everything else is where the named concern goes when it's written.

```
Bindrune.sln                ✓
Directory.Build.props       ✓ game path + build guards; see "Build setup" below
DoPrebuild.props            ✓ Jotunn's publicise/MMHOOK prebuild toggle
Environment.props           ✗ gitignored, machine-local (copy from .example)
Bindrune/
  Bindrune.csproj           ✓
  BuildInfo.cs              ✓ GUID / name / version consts
  Plugin.cs                 ✓ BepInPlugin entry, Harmony bootstrap
  Config/                   ✓ ServerSync'd config
  Compat/                   ✓ conflicting-mod detection
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

## Build setup — settled, don't re-litigate

Confirmed against the Jotunn NuGet package and the JotunnModStub template, not guessed:

- **`JotunnLib` 2.29.2**, target framework **`net48`**, `LangVersion` 10.
- Jotunn's package supplies *every* reference — publicised game assemblies, BepInEx, `0Harmony`,
  all UnityEngine modules. Do not add game or BepInEx `<Reference>` entries by hand.
- **`BepInEx.AssemblyPublicizer.MSBuild` is not used.** Jotunn's own prebuild task publicises and
  generates MMHOOK, gated by `ExecutePrebuild` in `DoPrebuild.props`. The earlier note in this file
  calling for the publicizer package was wrong.
- `VALHEIM_INSTALL` comes from an env var or `Environment.props`; Jotunn derives `BEPINEX_PATH` and
  `VALHEIM_MANAGED` from it. `Directory.Build.props` imports it *before* NuGet's props, which is
  load-bearing — Jotunn resolves reference HintPaths at evaluation time, so setting the path later
  in the csproj silently yields references pointing at a guessed Steam path. It also defines
  `SolutionDir` so building the csproj directly behaves like building the solution.

Nothing in the build writes into the repo; the prebuild writes only into the game folder.

## Working notes

- Suggested plugin GUID: `com.recognizerhd.bindrune`.
- Detect conflicting mods by GUID at startup (Valheim Plus, Advanced Portals, Progression Portals,
  Gate of Ore-thority, unrestricted-portal mods) and log a loud warning — don't fight over patches.
- Costs in the ward ladder are **placeholders**. Don't treat them as balanced.
