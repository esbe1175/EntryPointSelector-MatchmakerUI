using BepInEx.Configuration;
using hazelify.EntryPointSelector.Data;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;
using UnityEngine;
using OriginalPlugin = EntryPointSelector.Plugin;

namespace archon.EntryPointSelector.MatchmakerUI.Data
{
    internal static class OriginalPluginAccessor
    {
        private static JObject _playerData;
        private static DateTime _playerDataLastWriteUtc;
        private static Dictionary<string, string> _localeMap;
        private static DateTime _localeMapLastWriteUtc;

        public static string NormalizeLocationId(string locationId)
        {
            return locationId?.ToLowerInvariant();
        }

        public static bool UseLastExfilEnabled => OriginalPlugin.useLastExfil != null && OriginalPlugin.useLastExfil.Value;
        public static bool ChooseInfilEnabled => OriginalPlugin.chooseInfil != null && OriginalPlugin.chooseInfil.Value;
        public static bool IsHomeComfortsInstalled => OriginalPlugin.isLITInstalled;

        public static bool HasSavedPlayerData(string locationId)
        {
            return TryGetSavedPosition(locationId, out _);
        }

        public static bool TryGetSavedPosition(string locationId, out Vector3 savedPosition)
        {
            savedPosition = default;
            string normalizedLocationId = NormalizeLocationId(locationId);
            if (string.IsNullOrWhiteSpace(normalizedLocationId))
            {
                return false;
            }

            JObject mapNode = GetPlayerDataNode(normalizedLocationId);
            if (mapNode == null)
            {
                return false;
            }

            float x = mapNode.Value<float?>("Position_X").GetValueOrDefault();
            float y = mapNode.Value<float?>("Position_Y").GetValueOrDefault();
            float z = mapNode.Value<float?>("Position_Z").GetValueOrDefault();
            if (Mathf.Approximately(x, 0f) && Mathf.Approximately(y, 0f) && Mathf.Approximately(z, 0f))
            {
                return false;
            }

            savedPosition = new Vector3(x, y, z);
            return true;
        }

        public static ConfigEntry<string> GetExfilConfigEntry(string locationId, bool isScav)
        {
            switch (NormalizeLocationId(locationId))
            {
                case "factory4_day":
                case "factory4_night":
                    return isScav ? OriginalPlugin.Factory_Exfils_Scavs : OriginalPlugin.Factory_Exfils;
                case "bigmap":
                    return isScav ? OriginalPlugin.Customs_Exfils_Scavs : OriginalPlugin.Customs_Exfils;
                case "sandbox":
                case "sandbox_high":
                    return isScav ? OriginalPlugin.GZ_Exfils_Scavs : OriginalPlugin.GZ_Exfils;
                case "rezervbase":
                    return isScav ? OriginalPlugin.Reserve_Exfils_Scavs : OriginalPlugin.Reserve_Exfils;
                case "lighthouse":
                    return isScav ? OriginalPlugin.Lighthouse_Exfils_Scavs : OriginalPlugin.Lighthouse_Exfils;
                case "shoreline":
                    return isScav ? OriginalPlugin.Shoreline_Exfils_Scavs : OriginalPlugin.Shoreline_Exfils;
                case "woods":
                    return isScav ? OriginalPlugin.Woods_Exfils_Scavs : OriginalPlugin.Woods_Exfils;
                case "interchange":
                    return isScav ? OriginalPlugin.Interchange_Exfils_Scavs : OriginalPlugin.Interchange_Exfils;
                case "tarkovstreets":
                    return isScav ? OriginalPlugin.Streets_Exfils_Scavs : OriginalPlugin.Streets_Exfils;
                case "laboratory":
                    return isScav ? OriginalPlugin.Labs_Exfils_Scavs : OriginalPlugin.Labs_Exfils;
                default:
                    return null;
            }
        }

