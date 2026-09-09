using HarmonyLib;
using System;
using UnityEngine;
using System.Reflection;

namespace AgriHarvestPriority.Patches
{
    [HarmonyPatch(typeof(HarvestTool))]
    public static class HarvestToolPatch
    {
        private static ToolParameterMenu.ToggleData[] _cachedOptions;
        private static readonly int UPROOT_INDEX = 2;
        private static readonly int CANCEL_UPROOT_INDEX = 3;
        private const string UPROOT_KEY = "UPROOT";
        private const string CANCEL_UPROOT_KEY = "CANCEL_UPROOT";

        // ✅ 缓存字段，避免重复反射
        private static readonly FieldInfo _optionsField;
        internal static readonly Action<HarvestDesignatable, object> _refreshIcon;

        static HarvestToolPatch()
        {
            _optionsField = AccessTools.Field(typeof(HarvestTool), "options");
            if (_optionsField == null)
                throw new Exception("Field 'options' not found in HarvestTool.");

            var refreshMethod = AccessTools.Method(typeof(HarvestDesignatable), "RefreshOverlayIcon");
            _refreshIcon = (Action<HarvestDesignatable, object>)Delegate.CreateDelegate(
                typeof(Action<HarvestDesignatable, object>), refreshMethod);
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnPrefabInit")]
        public static void OnPrefabInit_Postfix(HarvestTool __instance)
        {
            var original = (ToolParameterMenu.ToggleData[])_optionsField.GetValue(__instance);
            var newOptions = new ToolParameterMenu.ToggleData[original.Length + 2];
            Array.Copy(original, newOptions, original.Length);
            newOptions[UPROOT_INDEX] = new ToolParameterMenu.ToggleData(UPROOT_KEY, ToolParameterMenu.ToggleState.Off, false);
            newOptions[CANCEL_UPROOT_INDEX] = new ToolParameterMenu.ToggleData(CANCEL_UPROOT_KEY, ToolParameterMenu.ToggleState.Off, false);
            _optionsField.SetValue(__instance, newOptions);
            _cachedOptions = newOptions;
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnActivateTool")]
        public static void OnActivateTool_Postfix(HarvestTool __instance)
        {
            // ✅ 激活工具时刷新缓存，与实例同步
            _cachedOptions = (ToolParameterMenu.ToggleData[])_optionsField.GetValue(__instance);

            GameScheduler.Instance.Schedule("RefreshAllHarvestIcons", 0f, (obj) =>
            {
                foreach (var item in Components.HarvestDesignatables.Items)
                    _refreshIcon(item, null);
            }, null);
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnDragTool")]
        public static void OnDragTool_Postfix(HarvestTool __instance, int cell, int distFromOrigin)
        {
            // ✅ 双重保险：缓存失效时重新读取（极少数情况）
            if (_cachedOptions == null || _cachedOptions.Length < 4)
            {
                _cachedOptions = (ToolParameterMenu.ToggleData[])_optionsField.GetValue(__instance);
                if (_cachedOptions == null || _cachedOptions.Length < 4) return;
            }

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
                if (hd != null) _refreshIcon(hd, null);
            }
            else if (isCancelMode)
            {
                uprootable.ForceCancelUproot(null);
                var hd = go.GetComponent<HarvestDesignatable>();
                if (hd != null) _refreshIcon(hd, null);
            }
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