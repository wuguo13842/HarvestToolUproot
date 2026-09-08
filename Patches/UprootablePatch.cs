using HarmonyLib;

namespace HarvestToolUproot.Patches
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
    }
}