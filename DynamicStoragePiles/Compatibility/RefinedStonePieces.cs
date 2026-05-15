using System.Collections.Generic;
using System.Linq;
using Jotunn.Managers;
using UnityEngine;
namespace DynamicStoragePiles.Compatibility {
    public static class RefinedStonePieces {
        private static AssetBundle assetBundle;

        public static List<string> staticStacks = new List<string>();
        public static List<string> dynamicStacks = new List<string>();

        public static void Init() {
            assetBundle = PieceHelper.GetAssetBundle("refinedstonepieces");
            ConvertRefinedStoneStackToPiece("BRP_RefinedStone_Stack", "MS_RefinedStonePieces_RefinedStone_Stack", "BRP_RefinedStone");
        }

        public static void ConvertRefinedStoneStackToPiece(string baseAssetName, string newPrefabName, string resource) {
            staticStacks.Add(baseAssetName);
            dynamicStacks.Add(newPrefabName);

            GameObject basePrefab = assetBundle.LoadAsset<GameObject>(baseAssetName);
            GameObject stack = PrefabManager.Instance.CreateClonedPrefab(newPrefabName, basePrefab);
            stack = PieceHelper.PreparePiecePrefab(stack, basePrefab, "$brp_refinedstone_stack");
            stack.name = newPrefabName;

            List<Transform> stackMeshes = stack.GetComponent<VisualStack>().stackMeshes.OrderBy(t => t.position.y).ToList();
            var layer1 = stackMeshes.Skip(0).Take(6).OrderBy(t => t.position.z);
            var layer2 = stackMeshes.Skip(6).Take(6).OrderBy(t => t.position.x);
            var layer3 = stackMeshes.Skip(12).Take(6).OrderBy(t => t.position.z);
            stack.GetComponent<VisualStack>().stackMeshes = layer1.Concat(layer2).Concat(layer3).ToList();

            DynamicStoragePiles.Instance.AddCompatPiece(stack, resource, 10, true);
        }
    }
}
