using HarmonyLib;
using System;
using UnityEngine;

namespace YourModName.Patches
{
    [HarmonyPatch(typeof(PrioritizeTool))]
    public static class PrioritizeToolPatch
    {
        // ---------- 1. 添加“农业”选项到菜单 ----------
        [HarmonyPrefix]
        [HarmonyPatch("GetDefaultFilters")]
        public static bool GetDefaultFilters_Prefix(PrioritizeTool __instance, out ToolParameterMenu.ToggleData[] filters)
        {
            filters = new ToolParameterMenu.ToggleData[]
            {
                new ToolParameterMenu.ToggleData(ToolParameterMenu.FILTERLAYERS.ALL, ToolParameterMenu.ToggleState.On, false),
                new ToolParameterMenu.ToggleData(ToolParameterMenu.FILTERLAYERS.CONSTRUCTION, ToolParameterMenu.ToggleState.Off, false),
                new ToolParameterMenu.ToggleData(ToolParameterMenu.FILTERLAYERS.DIG, ToolParameterMenu.ToggleState.Off, false),
                new ToolParameterMenu.ToggleData(ToolParameterMenu.FILTERLAYERS.CLEAN, ToolParameterMenu.ToggleState.Off, false),
                new ToolParameterMenu.ToggleData(ToolParameterMenu.FILTERLAYERS.OPERATE, ToolParameterMenu.ToggleState.Off, false),
                new ToolParameterMenu.ToggleData("AGRICULTURE", ToolParameterMenu.ToggleState.Off, false)
            };
            return false;
        }

        // ---------- 2. 修改 GetFilterLayerFromGameObject，让植物返回“AGRICULTURE”层 ----------
        [HarmonyPrefix]
        [HarmonyPatch("GetFilterLayerFromGameObject")]
        public static bool GetFilterLayerFromGameObject_Prefix(PrioritizeTool __instance, GameObject input, ref string __result)
        {
            // 检查农业模式是否开启
            bool isAgricultureMode = __instance.IsActiveLayer("AGRICULTURE");
            
            // 如果农业模式开启，且目标是植物，返回“AGRICULTURE”层
            if (isAgricultureMode && IsPlant(input))
            {
                __result = "AGRICULTURE";
                return false; // 跳过原方法
            }
            
            // 否则执行原逻辑
            return true;
        }

        // ---------- 3. 修改 OnDragTool，农业模式下只处理植物 ----------
        [HarmonyPrefix]
        [HarmonyPatch("OnDragTool")]
        public static bool OnDragTool_Prefix(PrioritizeTool __instance, int cell, int distFromOrigin)
        {
            bool isAgricultureMode = __instance.IsActiveLayer("AGRICULTURE");
            if (!isAgricultureMode) return true;

            PrioritySetting lastSelectedPriority = ToolMenu.Instance.PriorityScreen.GetLastSelectedPriority();
            int count = 0;

            for (int i = 0; i < 45; i++)
            {
                GameObject go = Grid.Objects[cell, i];
                if (go == null) continue;

                Pickupable pickupable = go.GetComponent<Pickupable>();
                if (pickupable != null)
                {
                    ObjectLayerListItem item = pickupable.objectLayerListItem;
                    while (item != null)
                    {
                        GameObject itemGO = item.gameObject;
                        item = item.nextItem;
                        if (itemGO != null && itemGO.GetComponent<MinionIdentity>() == null)
                        {
                            if (IsPlant(itemGO) && TrySetPriority(itemGO, lastSelectedPriority))
                                count++;
                        }
                    }
                }
                else
                {
                    if (IsPlant(go) && TrySetPriority(go, lastSelectedPriority))
                        count++;
                }
            }

            if (count > 0)
                PriorityScreen.PlayPriorityConfirmSound(lastSelectedPriority);
            
            return false;
        }

        private static bool TrySetPriority(GameObject go, PrioritySetting priority)
        {
            Prioritizable p = go.GetComponent<Prioritizable>();
            if (p == null || !p.showIcon || !p.IsPrioritizable()) return false;
            p.SetMasterPriority(priority);
            return true;
        }

        // 判断是否为植物（供多个补丁共享）
        internal static bool IsPlant(GameObject go)
        {
            if (go == null) return false;

            KPrefabID kpid = go.GetComponent<KPrefabID>();
            if (kpid != null)
            {
                if (kpid.HasTag(GameTags.Plant) ||
                    kpid.HasTag(GameTags.Seed) ||
                    kpid.HasTag(GameTags.CropSeed) ||
                    kpid.HasTag(GameTags.Harvestable))
                    return true;
            }

            string name = go.name.ToLower();
            if (name.Contains("plant") || name.Contains("crop") ||
                name.Contains("seed") || name.Contains("growing") ||
                name.Contains("sprout") || name.Contains("vine"))
                return true;

            return false;
        }
    }
}