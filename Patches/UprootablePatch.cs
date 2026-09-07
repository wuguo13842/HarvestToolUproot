using HarmonyLib;
using System.Reflection;

namespace HarvestToolUproot.Patches
{
    [HarmonyPatch(typeof(Uprootable))]
    public static class UprootablePatch
    {
        private static MethodInfo refreshOverlayIconMethod;

        private static void RefreshHarvestIcon(Uprootable uprootable)
        {
            if (uprootable == null) return;
            var hd = uprootable.GetComponent<HarvestDesignatable>();
            if (hd == null) return;
            if (refreshOverlayIconMethod == null)
            {
                refreshOverlayIconMethod = AccessTools.Method(typeof(HarvestDesignatable), "RefreshOverlayIcon");
            }
            refreshOverlayIconMethod?.Invoke(hd, new object[] { null });
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnCancel")]
        public static void OnCancel_Postfix(Uprootable __instance)
        {
            RefreshHarvestIcon(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch("ForceCancelUproot")]
        public static void ForceCancelUproot_Postfix(Uprootable __instance)
        {
            RefreshHarvestIcon(__instance);
        }
    }
}