using System.Collections;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using BepInEx;
using System;
using System.Reflection;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace DynamicStoragePiles.Compatibility {
    public static class MoreStacks {
        public static List<string> staticStacks = new List<string>();
        public static List<string> dynamicStacks = new List<string>();
        private static BaseUnityPlugin moreStacksInstance = null;

        private static Dictionary<string, string> vanillaNameToDynamicStorageNameMap = new Dictionary<string, string> {
            { "wood_fine_stack", "finewood_stack" },
            { "wood_core_stack", "corewood_stack" },
            { "wood_yggdrasil_stack", "yggdrasil_wood_stack" },
            { "treasure_stack", "coin_pile" }
        };

        public static void Init() {
            if (!moreStacksInstance && Chainloader.PluginInfos.TryGetValue(DynamicStoragePiles.PLUGIN_MORESTACKS_GUID, out var pluginInfo)) {
                moreStacksInstance = pluginInfo.Instance;
                if (!moreStacksInstance) {
                    Logger.LogWarning($"Failed to cast mod instance for '{DynamicStoragePiles.PLUGIN_MORESTACKS_GUID}'.");
                    return;
                }
            }

            FieldInfo fieldInfo = moreStacksInstance.GetType().GetField("stackInfoList", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (fieldInfo != null) {
                object stackInfoObj = fieldInfo.GetValue(moreStacksInstance);
                if (stackInfoObj is IEnumerable stackInfoEnumerable) {
                    foreach (var item in stackInfoEnumerable) {
                        // Get properties dynamically
                        Type stackInfoType = item.GetType();
                        PropertyInfo idStackToDuplicateProp = stackInfoType.GetProperty("idStackToDuplicate");
                        PropertyInfo idStackMaterialProp = stackInfoType.GetProperty("idStackMaterial");

                        string idStackToDuplicate = idStackToDuplicateProp?.GetValue(item) as string;
                        string idStackMaterial = idStackMaterialProp?.GetValue(item) as string;

                        //Jotunn.Logger.LogError($"StackInfo -> Duplicate: {idStackToDuplicate}, Material: {idStackMaterial}");

                        ConvertToPiece(idStackToDuplicate, idStackMaterial);
                    }
                }
            } else {
                Logger.LogWarning($"{DynamicStoragePiles.PLUGIN_MORESTACKS_GUID}: Failed to retrieve stackInfoList field via reflection, dynamic piles could not be generated");
            }
        }

        public static void ConvertToPiece(string stackToCloneId, string materialPrefabId) {
            if (vanillaNameToDynamicStorageNameMap.TryGetValue(stackToCloneId, out string mappedValue)) {
                stackToCloneId = mappedValue;
            }

            MethodInfo method = moreStacksInstance.GetType().GetMethod("getStackName", BindingFlags.Public | BindingFlags.Static);
            string moreStacksOriginalStackId = (string)method?.Invoke(moreStacksInstance, new object[] { materialPrefabId });
            string stackNamePrefix = "MS_container_MoreStacks";
            string newPrefabName = $"{stackNamePrefix}_{materialPrefabId}_pile";
            //string moreStacksOriginalStackId = $"MoreStacks_{materialPrefabId}_pile";
            string dynamicStackToCloneId = $"MS_container_{stackToCloneId}";

            //Jotunn.Logger.LogError($"Found mod: {DynamicStoragePiles.PLUGIN_MORESTACKS_GUID}, Instance: {moreStacksInstance}");
            //Jotunn.Logger.LogError($"Nombre generado: {stackName}");

            staticStacks.Add(moreStacksOriginalStackId);
            dynamicStacks.Add(newPrefabName);

            GameObject sourceStack = PrefabManager.Instance.GetPrefab(dynamicStackToCloneId);
            GameObject moreStacksOriginalStack = PrefabManager.Instance.GetPrefab(moreStacksOriginalStackId);
            GameObject materialPrefab = PrefabManager.Instance.GetPrefab(materialPrefabId);

            GameObject newStack = PrefabManager.Instance.CreateClonedPrefab(newPrefabName, sourceStack);
            UnityEngine.Object.DestroyImmediate(newStack.GetComponent<Container>());

            newStack.transform.localScale = moreStacksOriginalStack.transform.localScale;

            string newStackPieceName = moreStacksOriginalStack.GetComponent<Piece>().m_name;
            newStack = PieceHelper.PreparePiecePrefab(newStack, sourceStack, newStackPieceName);
            newStack.GetComponent<Piece>().m_name = newStackPieceName;

            Material newMaterial = moreStacksOriginalStack.GetComponentInChildren<MeshRenderer>().sharedMaterial;

            // Apply the material to all MeshRenderers in the stack
            foreach (MeshRenderer renderer in newStack.GetComponentsInChildren<MeshRenderer>()) {
                renderer.sharedMaterial = newMaterial;
            }

            DynamicStoragePiles.Instance.AddCompatPiece(newStack, materialPrefabId, GetMaterialCostForPiece(materialPrefabId), true);
        }

        public static int GetMaterialCostForPiece(string materialPrefabId) {
            if (materialPrefabId.Contains("Ore") || materialPrefabId.Contains("Scrap")) {
                return 3;
            }

            return 10;
        }
    }
}
