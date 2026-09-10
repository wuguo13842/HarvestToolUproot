using System.Collections.Generic;
using UnityEngine;

namespace AgriHarvestPriority.Patches
{
    /// <summary>
    /// 让观赏性植物在收割覆盖层下也显示高亮。
    /// 原理：模仿 OverlayModes.Harvest 对其他植物的处理，手动设置
    /// KBatchedAnimController.HighlightColour 与层，工具关闭时恢复。
    /// </summary>
    public static class DecorativePlantOverlayPatch
    {
        // OverlayModes.Harvest 使用的高亮颜色（源码硬编码）
        private static readonly Color HarvestHighlight = new Color(0.65f, 0.65f, 0.65f, 0.65f);

        private struct AppliedEntry
        {
            public KBatchedAnimController kbac;
            public int defaultLayer;
        }

        // ★ 直接存储组件引用，RemoveHighlight 时无需再次扫描
        private static readonly Dictionary<int, AppliedEntry> _applied = new Dictionary<int, AppliedEntry>();

        // 缓存 MaskedOverlay 层（避免每次查询）
        private static int _maskedOverlayLayer = -2;

        private static int GetMaskedOverlayLayer()
        {
            if (_maskedOverlayLayer == -2)
                _maskedOverlayLayer = LayerMask.NameToLayer("MaskedOverlay");
            return _maskedOverlayLayer;
        }

        /// <summary>接收已扫描好的装饰性植物列表。</summary>
        public static void ApplyFrom(List<Uprootable> decorativePlants, bool includeUnmarked)
        {
            try
            {
                int targetLayer = GetMaskedOverlayLayer();
                if (targetLayer < 0)
                {
                    Debug.LogWarning("[AgriHarvestPriority] MaskedOverlay layer not found");
                    return;
                }

                int appliedCount = 0;

                foreach (var up in decorativePlants)
                {
                    if (up == null || up.gameObject == null) continue;

                    int id = up.gameObject.GetInstanceID();

                    if (!includeUnmarked && !up.IsMarkedForUproot)
                    {
                        // 未标记 → 若有已应用的高亮则恢复
                        // ★ .NET Framework 4.8 不支持 Remove(key, out value)，改用 TryGetValue + Remove
                        if (_applied.TryGetValue(id, out var entry))
                        {
                            if (entry.kbac != null)
                            {
                                entry.kbac.HighlightColour = Color.clear;
                                entry.kbac.SetLayer(entry.defaultLayer);
                            }
                            _applied.Remove(id);
                        }
                        continue;
                    }

                    if (ApplyOneInternal(up, id, targetLayer))
                        appliedCount++;
                }

                if (appliedCount > 0)
                    Debug.Log($"[AgriHarvestPriority] Applied highlight to {appliedCount} decorative plants");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AgriHarvestPriority] ApplyFrom failed: {e}");
            }
        }

        /// <summary>单个植物应用高亮（拖拽时按需调用）。</summary>
        public static bool ApplyOne(Uprootable up)
        {
            if (up == null || up.gameObject == null) return false;
            int targetLayer = GetMaskedOverlayLayer();
            if (targetLayer < 0) return false;
            return ApplyOneInternal(up, up.gameObject.GetInstanceID(), targetLayer);
        }

        /// <summary>★ 单个清理（应对 instanceID 重用与正常销毁）。</summary>
        public static void RemoveOne(GameObject go)
        {
            if (go == null) return;
            int id = go.GetInstanceID();
            // ★ .NET Framework 4.8 不支持 Remove(key, out value)，改用 TryGetValue + Remove
            if (_applied.TryGetValue(id, out var entry))
            {
                if (entry.kbac != null)
                {
                    entry.kbac.HighlightColour = Color.clear;
                    entry.kbac.SetLayer(entry.defaultLayer);
                }
                _applied.Remove(id);
            }
        }

        private static bool ApplyOneInternal(Uprootable up, int id, int targetLayer)
        {
            if (_applied.ContainsKey(id)) return false;

            var kbac = up.GetComponent<KBatchedAnimController>();
            if (kbac == null) return false;

            var kpid = up.GetComponent<KPrefabID>();
            int defaultLayer = kpid != null ? kpid.defaultLayer : 0;

            _applied[id] = new AppliedEntry { kbac = kbac, defaultLayer = defaultLayer };

            kbac.HighlightColour = HarvestHighlight;
            kbac.SetLayer(targetLayer);
            return true;
        }

        /// <summary>恢复所有已应用的高亮（直接迭代字典，不再扫描场景）。</summary>
        public static void RemoveHighlight()
        {
            try
            {
                foreach (var kv in _applied)
                {
                    var entry = kv.Value;
                    if (entry.kbac != null)
                    {
                        entry.kbac.HighlightColour = Color.clear;
                        entry.kbac.SetLayer(entry.defaultLayer);
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AgriHarvestPriority] RemoveHighlight failed: {e}");
            }
            finally
            {
                _applied.Clear();
            }
        }
    }
}