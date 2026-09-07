using UnityEngine;
using UnityEngine.UI;
using System.Reflection;

namespace HarvestToolUproot.Components
{
    public class UprootOverlayIcon : KMonoBehaviour
    {
        private RectTransform iconRect;
        private Uprootable uprootable;
        private Sprite uprootSprite;
        private HarvestDesignatable harvestDesignatable;
        private static MethodInfo isOptionOnMethod;

        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            uprootable = GetComponent<Uprootable>();
            harvestDesignatable = GetComponent<HarvestDesignatable>();
            if (uprootable == null) return;

            // 获取原生拔除图标
            var statusItem = Db.Get().MiscStatusItems.PendingUproot;
            if (statusItem != null && statusItem.sprite != null)
                uprootSprite = statusItem.sprite.sprite;

            if (uprootSprite == null)
            {
                Debug.LogWarning("[UprootOverlayIcon] 无法获取 PendingUproot 图标");
                return;
            }

            // 缓存 IsOptionOn 方法（备用，实际上新逻辑不再需要）
            if (isOptionOnMethod == null)
            {
                isOptionOnMethod = typeof(HarvestTool).GetMethod("IsOptionOn",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }

            // 订阅 Overlay 事件
            Game.Instance.Subscribe(1248612973, OnEnableOverlay);
            Game.Instance.Subscribe(1798162660, OnEnableOverlay);
            Game.Instance.Subscribe(2015652040, OnDisableOverlay);
            Game.Instance.Subscribe(1983128072, OnRefresh);

            // 订阅 Uprootable 状态变化
            Subscribe(-216549700, OnUprootComplete);
            Subscribe(1198393204, OnUprootCancelled);
        }

        protected override void OnCleanUp()
        {
            Game.Instance.Unsubscribe(1248612973, OnEnableOverlay);
            Game.Instance.Unsubscribe(1798162660, OnEnableOverlay);
            Game.Instance.Unsubscribe(2015652040, OnDisableOverlay);
            Game.Instance.Unsubscribe(1983128072, OnRefresh);

            if (iconRect != null)
                Object.Destroy(iconRect.gameObject);

            base.OnCleanUp();
        }

        private void OnEnableOverlay(object data)
        {
            if (((Boxed<HashedString>)data).value == OverlayModes.Harvest.ID)
            {
                CreateIcon();
                Refresh();
            }
            else
            {
                if (iconRect != null)
                    iconRect.gameObject.SetActive(false);
            }
        }

        private void OnDisableOverlay(object data)
        {
            if (iconRect != null)
                iconRect.gameObject.SetActive(false);
        }

        private void OnRefresh(object data) => Refresh();
        private void OnUprootComplete(object data) => Refresh();
        private void OnUprootCancelled(object data) => Refresh();

        private void CreateIcon()
        {
            if (iconRect != null || uprootSprite == null) return;

            var prefab = Assets.UIPrefabs.HarvestWhenReadyOverlayIcon;
            if (prefab == null) return;

            GameObject newIcon = Util.KInstantiate(prefab, GameScreenManager.Instance.worldSpaceCanvas, null);
            iconRect = newIcon.GetComponent<RectTransform>();

            var images = newIcon.GetComponentsInChildren<Image>(true);
            foreach (var img in images)
            {
                img.sprite = uprootSprite;
                img.color = Color.white;
            }

            newIcon.SetActive(false);

            var area = GetComponent<OccupyArea>();
            if (area != null)
            {
                Extents extents = area.GetExtents();
                Vector3 pos;
                if (GetComponent<KPrefabID>().HasTag(GameTags.Hanging))
                    pos = new Vector3(extents.x + extents.width / 2f + 0.5f, extents.y + extents.height);
                else
                    pos = new Vector3(extents.x + extents.width / 2f + 0.5f, extents.y);
                iconRect.position = pos;
            }
        }

        public void Refresh()
        {
            // 检查植物是否被标记拔除
            bool isMarkedForUproot = uprootable != null && uprootable.IsMarkedForUproot;

            // 如果植物被标记拔除，显示拔除图标，隐藏收获图标
            if (isMarkedForUproot)
            {
                // 显示拔除图标
                if (iconRect != null)
                    iconRect.gameObject.SetActive(true);

                // 强制隐藏收获图标
                if (harvestDesignatable != null && harvestDesignatable.HarvestWhenReadyOverlayIcon != null)
                {
                    harvestDesignatable.HarvestWhenReadyOverlayIcon.gameObject.SetActive(false);
                }
            }
            else
            {
                // 未标记拔除，隐藏拔除图标
                if (iconRect != null)
                    iconRect.gameObject.SetActive(false);

                // 恢复收获图标的正常显示（根据 HarvestWhenReady 状态）
                if (harvestDesignatable != null && harvestDesignatable.HarvestWhenReadyOverlayIcon != null)
                {
                    bool harvestShouldShow = harvestDesignatable.HarvestWhenReady;
                    harvestDesignatable.HarvestWhenReadyOverlayIcon.gameObject.SetActive(harvestShouldShow);
                }
            }
        }
    }
}