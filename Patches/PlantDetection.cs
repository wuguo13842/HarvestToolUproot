using System.Collections.Generic;
using UnityEngine;

namespace AgriHarvestPriority.Patches
{
    /// <summary>共享的植物识别与扫描逻辑，避免多处重复。</summary>
    internal static class PlantDetection
    {
        /// <summary>是否为植物（不含散落的种子）。</summary>
        public static bool IsPlant(GameObject go)
        {
            if (go == null) return false;

            // 已种下的植物：有 Growing 组件（幼苗/成熟都包含）
            if (go.GetComponent<Growing>() != null) return true;
            // 可收获的植物
            if (go.GetComponent<HarvestDesignatable>() != null) return true;

            // 野生植物等：靠标签识别
            // ★ 注意：不检查 Seed / CropSeed，避免把地上散落的种子也算进来
            var kpid = go.GetComponent<KPrefabID>();
            if (kpid != null)
            {
                return kpid.HasTag(GameTags.Plant)
                    // || kpid.HasTag(GameTags.Seed) //不含散落的种子
                    // || kpid.HasTag(GameTags.CropSeed) //不含散落的种子
                    || kpid.HasTag(GameTags.Harvestable);
            }
            return false;
        }

        /// <summary>是否为观赏性植物（有 Uprootable、无 HarvestDesignatable、有 DecorProvider）。</summary>
        public static bool IsDecorativePlant(GameObject go)
        {
            if (go == null) return false;
            if (go.GetComponent<Uprootable>() == null) return false;
            if (go.GetComponent<HarvestDesignatable>() != null) return false;
            return go.GetComponent<DecorProvider>() != null;
        }

        /// <summary>扫描全场景的装饰性植物，供多个刷新路径共享。</summary>
        public static List<Uprootable> ScanDecorativePlants()
        {
            var result = new List<Uprootable>();
            var all = Object.FindObjectsOfType<Uprootable>();
            if (all == null) return result;

            foreach (var up in all)
                if (up != null && up.gameObject != null && IsDecorativePlant(up.gameObject))
                    result.Add(up);

            return result;
        }
    }
}