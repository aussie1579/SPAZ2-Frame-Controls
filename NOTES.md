# Reverse-engineering notes

## Environment

- Install: `G:\Steam\steamapps\common\SPAZ2`
- Unity **5.4.5p1** (from `output_log.txt` and `SPAZ2_64.exe` version 5.4.5.3815293). No `UnityPlayer.dll`, single `UnityEngine.dll`.
- Old Mono runtime (`SPAZ2_64_Data/Mono/mono.dll`, not MonoBleedingEdge), so the **.NET 3.5 profile**. The plugin must target `net35`. BepInEx 5.x x64 (Mono) supports this.
- VR: Unity built-in VR (`UnityEngine.VR.VRSettings`). `loadedDeviceName` is `"OpenVR"` or `"Oculus"`. The log shows `OpenVR initialized!`.
- SteamVR plugin: **legacy** (`SteamVR_Controller.Device`, bitmask buttons). It's compiled into `Assembly-CSharp.dll`, not firstpass. The Oculus Utilities (`OVRInput`) are bundled too.

## Input architecture

`SPAZ2VR_Input` (static) → `VR_Utility` (MonoBehaviour, via `GameManager.GetVRUtility()`) → `SteamVR_Controller.Device`.

- `SPAZ2VR_Input.RegesterControls()` registers each `VR_Inputs` entry as either:
  - a **Vive bitmask** (`ulong`): trigger `1<<33`, menu `1<<1` (2), grip `1<<2` (4), or
  - a **touchpad direction** (`Vector2.up/down/left/right`).
- `VR_Utility.Button / ButtonDown / ButtonUp(VR_Inputs, ...)` on OpenVR:
  ```
  device.GetPressX(member.viveInput)                                   // bitmask path
  || (device.GetPressX(1<<32 /*Touchpad*/) && Vive_CheckTouchpadButton(member, device))
  ```
  `Vive_CheckTouchpadButton` passes when the pad axis points the registered way: dominant axis, and `|component| > viveDpadTouchButtonThreshold` (0.3).
- `VR_Utility.GetAxis_X/GetAxis_Y/GetAxisRaw(hand, mustPressPad, useSweep)`: when `mustPressPad`, returns 0 unless `device.GetPress(1<<32)`. Movement, turning and zoom use `mustPressPad: true` (see `SPAZ2VR_Input.GetAxis`).
  - Menu scrolling (`MenuControls`, `StoryTellerTextScroller`) uses `mustPressPad: false, useSweep: true`, which is swipe-delta based (×20).
- `SteamVR_Controller.Device.Update()` reads `GetControllerStateWithPose` **once per frame** (guarded by `Time.frameCount != prevFrameCount`) into the private fields `state` and `prevState` (`VRControllerState_t`). `GetPress` / `GetPressDown` / `GetPressUp` are pure bit tests on `state` vs `prevState`. `GetAxis()` reads `state.rAxis0`.

### Why "touch = press" breaks commands

`ButtonDown` needs the pad-press **rising edge** *and* a valid direction **on the same frame**. With touch bound as press, the edge fires when the thumb lands on the centred stick (axis ≈ 0), so the direction check fails. Tilting afterwards produces no new edge, so the command never fires. Holds (movement) still work because `GetPress` is level-based.

## Chosen patch point: input (StickInputPatch)

**Postfix on `SteamVR_Controller.Device.Update()`.** On the call that actually refreshes (detected in a prefix: `Time.frameCount != prevFrameCount`), rewrite `state.ulButtonPressed` bit 32:

- pressed = realClick || |rAxis0| > threshold, with hysteresis (lower release threshold). Use `prevState`'s bit as the latch, so no extra state is needed.
- Every downstream call (`GetPress/Down/Up`, movement, commands, button hints) then sees Vive-like semantics. The direction is already past 0.3 when the edge fires, so commands work.
- Optional: on a direction-sector change while held, force one released frame so a new `ButtonDown` fires. Caveat: `ButtonUp` for the *new* sector would also fire that frame.
- This is naturally VR/OpenVR-only: `Device` is only used when `loadedDeviceName == "OpenVR"`.
- `VRControllerState_t` is a struct field, so modify it via `AccessTools.FieldRefAccess` (ref) or a boxed copy written back.

`CommandFlickPatch` probably isn't needed as a separate mechanism, since the threshold edge *is* the flick.

## Pointer (PointerOffsetPatch)

`VR_Utility.LateUpdate` → `ControllerUpdate()` (copies the controller local pose to `VR_HelperTransform_*`) → … → `UpdateRaycast()`.

`UpdateRaycast()`:
- Gameplay ray: `VR_LaserRay = new Ray(helper.position, helper.forward)`. The helper is reparented under the gameplay/starmap camera rig (`VR_TransportControllerToCamera`). Consumers: `GameCameraBase` (aim), `MouseTractor`, `StoryTellerSequence`, via `GetVRLaserRay()`.
- HUD ray and visible line: `controllerGO.position/forward` (`VR_Controller_Right/Left`, the SteamVR tracked object).
- Hands are swapped when the `VR_SwapMotionControlHands` option is set.

Plan: Prefix on `UpdateRaycast` applies `rot * Euler(pitch, yaw, 0)` to both the helper and the controller GO for the active hand. Postfix restores the controller GO's rotation, so the rendered model is unaffected. The helper is overwritten by `ControllerUpdate` every frame anyway. The line, hit dot, HUD cursor and gameplay ray then all agree.
- `VR_AimCursor` is parented to the controller GO. It may need the same offset (check in-headset).

## Other

- `VR_ControllerButtonHints` also reads `SteamVR_Controller.Input` (for showing hints). It's harmless.
- Vive-mode shift: holding grip (`PrimaryShift/SecondaryShift`) zeroes movement axes (`GetTouchpadShiftBlockMult`) so pad directions become shifted commands, e.g. grip + right stick right = CameraToggle.

## Load-order gotcha (Unity 5.4)

The BepInEx chainloader runs when `UnityEngine.dll` loads, which is **before** Unity loads `Assembly-CSharp`. If plugin code touches any game type during `Awake`, Mono loads the assembly early, Unity then can't bind any game script ("The referenced script on this Behaviour is missing!"), and the game shows a black screen. Patches are therefore applied on the plugin's first `Update()`. If that ever proves unreliable, the alternative is changing `[Preloader.Entrypoint]` in `BepInEx.cfg` to a later type (e.g. `Type = MonoBehaviour`).

**Update:** deferring alone wasn't enough. BepInEx itself creating its manager MonoBehaviour during `ReloadAssembly` breaks script binding too. Fix: `BepInEx.cfg` → `[Preloader.Entrypoint] Type = Camera` (the default is `Application`). A backup of the original is at `BepInEx/config/BepInEx.cfg.bak`.
