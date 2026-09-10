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

        // ---------- 反射缓存 ----------
        private static readonly FieldInfo _currentFiltersField;
        private static readonly MethodInfo _prioritizableOnSpawn;

        // ---------- 性能缓存 ----------
        private static ToolParameterMenu.ToggleData[] _cachedFilters;
        private static int _agricultureIndex = -1;

        static PrioritizeToolPatch()
        {
            _currentFiltersField = AccessTools.Field(typeof(FilteredDragTool), "currentFilters");
            if (_currentFiltersField == null)
                throw new Exception("[AgriHarvestPriority] Field 'currentFilters' not found.");

            _prioritizableOnSpawn = AccessTools.Method(typeof(Prioritizable), "OnSpawn");
            if (_prioritizableOnSpawn == null)
                Debug.LogWarning("[AgriHarvestPriority] Prioritizable.OnSpawn not found; runtime-added components may not register.");
        }

        // ---------- 字段读写 ----------
        private static ToolParameterMenu.ToggleData[] GetFilters(FilteredDragTool tool)
            => (ToolParameterMenu.ToggleData[])_currentFiltersField.GetValue(tool);

        private static void SetFilters(FilteredDragTool tool, ToolParameterMenu.ToggleData[] filters)
            => _currentFiltersField.SetValue(tool, filters);

        /// <summary>重建缓存：抓取最新数组并定位 AGRICULTURE 索引。</summary>
        private static void RefreshCache(PrioritizeTool tool)
        {
            _cachedFilters = GetFilters(tool);
            _agricultureIndex = -1;

            if (_cachedFilters == null) return;

            for (int i = 0; i < _cachedFilters.Length; i++)
            {
                if (_cachedFilters[i] != null && _cachedFilters[i].name == AgricultureFilter)
                {
                    _agricultureIndex = i;
                    break;
                }
            }
        }

        // ---------- 1. 工具激活 ----------
        [HarmonyPostfix]
        [HarmonyPatch("OnActivateTool")]
        public static void OnActivateTool_Postfix(PrioritizeTool __instance)
        {
            // ---- 1a. 追加农业选项并重建缓存 ----
            try
            {
                AppendAgricultureFilter(__instance);
                RefreshCache(__instance);
            }
            catch (Exception e)
            {
                Debug.LogError($"[AgriHarvestPriority] Add filter failed: {e}");
            }

            // ---- 1b. 全场景补齐 refCount（每次激活都扫，保证读档后新植物也显示） ----
            try
            {
                EnsureAllPlantsPrioritizable();
            }
            catch (Exception e)
            {
                Debug.LogError($"[AgriHarvestPriority] EnsureAllPlantsPrioritizable failed: {e}");
            }
        }

        private static void AppendAgricultureFilter(PrioritizeTool tool)
        {
            var filters = GetFilters(tool);

            if (filters != null)
            {
                foreach (var t in filters)
                    if (t != null && t.name == AgricultureFilter) return;
            }

            var list = new List<ToolParameterMenu.ToggleData>(
                filters ?? new ToolParameterMenu.ToggleData[0]);
            list.Add(new ToolParameterMenu.ToggleData(
                AgricultureFilter, ToolParameterMenu.ToggleState.Off, false));
            var newFilters = list.ToArray();

            SetFilters(tool, newFilters);

            if (ToolMenu.Instance?.toolParameterMenu != null)
                ToolMenu.Instance.toolParameterMenu.PopulateMenu(newFilters);
        }

        // ---------- 2. 植物永远归入农业层 ----------
        // 原版 GetFilterLayerFromGameObject 对植物返回 OPERATE（职务），
        // 这里拦截，让植物永远归入农业层，从而不在「职务」筛选下显示。
        [HarmonyPrefix]
        [HarmonyPatch("GetFilterLayerFromGameObject")]
        public static bool GetFilterLayerFromGameObject_Prefix(
            PrioritizeTool __instance, GameObject input, ref string __result)
        {
            if (input == null || !IsPlant(input)) return true;

            // 按需补齐（兜底，避免依赖首次全场景扫描）
            EnsurePlantPrioritizable(input);

            __result = AgricultureFilter;
            return false;
        }

        // ---------- 3. 拖拽时兜底补齐 ----------
        [HarmonyPrefix]
        [HarmonyPatch("TryPrioritizeGameObject")]
        public static void TryPrioritizeGameObject_Prefix(GameObject target)
        {
            if (IsPlant(target))
                EnsurePlantPrioritizable(target);
        }

        // ---------- 4. 全场景补齐（每次激活工具都扫） ----------
        private static void EnsureAllPlantsPrioritizable()
        {
            var plantSet = new HashSet<GameObject>();

            if (Components.HarvestDesignatables != null)
            {
                foreach (var hd in Components.HarvestDesignatables.Items)
                    if (hd != null && hd.gameObject != null) plantSet.Add(hd.gameObject);
            }

            var growing = UnityEngine.Object.FindObjectsOfType<Growing>();
            if (growing != null)
            {
                foreach (var g in growing)
                    if (g != null && g.gameObject != null) plantSet.Add(g.gameObject);
            }

            int added = 0, fixedRef = 0;
            foreach (var go in plantSet)
            {
                var p = go.GetComponent<Prioritizable>();
                bool had = p != null;
                if (EnsurePlantPrioritizable(go))
                {
                    if (had) fixedRef++;
                    else added++;
                }
            }

            Debug.Log($"[AgriHarvestPriority] Scan: plants={plantSet.Count}, newComponents={added}, refFixed={fixedRef}");
        }

        // ---------- 5. 单个植物补齐 ----------
        private static bool EnsurePlantPrioritizable(GameObject go)
        {
            if (go == null) return false;

            try
            {
                var p = go.GetComponent<Prioritizable>();

                if (p == null)
                {
                    // 动态添加组件
                    p = go.AddComponent<Prioritizable>();
                    p.showIcon = true;

                    // 运行时添加的 MonoBehaviour 需手动触发 OnSpawn
                    _prioritizableOnSpawn?.Invoke(p, null);

                    if (!p.IsPrioritizable()) p.AddRef();
                    return true;
                }
                else
                {
                    bool changed = false;
                    if (!p.showIcon) { p.showIcon = true; changed = true; }
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

        // ---------- 6. 判断植物 ----------
        private static bool IsPlant(GameObject go)
        {
            if (go == null) return false;

            // 组件识别（最可靠）
            if (go.GetComponent<Growing>() != null) return true;
            if (go.GetComponent<HarvestDesignatable>() != null) return true;

            // 标签识别（兼容种子、幼苗等）
            var kpid = go.GetComponent<KPrefabID>();
            if (kpid != null)
            {
                return kpid.HasTag(GameTags.Plant)
                    || kpid.HasTag(GameTags.Seed)
                    || kpid.HasTag(GameTags.CropSeed)
                    || kpid.HasTag(GameTags.Harvestable);
            }

            return false;
        }
    }
}