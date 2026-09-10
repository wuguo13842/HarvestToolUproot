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
        internal static readonly System.Action<HarvestDesignatable, object> _refreshIcon;

        // ---------- 事件监听 ----------
        private static HarvestTool _activeTool;
        private static System.Action _optionsChangedDelegate;

        static HarvestToolPatch()
        {
            _optionsField = AccessTools.Field(typeof(HarvestTool), "options");
            if (_optionsField == null)
                throw new Exception("[AgriHarvestPriority] Field 'options' not found.");

            var refreshMethod = AccessTools.Method(typeof(HarvestDesignatable), "RefreshOverlayIcon");
            _refreshIcon = (System.Action<HarvestDesignatable, object>)Delegate.CreateDelegate(
                typeof(System.Action<HarvestDesignatable, object>), refreshMethod);
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

        // ---------- 2. 工具激活：注册监听 + 根据当前选项决定显示范围 ----------
        [HarmonyPostfix]
        [HarmonyPatch("OnActivateTool")]
        public static void OnActivateTool_Postfix(HarvestTool __instance)
        {
            _cachedOptions = (ToolParameterMenu.ToggleData[])_optionsField.GetValue(__instance);
            _activeTool = __instance;

            // ---- 2a. 注册"选项变化"监听 ----
            try
            {
                if (ToolMenu.Instance?.toolParameterMenu != null)
                {
                    _optionsChangedDelegate = OnOptionsChanged;
                    ToolMenu.Instance.toolParameterMenu.onParametersChanged += _optionsChangedDelegate;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AgriHarvestPriority] Register onParametersChanged failed: {e}");
            }

            // ---- 2b. 根据当前选项决定显示范围 ----
            try
            {
                ApplyDisplayByMode(__instance);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AgriHarvestPriority] Initial apply failed: {e}");
            }

            // ---- 2c. 可收获植物仍走 GameScheduler ----
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

                // ★ 刷新观赏性植物图标 + 兜底高亮
                if (DecorativePlantIconPatch.IsDecorativePlant(go))
                {
                    DecorativePlantIconPatch.RefreshOne(uprootable);
                    DecorativePlantOverlayPatch.ApplyOne(uprootable);
                }
            }
            else if (isCancelMode)
            {
                uprootable.ForceCancelUproot(null);
                var hd = go.GetComponent<HarvestDesignatable>();
                if (hd != null) _refreshIcon(hd, null);

                // ★ 刷新观赏性植物图标 + 兜底高亮
                if (DecorativePlantIconPatch.IsDecorativePlant(go))
                {
                    DecorativePlantIconPatch.RefreshOne(uprootable);
                    DecorativePlantOverlayPatch.ApplyOne(uprootable);
                }
            }
        }

        // ---------- 4. 工具关闭：注销监听 + 清理图标 + 恢复高亮 ----------
        [HarmonyPostfix]
        [HarmonyPatch("OnDeactivateTool")]
        public static void OnDeactivateTool_Postfix(HarvestTool __instance)
        {
            // 注销参数变化监听
            try
            {
                if (_optionsChangedDelegate != null && ToolMenu.Instance?.toolParameterMenu != null)
                    ToolMenu.Instance.toolParameterMenu.onParametersChanged -= _optionsChangedDelegate;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AgriHarvestPriority] Unregister onParametersChanged failed: {e}");
            }
            finally
            {
                _optionsChangedDelegate = null;
                _activeTool = null;
            }

            foreach (var item in Components.HarvestDesignatables.Items)
                _refreshIcon(item, null);

            // ★ 清理观赏性植物图标
            DecorativePlantIconPatch.ClearAll();

            // ★ 恢复观赏性植物的原始外观
            try
            {
                DecorativePlantOverlayPatch.RemoveHighlight();
            }
            catch (Exception e)
            {
                Debug.LogError($"[AgriHarvestPriority] RemoveHighlight failed: {e}");
            }
        }

        // ---------- 5. 选项变化回调：按模式决定显示范围 ----------
        private static void OnOptionsChanged()
        {
            try
            {
                if (_activeTool == null) return;

                // 刷新缓存
                _cachedOptions = (ToolParameterMenu.ToggleData[])_optionsField.GetValue(_activeTool);

                ApplyDisplayByMode(_activeTool);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AgriHarvestPriority] OnOptionsChanged failed: {e}");
            }
        }

        // ---------- 6. ★ 核心：按当前模式决定显示范围 ----------
        // 拔除/取消拔除 → 显示所有观赏性植物（图标 + 高亮）
        // 其他模式       → 只显示已拔除的观赏性植物
        private static void ApplyDisplayByMode(HarvestTool tool)
        {
            if (IsUprootOrCancelModeActive(tool))
            {
                // 全部显示
                DecorativePlantIconPatch.RefreshAll();
                DecorativePlantOverlayPatch.ApplyHighlight();
            }
            else
            {
                // 只显示已拔除的
                DecorativePlantIconPatch.RefreshMarkedOnly();
                DecorativePlantOverlayPatch.ApplyHighlightMarkedOnly();
            }
        }

        // ---------- 7. 判断当前是否选中"拔除"或"取消拔除" ----------
        private static bool IsUprootOrCancelModeActive(HarvestTool tool)
        {
            if (tool == null) return false;
            var opts = (ToolParameterMenu.ToggleData[])_optionsField.GetValue(tool);
            if (opts == null || opts.Length < 4) return false;
            return opts[UPROOT_INDEX].IsOn || opts[CANCEL_UPROOT_INDEX].IsOn;
        }
    }
}