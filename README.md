# OA-27 (C / D / E)

Nuclear Option BepInEx plugin: **OA-27C**, **OA-27D**, and **OA-27E** hangar variants of Aryx's OA-27 Cavalier.

## Authorization

**Released with Aryx's authorization.**

This is a licensed derivative of [Aryx's OA-27 Cavalier](https://github.com/Aryx3D/Aryx-OA-27-Cavalier) (`Aryx_PropAttacker1`). The airframe mesh, textures, and original `.nobp` belong to **Aryx**. This repository ships only the IAL variant plugin (`OA-27Variant.dll`). It does not replace Aryx's aircraft and does not re-upload Aryx's assets.

**经 Aryx 授权发布。** 机体为 Aryx 的 OA-27 Cavalier。网格、贴图与原版 `.nobp` 归 Aryx。本仓库只发布 IAL 的 C/D/E 变体插件，不重新上传 Aryx 的资源。

Install Aryx's Cavalier first: [Aryx3D/Aryx-OA-27-Cavalier](https://github.com/Aryx3D/Aryx-OA-27-Cavalier/releases).

## Install

1. [BepInEx 5](https://github.com/BepInEx/BepInEx) in the game folder.
2. [Blueprinter](https://github.com/nikkorap/NOBlueprinter-Releases/releases/latest) and Aryx's `Aryx.OA-27.Cavalier_*.nobp` in `BepInEx/plugins`.
3. Download **OA-27-1.1.5.zip** from [Releases](https://github.com/iallemege/OA-27/releases).
4. Extract into the Nuclear Option game folder so `OA-27Variant.dll` lands in `BepInEx/plugins/`.
5. Delete leftover `OA27C.dll` if it is still there.
6. Fully quit Steam, then launch.

Plugin GUID: `com.ial.oa27variant`. Standalone: no Oritasy / BIA / MiG-15S compile dependency.

## Variants

| Hangar | Key | Notes |
|--------|-----|--------|
| **OA-27C Cavalier** | `Aryx_OA27_C` | Both factions. RCS 0, STOL / low-alt G, 2× turbine, 1 kt suicide fuze. First eject punches the WSO decoy; you cannot bail. |
| **OA-27D Cavalier** | `Aryx_OA27_D` | **BDF only.** Extra armor and fuel, 2× turbine, no suicide kit. Second eject is a real bail-out. |
| **OA-27E Cavalier** | `Aryx_OA27_E` | **PALA only.** 3× turbine, standing ECM, ground autocannons cannot damage it. Second eject is a real bail-out. |

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
