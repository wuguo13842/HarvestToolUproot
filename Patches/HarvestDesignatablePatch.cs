using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace HarvestToolUproot.Patches
{
    [HarmonyPatch(typeof(HarvestDesignatable))]
    public static class HarvestDesignatablePatch
    {
        private static Sprite originalHarvestSprite;
        private static Sprite uprootSprite;
        private static Color uprootColor;

        private static void LoadSprites()
        {
            if (originalHarvestSprite == null)
            {
                var prefab = Assets.UIPrefabs.HarvestWhenReadyOverlayIcon;
                if (prefab != null)
                {
                    var img = prefab.GetComponentInChildren<Image>(true);
                    if (img != null)
                        originalHarvestSprite = img.sprite;
                }
            }

            if (uprootSprite == null)
            {
                var statusItem = Db.Get().MiscStatusItems.PendingUproot;
                if (statusItem != null && statusItem.sprite != null)
                {
                    uprootSprite = statusItem.sprite.sprite;
                    uprootColor = statusItem.sprite.color; // 黑色
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("RefreshOverlayIcon")]
        public static void RefreshOverlayIcon_Postfix(HarvestDesignatable __instance)
        {
            Uprootable uprootable = __instance.GetComponent<Uprootable>();
            if (uprootable == null) return;

            var icon = __instance.HarvestWhenReadyOverlayIcon;
            if (icon == null) return;

            LoadSprites();
            if (originalHarvestSprite == null || uprootSprite == null) return;

            bool marked = uprootable.IsMarkedForUproot;
            var images = icon.GetComponentsInChildren<Image>(true);

            if (marked)
            {
                foreach (var img in images)
                {
                    img.sprite = uprootSprite;
                    img.color = uprootColor; // 黑色

                    // 添加白色描边
                    var outline = img.gameObject.GetComponent<Outline>();
                    if (outline == null)
                        outline = img.gameObject.AddComponent<Outline>();
                    outline.effectColor = Color.white;
                    outline.effectDistance = new Vector2(1, -1);

                    img.gameObject.SetActive(true);
                }
                icon.gameObject.SetActive(true);
            }
            else
            {
                foreach (var img in images)
                {
                    // 恢复原始收获图标
                    img.sprite = originalHarvestSprite;
                    // 移除描边
                    var outline = img.gameObject.GetComponent<Outline>();
                    if (outline != null)
                        Object.Destroy(outline);
                }
            }
        }
    }
}