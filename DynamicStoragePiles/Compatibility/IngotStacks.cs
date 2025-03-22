using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;

namespace DynamicStoragePiles.Compatibility {
    public static class IngotStacks {
        private static AssetBundle assetBundle;

        public static List<string> ingotStacks = new List<string>();
        public static List<string> dynamicIngotStacks = new List<string>();

        public static void Init() {
            assetBundle = PieceHelper.GetAssetBundle("stackedingots");
            ConvertToPiece("ingot_copper_stack", "vfx_copper_stack_destroyed", "MS_IngotStacks_Copper", "Copper");
            ConvertToPiece("ingot_tin_stack", "vfx_tin_stack_destroyed", "MS_IngotStacks_Tin", "Tin");
            ConvertToPiece("ingot_bronze_stack", "vfx_bronze_stack_destroyed", "MS_IngotStacks_Bronze", "Bronze");
            ConvertToPiece("ingot_iron_stack", "vfx_iron_stack_destroyed", "MS_IngotStacks_Iron", "Iron");
            ConvertToPiece("ingot_silver_stack", "vfx_silver_stack_destroyed", "MS_IngotStacks_Silver", "Silver");
            ConvertToPiece("ingot_blackmetal_stack", "vfx_blackmetal_stack_destroyed", "MS_IngotStacks_Blackmetal", "BlackMetal");
            //ConvertToPiece("ingot_flametal_stack", "vfx_flametal_stack_destroyed", "MS_IngotStacks_Flametal", "Flametal");
            ConvertToPiece("ingot_flametal_stack", "vfx_flametal_stack_destroyed", "MS_IngotStacks_FlametalNew", "FlametalNew");
        }

        public static void ConvertToPiece(string baseAssetName, string vfxName, string newPrefabName, string resource) {
            ingotStacks.Add(baseAssetName);
            dynamicIngotStacks.Add(newPrefabName);

            GameObject basePrefab = assetBundle.LoadAsset<GameObject>(baseAssetName);
            GameObject vfx = PieceHelper.FindDestroyVFX(basePrefab, vfxName);
            GameObject stack = PrefabManager.Instance.CreateClonedPrefab(newPrefabName, vfx);
            stack = PieceHelper.PreparePiecePrefab(stack, basePrefab, $"${baseAssetName}");
            stack.name = newPrefabName;
            DynamicStoragePiles.Instance.AddCompatPiece(stack, resource, 3);
        }
    }
}
