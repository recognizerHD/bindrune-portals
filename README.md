# bindrune-portals

Pick any portal by name. What you may carry through is decided by the bindrunes standing at the far end — and every bindrune is bought with a boss trophy.

**Bindrune** is a Valheim mod. Build an Elder's Bindrune at your base and from then on *every* portal
in the world can send copper, tin and bronze **to** your base — and none of them can receive it back
until you go build a bindrune there too. Ore flows inward, toward the places you have invested in.

See [DESIGN.md](DESIGN.md) for the full spec: the rules, the bindrune ladder, the architecture, and the
build order.

## Status

Pre-alpha, but the whole idea is playable. **Phases 1 and 2 are done.** Interact with a portal, pick
any portal in the world off the map, and it points there for everyone until someone re-aims it.
Pointers are one-way. Build a bindrune next to a portal and that portal will accept the metals that
rune covers — and refuse the rest, by name:

> Iron cannot enter "Copper Mine" — no Bonemass's Bindrune there.

Only the destination is ever checked, so an outpost with no bindrunes can send ore to your base
forever and never receive any. That asymmetry is the point of the mod.

What is missing is explanation rather than mechanism. A bindrune planted just out of range does
nothing and does not say so, and you find out what a destination refuses at the doorway rather than
while packing. Both are the next phase.

Not yet tested over a network with clearance in play.

### Controls

| | |
|---|---|
| **E** at a portal | Open the destination selector |
| **Shift+E** | Rename the portal, as vanilla |
| **← →** | Change the highlighted destination |
| **P** | Confirm |
| **Escape** | Cancel |
| **O** | Cycle the list order |

All rebindable, with gamepad equivalents, under `5 - Selector keys` in the config.

### Console commands

`bindrune_portals` lists every portal this instance knows about and where it points.
`bindrune_aim <name>` re-aims the nearest portal without the map. `bindrune_net` reports the sync's
state — run it on a server and a client and compare.

## Building

You need [BepInEx 5](https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/)
installed into Valheim, and the game run once so BepInEx generates its folders.

1. Tell the build where Valheim lives, either way round:
   - set `VALHEIM_INSTALL` as an environment variable, or
   - copy `Environment.props.example` to `Environment.props` and edit the path in it.

   `Environment.props` is gitignored on purpose — it is a machine-local path.

2. Build:

   ```sh
   dotnet build Bindrune.sln -c Release
   ```

   The first build runs Jotunn's prebuild task, which publicises the game assemblies and generates
   the MMHOOK assemblies **inside your game folder**. Nothing it produces belongs in this repo. Once
   that has run you can set `ExecutePrebuild` to `false` in `DoPrebuild.props` for faster builds; set
   it back to `true` after a game update.

3. The output is a single `Bindrune.dll`. Set `MOD_DEPLOYPATH` in `Environment.props` to have a
   successful build copy it straight into `BepInEx/plugins`.

   That one file is the whole mod — everything else it needs is BepInEx and Jotunn, which are
   installed separately. The `.pdb` beside it is optional and only affects debugging: with it,
   exceptions in the log carry file names and line numbers, which is worth having while developing
   and worth leaving out of a release.

If the game path is wrong or BepInEx is missing, the build stops with one plain error saying which,
rather than a hundred unresolved references.

### Checking the game API

Anything this mod calls in the game has to be read off the shipped assemblies first, not remembered —
see [DESIGN.md §12](DESIGN.md#12-game-api--verified-and-still-unverified). `tools/Dump-GameApi.ps1`
is what that was done with: it reads metadata through the Mono.Cecil that BepInEx already ships, using
the same `VALHEIM_INSTALL` the build uses, and copies nothing out of the game folder.

```powershell
./tools/Dump-GameApi.ps1 -Type TeleportWorld
./tools/Dump-GameApi.ps1 -Type Game -IL ConnectPortals
./tools/Dump-GameApi.ps1 -Member IsTeleportable
```

### Toolchain

| | |
|---|---|
| BepInEx | 5 (HarmonyX / `0Harmony` comes with it) |
| [Jotunn](https://github.com/Valheim-Modding/Jotunn) | `JotunnLib` 2.29.2 — pieces, localisation, config sync, and the game/BepInEx reference set |
| Target framework | `net48` |
| Publicising | Jotunn's own prebuild task. `BepInEx.AssemblyPublicizer.MSBuild` is **not** needed |

## Installing

You need **BepInEx 5** and **Jotunn**, then drop `Bindrune.dll` into `BepInEx/plugins`. Jotunn is a
hard dependency: without it the plugin is skipped and the log says so.

Bindrune must be installed on the **server and every client**. The server owns clearance and computes
the masks; clients need the destination panel and the travel gate. Config synchronises from the
server, so clearance rules cannot be edited client-side.

While the mod is pre-release, `LogNetworkSync` defaults to **on** and narrates the portal sync into
the log. Turn it off in the config if you'd rather it were quiet; it will default off at release.

Cargo checks are client-trusting, because player inventories are client-side in Valheim — same as
vanilla. This is a rule system for a co-op server, **not anti-cheat.**

Removing the mod removes its custom pieces, so any bindrunes you built will vanish. The extra
ZDO keys it writes are harmless to a vanilla client.

## Licensing

Code: **MIT** — see [LICENSE](LICENSE).

Valheim and its assets are the property of Iron Gate Studio. No game files are redistributed here,
and none may be committed to this repo — not the DLLs, not publicised assemblies, not extracted
meshes or textures. The build references your own local install instead.

If original art ever ships in an asset bundle it will be licensed separately in this section, because
MIT's "software" wording maps badly onto art.
