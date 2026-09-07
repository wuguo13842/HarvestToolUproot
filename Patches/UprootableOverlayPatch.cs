using HarmonyLib;
using UnityEngine;
using HarvestToolUproot.Components;

namespace HarvestToolUproot.Patches
{
    [HarmonyPatch(typeof(Uprootable))]
    public static class UprootableOverlayPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("OnSpawn")]
        public static void OnSpawn_Postfix(Uprootable __instance)
        {
            if (__instance.gameObject.GetComponent<UprootOverlayIcon>() == null)
            {
                __instance.gameObject.AddComponent<UprootOverlayIcon>();
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnCleanUp")]
        public static void OnCleanUp_Postfix(Uprootable __instance)
        {
            var icon = __instance.gameObject.GetComponent<UprootOverlayIcon>();
            if (icon != null)
            {
                Object.Destroy(icon);
            }
        }
    }
}