        public static List<string> GetExfilChoices(string locationId, bool isScav)
        {
            string normalizedLocationId = NormalizeLocationId(locationId);
            if (string.IsNullOrWhiteSpace(normalizedLocationId))
            {
                return null;
            }

            string fieldName = normalizedLocationId + (isScav ? "_scav" : string.Empty);
            FieldInfo field = typeof(ExfilDescData).GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
            return (field?.GetValue(null) as List<string>)?.ToList();
        }

        public static string TranslateInternalName(string locationId, string internalName)
        {
            string localizedName = GetLocalizedExtractName(internalName);
            if (!string.IsNullOrWhiteSpace(localizedName))
            {
                return localizedName;
            }

            return TranslateInternalName(locationId, false, internalName) ??
                   TranslateInternalName(locationId, true, internalName) ??
                   OriginalPlugin.GetLocalizedName(internalName);
        }

        public static string TranslateInternalName(string locationId, bool isScav, string internalName)
        {
            string normalizedLocationId = NormalizeLocationId(locationId);
            if (string.IsNullOrWhiteSpace(normalizedLocationId) || string.IsNullOrWhiteSpace(internalName))
            {
                return null;
            }

            string fieldName = normalizedLocationId + (isScav ? "_scav" : string.Empty);
            List<string> internalList = GetStaticList(typeof(ExfilData), fieldName);
            List<string> displayList = GetStaticList(typeof(ExfilDescData), fieldName);
            if (internalList == null || displayList == null)
            {
                return null;
            }

            for (int i = 0; i < internalList.Count && i < displayList.Count; i++)
            {
                if (string.Equals(internalList[i], internalName, StringComparison.OrdinalIgnoreCase))
                {
                    return displayList[i];
                }
            }

            return null;
        }

        private static List<string> GetStaticList(Type declaringType, string fieldName)
        {
            FieldInfo field = declaringType.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
            return field?.GetValue(null) as List<string>;
        }

        private static JObject GetPlayerDataNode(string normalizedLocationId)
        {
            EnsurePlayerDataLoaded();
            return _playerData?[normalizedLocationId] as JObject;
        }

        private static void EnsurePlayerDataLoaded()
        {
            string filePath = Path.Combine(Environment.CurrentDirectory, "BepInEx", "plugins", "hazelify.EntryPointSelector", "PlayerData.json");
            if (!File.Exists(filePath))
            {
                _playerData = null;
                _playerDataLastWriteUtc = default;
                return;
            }

            DateTime lastWriteUtc = File.GetLastWriteTimeUtc(filePath);
            if (_playerData != null && lastWriteUtc == _playerDataLastWriteUtc)
            {
                return;
            }

            string content = File.ReadAllText(filePath);
            _playerData = JObject.Parse(content);
            _playerDataLastWriteUtc = lastWriteUtc;
        }

        private static string GetLocalizedExtractName(string internalName)
        {
            if (string.IsNullOrWhiteSpace(internalName))
            {
                return null;
            }

            EnsureLocaleLoaded();
            return _localeMap != null && _localeMap.TryGetValue(internalName, out string localizedName)
                ? localizedName
                : null;
        }

        private static void EnsureLocaleLoaded()
        {
            string filePath = Path.Combine(
                Environment.CurrentDirectory,
                "SPT",
                "SPT_Data",
                "database",
                "locales",
                "global",
                "en.json");

            if (!File.Exists(filePath))
            {
                _localeMap = null;
                _localeMapLastWriteUtc = default;
                return;
            }

            DateTime lastWriteUtc = File.GetLastWriteTimeUtc(filePath);
            if (_localeMap != null && lastWriteUtc == _localeMapLastWriteUtc)
            {
                return;
            }

            string content = File.ReadAllText(filePath);
            Dictionary<string, string> localeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in Regex.Matches(
                content,
                "\"((?:[^\"\\\\]|\\\\.)+)\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"",
                RegexOptions.Singleline))
            {
                string key = Regex.Unescape(match.Groups[1].Value);
                string value = Regex.Unescape(match.Groups[2].Value);
                localeMap[key] = value;
            }

            _localeMap = localeMap;
            _localeMapLastWriteUtc = lastWriteUtc;
        }
    }
}
