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

        // ---------- 追加到菜单的额外过滤器（原有 5 项语义分类 + 这 8 项） ----------
        private static readonly string[] ExtraFilters = new[]
        {
            ToolParameterMenu.FILTERLAYERS.WIRES,
            ToolParameterMenu.FILTERLAYERS.LIQUIDCONDUIT,
            ToolParameterMenu.FILTERLAYERS.GASCONDUIT,
            ToolParameterMenu.FILTERLAYERS.SOLIDCONDUIT,
            ToolParameterMenu.FILTERLAYERS.BUILDINGS,
            ToolParameterMenu.FILTERLAYERS.LOGIC,
            ToolParameterMenu.FILTERLAYERS.BACKWALL,
            AgricultureFilter,
        };

        // ---------- 反射缓存 ----------
        private static readonly FieldInfo _currentFiltersField;
        private static readonly MethodInfo _prioritizableOnSpawn;

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

        // ---------- 1. 工具激活 ----------
		[HarmonyPostfix]
		[HarmonyPatch("OnActivateTool")]
		public static void OnActivateTool_Postfix(PrioritizeTool __instance)
		{
			// ---- 1a. 追加农业 + 物理分类选项 ----
			try
			{
				EnsureExtraFilters(__instance);
			}
			catch (Exception e)
			{
				Debug.LogError($"[AgriHarvestPriority] Add filters failed: {e}");
			}

			// ---- 1b. 全场景补齐 refCount ----
			try
			{
				EnsureAllPlantsPrioritizable();
			}
			catch (Exception e)
			{
				Debug.LogError($"[AgriHarvestPriority] EnsureAllPlantsPrioritizable failed: {e}");
			}

			// ---- 1c. ★ 新增：隐藏"任务优先度"图示，避免与扩长的菜单重叠 ----
			try
			{
				ToolMenu.Instance?.PriorityScreen?.ShowDiagram(false);
			}
			catch (Exception e)
			{
				Debug.LogError($"[AgriHarvestPriority] Hide diagram failed: {e}");
			}
		}

        /// <summary>在原有过滤器之后追加缺失项，不替换。</summary>
        private static void EnsureExtraFilters(PrioritizeTool tool)
        {
            var filters = GetFilters(tool);
            if (filters == null) return;

            var list = new List<ToolParameterMenu.ToggleData>(filters);
            bool added = false;

            foreach (var name in ExtraFilters)
            {
                bool exists = false;
                foreach (var t in list)
                    if (t != null && t.name == name) { exists = true; break; }

                if (!exists)
                {
                    list.Add(new ToolParameterMenu.ToggleData(
                        name, ToolParameterMenu.ToggleState.Off, false));
                    added = true;
                }
            }

            if (!added) return;

            var newFilters = list.ToArray();
            SetFilters(tool, newFilters);

            if (ToolMenu.Instance?.toolParameterMenu != null)
                ToolMenu.Instance.toolParameterMenu.PopulateMenu(newFilters);
        }

        // ---------- 2. ★ C 方案：返回第一个被激活的候选层 ----------
        // 拖拽路径 TryPrioritizeGameObject 与渲染路径 PrioritizableRenderer.renderEveryTickVisitHelper
        // 都调用 GetFilterLayerFromGameObject + IsActiveLayer 单层判定。
        // 只要返回"激活的那个层"，两处路径都会自动通过 → 实现多维度命中。
        [HarmonyPrefix]
        [HarmonyPatch("GetFilterLayerFromGameObject")]
        public static bool GetFilterLayerFromGameObject_Prefix(
            PrioritizeTool __instance, GameObject input, ref string __result)
        {
            if (input == null) return true;

            // 植物 → 永远归入 AGRICULTURE
            if (PlantDetection.IsPlant(input))
            {
                EnsurePlantPrioritizable(input);
                __result = AgricultureFilter;
                return false;
            }

            string semanticLayer = GetSemanticLayer(input);
            string physicalLayer = GetPhysicalLayer(__instance, input);

            // 任一激活 → 返回它（拖拽/渲染都会通过）
            if (semanticLayer != null && __instance.IsActiveLayer(semanticLayer))
            {
                __result = semanticLayer;
                return false;
            }
            if (physicalLayer != null && __instance.IsActiveLayer(physicalLayer))
            {
                __result = physicalLayer;
                return false;
            }

            // 都不激活 → 返回主层（IsActiveLayer 会返回 false，不处理）
            __result = physicalLayer ?? semanticLayer ?? ToolParameterMenu.FILTERLAYERS.OPERATE;
            return false;
        }

        /// <summary>语义分类：建造 / 挖掘 / 清洁。</summary>
        private static string GetSemanticLayer(GameObject go)
        {
            // 建造（Constructable 或已标记拆除）
            if (go.GetComponent<Constructable>() != null)
                return ToolParameterMenu.FILTERLAYERS.CONSTRUCTION;

            var decon = go.GetComponent<Deconstructable>();
            if (decon != null && decon.IsMarkedForDeconstruction())
                return ToolParameterMenu.FILTERLAYERS.CONSTRUCTION;

            // 挖掘
            if (go.GetComponent<Diggable>() != null)
                return ToolParameterMenu.FILTERLAYERS.DIG;

            // 清洁
            if (go.GetComponent<Clearable>() != null
                || go.GetComponent<Moppable>() != null
                || go.GetComponent<StorageLocker>() != null)
                return ToolParameterMenu.FILTERLAYERS.CLEAN;

            return null;
        }

        /// <summary>物理分类：建筑按 ObjectLayer 细分。</summary>
        private static string GetPhysicalLayer(PrioritizeTool tool, GameObject go)
        {
            var building = go.GetComponent<BuildingComplete>();
            if (building != null)
                return tool.GetFilterLayerFromObjectLayer(building.Def.ObjectLayer);

            var uc = go.GetComponent<BuildingUnderConstruction>();
            if (uc != null)
                return tool.GetFilterLayerFromObjectLayer(uc.Def.ObjectLayer);

            return null;
        }

        // ---------- 3. 拖拽时兜底补齐植物 refCount ----------
        [HarmonyPrefix]
        [HarmonyPatch("TryPrioritizeGameObject")]
        public static void TryPrioritizeGameObject_Prefix(GameObject target)
        {
            if (PlantDetection.IsPlant(target))
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
    }
}