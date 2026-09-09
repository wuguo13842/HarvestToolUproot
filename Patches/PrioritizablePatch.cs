using HarmonyLib;
using UnityEngine;

namespace AgriHarvestPriority.Patches
{
    [HarmonyPatch(typeof(Prioritizable))]
    public static class PrioritizablePatch
    {
        // 额外微调偏移（可手动调整，例如 new Vector2(0f, 0.1f)）
        private static readonly Vector2 EXTRA_OFFSET = new Vector2(0f, 0f);

        [HarmonyPostfix]
        [HarmonyPatch("OnSpawn")]
        public static void OnSpawn_Postfix(Prioritizable __instance)
        {
            if (__instance == null) return;
            AdjustIconOffset(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch("AddRef", new System.Type[] { })]
        public static void AddRef_Postfix(Prioritizable __instance)
        {
            if (__instance == null) return;
            AdjustIconOffset(__instance);
        }

        private static void AdjustIconOffset(Prioritizable __instance)
        {
            // 仅处理悬挂植物（倒着生长）
            var kpid = __instance.GetComponent<KPrefabID>();
            if (kpid == null || !kpid.HasTag(GameTags.Hanging))
                return;

            // 获取占用区域
            var occupyArea = __instance.GetComponent<OccupyArea>();
            if (occupyArea == null)
            {
                __instance.iconOffset = new Vector2(0f, 0.5f);
                return;
            }

            // 获取占用网格的总高度（格数）
            int height = occupyArea.GetHeightInCells();
            if (height <= 0)
            {
                __instance.iconOffset = new Vector2(0f, 0.5f);
                return;
            }

            // 获取水平偏移中心（若有多列，取平均值）
            var offsets = occupyArea.OccupiedCellsOffsets;
            float avgX = 0f;
            if (offsets != null && offsets.Length > 0)
            {
                float sumX = 0f;
                foreach (var offset in offsets)
                    sumX += offset.x;
                avgX = sumX / offsets.Length;
            }

            // 对于倒挂植物，根部在顶部，图标应放在物体上方 height 格处（您测试有效）
            // 如果希望图标在根部上方 0.5 格，可改为 height + 0.5f，但您测试 3 有效，故直接用 height
            float offsetY = height + EXTRA_OFFSET.y;
            float offsetX = avgX + EXTRA_OFFSET.x;

            __instance.iconOffset = new Vector2(offsetX, offsetY);
        }
    }
}