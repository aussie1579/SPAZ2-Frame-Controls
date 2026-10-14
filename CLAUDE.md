# SPAZ2 Frame Controls — BepInEx mod

A BepInEx + Harmony plugin that fixes Space Pirates and Zombies 2's VR controls on joystick-based controllers. The target is the Steam Frame, streamed from PC via SteamVR. It should also help Index and Touch controllers.

## The problem

SPAZ 2's VR mode was built for **Vive wands**. Their clickable trackpad means "press a direction" is a single input. On joystick controllers, SteamVR maps the stick as the pad, so the player must **tilt the stick AND click it in** to do anything (e.g. turning left = stick left + press). The developer (MinMax / Narlak) confirmed this in 2019 and dropped Index support. No official fix is coming.

Problems to fix:
1. **Stick needs clicking.** Tilting alone should drive movement and turning.
2. **Directional commands.** Ship commands (right stick up) and the camera toggle (grip + right stick right) look like *press-and-release* events, not holds. A SteamVR "touch = press" binding fixes movement but breaks these, so the mod must handle them properly. One way: synthesise a short press when the stick is flicked past a threshold and then released or returned.
3. **Laser pointer angle.** The pointer ray points too high and strains the wrists. It needs a configurable downward pitch offset (and possibly yaw/pos offsets).

## Known facts

- Unity game, **Mono** scripting backend. The game code is in `SPAZ2_64_Data/Managed/Assembly-CSharp.dll`. It has been hand-patched by modders before, so it decompiles cleanly.
- Steam app ID **252470**. VR and flat modes ship in the same build.
- Without a Frame-specific binding, SteamVR falls back to an OpenXR generic or Oculus Touch binding and remaps it to the Frame controllers.
- Install path: `G:\Steam\steamapps\common\SPAZ2` (the csproj default `GameDir`).

## Unknowns — confirm first

- [x] Unity **5.4.5p1**, old Mono (.NET 3.5 profile). BepInEx **5.4.23.5 x64** is installed in the game folder.
- [x] Input: legacy SteamVR plugin via `SPAZ2VR_Input` → `VR_Utility` → `SteamVR_Controller.Device`. See NOTES.md.
- [x] Pointer ray: `VR_Utility.UpdateRaycast()`. See NOTES.md.
- [x] Commands use `GetPressDown` plus a same-frame direction check, so edges matter. See NOTES.md.

## Workflow

1. **Never modify the original game files.** Copy `Assembly-CSharp.dll` (and `Assembly-CSharp-firstpass.dll` if present) into `ref/` for decompiling and referencing. Keep `ref/` out of git.
2. Decompile with `ilspycmd` (`dotnet tool install -g ilspycmd`) into `decompiled/`, also gitignored. Search it for: `SteamVR`, `Touchpad`, `GetPress`, `GetAxis`, `EVRButtonId`, `Raycast`, `pointer`, `laser`, `VRInput`, `Controller`.
3. Write findings to `NOTES.md` as you go: class/method names, what each does, and chosen patch points.
4. Build the plugin as a **net35** class library (`dotnet build -c Release` in `src/`; matches the game's Mono profile). Reference `BepInEx.dll`, `0Harmony.dll`, `UnityEngine*.dll`, and the game assembly from `ref/`.
5. Prefer **Harmony Postfix/Prefix patches** on the narrowest input-reading method. Transpilers only if unavoidable.
6. Deploy by copying the built DLL into `<game>/BepInEx/plugins/SPAZ2FrameControls/`. A build script/target should do this when the game path is configured.
7. Test in-headset. The maintainer tests on the Steam Frame and Quest 2. Log diagnostics through the BepInEx logger, not `Debug.Log`.

## Config (BepInEx ConfigFile)

Expose at least:
- `StickPressThreshold` (float, default 0.5) — deflection that counts as a pad press
- `RequireClick` (bool, default false) — restores vanilla behaviour
- `FlickReleaseThreshold` (float) — for synthesised press/release on command inputs
- `PointerPitchOffset` (degrees, default ~-35), `PointerYawOffset` (default 0)
- `DebugLogging` (bool)

## Conventions

- Plugin GUID: `aussie1579.spaz2.framecontrols`. Name: `SPAZ2 Frame Controls`.
- Use C# 7.3-compatible syntax unless the project's LangVersion allows newer while still targeting net4x.
- Keep patches isolated per concern: `StickInputPatch` (also covers command edges, so no separate `CommandFlickPatch`), `PointerOffsetPatch`.
- Patches should fail soft: if a target method isn't found, log a warning and skip, don't crash the game.
- Flat (non-VR) mode must be unaffected. Gate patches on VR being active.

## Repo layout (target)

```
SPAZ2FrameControls/
  CLAUDE.md
  NOTES.md            # reverse-engineering findings
  src/                # plugin source + .csproj
  ref/                # copied game DLLs (gitignored)
  decompiled/         # ilspycmd output (gitignored)
  README.md           # install + config instructions for end users
```

## Fallback

If the input layer can't be patched cleanly, a custom SteamVR binding is the fallback (Settings → Controllers → Manage Controller Bindings → SPAZ 2 → Custom). There, map stick *touch* to the pad click. It fixes movement but not commands. It can be exported and shared, so document it in the README either way.
