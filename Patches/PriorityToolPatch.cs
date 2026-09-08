using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace YourModName.Patches
{
    [HarmonyPatch(typeof(PrioritizeTool))]
    public static class PrioritizeToolPatch
    {
        private const string AgricultureFilter = "AGRICULTURE";

        // 缓存 currentFilters 字段（基类 FilteredDragTool 的 protected 字段）
        private static readonly FieldInfo _currentFiltersField;

        static PrioritizeToolPatch()
        {
            _currentFiltersField = AccessTools.Field(typeof(FilteredDragTool), "currentFilters");
            if (_currentFiltersField == null)
                throw new Exception("Field 'currentFilters' not found in FilteredDragTool.");
        }

        // 辅助方法：读取 currentFilters
        private static ToolParameterMenu.ToggleData[] GetFilters(FilteredDragTool tool)
        {
            return (ToolParameterMenu.ToggleData[])_currentFiltersField.GetValue(tool);
        }

        // 辅助方法：写入 currentFilters
        private static void SetFilters(FilteredDragTool tool, ToolParameterMenu.ToggleData[] filters)
        {
            _currentFiltersField.SetValue(tool, filters);
        }

        // ---------- 1. 在工具激活时追加“AGRICULTURE”选项 ----------
        [HarmonyPostfix]
        [HarmonyPatch("OnActivateTool")]
        public static void OnActivateTool_Postfix(PrioritizeTool __instance)
        {
            var filters = GetFilters(__instance);

            // 检查是否已存在，避免重复
            foreach (var t in filters)
                if (t.name == AgricultureFilter) return;

            // 追加新选项
            var list = new List<ToolParameterMenu.ToggleData>(filters);
            list.Add(new ToolParameterMenu.ToggleData(AgricultureFilter, ToolParameterMenu.ToggleState.Off, false));
            var newFilters = list.ToArray();

            // 更新 currentFilters
            SetFilters(__instance, newFilters);

            // 刷新菜单，让新增选项立即显示
            ToolMenu.Instance.toolParameterMenu.PopulateMenu(newFilters);
        }

        // ---------- 2. 植物在农业模式激活时返回 AGRICULTURE 层 ----------
        [HarmonyPrefix]
        [HarmonyPatch("GetFilterLayerFromGameObject")]
        public static bool GetFilterLayerFromGameObject_Prefix(PrioritizeTool __instance, GameObject input, ref string __result)
        {
            if (input == null) return true;

            // 检查农业模式是否开启
            var filters = GetFilters(__instance);
            bool agricultureOn = false;
            foreach (var toggle in filters)
                if (toggle.name == AgricultureFilter && toggle.IsOn) { agricultureOn = true; break; }

            if (agricultureOn && IsPlant(input))
            {
                __result = AgricultureFilter;
                return false; // 跳过原方法
            }

            return true; // 继续原逻辑
        }

        // 判断是否为植物（完全基于 GameTags）
        private static bool IsPlant(GameObject go)
        {
            if (go == null) return false;
            var kpid = go.GetComponent<KPrefabID>();
            if (kpid == null) return false;
            return kpid.HasTag(GameTags.Plant) ||
                   kpid.HasTag(GameTags.Seed) ||
                   kpid.HasTag(GameTags.CropSeed) ||
                   kpid.HasTag(GameTags.Harvestable);
        }
    }
}