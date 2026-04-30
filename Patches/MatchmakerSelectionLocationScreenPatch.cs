using EFT.UI.Matchmaker;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;
using archon.EntryPointSelector.MatchmakerUI.UI;

namespace archon.EntryPointSelector.MatchmakerUI.Patches
{
    internal class MatchmakerSelectionLocationScreenPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(MatchMakerSelectionLocationScreen), "method_7");
        }

        [PatchPostfix]
        private static void PatchPostfix(MatchMakerSelectionLocationScreen __instance, LocationSettingsClass.Location location)
        {
            try
            {
                MatchmakerLocationSelectorPanel.EnsureFor(__instance, location);
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogError($"[Archon EPS UI] Failed to build selector: {ex}");
            }
        }
    }
}
