using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;

namespace HarvestToolUproot.Patches
{
    [HarmonyPatch(typeof(HarvestTool))]
    public static class HarvestToolPatch
    {
        // ---------- 1. 添加 UPROOT 选项 ----------
        [HarmonyPostfix]
        [HarmonyPatch("OnPrefabInit")]
        public static void OnPrefabInit_Postfix(HarvestTool __instance)
        {
            // 获取私有字段 options
            var optionsField = AccessTools.Field(typeof(HarvestTool), "options");
            var original = (ToolParameterMenu.ToggleData[])optionsField.GetValue(__instance);
            var newOptions = new ToolParameterMenu.ToggleData[original.Length + 1];
            System.Array.Copy(original, newOptions, original.Length);
            newOptions[newOptions.Length - 1] = new ToolParameterMenu.ToggleData(
                "UPROOT",
                ToolParameterMenu.ToggleState.Off,
                false
            );
            optionsField.SetValue(__instance, newOptions);
        }

        // ---------- 2. 在拖动时执行拔除 ----------
        [HarmonyPostfix]
        [HarmonyPatch("OnDragTool")]
        public static void OnDragTool_Postfix(HarvestTool __instance, int cell, int distFromOrigin)
        {
            // 通过反射调用私有方法 IsOptionOn
            var isOptionOnMethod = AccessTools.Method(typeof(HarvestTool), "IsOptionOn");
            if (!(bool)isOptionOnMethod.Invoke(__instance, new object[] { "UPROOT" })) return;
            if (!Grid.IsValidCell(cell)) return;

            // 尝试从层 1 (Building) 或层 5 (Plant) 获取物体
            GameObject go = null;
            // Grid.ObjectLayers 是 Dictionary<int, GameObject>[]
            var layers = Grid.ObjectLayers;
            if (layers.Length > 1 && layers[1] != null && layers[1].TryGetValue(cell, out go)) { }
            else if (layers.Length > 5 && layers[5] != null && layers[5].TryGetValue(cell, out go)) { }

            if (go == null) return;

            Uprootable uprootable = go.GetComponent<Uprootable>();
            if (uprootable != null && uprootable.CanUproot())
            {
                uprootable.MarkForUproot(true);
            }
        }

        // ---------- 3. （可选）修改光标纹理 ----------
        // 如需为 UPROOT 选项设置不同纹理，可在此扩展
    }
}