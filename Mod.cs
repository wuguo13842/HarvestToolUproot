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

            // 注册补丁管理（会自动应用所有 HarmonyPatch 类）
            new PPatchManager(harmony).RegisterPatchClass(typeof(Mod));

            // 注册本地化（加载 .po 翻译）
            new PLocalization().Register();

            // 不需要再手动 PatchAll，因为 PPatchManager 已经做了
        }
    }
}