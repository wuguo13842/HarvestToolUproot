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

        // 已应用的 GameObject instanceID 集合（用于恢复时精确匹配）
        private static readonly HashSet<int> _appliedIds = new HashSet<int>();

        // 缓存 MaskedOverlay 层（避免每次查询）
        private static int _maskedOverlayLayer = -2;

        private static int GetMaskedOverlayLayer()
        {
            if (_maskedOverlayLayer == -2)
                _maskedOverlayLayer = LayerMask.NameToLayer("MaskedOverlay");
            return _maskedOverlayLayer;
        }

        /// <summary>在拔除/取消拔除模式下调用：为所有观赏性植物应用高亮。</summary>
        public static void ApplyHighlight()
        {
            try
            {
                int targetLayer = GetMaskedOverlayLayer();
                if (targetLayer < 0)
                {
                    Debug.LogWarning("[AgriHarvestPriority] MaskedOverlay layer not found");
                    return;
                }

                var uprootables = Object.FindObjectsOfType<Uprootable>();
                if (uprootables == null) return;

                int applied = 0;
                foreach (var up in uprootables)
                {
                    if (up == null || up.gameObject == null) continue;
                    if (!DecorativePlantIconPatch.IsDecorativePlant(up.gameObject)) continue;
                    if (ApplyOne(up, targetLayer)) applied++;
                }

                Debug.Log($"[AgriHarvestPriority] Applied overlay highlight to {applied} decorative plants");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AgriHarvestPriority] ApplyHighlight failed: {e}");
            }
        }

        /// <summary>
        /// ★ 新增：只高亮"已标记拔除"的观赏性植物，其余恢复。
        /// 用于非拔除/取消拔除模式下，让已拔除的植物仍然可见。
        /// </summary>
        public static void ApplyHighlightMarkedOnly()
        {
            try
            {
                int targetLayer = GetMaskedOverlayLayer();
                if (targetLayer < 0) return;

                var uprootables = Object.FindObjectsOfType<Uprootable>();
                if (uprootables == null) return;

                foreach (var up in uprootables)
                {
                    if (up == null || up.gameObject == null) continue;
                    if (!DecorativePlantIconPatch.IsDecorativePlant(up.gameObject)) continue;

                    if (up.IsMarkedForUproot)
                    {
                        // 已标记 → 保持高亮
                        ApplyOne(up, targetLayer);
                    }
                    else
                    {
                        // 未标记 → 确保恢复
                        int id = up.gameObject.GetInstanceID();
                        if (_appliedIds.Contains(id))
                        {
                            _appliedIds.Remove(id);
                            var kbac = up.GetComponent<KBatchedAnimController>();
                            if (kbac != null)
                            {
                                kbac.HighlightColour = Color.clear;
                                var kpid = up.GetComponent<KPrefabID>();
                                kbac.SetLayer(kpid != null ? kpid.defaultLayer : 0);
                            }
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AgriHarvestPriority] ApplyHighlightMarkedOnly failed: {e}");
            }
        }

        /// <summary>为单个观赏性植物应用高亮（幂等）。</summary>
        public static bool ApplyOne(Uprootable up)
        {
            int targetLayer = GetMaskedOverlayLayer();
            if (targetLayer < 0) return false;
            return ApplyOne(up, targetLayer);
        }

        private static bool ApplyOne(Uprootable up, int targetLayer)
        {
            if (up == null || up.gameObject == null) return false;

            var kbac = up.GetComponent<KBatchedAnimController>();
            if (kbac == null) return false;

            int id = up.gameObject.GetInstanceID();
            if (_appliedIds.Contains(id)) return false;

            _appliedIds.Add(id);
            kbac.HighlightColour = HarvestHighlight;
            kbac.SetLayer(targetLayer);
            return true;
        }

        /// <summary>在收割工具关闭时调用：恢复原始状态。</summary>
        public static void RemoveHighlight()
        {
            try
            {
                var uprootables = Object.FindObjectsOfType<Uprootable>();
                if (uprootables != null)
                {
                    foreach (var up in uprootables)
                    {
                        if (up == null || up.gameObject == null) continue;

                        int id = up.gameObject.GetInstanceID();
                        if (!_appliedIds.Contains(id)) continue;

                        var kbac = up.GetComponent<KBatchedAnimController>();
                        if (kbac != null)
                        {
                            // 恢复高亮颜色
                            kbac.HighlightColour = Color.clear;

                            // 恢复原始层（overlay 系统用的是 KPrefabID.defaultLayer）
                            var kpid = up.GetComponent<KPrefabID>();
                            kbac.SetLayer(kpid != null ? kpid.defaultLayer : 0);
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AgriHarvestPriority] RemoveHighlight failed: {e}");
            }
            finally
            {
                _appliedIds.Clear();
            }
        }
    }
}