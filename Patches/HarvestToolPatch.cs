using HarmonyLib;
using UnityEngine;
using System.Reflection;  // 添加这行

namespace HarvestToolUproot.Patches
{
    [HarmonyPatch(typeof(HarvestTool))]
    public static class HarvestToolPatch
    {
        // 缓存 RefreshOverlayIcon 方法
        private static MethodInfo refreshOverlayIconMethod;

        private static void RefreshIcon(HarvestDesignatable hd)
        {
            if (hd == null) return;
            if (refreshOverlayIconMethod == null)
            {
                refreshOverlayIconMethod = AccessTools.Method(typeof(HarvestDesignatable), "RefreshOverlayIcon");
            }
            refreshOverlayIconMethod?.Invoke(hd, new object[] { null });
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnPrefabInit")]
        public static void OnPrefabInit_Postfix(HarvestTool __instance)
        {
            var optionsField = AccessTools.Field(typeof(HarvestTool), "options");
            var original = (ToolParameterMenu.ToggleData[])optionsField.GetValue(__instance);
            var newOptions = new ToolParameterMenu.ToggleData[original.Length + 1];
            System.Array.Copy(original, newOptions, original.Length);
            newOptions[newOptions.Length - 1] = new ToolParameterMenu.ToggleData(
                "UPROOT",
                ToolParameterMenu.ToggleState.Off,
                false
            );
            optionsField.SetValue(__instance, newOptions);
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnDragTool")]
        public static void OnDragTool_Postfix(HarvestTool __instance, int cell, int distFromOrigin)
        {
            var isOptionOnMethod = AccessTools.Method(typeof(HarvestTool), "IsOptionOn");
            if (!(bool)isOptionOnMethod.Invoke(__instance, new object[] { "UPROOT" })) return;
            if (!Grid.IsValidCell(cell)) return;

            GameObject go = null;
            var layers = Grid.ObjectLayers;
            if (layers.Length > 1 && layers[1] != null && layers[1].TryGetValue(cell, out go)) { }
            else if (layers.Length > 5 && layers[5] != null && layers[5].TryGetValue(cell, out go)) { }

            if (go == null) return;

            Uprootable uprootable = go.GetComponent<Uprootable>();
            if (uprootable != null && uprootable.CanUproot())
            {
                uprootable.MarkForUproot(true);
                var harvestDesignatable = go.GetComponent<HarvestDesignatable>();
                if (harvestDesignatable != null)
                    RefreshIcon(harvestDesignatable);  // 使用反射调用
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnActivateTool")]
        public static void OnActivateTool_Postfix(HarvestTool __instance)
        {
            GameScheduler.Instance.Schedule("RefreshAllHarvestIcons", 0f, (obj) =>
            {
                foreach (var item in Components.HarvestDesignatables.Items)
                {
                    RefreshIcon(item);
                }
            }, null);
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnDeactivateTool")]
        public static void OnDeactivateTool_Postfix(HarvestTool __instance)
        {
            foreach (var item in Components.HarvestDesignatables.Items)
            {
                RefreshIcon(item);
            }
        }
    }
}