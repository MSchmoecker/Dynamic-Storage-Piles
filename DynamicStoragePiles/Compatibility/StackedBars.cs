using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;

namespace DynamicStoragePiles.Compatibility {
    public static class StackedBars {
        private static AssetBundle assetBundle;

        public static List<string> stackedBars = new List<string>();
        public static List<string> dynamicStackedBars = new List<string>();

        public static void Init() {
            assetBundle = PieceHelper.GetAssetBundle("stackedbars");
            ConvertToPiece("stack_tinbars", "MS_StackedBars_Tin", "Tin");
            ConvertToPiece("stack_copperbars", "MS_StackedBars_Copper", "Copper");
            ConvertToPiece("stack_bronzebars", "MS_StackedBars_Bronze", "Bronze");
            ConvertToPiece("stack_ironbars", "MS_StackedBars_Iron", "Iron");
            ConvertToPiece("stack_silverbars", "MS_StackedBars_Silver", "Silver");
            ConvertToPiece("stack_blackmetalbars", "MS_StackedBars_BlackMetal", "BlackMetal");
            //ConvertToPiece("stack_flametalbars", "MS_StackedBars_FlameMetal", "Flametal");
            ConvertToPiece("stack_flametalbars", "MS_StackedBars_FlameMetalNew", "FlametalNew");
        }

        public static void ConvertToPiece(string baseAssetName, string newPrefabName, string resource) {
            stackedBars.Add(baseAssetName);
            dynamicStackedBars.Add(newPrefabName);

            GameObject basePrefab = assetBundle.LoadAsset<GameObject>(baseAssetName);
            GameObject stack = PrefabManager.Instance.CreateClonedPrefab(newPrefabName, basePrefab);
            stack = PieceHelper.PreparePiecePrefab(stack, basePrefab, $"$piece_{baseAssetName}");
            stack.name = newPrefabName;
            DynamicStoragePiles.Instance.AddCompatPiece(stack, resource, 3);
        }
    }
}
