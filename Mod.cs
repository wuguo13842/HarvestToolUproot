using HarmonyLib;
using KMod;
using PeterHan.PLib.Core;
using PeterHan.PLib.Database;
using PeterHan.PLib.PatchManager;
using System.Reflection;
using UnityEngine;
using AgriHarvestPriority.Patches;   // ★ 新增

namespace AgriHarvestPriority
{
    public class Mod : UserMod2
    {
        public static Sprite UprootIconSprite { get; private set; }

        [PLibMethod(RunAt.BeforeDbInit)]
        internal static void BeforeDbInit()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourcePrefix = $"{assembly.GetName().Name}.ModAssets.assets.";

            UprootIconSprite = Utilities.CreateSpriteDxt5(
                assembly.GetManifestResourceStream(resourcePrefix + "uproot_icon.dds"),
                128, 128);
            UprootIconSprite.name = "uproot_icon";

            if (Assets.Sprites.ContainsKey(UprootIconSprite.name))
                Assets.Sprites.Remove(UprootIconSprite.name);
            Assets.Sprites.Add(UprootIconSprite.name, UprootIconSprite);
        }

        // ★ 新增：每次进入游戏世界（新游戏或读档）时清空 HarvestToolPatch 缓存
        [PLibMethod(RunAt.OnStartGame)]
        internal static void OnStartGame()
        {
            HarvestToolPatch.InvalidateCache();
        }

        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
            PUtil.InitLibrary();

            new PPatchManager(harmony).RegisterPatchClass(typeof(Mod));
            new PLocalization().Register();
        }
    }

    public static class Utilities
    {
        public static Sprite CreateSpriteDxt5(System.IO.Stream inputStream, int width, int height)
        {
            if (inputStream == null) return null;

            byte[] buffer = new byte[inputStream.Length - 128];
            inputStream.Seek(128, System.IO.SeekOrigin.Current);
            inputStream.Read(buffer, 0, buffer.Length);

            Texture2D texture = new Texture2D(width, height, TextureFormat.DXT5, false);
            texture.LoadRawTextureData(buffer);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
        }
    }
}