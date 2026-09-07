using HarmonyLib;
using UnityEngine;
using HarvestToolUproot.Components;

namespace HarvestToolUproot.Patches
{
    [HarmonyPatch(typeof(HarvestDesignatable))]
    public static class HarvestDesignatablePatch
    {
        [HarmonyPostfix]
        [HarmonyPatch("RefreshOverlayIcon")]
        public static void RefreshOverlayIcon_Postfix(HarvestDesignatable __instance)
        {
            // 检查是否有 Uprootable 组件
            Uprootable uprootable = __instance.GetComponent<Uprootable>();
            if (uprootable == null) return;

            // 如果植物被标记拔除，强制隐藏收获图标
            if (uprootable.IsMarkedForUproot)
            {
                if (__instance.HarvestWhenReadyOverlayIcon != null)
                {
                    __instance.HarvestWhenReadyOverlayIcon.gameObject.SetActive(false);
                }
            }
        }
    }
}