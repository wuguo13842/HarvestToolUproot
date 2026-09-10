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

            // 1. 图标偏移（针对悬挂植物）
            AdjustIconOffset(__instance);

            // 2. ★ 方案 B：植物生成时立即补齐 refCount
            TryFixPlantRefCount(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch("AddRef", new System.Type[] { })]
        public static void AddRef_Postfix(Prioritizable __instance)
        {
            if (__instance == null) return;
            AdjustIconOffset(__instance);
        }

        // ---------- 方案 B：OnSpawn 时补齐植物的 refCount ----------
        private static void TryFixPlantRefCount(Prioritizable p)
        {
            try
            {
                var go = p.gameObject;
                if (go == null || !PlantDetection.IsPlant(go)) return;   // ★ 改用共享方法

                if (!p.showIcon) p.showIcon = true;
                if (!p.IsPrioritizable()) p.AddRef();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AgriHarvestPriority] TryFixPlantRefCount failed: {e}");
            }
        }

        // ---------- 图标偏移（原有逻辑，完整保留） ----------
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

            // 对于倒挂植物，根部在顶部，图标应放在物体上方 height 格处
            float offsetY = height + EXTRA_OFFSET.y;
            float offsetX = avgX + EXTRA_OFFSET.x;

            __instance.iconOffset = new Vector2(offsetX, offsetY);
        }
    }
}