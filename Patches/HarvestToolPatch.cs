using HarmonyLib;
using System;
using UnityEngine;

namespace HarvestToolUproot.Patches
{
    [HarmonyPatch(typeof(HarvestTool))]
    public static class HarvestToolPatch
    {
        // 缓存修改后的选项数组
        private static ToolParameterMenu.ToggleData[] _cachedOptions;
        private static readonly int UPROOT_INDEX = 2;
        private static readonly int CANCEL_UPROOT_INDEX = 3;

        // 缓存 RefreshOverlayIcon 委托（注意签名：void RefreshOverlayIcon(object)）
        internal static readonly Action<HarvestDesignatable, object> _refreshIcon;

        static HarvestToolPatch()
        {
            var refreshMethod = AccessTools.Method(typeof(HarvestDesignatable), "RefreshOverlayIcon");
            _refreshIcon = (Action<HarvestDesignatable, object>)Delegate.CreateDelegate(
                typeof(Action<HarvestDesignatable, object>), refreshMethod);
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnPrefabInit")]
        public static void OnPrefabInit_Postfix(HarvestTool __instance)
        {
            var optionsField = AccessTools.Field(typeof(HarvestTool), "options");
            var original = (ToolParameterMenu.ToggleData[])optionsField.GetValue(__instance);
            var newOptions = new ToolParameterMenu.ToggleData[original.Length + 2];
            Array.Copy(original, newOptions, original.Length);
            newOptions[UPROOT_INDEX] = new ToolParameterMenu.ToggleData("UPROOT", ToolParameterMenu.ToggleState.Off, false);
            newOptions[CANCEL_UPROOT_INDEX] = new ToolParameterMenu.ToggleData("CANCEL_UPROOT", ToolParameterMenu.ToggleState.Off, false);
            optionsField.SetValue(__instance, newOptions);
            _cachedOptions = newOptions;
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnDragTool")]
        public static void OnDragTool_Postfix(HarvestTool __instance, int cell, int distFromOrigin)
        {
            bool isUprootMode = _cachedOptions[UPROOT_INDEX].IsOn;
            bool isCancelMode = _cachedOptions[CANCEL_UPROOT_INDEX].IsOn;

            if (!isUprootMode && !isCancelMode) return;
            if (!Grid.IsValidCell(cell)) return;

            GameObject go = null;
            var layers = Grid.ObjectLayers;
            if (layers.Length > 1 && layers[1] != null && layers[1].TryGetValue(cell, out go)) { }
            else if (layers.Length > 5 && layers[5] != null && layers[5].TryGetValue(cell, out go)) { }
            if (go == null) return;

            Uprootable uprootable = go.GetComponent<Uprootable>();
            if (uprootable == null) return;

            if (isUprootMode && uprootable.CanUproot())
            {
                uprootable.MarkForUproot(true);
                var hd = go.GetComponent<HarvestDesignatable>();
                if (hd != null) _refreshIcon(hd, null);   // 注意传 null
            }
            else if (isCancelMode)
            {
                uprootable.ForceCancelUproot(null);
                var hd = go.GetComponent<HarvestDesignatable>();
                if (hd != null) _refreshIcon(hd, null);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnActivateTool")]
        public static void OnActivateTool_Postfix(HarvestTool __instance)
        {
            GameScheduler.Instance.Schedule("RefreshAllHarvestIcons", 0f, (obj) =>
            {
                foreach (var item in Components.HarvestDesignatables.Items)
                    _refreshIcon(item, null);
            }, null);
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnDeactivateTool")]
        public static void OnDeactivateTool_Postfix(HarvestTool __instance)
        {
            foreach (var item in Components.HarvestDesignatables.Items)
                _refreshIcon(item, null);
        }
    }
}