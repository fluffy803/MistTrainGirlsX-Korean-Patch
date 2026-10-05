using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace MistX.Demosaic
{
    [BepInPlugin(GUID, "MistX Demosaic", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string GUID = "mistx.demosaic";
        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;

            var h = new Harmony(GUID);
            h.PatchAll(typeof(ShaderFindPatch));
            Log.LogInfo("Demosaic patch applied: Spine/SkeletonMosaic -> Spine/Skeleton");
        }
    }

    [HarmonyPatch(typeof(Shader), nameof(Shader.Find))]
    internal static class ShaderFindPatch
    {
        private static bool _logged;

        private static void Prefix(ref string name)
        {
            if (name == "Spine/SkeletonMosaic")
            {
                name = "Spine/Skeleton";
                if (!_logged)
                {
                    _logged = true;
                    Plugin.Log.LogInfo("Intercepted Spine/SkeletonMosaic request -> Spine/Skeleton (mosaic disabled)");
                }
            }
        }
    }
}
