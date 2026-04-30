using Comfort.Common;
using EFT;
using EFT.Interactive;
using EFT.Interactive.SecretExfiltrations;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using SPT.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;
using archon.EntryPointSelector.MatchmakerUI.Data;

namespace archon.EntryPointSelector.MatchmakerUI.Patches
{
    internal class GameWorldRuntimeExtractCatalogPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(GameWorld), nameof(GameWorld.OnGameStarted));
        }

        [PatchPostfix]
        private static void PatchPostfix(ref GameWorld __instance)
        {
            try
            {
                if (__instance == null || Singleton<GameWorld>.Instance == null || __instance.ExfiltrationController == null)
                {
                    return;
                }

                string mapId = __instance.LocationId != null
                    ? __instance.LocationId.ToString().ToLowerInvariant()
                    : null;

                if (string.IsNullOrWhiteSpace(mapId))
                {
                    return;
                }

                List<JObject> captures = new List<JObject>();
                Dictionary<string, JObject> mergedCaptures = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);

                foreach (ExfiltrationPoint point in __instance.ExfiltrationController.ExfiltrationPoints ?? Array.Empty<ExfiltrationPoint>())
                {
                    CapturePoint(point, mapId, "pmc", captures, mergedCaptures);
                }

                foreach (ScavExfiltrationPoint point in __instance.ExfiltrationController.ScavExfiltrationPoints ?? Array.Empty<ScavExfiltrationPoint>())
                {
                    CapturePoint(point, mapId, "scav", captures, mergedCaptures);
                }

                foreach (SecretExfiltrationPoint point in __instance.ExfiltrationController.SecretExfiltrationPoints ?? Array.Empty<SecretExfiltrationPoint>())
                {
                    CapturePoint(point, mapId, "secret", captures, mergedCaptures);
                }

                RuntimeExtractCatalogStore.PersistCaptures(mapId, captures);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"[Archon EPS UI] Failed to update runtime extract catalog: {ex}");
            }
        }

        private static void CapturePoint(ExfiltrationPoint point, string mapId, string sourceBucket, List<JObject> captures, Dictionary<string, JObject> mergedCaptures)
        {
            if (point == null || point.Settings == null || string.IsNullOrWhiteSpace(point.Settings.Name))
            {
                return;
            }

            bool supportsPmc;
            bool supportsScav;
            bool requiresCooperation = point is SharedExfiltrationPoint;

            if (point is SecretExfiltrationPoint secretPoint)
            {
                supportsPmc = secretPoint.EligibleForPmc;
                supportsScav = secretPoint.EligibleForScav;
            }
            else if (point is SharedExfiltrationPoint)
            {
                supportsPmc = true;
                supportsScav = true;
            }
            else if (point is ScavExfiltrationPoint)
            {
                supportsPmc = false;
                supportsScav = true;
            }
            else
            {
                supportsPmc = true;
                supportsScav = false;
            }

            string mergeKey = string.Format(
                CultureInfo.InvariantCulture,
                "{0}|{1}|{2:F3}|{3:F3}|{4:F3}",
                mapId,
                point.Settings.Name,
                point.transform.position.x,
                point.transform.position.y,
                point.transform.position.z);

            if (mergedCaptures.TryGetValue(mergeKey, out JObject existing))
            {
                existing["supportsPmc"] = (bool)existing["supportsPmc"] || supportsPmc;
                existing["supportsScav"] = (bool)existing["supportsScav"] || supportsScav;
                existing["requiresCooperation"] = (bool)existing["requiresCooperation"] || requiresCooperation;
                MergeStringIntoArray(existing, "sourceBuckets", sourceBucket);
                MergeStringIntoArray(existing, "pointTypes", point.GetType().FullName);
                if (point.EligibleEntryPoints != null)
                {
                    foreach (string entryPoint in point.EligibleEntryPoints)
                    {
                        MergeStringIntoArray(existing, "eligibleEntryPoints", entryPoint);
                    }
                }
                return;
            }

            JObject payload = new JObject
            {
                ["mapId"] = mapId,
                ["extractName"] = point.Settings.Name,
                ["pointType"] = point.GetType().FullName,
                ["pointTypes"] = new JArray(point.GetType().FullName),
                ["sourceBucket"] = sourceBucket,
                ["sourceBuckets"] = new JArray(sourceBucket),
                ["supportsPmc"] = supportsPmc,
                ["supportsScav"] = supportsScav,
                ["requiresCooperation"] = requiresCooperation,
                ["worldPosition"] = SerializeVector3(point.transform.position),
                ["worldRotationEuler"] = SerializeVector3(point.transform.rotation.eulerAngles),
                ["settings"] = new JObject
                {
                    ["chance"] = point.Settings.Chance,
                    ["entryPoints"] = point.Settings.EntryPoints,
                    ["exfiltrationType"] = point.Settings.ExfiltrationType.ToString(),
                    ["startTime"] = point.Settings.StartTime
                }
            };

            if (point.EligibleEntryPoints != null && point.EligibleEntryPoints.Length > 0)
            {
                payload["eligibleEntryPoints"] = new JArray(point.EligibleEntryPoints);
            }

            BoxCollider primaryCollider = point.GetComponent<BoxCollider>();
            if (primaryCollider != null)
            {
                payload["primaryCollider"] = SerializeCollider(primaryCollider);
            }

            if (point.ExtendedCollider != null)
            {
                payload["extendedCollider"] = SerializeCollider(point.ExtendedCollider);
            }

            captures.Add(payload);
            mergedCaptures[mergeKey] = payload;
        }

        private static void MergeStringIntoArray(JObject payload, string propertyName, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            JArray array = payload[propertyName] as JArray;
            if (array == null)
            {
                array = new JArray();
                payload[propertyName] = array;
            }

            if (!array.Values<string>().Any(existing => string.Equals(existing, value, StringComparison.OrdinalIgnoreCase)))
            {
                array.Add(value);
            }
        }

        private static JObject SerializeCollider(Collider collider)
        {
            JObject payload = new JObject
            {
                ["colliderType"] = collider.GetType().FullName,
                ["enabled"] = collider.enabled,
                ["isTrigger"] = collider.isTrigger,
                ["boundsCenter"] = SerializeVector3(collider.bounds.center),
                ["boundsSize"] = SerializeVector3(collider.bounds.size)
            };

            if (collider is BoxCollider box)
            {
                payload["center"] = SerializeVector3(box.center);
                payload["size"] = SerializeVector3(box.size);
            }

            return payload;
        }

        private static JObject SerializeVector3(Vector3 vector)
        {
            return new JObject
            {
                ["x"] = vector.x,
                ["y"] = vector.y,
                ["z"] = vector.z
            };
        }
    }
}
