using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;

namespace DynamicStoragePiles {
    public static class ConfigSettings {
        private static ConfigFile config;

        public static ConfigEntry<RecipeSetting> VanillaRecipeSetting { get; private set; }
        public static ConfigEntry<RecipeSetting> IngotStacksRecipeSetting { get; private set; }
        public static ConfigEntry<RecipeSetting> StackedBarsRecipeSetting { get; private set; }
        public static ConfigEntry<RecipeSetting> MoreStacksRecipeSetting { get; private set; }

        public static ConfigEntry<bool> azuAutoStoreCompat;
        public static ConfigEntry<bool> azuAutoStoreItemWhitelist;
        public static ConfigEntry<bool> restrictDynamicPiles;

        public enum RecipeSetting {
            AllStoragePiles,
            OnlyDynamicStoragePiles,
            OnlyOriginalStoragePiles,
        }

        public static void Init(ConfigFile configFile) {
            config = configFile;

            ConfigurationManagerAttributes adminOnly = new ConfigurationManagerAttributes { IsAdminOnly = true };
            string disableRecipeDescription = "\nCheats or world modifiers can ignore this setting. Existing pieces in the world are not affected";
            string restartDescription = "\nRequires a restart to take effect";

            string section = "1 - General";

            VanillaRecipeSetting = config.Bind(section, "Vanilla Stack Recipes", RecipeSetting.AllStoragePiles, $"Sets which pieces are placeable.{disableRecipeDescription}");
            VanillaRecipeSetting.SettingChanged += (sender, args) => DynamicStoragePiles.UpdateAllRecipes();

            restrictDynamicPiles = config.Bind(section, "Restrict Container Item Type", true, new ConfigDescription("Only allows the respective items to be stored in stack piles, so wood piles only accept wood etc.\nSynced with server", null, adminOnly));

            section = "2.0 - Compatibility AzuAutoStore";

            azuAutoStoreCompat = config.Bind(section, "Enable Compatibility", true, $"Enables compatibility with AzuAutoStore.{restartDescription}");
            azuAutoStoreItemWhitelist = config.Bind(section, "Enable Item Whitelist", true, "Only allows the respective items to be stored in stack piles");

            section = "2.1 - Compatibility IngotStacks";

            IngotStacksRecipeSetting = config.Bind(section, "IngotStacks Stack Recipes", RecipeSetting.AllStoragePiles, $"Sets which pieces are placeable.{disableRecipeDescription}");
            IngotStacksRecipeSetting.SettingChanged += (sender, args) => DynamicStoragePiles.UpdateAllRecipes();

            section = "2.2 - Compatibility StackedBars";

            StackedBarsRecipeSetting = config.Bind(section, "StackedBars Stack Recipes", RecipeSetting.AllStoragePiles, $"Sets which pieces are placeable.{disableRecipeDescription}");
            StackedBarsRecipeSetting.SettingChanged += (sender, args) => DynamicStoragePiles.UpdateAllRecipes();

            section = "2.3 - Compatibility MoreStacks";

            MoreStacksRecipeSetting = config.Bind(section, "MoreStacks Stack Recipes", RecipeSetting.AllStoragePiles, $"Sets which pieces are placeable.{disableRecipeDescription}");
            MoreStacksRecipeSetting.SettingChanged += (sender, args) => DynamicStoragePiles.UpdateAllRecipes();
        }

        public static bool IsEnabled(this ConfigEntry<RecipeSetting> configEntry, bool isOriginal) {
            if (isOriginal) {
                return configEntry.Value == RecipeSetting.AllStoragePiles || configEntry.Value == RecipeSetting.OnlyOriginalStoragePiles;
            } else {
                return configEntry.Value == RecipeSetting.AllStoragePiles || configEntry.Value == RecipeSetting.OnlyDynamicStoragePiles;
            }
        }
    }
}
