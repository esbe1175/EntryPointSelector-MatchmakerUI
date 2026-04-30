using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace archon.EntryPointSelector.MatchmakerUI.Data
{
    internal static class RuntimeExtractCatalogStore
    {
        private static JObject _catalog;
        private static DateTime _catalogLastWriteUtc;

        public static RuntimeExtractMatch FindNearestExtract(string locationId, Vector3 savedPosition, bool isScav)
        {
            string normalizedLocationId = OriginalPluginAccessor.NormalizeLocationId(locationId);
            if (string.IsNullOrWhiteSpace(normalizedLocationId))
            {
                return null;
            }

            JObject mapNode = GetMapNode(normalizedLocationId);
            if (mapNode == null)
            {
                return null;
            }

            List<RuntimeExtractMatch> eligibleMatches = BuildMatches(mapNode, savedPosition, isScav, preferSideMatch: true);
            if (eligibleMatches.Count == 0)
            {
                eligibleMatches = BuildMatches(mapNode, savedPosition, isScav, preferSideMatch: false);
            }

            return eligibleMatches
                .OrderBy(match => match.Distance)
                .ThenBy(match => match.DisplayName, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }

        internal static void PersistCaptures(string mapId, IEnumerable<JObject> captures)
        {
            if (string.IsNullOrWhiteSpace(mapId) || captures == null)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Plugin.RuntimeExtractCatalogPath));

            JObject root;
            if (File.Exists(Plugin.RuntimeExtractCatalogPath))
            {
                string existing = File.ReadAllText(Plugin.RuntimeExtractCatalogPath);
                root = string.IsNullOrWhiteSpace(existing) ? new JObject() : JObject.Parse(existing);
            }
            else
            {
                root = new JObject();
            }

            JArray maps = root["maps"] as JArray;
            if (maps == null)
            {
                maps = new JArray();
                root["maps"] = maps;
            }

            JObject mapNode = maps
                .Children<JObject>()
                .FirstOrDefault(node => string.Equals((string)node["mapId"], mapId, StringComparison.OrdinalIgnoreCase));

            if (mapNode == null)
            {
                mapNode = new JObject
                {
                    ["mapId"] = mapId
                };
                maps.Add(mapNode);
            }

            mapNode["captures"] = new JArray(
                captures
                    .OrderBy(capture => (string)capture["extractName"], StringComparer.OrdinalIgnoreCase)
                    .ThenBy(capture => (string)capture["pointType"], StringComparer.OrdinalIgnoreCase));

            root["generatedAtUtc"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            File.WriteAllText(Plugin.RuntimeExtractCatalogPath, root.ToString(Formatting.Indented));

            _catalog = root;
            _catalogLastWriteUtc = File.GetLastWriteTimeUtc(Plugin.RuntimeExtractCatalogPath);
        }

        private static List<RuntimeExtractMatch> BuildMatches(JObject mapNode, Vector3 savedPosition, bool isScav, bool preferSideMatch)
        {
            List<RuntimeExtractMatch> matches = new List<RuntimeExtractMatch>();
            foreach (JObject capture in mapNode["captures"]?.Children<JObject>() ?? Enumerable.Empty<JObject>())
            {
                bool supportsRequestedSide = isScav
                    ? capture.Value<bool?>("supportsScav").GetValueOrDefault()
                    : capture.Value<bool?>("supportsPmc").GetValueOrDefault();

                if (preferSideMatch && !supportsRequestedSide)
                {
                    continue;
                }

                string extractName = capture.Value<string>("extractName");
                if (string.IsNullOrWhiteSpace(extractName))
                {
                    continue;
                }

                float distance = ComputeNearestDistance(savedPosition, capture);
                matches.Add(new RuntimeExtractMatch
                {
                    InternalName = extractName,
                    DisplayName = OriginalPluginAccessor.TranslateInternalName(mapNode.Value<string>("mapId"), extractName),
                    Distance = distance
                });
            }

            return matches;
        }

        private static float ComputeNearestDistance(Vector3 savedPosition, JObject capture)
        {
            float bestDistance = float.MaxValue;

            foreach (Vector3 candidate in EnumerateCandidatePoints(capture))
            {
                float distance = Vector3.Distance(savedPosition, candidate);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                }
            }

            return bestDistance;
        }

        private static IEnumerable<Vector3> EnumerateCandidatePoints(JObject capture)
        {
            Vector3? worldPosition = TryReadVector3(capture["worldPosition"] as JObject);
            if (worldPosition.HasValue)
            {
                yield return worldPosition.Value;
            }

            Vector3? primaryBoundsCenter = TryReadVector3(capture["primaryCollider"]?["boundsCenter"] as JObject);
            if (primaryBoundsCenter.HasValue)
            {
                yield return primaryBoundsCenter.Value;
            }

            Vector3? extendedBoundsCenter = TryReadVector3(capture["extendedCollider"]?["boundsCenter"] as JObject);
            if (extendedBoundsCenter.HasValue)
            {
                yield return extendedBoundsCenter.Value;
            }
        }

        private static Vector3? TryReadVector3(JObject node)
        {
            if (node == null)
            {
                return null;
            }

            float? x = node.Value<float?>("x");
            float? y = node.Value<float?>("y");
            float? z = node.Value<float?>("z");
            if (!x.HasValue || !y.HasValue || !z.HasValue)
            {
                return null;
            }

            return new Vector3(x.Value, y.Value, z.Value);
        }

        private static JObject GetMapNode(string normalizedLocationId)
        {
            EnsureCatalogLoaded();
            return _catalog?["maps"]?
                .Children<JObject>()
                .FirstOrDefault(node => string.Equals((string)node["mapId"], normalizedLocationId, StringComparison.OrdinalIgnoreCase));
        }

        private static void EnsureCatalogLoaded()
        {
            string filePath = Plugin.RuntimeExtractCatalogPath;
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                filePath = Plugin.LegacyRuntimeExtractCatalogPath;
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                {
                    _catalog = null;
                    _catalogLastWriteUtc = default;
                    return;
                }
            }

            DateTime lastWriteUtc = File.GetLastWriteTimeUtc(filePath);
            if (_catalog != null && lastWriteUtc == _catalogLastWriteUtc)
            {
                return;
            }

            string content = File.ReadAllText(filePath);
            _catalog = string.IsNullOrWhiteSpace(content) ? null : JObject.Parse(content);
            _catalogLastWriteUtc = lastWriteUtc;
        }
    }

    internal sealed class RuntimeExtractMatch
    {
        public string InternalName { get; set; }

        public string DisplayName { get; set; }

        public float Distance { get; set; }
    }
}
