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

        private static void LoadSprites()
        {
            if (originalHarvestSprite == null)
            {
                // 直接从预制体获取原始 Sprite
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
                    uprootSprite = statusItem.sprite.sprite;
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
                    img.color = Color.white;
                    img.gameObject.SetActive(true);
                }
                icon.gameObject.SetActive(true);
            }
            else
            {
                foreach (var img in images)
                {
                    img.sprite = originalHarvestSprite;
                }
            }
        }
    }
}