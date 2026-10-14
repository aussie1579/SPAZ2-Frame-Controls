using HarmonyLib;
using UnityEngine;

namespace SPAZ2FrameControls.Patches
{
    // VR_Utility.UpdateRaycast builds the gameplay ray from VR_HelperTransform_* (a copy of the
    // controller pose under the camera rig) and the visible line / HUD ray from VR_Controller_*.
    // Rotate both for the duration of the call so everything agrees, then restore the controller
    // so its rendered model is not tilted. The helper is rewritten by ControllerUpdate each frame.
    internal static class PointerOffsetPatch
    {
        private static Transform restoreTransform;
        private static Quaternion restoreRotation;

        public static bool Apply(Harmony harmony)
        {
            var raycast = AccessTools.Method(typeof(VR_Utility), "UpdateRaycast");
            if (raycast == null)
                return false;

            harmony.Patch(raycast,
                prefix: new HarmonyMethod(typeof(PointerOffsetPatch), nameof(RaycastPrefix)),
                postfix: new HarmonyMethod(typeof(PointerOffsetPatch), nameof(RaycastPostfix)));

            var initControllers = AccessTools.Method(typeof(VR_Utility), "VR_InitControllers");
            if (initControllers != null)
                harmony.Patch(initControllers,
                    postfix: new HarmonyMethod(typeof(PointerOffsetPatch), nameof(InitControllersPostfix)));
            else
                Plugin.Log.LogWarning("PointerOffsetPatch: VR_InitControllers not found, aim cursor will not be offset");

            return true;
        }

        private static Quaternion Offset
        {
            // Unity's positive X rotation pitches down; config uses negative = down.
            get { return Quaternion.Euler(-Plugin.PointerPitchOffset.Value, Plugin.PointerYawOffset.Value, 0f); }
        }

        private static bool HasOffset
        {
            get { return Plugin.PointerPitchOffset.Value != 0f || Plugin.PointerYawOffset.Value != 0f; }
        }

        private static bool HandsSwapped
        {
            get { return GameManager.GetVROptions().GetBoolData("VR_SwapMotionControlHands"); }
        }

        private static void RaycastPrefix(VR_Utility __instance)
        {
            restoreTransform = null;
            if (!HasOffset || !__instance.IsUsingMotionControls())
                return;

            bool swapped = HandsSwapped;
            Transform controller = (swapped ? __instance.VR_Controller_Left : __instance.VR_Controller_Right).transform;
            Transform helper = swapped ? __instance.VR_HelperTransform_Left : __instance.VR_HelperTransform_Right;
            Quaternion offset = Offset;

            restoreTransform = controller;
            restoreRotation = controller.localRotation;
            controller.localRotation = controller.localRotation * offset;
            helper.localRotation = helper.localRotation * offset;
        }

        private static void RaycastPostfix()
        {
            if (restoreTransform == null)
                return;
            restoreTransform.localRotation = restoreRotation;
            restoreTransform = null;
        }

        // The aim cursor is parented to the controller, so swing it round to follow the offset ray.
        private static void InitControllersPostfix(VR_Utility __instance)
        {
            if (!Plugin.OffsetAimCursor.Value || !HasOffset || !__instance.IsUsingMotionControls())
                return;

            Transform cursor = __instance.VR_AimCursor.transform;
            Quaternion offset = Offset;
            cursor.localPosition = offset * cursor.localPosition;
            cursor.localRotation = offset * cursor.localRotation;
        }
    }
}
