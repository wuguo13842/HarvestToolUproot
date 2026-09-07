using HarmonyLib;
using KMod;
using PeterHan.PLib.Core;
using PeterHan.PLib.Database;
using PeterHan.PLib.PatchManager;

namespace HarvestToolUproot
{
    public class Mod : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
            PUtil.InitLibrary();

            // 可选：注册补丁管理（如不需要可省略）
            new PPatchManager(harmony).RegisterPatchClass(typeof(Mod));

            // 注册本地化（加载 .po 翻译）
            new PLocalization().Register();
        }
    }
}