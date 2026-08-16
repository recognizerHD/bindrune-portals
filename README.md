# bindrune-portals

Pick any portal by name. What you may carry through is decided by the bindrunes standing at the far end — and every bindrune is bought with a boss trophy.

**Bindrune** is a Valheim mod. Build an Elder's Bindrune at your base and from then on *every* portal
in the world can send copper, tin and bronze **to** your base — and none of them can receive it back
until you go build a bindrune there too. Ore flows inward, toward the places you have invested in.

See [DESIGN.md](DESIGN.md) for the full spec: the rules, the bindrune ladder, the architecture, and the
build order.

## Status

Pre-alpha. The project scaffold, config and mod-conflict detection are in place; **no portal or bindrune
behaviour is implemented yet.** Phase 1 (the destination list) has not started.

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

If the game path is wrong or BepInEx is missing, the build stops with one plain error saying which,
rather than a hundred unresolved references.

### Toolchain

| | |
|---|---|
| BepInEx | 5 (HarmonyX / `0Harmony` comes with it) |
| [Jotunn](https://github.com/Valheim-Modding/Jotunn) | `JotunnLib` 2.29.2 — pieces, localisation, config sync, and the game/BepInEx reference set |
| Target framework | `net48` |
| Publicising | Jotunn's own prebuild task. `BepInEx.AssemblyPublicizer.MSBuild` is **not** needed |

## Installing

Bindrune must be installed on the **server and every client**. The server owns clearance and computes
the masks; clients need the destination panel and the travel gate. Config synchronises from the
server, so clearance rules cannot be edited client-side.

Cargo checks are client-trusting, because player inventories are client-side in Valheim — same as
vanilla. This is a rule system for a co-op server, **not anti-cheat.**

Removing the mod removes its custom pieces, so any anchors and bindrunes you built will vanish. The extra
ZDO keys it writes are harmless to a vanilla client.

## Licensing

Code: **MIT** — see [LICENSE](LICENSE).

Valheim and its assets are the property of Iron Gate Studio. No game files are redistributed here,
and none may be committed to this repo — not the DLLs, not publicised assemblies, not extracted
meshes or textures. The build references your own local install instead.

If original art ever ships in an asset bundle it will be licensed separately in this section, because
MIT's "software" wording maps badly onto art.
