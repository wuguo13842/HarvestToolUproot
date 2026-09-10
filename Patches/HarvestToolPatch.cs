using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace AgriHarvestPriority.Patches
{
    [HarmonyPatch(typeof(HarvestTool))]
    public static class HarvestToolPatch
    {
        // ---------- 常量 ----------
        private const int UPROOT_INDEX = 2;
        private const int CANCEL_UPROOT_INDEX = 3;
        private const string UPROOT_KEY = "UPROOT";
        private const string CANCEL_UPROOT_KEY = "CANCEL_UPROOT";

        // ---------- 缓存 ----------
        private static ToolParameterMenu.ToggleData[] _cachedOptions;
        private static readonly FieldInfo _optionsField;
        internal static readonly Action<HarvestDesignatable, object> _refreshIcon;

        static HarvestToolPatch()
        {
            _optionsField = AccessTools.Field(typeof(HarvestTool), "options");
            if (_optionsField == null)
                throw new Exception("[AgriHarvestPriority] Field 'options' not found.");

            var refreshMethod = AccessTools.Method(typeof(HarvestDesignatable), "RefreshOverlayIcon");
            _refreshIcon = (Action<HarvestDesignatable, object>)Delegate.CreateDelegate(
                typeof(Action<HarvestDesignatable, object>), refreshMethod);
        }

        /// <summary>
        /// 清空缓存。由 Mod.OnStartGame（RunAt.OnStartGame）在每次进入游戏世界时调用。
        /// </summary>
        internal static void InvalidateCache()
        {
            _cachedOptions = null;
            Debug.Log("[AgriHarvestPriority] HarvestTool cache invalidated (OnStartGame)");
        }

        // ---------- 1. 初始化选项 ----------
        [HarmonyPostfix]
        [HarmonyPatch("OnPrefabInit")]
        public static void OnPrefabInit_Postfix(HarvestTool __instance)
        {
            var original = (ToolParameterMenu.ToggleData[])_optionsField.GetValue(__instance);
            if (original == null) return;

            var newOptions = new ToolParameterMenu.ToggleData[original.Length + 2];
            Array.Copy(original, newOptions, original.Length);
            newOptions[UPROOT_INDEX] = new ToolParameterMenu.ToggleData(
                UPROOT_KEY, ToolParameterMenu.ToggleState.Off, false);
            newOptions[CANCEL_UPROOT_INDEX] = new ToolParameterMenu.ToggleData(
                CANCEL_UPROOT_KEY, ToolParameterMenu.ToggleState.Off, false);

            _optionsField.SetValue(__instance, newOptions);
            _cachedOptions = newOptions;
        }

		// ---------- 2. 工具激活：同步刷新观赏性植物图标 + 异步刷新可收获植物 ----------
		[HarmonyPostfix]
		[HarmonyPatch("OnActivateTool")]
		public static void OnActivateTool_Postfix(HarvestTool __instance)
		{
			_cachedOptions = (ToolParameterMenu.ToggleData[])_optionsField.GetValue(__instance);

			// ★ 同步刷新观赏性植物图标（不走 GameScheduler，避免异步时序问题）
			try
			{
				DecorativePlantIconPatch.RefreshAll();
			}
			catch (Exception e)
			{
				Debug.LogError($"[AgriHarvestPriority] RefreshAll failed: {e}");
			}

			// 可收获植物仍走 GameScheduler（保持原版节奏）
			GameScheduler.Instance.Schedule("RefreshAllHarvestIcons", 0f, (obj) =>
			{
				foreach (var item in Components.HarvestDesignatables.Items)
					_refreshIcon(item, null);
			}, null);
		}

        // ---------- 3. 拖动时使用缓存（含双重兜底） ----------
        [HarmonyPostfix]
        [HarmonyPatch("OnDragTool")]
        public static void OnDragTool_Postfix(HarvestTool __instance, int cell, int distFromOrigin)
        {
            // 兜底保护：OnStartGame 已清空，或游戏重置了数组内容时重建
            if (_cachedOptions == null
                || _cachedOptions.Length < 4
                || _cachedOptions[UPROOT_INDEX].name != UPROOT_KEY
                || _cachedOptions[CANCEL_UPROOT_INDEX].name != CANCEL_UPROOT_KEY)
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

                // ★ 刷新观赏性植物图标
                if (DecorativePlantIconPatch.IsDecorativePlant(go))
                    DecorativePlantIconPatch.RefreshOne(uprootable);
            }
            else if (isCancelMode)
            {
                uprootable.ForceCancelUproot(null);
                var hd = go.GetComponent<HarvestDesignatable>();
                if (hd != null) _refreshIcon(hd, null);

                // ★ 刷新观赏性植物图标
                if (DecorativePlantIconPatch.IsDecorativePlant(go))
                    DecorativePlantIconPatch.RefreshOne(uprootable);
            }
        }

        // ---------- 4. 工具关闭：刷新可收获植物 + 清理观赏性植物图标 ----------
        [HarmonyPostfix]
        [HarmonyPatch("OnDeactivateTool")]
        public static void OnDeactivateTool_Postfix(HarvestTool __instance)
        {
            foreach (var item in Components.HarvestDesignatables.Items)
                _refreshIcon(item, null);

            // ★ 清理观赏性植物图标
            DecorativePlantIconPatch.ClearAll();
        }
    }
}