using System.Collections;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace DynamicStoragePiles {
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [BepInDependency(PLUGIN_MORESTACKS_GUID, BepInDependency.DependencyFlags.SoftDependency)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class DynamicStoragePiles : BaseUnityPlugin {
        public const string PluginName = "DynamicStoragePiles";
        public const string PluginGuid = "com.maxsch.valheim.DynamicStoragePiles";
        public const string PluginVersion = "0.7.0";

        public const string PLUGIN_MORESTACKS_GUID = "ujhik.MoreStacks";

        public static DynamicStoragePiles Instance { get; private set; }
        public static Harmony harmony;

        private static AssetBundle assetBundle;
        private List<CustomPiece> renderSprites = new List<CustomPiece>();

        public static Dictionary<string, string> allowedItemsByStack = new Dictionary<string, string>();
        public static Dictionary<string, string> allowedItemsByContainer = new Dictionary<string, string>();

        private static List<string> vanillaStacks = new List<string> {
            "wood_stack",
            "wood_fine_stack",
            "wood_core_stack",
            "wood_yggdrasil_stack",
            "blackwood_stack",
            "stone_pile",
            "coal_pile",
            "blackmarble_pile",
            "grausten_pile",
            "skull_pile",
            "treasure_stack",
            "bone_stack",
        };

        private static List<string> dynamicStacks = new List<string>();

        private void Awake() {
            Instance = this;

            assetBundle = AssetUtils.LoadAssetBundleFromResources("containerstacks");

            AddStackPiece("MS_container_wood_stack", "Wood");
            AddStackPiece("MS_container_finewood_stack", "FineWood");
            AddStackPiece("MS_container_corewood_stack", "RoundLog");
            AddStackPiece("MS_container_yggdrasil_wood_stack", "YggdrasilWood");
            AddStackPiece("MS_container_blackwood_stack", "Blackwood");
            AddStackPiece("MS_container_stone_pile", "Stone");
            AddStackPiece("MS_container_coal_pile", "Coal");
            AddStackPiece("MS_container_blackmarble_pile", "BlackMarble");
            AddStackPiece("MS_container_grausten_pile", "Grausten");
            AddStackPiece("MS_container_skull_pile", "Charredskull");
            AddStackPiece("MS_container_bone_stack", "BoneFragments");
            AddStackPiece("MS_container_coin_pile", "Coins");

            ConfigSettings.Init(Config);

            PrefabManager.OnPrefabsRegistered += OnPrefabsRegistered;
            PieceManager.OnPiecesRegistered += OnPiecesRegistered;

            harmony = new Harmony(PluginGuid);
            harmony.PatchAll();
        }

        private void Start() {
            if (Chainloader.PluginInfos.ContainsKey("Azumatt.AzuAutoStore") && ConfigSettings.azuAutoStoreCompat.Value) {
                Compatibility.AzuAutoStore.Init();
            }

            if (Chainloader.PluginInfos.ContainsKey("Richard.IngotStacks")) {
                Compatibility.IngotStacks.Init();
            }

            if (Chainloader.PluginInfos.ContainsKey("Azumatt.StackedBars")) {
                Compatibility.StackedBars.Init();
            }
        }

        public static void UpdateAllRecipes() {
            if (!ZNetScene.instance) {
                return;
            }

            UpdateRecipes(vanillaStacks, dynamicStacks, ConfigSettings.VanillaRecipeSetting);
            UpdateRecipes(Compatibility.IngotStacks.ingotStacks, Compatibility.IngotStacks.dynamicIngotStacks, ConfigSettings.IngotStacksRecipeSetting);
            UpdateRecipes(Compatibility.StackedBars.stackedBars, Compatibility.StackedBars.dynamicStackedBars, ConfigSettings.StackedBarsRecipeSetting);
            UpdateRecipes(Compatibility.MoreStacks.staticStacks, Compatibility.MoreStacks.dynamicStacks, ConfigSettings.MoreStacksRecipeSetting);

            if (Player.m_localPlayer) {
                Player.m_localPlayer.UpdateAvailablePiecesList();
            }
        }

        private static void UpdateRecipes(List<string> originalStackNames, List<string> dynamicStackNames, ConfigEntry<ConfigSettings.RecipeSetting> config) {
            foreach (string vanillaStack in originalStackNames) {
                EnablePieceRecipes(vanillaStack, config.IsEnabled(true));
            }

            foreach (string dynamicStack in dynamicStackNames) {
                EnablePieceRecipes(dynamicStack, config.IsEnabled(false));
            }
        }

        public static void EnablePieceRecipes(string prefabName, bool enabled) {
            GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);

            if (prefab && prefab.TryGetComponent(out Piece piece)) {
                piece.m_enabled = enabled;
            } else {
                Jotunn.Logger.LogWarning($"Could not find Piece {prefabName} to toggle recipes");
            }
        }

        private void AddStackPiece(string pieceName, string craftItem) {
            CustomPiece piece = new CustomPiece(assetBundle, pieceName, true, StackConfig(craftItem, 10));
            renderSprites.Add(piece);
            dynamicStacks.Add(pieceName);
            AddPiece(piece, craftItem);
        }

        public void AddCompatPiece(GameObject prefab, string craftItem, int amount) {
            CustomPiece piece = new CustomPiece(prefab, false, StackConfig(craftItem, amount));
            AddPiece(piece, craftItem);
        }

        public void AddCompatPiece(GameObject prefab, string craftItem, int amount, bool addGraphics) {
            CustomPiece piece = new CustomPiece(prefab, true, StackConfig(craftItem, amount));

            if (addGraphics) { 
                renderSprites.Add(piece);
            }

            AddPiece(piece, craftItem);
        }

        private void AddPiece(CustomPiece piece, string craftItem) {
            PieceManager.Instance.AddPiece(piece);
            allowedItemsByStack.Add(piece.PiecePrefab.name, craftItem);
            allowedItemsByContainer.Add(piece.PiecePrefab.GetComponent<Container>().m_name, craftItem);
        }

        private void OnPrefabsRegistered() {
            if (Chainloader.PluginInfos.ContainsKey(PLUGIN_MORESTACKS_GUID)) {
                Compatibility.MoreStacks.Init();
            }
        }

        private void OnPiecesRegistered() {
            PieceManager.OnPiecesRegistered -= OnPiecesRegistered;

            foreach (CustomPiece piece in renderSprites) {
                StartCoroutine(RenderSprite(piece));
            }

            UpdateAllRecipes();
        }

        private IEnumerator RenderSprite(CustomPiece piece) {
            yield return null;

            var visualStacks = piece.Piece.GetComponentsInChildren<VisualStack>();

            foreach (VisualStack visualStack in visualStacks) {
                visualStack.SetVisualsActive(55f);
            }

            piece.Piece.m_icon = RenderManager.Instance.Render(new RenderManager.RenderRequest(piece.PiecePrefab) {
                Width = 64,
                Height = 64,
                Rotation = RenderManager.IsometricRotation * Quaternion.Euler(0f, -90f, 0f),
                UseCache = true,
                TargetPlugin = Info.Metadata,
            });

            foreach (VisualStack visualStack in visualStacks) {
                visualStack.SetVisualsActive(100f);
            }
        }

        private PieceConfig StackConfig(string item, int amount) {
            PieceConfig stackConfig = new PieceConfig();
            stackConfig.PieceTable = PieceTables.Hammer;
            stackConfig.Category = PieceCategories.Misc;
            stackConfig.AddRequirement(new RequirementConfig(item, amount, 0, true));
            return stackConfig;
        }

        public static bool IsStackPiece(string pieceName, out string allowedItem) {
            return allowedItemsByStack.TryGetValue(pieceName, out allowedItem);
        }
    }
}
