# SPAZ2 Frame Controls

A mod that makes **Space Pirates and Zombies 2**'s VR controls work properly on joystick controllers: Steam Frame, Valve Index, Quest/Touch and the like, played through SteamVR.

SPAZ 2's VR mode was built for HTC Vive wands, whose round trackpad you press like a button. On a thumbstick, SteamVR treats the stick as that trackpad, so you have to **tilt the stick *and* click it in** for anything to happen. Turning, moving, issuing commands: every one needs a click. The developers confirmed the problem in 2019, and no official fix is coming.

## What it fixes

- **Tilting is enough.** Move, turn and zoom by tilting the stick; no clicking.
- **Stick commands work.** Directional commands fire when you push the stick past a threshold. That covers ship commands (right stick up), the camera toggle (grip + right stick right), weapon switching and the rest. A plain SteamVR "touch = click" binding can't do this.
- **Comfortable laser pointer.** The pointer is tilted 35° downward (adjustable), so you can aim without bending your wrists.

It doesn't change flat-screen mode, Oculus-runtime mode or real Vive wands. It adds files next to the game but never modifies the game's own files.

| Controller | Status |
|---|---|
| Quest 2 (via SteamVR) | Tested, working |
| Steam Frame | Intended target; reports should match Quest |
| Valve Index | Expected to work; untested |
| HTC Vive wands | Not changed (the mod detects trackpads and leaves them alone) |

## Install

### Easy: all-in-one package

1. Download **`SPAZ2FrameControls-<version>-with-BepInEx.zip`** from the [Releases](../../releases) page.
2. Find the game folder: in Steam, right-click *Space Pirates and Zombies 2* → *Manage* → *Browse local files*. It's the folder that contains `SPAZ2_64.exe`.
3. Extract the zip **into that folder**, so `winhttp.dll` and the `BepInEx` folder sit next to `SPAZ2_64.exe`.
4. Play in VR as normal.

### Manual: you already have BepInEx 5

1. Download **`SPAZ2FrameControls-<version>.zip`** and extract it into the game folder.
2. **Important for this game:** open `BepInEx/config/BepInEx.cfg` and, under `[Preloader.Entrypoint]`, change `Type = Application` to `Type = Camera`. SPAZ 2 runs on an old Unity version (5.4), and without this change the game shows a black screen.

The mod needs BepInEx **5.x, 64-bit**. BepInEx 6 is untested.

### Uninstall

- To remove just the mod, delete `BepInEx/plugins/SPAZ2FrameControls/`.
- To remove everything, also delete `BepInEx/`, `winhttp.dll`, `doorstop_config.ini` and `.doorstop_version`.

## Settings

Settings live in `BepInEx/config/aussie1579.spaz2.framecontrols.cfg`. The file is created the first time you launch the game with the mod, and you can edit it with Notepad while the game is closed.

| Setting | Default | What it does |
|---|---|---|
| `RequireClick` | `false` | Set to `true` to turn off the stick fix and go back to tilt-and-click. |
| `StickPressThreshold` | `0.5` | How far (0–1) you tilt the stick before it counts. Lower values are more sensitive. |
| `FlickReleaseThreshold` | `0.35` | Once pressed, the press lets go when the tilt drops below this. Keep it below `StickPressThreshold`. |
| `RepressOnDirectionChange` | `false` | Fire a new command when you slide the stick from one direction to another without returning to centre. |
| `PointerPitchOffset` | `-35` | Pointer tilt in degrees. More negative points lower, and `0` is the original angle. |
| `PointerYawOffset` | `0` | Pointer left/right turn in degrees. Positive turns it right. |
| `OffsetAimCursor` | `true` | Rotate the aim cursor on the controller to match the pointer. |
| `DebugLogging` | `false` | Write every stick press to the log, for troubleshooting. |

## Troubleshooting

**Black screen on launch.** The BepInEx entrypoint hasn't been changed. See step 2 of the manual install.

**Did the mod load?** Open `BepInEx/LogOutput.log`. It should contain `StickInputPatch applied` and `PointerOffsetPatch applied`. Once you're in VR, it should also show a `Controller N: model=...` line for each controller.

**Sticks still need clicking.** Check that `RequireClick` is `false`. Then check the `Controller` lines in the log: if your controller shows `axis0Type=k_eControllerAxis_TrackPad`, the mod deliberately leaves it alone. Please open an issue with your log.

**Reporting a problem.** Open an issue and attach `BepInEx/LogOutput.log` and `SPAZ2_64_Data/output_log.txt`. Turning on `DebugLogging` first helps.

## How it works

The game reads VR input through the legacy SteamVR plugin. Every command checks "was the trackpad *just* clicked, and which way was the thumb pointing at that moment?" The mod patches the one place where the controller state is read each frame, and reports the trackpad as clicked whenever the stick is tilted past the threshold. Every control in the game then behaves as it would on a Vive wand.

With a "touch = click" binding, by contrast, the click happens as soon as your thumb lands on the centred stick. The direction is zero at that moment, so commands never fire.

The full reverse-engineering notes are in [NOTES.md](NOTES.md).

## Building from source

Requirements: the .NET SDK (6 or newer) and a copy of the game.

1. Copy `SPAZ2_64_Data/Managed/*.dll` from your game folder into `ref/`.
2. Copy `BepInEx.dll` and `0Harmony.dll` from `BepInEx/core/` into `ref/bepinex/`.
3. Build:
   ```
   dotnet build src -c Release -p:GameDir="C:\path\to\SPAZ2"
   ```
   If `GameDir` contains a `BepInEx` folder, the DLL is copied straight into it.
4. To produce the release zips, run `pwsh scripts/package.ps1`. They're written to `dist/`.

The game's DLLs are not included in this repository and must not be committed.

## Credits and license

- Built with [BepInEx](https://github.com/BepInEx/BepInEx) (LGPL-2.1) and [HarmonyX](https://github.com/BepInEx/HarmonyX). The all-in-one package includes an unmodified BepInEx 5.4.23.5 with its license.
- This is an unofficial fan mod, not affiliated with or endorsed by MinMax Games. *Space Pirates and Zombies 2* is their property.
- The mod's code is under the [MIT License](LICENSE).
