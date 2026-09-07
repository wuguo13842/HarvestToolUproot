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
            new PPatchManager(harmony).RegisterPatchClass(typeof(Mod));
            new PLocalization().Register();
        }
    }
}