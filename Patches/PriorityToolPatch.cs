using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace AgriHarvestPriority.Patches
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

        // ---------- 1. 在工具激活时追加“AGRICULTURE”选项 + 补齐植物 refCount ----------
        [HarmonyPostfix]
        [HarmonyPatch("OnActivateTool")]
        public static void OnActivateTool_Postfix(PrioritizeTool __instance)
        {
            // ---- 1a. 追加农业选项（独立 try/catch，避免异常中断后续逻辑） ----
            try
            {
                var filters = GetFilters(__instance);

                // 检查是否已存在，避免重复
                bool hasFilter = false;
                if (filters != null)
                {
                    foreach (var t in filters)
                        if (t.name == AgricultureFilter) { hasFilter = true; break; }
                }

                if (!hasFilter)
                {
                    // 追加新选项
                    var list = new List<ToolParameterMenu.ToggleData>(
                        filters ?? new ToolParameterMenu.ToggleData[0]);
                    list.Add(new ToolParameterMenu.ToggleData(AgricultureFilter, ToolParameterMenu.ToggleState.Off, false));
                    var newFilters = list.ToArray();

                    // 更新 currentFilters
                    SetFilters(__instance, newFilters);

                    // 刷新菜单，让新增选项立即显示
                    if (ToolMenu.Instance != null && ToolMenu.Instance.toolParameterMenu != null)
                        ToolMenu.Instance.toolParameterMenu.PopulateMenu(newFilters);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AgriHarvestPriority] Add filter failed: {e}");
            }

            // ---- 1b. ★ 核心修复：为所有植物补齐 Prioritizable 的 refCount ----
            try
            {
                EnsureAllPlantsPrioritizable();
            }
            catch (Exception e)
            {
                Debug.LogError($"[AgriHarvestPriority] EnsureAllPlantsPrioritizable failed: {e}");
            }
        }

        // ---------- 2. 植物永远返回 AGRICULTURE 层 ----------
        // 说明：原版 GetFilterLayerFromGameObject 对植物返回 OPERATE（职务），
        //       导致植物出现在"职务"筛选里。这里拦截，让植物永远归入农业层，
        //       从而不在"职务"下显示（只在「全部」或「农业」下显示）。
        [HarmonyPrefix]
        [HarmonyPatch("GetFilterLayerFromGameObject")]
        public static bool GetFilterLayerFromGameObject_Prefix(PrioritizeTool __instance, GameObject input, ref string __result)
        {
            if (input == null) return true;

            // 植物：永远返回农业层，避免落入原版的 OPERATE（职务）
            if (IsPlant(input))
            {
                __result = AgricultureFilter;
                return false; // 跳过原方法
            }

            return true; // 继续原逻辑
        }

        // ---------- 3. 拖拽时也顺手补齐（兜底保险） ----------
        [HarmonyPrefix]
        [HarmonyPatch("TryPrioritizeGameObject")]
        public static void TryPrioritizeGameObject_Prefix(GameObject target)
        {
            if (IsPlant(target))
                EnsurePlantPrioritizable(target);
        }

        // ---------- 4. 收集所有植物并补齐 refCount ----------
        private static void EnsureAllPlantsPrioritizable()
        {
            var plantSet = new HashSet<GameObject>();

            // 从 HarvestDesignatable 收集（所有可收获的植物）
            if (Components.HarvestDesignatables != null)
            {
                foreach (var hd in Components.HarvestDesignatables.Items)
                    if (hd != null && hd.gameObject != null) plantSet.Add(hd.gameObject);
            }

            // 从 Growing 收集（幼苗、未成熟植物，可能还没有 HarvestDesignatable）
            var gs = UnityEngine.Object.FindObjectsOfType<Growing>();
            if (gs != null)
            {
                foreach (var g in gs)
                    if (g != null && g.gameObject != null) plantSet.Add(g.gameObject);
            }

            int added = 0, refFixed = 0;
            foreach (var go in plantSet)
            {
                var p = go.GetComponent<Prioritizable>();
                bool hadComponent = p != null;
                bool changed = EnsurePlantPrioritizable(go);
                if (!hadComponent && changed) added++;
                else if (hadComponent && changed) refFixed++;
            }

            Debug.Log($"[AgriHarvestPriority] plants={plantSet.Count}, newComponents={added}, refCountFixed={refFixed}");
        }

        // ---------- 5. 单个植物补齐逻辑 ----------
        private static bool EnsurePlantPrioritizable(GameObject go)
        {
            if (go == null) return false;
            try
            {
                var p = go.GetComponent<Prioritizable>();
                if (p == null)
                {
                    // 没有组件则动态添加
                    p = go.AddComponent<Prioritizable>();
                    p.showIcon = true;

                    // 动态添加的组件需要手动调用 OnSpawn 才注册到分区系统
                    var onSpawn = AccessTools.Method(typeof(Prioritizable), "OnSpawn");
                    onSpawn?.Invoke(p, null);

                    if (!p.IsPrioritizable()) p.AddRef();
                    return true;
                }
                else
                {
                    bool changed = false;
                    if (!p.showIcon) { p.showIcon = true; changed = true; }

                    // ★ 关键：即使已有组件，只要 refCount=0 也要补上（这就是种植植物不显示的根源）
                    if (!p.IsPrioritizable()) { p.AddRef(); changed = true; }

                    return changed;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[AgriHarvestPriority] {go.name}: {e}");
                return false;
            }
        }

        // ---------- 6. 判断是否为植物（只判断植物本体，不含建筑容器） ----------
        private static bool IsPlant(GameObject go)
        {
            if (go == null) return false;

            // 🌱 通过组件识别（最可靠）
            if (go.GetComponent<Growing>() != null) return true;
            if (go.GetComponent<HarvestDesignatable>() != null) return true;

            // 🌍 通过对象层识别（所有植物都在 Plants 层）
            if ((ObjectLayer)go.layer == ObjectLayer.Plants) return true;

            return false;
        }
    }
}