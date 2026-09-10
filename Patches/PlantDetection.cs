using System.Collections.Generic;
using UnityEngine;

namespace AgriHarvestPriority.Patches
{
    /// <summary>共享的植物识别与扫描逻辑，避免多处重复。</summary>
    internal static class PlantDetection
    {
        /// <summary>是否为植物（含种子、幼苗）。</summary>
        public static bool IsPlant(GameObject go)
        {
            if (go == null) return false;

            if (go.GetComponent<Growing>() != null) return true;
            if (go.GetComponent<HarvestDesignatable>() != null) return true;

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