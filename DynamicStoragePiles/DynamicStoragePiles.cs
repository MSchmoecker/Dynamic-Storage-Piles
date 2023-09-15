﻿using BepInEx;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace DynamicStoragePiles {
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    // [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class DynamicStoragePiles : BaseUnityPlugin {
        public const string PluginName = "DynamicStoragePiles";
        public const string PluginGuid = "com.maxsch.valheim.DynamicStoragePiles";
        public const string PluginVersion = "0.1.0";

        private static AssetBundle assetBundle;
        public static CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();

        private void Awake() {
            assetBundle = AssetUtils.LoadAssetBundleFromResources("containerstacks");

            AddPiece("MS_container_wood_stack", "Wood");
            AddPiece("MS_container_finewood_stack", "FineWood");
            AddPiece("MS_container_corewood_stack", "RoundLog");
            AddPiece("MS_container_stone_pile", "Stone");
            AddPiece("MS_container_coal_pile", "Coal");
            AddPiece("MS_container_yggdrasil_wood_stack", "YggdrasilWood");
            AddPiece("MS_container_blackmarble_pile", "BlackMarble");

            Harmony harmony = new Harmony(PluginGuid);
            harmony.PatchAll();
        }

        private void AddPiece(string pieceName, string craftItem) {
            PieceManager.Instance.AddPiece(new CustomPiece(assetBundle, pieceName, true, StackConfig(craftItem)));
        }

        private PieceConfig StackConfig(string item) {
            PieceConfig stackConfig = new PieceConfig();
            stackConfig.PieceTable = PieceTables.Hammer;
            stackConfig.AddRequirement(new RequirementConfig(item, 10, 0, true));
            return stackConfig;
        }
    }
}
