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

        /// <summary>接收已扫描好的装饰性植物列表，避免重复 FindObjectsOfType。</summary>
        public static void RefreshFrom(List<Uprootable> decorativePlants, bool includeUnmarked)
        {
            LoadSprites();
            if (_uprootSprite == null || _notUprootSprite == null)
            {
                Debug.LogWarning("[AgriHarvestPriority] uproot sprites missing.");
                return;
            }

            var touchedIds = new HashSet<int>(decorativePlants.Count);

            foreach (var up in decorativePlants)
            {
                if (up == null || up.gameObject == null) continue;
                if (!includeUnmarked && !up.IsMarkedForUproot) continue;

                int id = up.gameObject.GetInstanceID();
                touchedIds.Add(id);
                RefreshOneInternal(up, id);
            }

            // ★ 总是遍历检查（去掉 Count 前置判断，避免内容不同但数量相同时的僵尸图标）
            var staleIds = new List<int>();
            foreach (var kv in _icons)
                if (!touchedIds.Contains(kv.Key))
                    staleIds.Add(kv.Key);

            foreach (var id in staleIds)
            {
                if (_icons.TryGetValue(id, out var icon) && icon != null)
                    Object.Destroy(icon);
                _icons.Remove(id);
            }
        }

        /// <summary>单个植物刷新图标（拖拽时按需调用）。</summary>
        public static void RefreshOne(Uprootable uprootable)
        {
            if (uprootable == null || uprootable.gameObject == null) return;
            if (!PlantDetection.IsDecorativePlant(uprootable.gameObject)) return;
            RefreshOneInternal(uprootable, uprootable.gameObject.GetInstanceID());
        }

        private static void RefreshOneInternal(Uprootable uprootable, int id)
        {
            LoadSprites();
            if (_uprootSprite == null || _notUprootSprite == null) return;

            var go = uprootable.gameObject;
            if (!_icons.TryGetValue(id, out var icon) || icon == null)
            {
                icon = CreateIcon(go);
                if (icon == null) return;
                _icons[id] = icon;
            }

            var sprite = uprootable.IsMarkedForUproot ? _uprootSprite : _notUprootSprite;
            var images = icon.GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                img.sprite = sprite;
                img.color = Color.white;
                img.gameObject.SetActive(true);
            }
            icon.SetActive(true);
        }

        public static void RemoveOne(GameObject go)
        {
            if (go == null) return;
            int id = go.GetInstanceID();
            if (_icons.TryGetValue(id, out var icon))
            {
                if (icon != null) Object.Destroy(icon);
                _icons.Remove(id);
            }
        }

        public static void ClearAll()
        {
            foreach (var kv in _icons)
                if (kv.Value != null) Object.Destroy(kv.Value);
            _icons.Clear();
        }

        /// <summary>仿照 HarvestDesignatable.CreateOverlayIcon 创建图标。</summary>
        private static GameObject CreateIcon(GameObject target)
        {
            var prefab = Assets.UIPrefabs.HarvestWhenReadyOverlayIcon;
            if (prefab == null || GameScreenManager.Instance == null) return null;

            var icon = Util.KInstantiate(prefab, GameScreenManager.Instance.worldSpaceCanvas, null);
            if (icon == null) return null;

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