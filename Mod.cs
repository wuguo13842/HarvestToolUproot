using HarmonyLib;
using KMod;
using PeterHan.PLib.Core;
using PeterHan.PLib.Database;
using PeterHan.PLib.PatchManager;
using System.Reflection;
using UnityEngine;

namespace HarvestToolUproot
{
    public class Mod : UserMod2
    {
        public static Sprite UprootIconSprite { get; private set; }

        // 在游戏早期（Db 初始化之前）加载图标
        [PLibMethod(RunAt.BeforeDbInit)]
        internal static void BeforeDbInit()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourcePrefix = $"{assembly.GetName().Name}.ModAssets.assets.";

            // 加载拔除图标（带白边的 DDS）
            UprootIconSprite = Utilities.CreateSpriteDxt5(
                assembly.GetManifestResourceStream(resourcePrefix + "uproot_icon.dds"),
                128,128 // 图标尺寸
            );
            UprootIconSprite.name = "uproot_icon";

            // 添加到游戏精灵字典，方便其他代码通过 Assets.GetSprite 获取
            if (Assets.Sprites.ContainsKey(UprootIconSprite.name))
                Assets.Sprites.Remove(UprootIconSprite.name);
            Assets.Sprites.Add(UprootIconSprite.name, UprootIconSprite);
        }

        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
            PUtil.InitLibrary();

            // 注册补丁
            new PPatchManager(harmony).RegisterPatchClass(typeof(Mod));
            // 注册本地化
            new PLocalization().Register();
        }
    }

    // 辅助类：加载 .dds 文件（从 PliersPlus 复制）
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