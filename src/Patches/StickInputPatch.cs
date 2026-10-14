using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;
using Valve.VR;

namespace SPAZ2FrameControls.Patches
{
    // The game only acts on the pad when it is clicked (bit 32 of ulButtonPressed), then reads the
    // axis to decide which direction was pressed. Device.Update() caches the controller state once
    // per frame and every GetPress/GetPressDown/GetPressUp is a bit test on that cache, so setting
    // the bit from stick deflection here gives Vive-like behaviour to every caller at once.
    internal static class StickInputPatch
    {
        private const ulong PadMask = SteamVR_Controller.ButtonMask.Touchpad;

        // Per device index: true when axis 0 is a real trackpad (Vive wand), which needs no help.
        private static readonly Dictionary<uint, bool> trackpadDevices = new Dictionary<uint, bool>();

        public static bool Apply(Harmony harmony)
        {
            var target = AccessTools.Method(typeof(SteamVR_Controller.Device), "Update");
            if (target == null)
                return false;

            harmony.Patch(target,
                prefix: new HarmonyMethod(typeof(StickInputPatch), nameof(Prefix)),
                postfix: new HarmonyMethod(typeof(StickInputPatch), nameof(Postfix)));
            return true;
        }

        // __state is true when this call is the one that refreshes the cached state this frame.
        private static void Prefix(int ___prevFrameCount, out bool __state)
        {
            __state = ___prevFrameCount != Time.frameCount;
        }

        private static void Postfix(SteamVR_Controller.Device __instance, bool __state,
            ref VRControllerState_t ___state, VRControllerState_t ___prevState)
        {
            if (!__state || Plugin.RequireClick.Value)
                return;

            if (IsTrackpad(__instance.index))
                return;

            bool realClick = (___state.ulButtonPressed & PadMask) != 0;
            bool wasPressed = (___prevState.ulButtonPressed & PadMask) != 0;

            float x = ___state.rAxis0.x;
            float y = ___state.rAxis0.y;
            float deflection = Mathf.Sqrt(x * x + y * y);
            float threshold = wasPressed ? Plugin.FlickReleaseThreshold.Value : Plugin.StickPressThreshold.Value;
            bool tilted = deflection > threshold;

            if (tilted && wasPressed && !realClick && Plugin.RepressOnDirectionChange.Value
                && Sector(x, y) != Sector(___prevState.rAxis0.x, ___prevState.rAxis0.y))
            {
                tilted = false;
                Plugin.Debug("Device " + __instance.index + ": direction change, re-pressing");
            }

            bool pressed = realClick || tilted;
            if (pressed)
                ___state.ulButtonPressed |= PadMask;
            else
                ___state.ulButtonPressed &= ~PadMask;

            if (pressed != wasPressed)
                Plugin.Debug("Device " + __instance.index + ": pad " + (pressed ? "down" : "up")
                    + " axis=(" + x.ToString("F2") + ", " + y.ToString("F2") + ")"
                    + (realClick ? " [click]" : ""));
        }

        // Matches the dominant-axis test in VR_Utility.Vive_CheckTouchpadButton.
        private static int Sector(float x, float y)
        {
            if (Mathf.Abs(x) > Mathf.Abs(y))
                return x > 0f ? 1 : 3;
            return y > 0f ? 0 : 2;
        }

        private static bool IsTrackpad(uint index)
        {
            bool trackpad;
            if (trackpadDevices.TryGetValue(index, out trackpad))
                return trackpad;

            var system = OpenVR.System;
            if (system == null)
                return false;

            // Empty slots and not-yet-connected devices report no model; check again later.
            var error = ETrackedPropertyError.TrackedProp_Success;
            var model = new StringBuilder(256);
            system.GetStringTrackedDeviceProperty(index, ETrackedDeviceProperty.Prop_ModelNumber_String, model, 256, ref error);
            if (model.Length == 0)
                return false;

            var axis0 = (EVRControllerAxisType)system.GetInt32TrackedDeviceProperty(index, ETrackedDeviceProperty.Prop_Axis0Type_Int32, ref error);
            trackpad = axis0 == EVRControllerAxisType.k_eControllerAxis_TrackPad;
            trackpadDevices[index] = trackpad;
            Plugin.Log.LogInfo("Controller " + index + ": model='" + model + "' axis0Type=" + axis0
                + (trackpad ? " (trackpad, left unchanged)" : ""));
            return trackpad;
        }
    }
}
