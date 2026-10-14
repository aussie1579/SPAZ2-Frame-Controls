using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using SPAZ2FrameControls.Patches;

namespace SPAZ2FrameControls
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "aussie1579.spaz2.framecontrols";
        public const string Name = "SPAZ2 Frame Controls";
        public const string Version = "1.0.0";

        internal static ManualLogSource Log;

        internal static ConfigEntry<bool> RequireClick;
        internal static ConfigEntry<float> StickPressThreshold;
        internal static ConfigEntry<float> FlickReleaseThreshold;
        internal static ConfigEntry<bool> RepressOnDirectionChange;
        internal static ConfigEntry<float> PointerPitchOffset;
        internal static ConfigEntry<float> PointerYawOffset;
        internal static ConfigEntry<bool> OffsetAimCursor;
        internal static ConfigEntry<bool> DebugLogging;

        private Harmony harmony;

        private void Awake()
        {
            Log = Logger;

            RequireClick = Config.Bind("Stick", "RequireClick", false,
                "Restore vanilla behaviour: the stick must be clicked in to register a pad press.");
            StickPressThreshold = Config.Bind("Stick", "StickPressThreshold", 0.5f,
                new ConfigDescription("Stick deflection (0-1) that counts as pressing the pad.",
                    new AcceptableValueRange<float>(0.05f, 1f)));
            FlickReleaseThreshold = Config.Bind("Stick", "FlickReleaseThreshold", 0.35f,
                new ConfigDescription("Once pressed, the pad stays pressed until deflection drops below this. " +
                    "Keep it below StickPressThreshold to avoid chatter.",
                    new AcceptableValueRange<float>(0f, 1f)));
            RepressOnDirectionChange = Config.Bind("Stick", "RepressOnDirectionChange", false,
                "Release the pad for one frame when the stick slides into a different direction " +
                "(up/down/left/right) without returning to centre, so the new direction's command fires.");

            PointerPitchOffset = Config.Bind("Pointer", "PointerPitchOffset", -35f,
                "Laser pointer pitch offset in degrees. Negative tilts the pointer down.");
            PointerYawOffset = Config.Bind("Pointer", "PointerYawOffset", 0f,
                "Laser pointer yaw offset in degrees. Positive turns the pointer right.");
            OffsetAimCursor = Config.Bind("Pointer", "OffsetAimCursor", true,
                "Also rotate the 3D aim cursor attached to the controller by the pointer offset.");

            DebugLogging = Config.Bind("Debug", "DebugLogging", false,
                "Log synthesised pad presses and controller details.");

            Log.LogInfo(Name + " " + Version + " loaded, patching on first frame");
        }

        // Unity 5.4 loads Assembly-CSharp after the chainloader runs. Touching any game type in Awake
        // makes Mono load it first, and Unity then fails to bind every game script (black screen).
        // Patch on the first Update instead, once Unity has loaded the game assembly itself.
        private void Update()
        {
            if (harmony != null)
                return;

            harmony = new Harmony(Guid);
            ApplyPatches();
        }

        private void ApplyPatches()
        {
            Apply("StickInputPatch", StickInputPatch.Apply);
            Apply("PointerOffsetPatch", PointerOffsetPatch.Apply);
        }

        private void Apply(string patchName, Func<Harmony, bool> apply)
        {
            try
            {
                if (apply(harmony))
                    Log.LogInfo(patchName + " applied");
                else
                    Log.LogWarning(patchName + " skipped: target method not found");
            }
            catch (Exception e)
            {
                Log.LogError(patchName + " failed: " + e);
            }
        }

        internal static void Debug(string message)
        {
            if (DebugLogging.Value)
                Log.LogInfo(message);
        }
    }
}
