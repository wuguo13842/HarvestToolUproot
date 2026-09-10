using HarmonyLib;

namespace AgriHarvestPriority.Patches
{
    [HarmonyPatch(typeof(Uprootable))]
    public static class UprootablePatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("OnCancel")]
        public static void OnCancel_Postfix(Uprootable __instance)
        {
            var hd = __instance.GetComponent<HarvestDesignatable>();
            if (hd != null) HarvestToolPatch._refreshIcon(hd, null);
        }

        [HarmonyPostfix]
        [HarmonyPatch("ForceCancelUproot")]
        public static void ForceCancelUproot_Postfix(Uprootable __instance)
        {
            var hd = __instance.GetComponent<HarvestDesignatable>();
            if (hd != null) HarvestToolPatch._refreshIcon(hd, null);
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnCleanUp")]
        public static void OnCleanUp_Postfix(Uprootable __instance)
        {
            if (__instance != null)
            {
                // ★ 同时清理图标和高亮字典
                DecorativePlantIconPatch.RemoveOne(__instance.gameObject);
                DecorativePlantOverlayPatch.RemoveOne(__instance.gameObject);
            }
        }
    }
}