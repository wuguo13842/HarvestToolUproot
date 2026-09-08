using HarmonyLib;
using System;
using UnityEngine;

namespace YourModName.Patches  // 替换为你的命名空间
{
    [HarmonyPatch(typeof(PrioritizeTool))]
    public static class PrioritizeToolPatch
    {
        // ---------- 1. 完全替换 GetDefaultFilters 方法 ----------
        [HarmonyPrefix]
        [HarmonyPatch("GetDefaultFilters")]
        public static bool GetDefaultFilters_Prefix(PrioritizeTool __instance, out ToolParameterMenu.ToggleData[] filters)
        {
            // 构造包含“农业”选项的完整数组
            filters = new ToolParameterMenu.ToggleData[]
            {
                new ToolParameterMenu.ToggleData(ToolParameterMenu.FILTERLAYERS.ALL, ToolParameterMenu.ToggleState.On, false),
                new ToolParameterMenu.ToggleData(ToolParameterMenu.FILTERLAYERS.CONSTRUCTION, ToolParameterMenu.ToggleState.Off, false),
                new ToolParameterMenu.ToggleData(ToolParameterMenu.FILTERLAYERS.DIG, ToolParameterMenu.ToggleState.Off, false),
                new ToolParameterMenu.ToggleData(ToolParameterMenu.FILTERLAYERS.CLEAN, ToolParameterMenu.ToggleState.Off, false),
                new ToolParameterMenu.ToggleData(ToolParameterMenu.FILTERLAYERS.OPERATE, ToolParameterMenu.ToggleState.Off, false),
                // 农业选项（排他性标签，点击后只显示植物）
                new ToolParameterMenu.ToggleData("AGRICULTURE", ToolParameterMenu.ToggleState.Off, false)
            };

            // 返回 false 阻止原方法执行
            return false;
        }

        // ---------- 2. 修改 OnDragTool 方法，实现过滤逻辑 ----------
        [HarmonyPrefix]
        [HarmonyPatch("OnDragTool")]
        public static bool OnDragTool_Prefix(PrioritizeTool __instance, int cell, int distFromOrigin)
        {
            // 检查“农业”选项是否开启
            bool isAgricultureMode = __instance.IsActiveLayer("AGRICULTURE");

            // 如果农业模式未开启，执行原方法
            if (!isAgricultureMode) return true;

            // ----- 农业模式开启：只对植物/作物设置优先度 -----
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
                            if (TryPrioritizeIfAgriculture(__instance, itemGO, lastSelectedPriority)) count++;
                        }
                    }
                }
                else
                {
                    if (TryPrioritizeIfAgriculture(__instance, go, lastSelectedPriority)) count++;
                }
            }

            if (count > 0)
            {
                PriorityScreen.PlayPriorityConfirmSound(lastSelectedPriority);
            }

            return false;
        }

        // 辅助方法：判断并设置优先度
        private static bool TryPrioritizeIfAgriculture(PrioritizeTool tool, GameObject go, PrioritySetting priority)
        {
            string filterLayer = tool.GetFilterLayerFromGameObject(go);
            if (!tool.IsActiveLayer(filterLayer)) return false;

            Prioritizable p = go.GetComponent<Prioritizable>();
            if (p == null || !p.showIcon || !p.IsPrioritizable()) return false;

            if (!IsPlant(go)) return false;

            p.SetMasterPriority(priority);
            return true;
        }

        // 判断是否为植物/作物（仅使用名称和标签，不依赖未知组件）
        private static bool IsPlant(GameObject go)
        {
            if (go == null) return false;

            // ----- 策略1：通过建筑名称判断（植物/作物相关） -----
            Building building = go.GetComponent<Building>();
            if (building != null && building.Def != null)
            {
                string prefabID = building.Def.PrefabID.ToString().ToLower();
                // 关键词聚焦于植物、作物、种植相关
                string[] keywords = {
                    "plant", "crop", "farm", "planter",
                    "hydroponic", "farmstation", "fertilizer",
                    "seed", "growing", "sprout", "vine",
                    "bush", "tree", "flower", "weed"
                };
                foreach (string kw in keywords)
                {
                    if (prefabID.Contains(kw)) return true;
                }
            }

            // ----- 策略2：通过 KPrefabID 的 GameTags 判断 -----
            KPrefabID kpid = go.GetComponent<KPrefabID>();
            if (kpid != null)
            {
                // GameTags.Plant 和 GameTags.Seed 是游戏中真实存在的标签
                if (kpid.HasTag(GameTags.Plant) || kpid.HasTag(GameTags.Seed))
                    return true;
            }

            // ----- 策略3：通过组件类型判断（如果类型存在） -----
            // 注意：如果 Crop 或 PlantableSeed 在你的版本中不存在，可以注释掉
            // 这里只检查确切的组件名称，避免依赖不存在的类型
            // 如果你的游戏版本有这些组件，取消注释即可
            /*
            if (go.GetComponent<Crop>() != null ||
                go.GetComponent<PlantableSeed>() != null ||
                go.GetComponent<BasicPlant>() != null)
            {
                return true;
            }
            */

            return false;
        }
    }
}