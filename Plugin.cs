using BepInEx;
using BepInEx.Logging;
using System;
using System.IO;
using archon.EntryPointSelector.MatchmakerUI.Patches;

namespace archon.EntryPointSelector.MatchmakerUI
{
    [BepInPlugin("com.archon.entrypointselectormatchmakerui", "Archon-EntryPointSelectorMatchmakerUI", "1.0.0")]
    [BepInDependency("hazelify.EntryPointSelector", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("com.fika.core", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;
        internal static string RuntimeExtractCatalogPath;
        internal static string LegacyRuntimeExtractCatalogPath;

        private void Awake()
        {
            Log = Logger;
            RuntimeExtractCatalogPath = Path.Combine(
                Environment.CurrentDirectory,
                "BepInEx",
                "plugins",
                "archon.EntryPointSelector.MatchmakerUI",
                "RuntimeExtractCatalog.json");
            LegacyRuntimeExtractCatalogPath = Path.Combine(
                Environment.CurrentDirectory,
                "BepInEx",
                "plugins",
                "hazelify.EntryPointSelector.MatchmakerUI",
                "ExtractCoordinates.runtime.json");

            MigrateLegacyRuntimeCatalog();

            new MatchmakerSelectionLocationScreenPatch().Enable();
            new GameWorldRuntimeExtractCatalogPatch().Enable();
        }

        private static void MigrateLegacyRuntimeCatalog()
        {
            try
            {
                if (File.Exists(RuntimeExtractCatalogPath) || !File.Exists(LegacyRuntimeExtractCatalogPath))
                {
                    return;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(RuntimeExtractCatalogPath));
                File.Copy(LegacyRuntimeExtractCatalogPath, RuntimeExtractCatalogPath, overwrite: false);
            }
            catch (Exception ex)
            {
                Log?.LogWarning($"[Archon EPS UI] Failed to migrate legacy runtime catalog: {ex.Message}");
            }
        }
    }
}
