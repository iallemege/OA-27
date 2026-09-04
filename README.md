# OA-27 (C / D / E)

Nuclear Option BepInEx plugin: **OA-27C**, **OA-27D**, and **OA-27E** hangar variants of Aryx's OA-27 Cavalier. Hangar icons use the model codes `OA-27C` / `OA-27D` / `OA-27E`.

## Authorization

**Released with Aryx's authorization.**

This is a licensed derivative of [Aryx's OA-27 Cavalier](https://github.com/Aryx3D/Aryx-OA-27-Cavalier) (`Aryx_PropAttacker1`). The airframe mesh, textures, and original `.nobp` belong to **Aryx**. GitHub Releases include Aryx's `.nobp` so the pack is playable as a standalone install. This plugin does not replace Aryx's original hangar aircraft.

**经 Aryx 授权发布。** 机体为 Aryx 的 OA-27 Cavalier。网格、贴图与原版 `.nobp` 归 Aryx。Release 压缩包内含该 `.nobp`，方便独立安装。

## Install

1. [BepInEx 5](https://github.com/BepInEx/BepInEx) in the game folder.
2. [Blueprinter](https://github.com/nikkorap/NOBlueprinter-Releases/releases/latest) in `BepInEx/plugins` (recommended).
3. Download **OA-27-1.1.10.zip** from [Releases](https://github.com/iallemege/OA-27/releases).
4. Extract into the Nuclear Option game folder so these land in `BepInEx/plugins/`:
   - `OA-27Variant.dll`
   - `Aryx.OA-27.Cavalier_1.0.1.nobp`
5. Delete leftover `OA27C.dll` if it is still there.
6. Fully quit Steam, then launch.

Plugin GUID: `com.ial.oa27variant`. Standalone: no Oritasy / BIA / MiG-15S compile dependency.

## Variants

| Hangar icon | Key | Notes |
|-------------|-----|--------|
| **OA-27C** | `Aryx_OA27_C` | Both factions. RCS 0, STOL / low-alt G, 2× turbine, 1 kt suicide fuze. First eject punches the WSO decoy; you cannot bail. |
| **OA-27D** | `Aryx_OA27_D` | **BDF only.** Extra armor and fuel, 2× turbine, no suicide kit. Second eject is a real bail-out. |
| **OA-27E** | `Aryx_OA27_E` | **PALA only.** 3× turbine, standing ECM, ground autocannons cannot damage it. Second eject is a real bail-out. |

Unknown HQ keeps both D and E listed. Rank 1.

**OA WSO** (C/D/E while the rear seater is aboard): dumps inbound missiles inside 10 km; **Y/N** for flares and target lock (does not steal your selected countermeasure); extra gunsight lead; GLOC floor.

## Build from source

Windows, `csc` from .NET Framework 4.x. Edit `GAME=` in `build.bat` if the Steam folder is not `d:\Steam\steamapps\common\Nuclear Option`.

```bat
cd OA-27Variant
build.bat
```

## Credits

- Airframe (OA-27 Cavalier): **Aryx** — used with authorization. [Aryx-OA-27-Cavalier](https://github.com/Aryx3D/Aryx-OA-27-Cavalier)
- Asset loader: [Nikkorap / Blueprinter](https://github.com/nikkorap/NOBlueprinter-Releases)
- Variant plugin: IAL / [iallemege](https://github.com/iallemege)
