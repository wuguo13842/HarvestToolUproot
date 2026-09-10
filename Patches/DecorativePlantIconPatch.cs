using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AgriHarvestPriority.Patches
{
    // 为观赏性植物（有 DecorProvider、没有 HarvestDesignatable）在收割工具激活时显示拔除状态图标。
    // 未标记拔除 -> not_uproot_icon；已标记拔除 -> uproot_icon。
    public static class DecorativePlantIconPatch
    {
        // 每棵观赏性植物的图标缓存（key = GameObject.GetInstanceID()）
        private static readonly Dictionary<int, GameObject> _icons = new Dictionary<int, GameObject>();

        private static Sprite _uprootSprite;
        private static Sprite _notUprootSprite;

        private static void LoadSprites()
        {
            if (_uprootSprite == null)
                _uprootSprite = Assets.GetSprite("uproot_icon");
            if (_notUprootSprite == null)
                _notUprootSprite = Assets.GetSprite("not_uproot_icon");
        }

        /// <summary>判断是否为观赏性植物：有 Uprootable、无 HarvestDesignatable、有装饰属性。</summary>
        public static bool IsDecorativePlant(GameObject go)
        {
            if (go == null) return false;
            if (go.GetComponent<Uprootable>() == null) return false;
            // 有 HarvestDesignatable 的走原有逻辑，不在此处处理
            if (go.GetComponent<HarvestDesignatable>() != null) return false;
            // 有装饰属性
            if (go.GetComponent<DecorProvider>() != null) return true;
            return false;
        }

        /// <summary>收割工具激活 / 切换到拔除模式时调用：为所有观赏性植物创建并刷新图标。</summary>
        public static void RefreshAll()
        {
            LoadSprites();
            if (_uprootSprite == null || _notUprootSprite == null)
            {
                Debug.LogWarning("[AgriHarvestPriority] uproot sprites missing.");
                return;
            }

            var uprootables = Object.FindObjectsOfType<Uprootable>();
            if (uprootables == null) return;

            foreach (var up in uprootables)
            {
                if (up == null || up.gameObject == null) continue;
                if (!IsDecorativePlant(up.gameObject)) continue;
                RefreshOne(up);
            }
        }

        /// <summary>
        /// ★ 新增：只显示"已标记拔除"的观赏性植物图标，其余清理。
        /// 用于非拔除/取消拔除模式下，让已拔除的植物仍然可见。
        /// </summary>
        public static void RefreshMarkedOnly()
        {
            LoadSprites();
            if (_uprootSprite == null || _notUprootSprite == null) return;

            var uprootables = Object.FindObjectsOfType<Uprootable>();
            if (uprootables == null) return;

            // 本次应保留的图标 key 集合
            var keepIds = new HashSet<int>();

            foreach (var up in uprootables)
            {
                if (up == null || up.gameObject == null) continue;
                if (!IsDecorativePlant(up.gameObject)) continue;

                int id = up.gameObject.GetInstanceID();
                if (up.IsMarkedForUproot)
                {
                    keepIds.Add(id);
                    RefreshOne(up);
                }
            }

            // 清理不再需要的图标（先收集，再删除，避免遍历时修改字典）
            var toRemove = new List<int>();
            foreach (var kv in _icons)
            {
                if (!keepIds.Contains(kv.Key))
                    toRemove.Add(kv.Key);
            }
            foreach (var id in toRemove)
            {
                if (_icons.TryGetValue(id, out var icon) && icon != null)
                    Object.Destroy(icon);
                _icons.Remove(id);
            }
        }

        /// <summary>单个植物刷新图标（创建/更新）。</summary>
        public static void RefreshOne(Uprootable uprootable)
        {
            if (uprootable == null) return;
            var go = uprootable.gameObject;
            if (go == null) return;
            if (!IsDecorativePlant(go)) return;

            LoadSprites();
            if (_uprootSprite == null || _notUprootSprite == null) return;

            int id = go.GetInstanceID();
            GameObject icon;
            if (!_icons.TryGetValue(id, out icon) || icon == null)
            {
                icon = CreateIcon(go);
                if (icon == null) return;
                _icons[id] = icon;
            }

            // 根据拔除状态切换 sprite
            bool marked = uprootable.IsMarkedForUproot;
            var sprite = marked ? _uprootSprite : _notUprootSprite;

            var images = icon.GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                img.sprite = sprite;
                img.color = Color.white;
                img.gameObject.SetActive(true);
            }
            icon.SetActive(true);
        }

        /// <summary>移除某个植物的图标（植物被拔除/销毁时调用）。</summary>
        public static void RemoveOne(GameObject go)
        {
            if (go == null) return;
            int id = go.GetInstanceID();
            GameObject icon;
            if (_icons.TryGetValue(id, out icon))
            {
                if (icon != null) Object.Destroy(icon);
                _icons.Remove(id);
            }
        }

        /// <summary>工具关闭 / 切换到其他选项时清理所有图标。</summary>
        public static void ClearAll()
        {
            foreach (var kv in _icons)
            {
                if (kv.Value != null) Object.Destroy(kv.Value);
            }
            _icons.Clear();
        }

        /// <summary>仿照 HarvestDesignatable.CreateOverlayIcon 创建图标。</summary>
        private static GameObject CreateIcon(GameObject target)
        {
            var prefab = Assets.UIPrefabs.HarvestWhenReadyOverlayIcon;
            if (prefab == null) return null;
            if (GameScreenManager.Instance == null) return null;

            var icon = Util.KInstantiate(prefab, GameScreenManager.Instance.worldSpaceCanvas, null);
            if (icon == null) return null;

            // 计算位置
            var occupyArea = target.GetComponent<OccupyArea>();
            if (occupyArea != null)
            {
                var extents = occupyArea.GetExtents();
                var kpid = target.GetComponent<KPrefabID>();
                Vector3 position;
                if (kpid != null && kpid.HasTag(GameTags.Hanging))
                {
                    position = new Vector3(
                        (float)(extents.x + extents.width / 2) + 0.5f,
                        (float)(extents.y + extents.height));
                }
                else
                {
                    position = new Vector3(
                        (float)(extents.x + extents.width / 2) + 0.5f,
                        (float)extents.y);
                }
                icon.transform.SetPosition(position);
            }
            else
            {
                icon.transform.SetPosition(target.transform.GetPosition());
            }

            return icon;
        }
    }
}