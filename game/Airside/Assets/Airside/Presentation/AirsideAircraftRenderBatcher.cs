using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Airside.Presentation
{
    /// <summary>
    /// Combines static aircraft glazing that otherwise becomes one transparent draw per pane.
    /// Source renderers stay on their named transforms for animation, tests and art tooling,
    /// but are forced off after their geometry has been baked into material batches.
    /// </summary>
    public static class AirsideAircraftRenderBatcher
    {
        public const string BatchNamePrefix = "Cabin windows batch";

        public static int CombineStaticGlazing(Transform aircraft)
        {
            if (aircraft == null)
                return 0;

            var groups = new Dictionary<Material, List<MeshRenderer>>();
            foreach (var renderer in aircraft.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!IsStaticGlazing(renderer))
                    continue;
                var filter = renderer.GetComponent<MeshFilter>();
                var materials = renderer.sharedMaterials;
                if (filter == null || filter.sharedMesh == null || materials.Length != 1 || materials[0] == null)
                    continue;
                if (!groups.TryGetValue(materials[0], out var group))
                {
                    group = new List<MeshRenderer>();
                    groups.Add(materials[0], group);
                }
                group.Add(renderer);
            }

            var batchCount = 0;
            foreach (var pair in groups)
            {
                if (pair.Value.Count < 2)
                    continue;
                if (TryBuildBatch(aircraft, pair.Key, pair.Value, batchCount + 1))
                    batchCount++;
            }

            if (batchCount > 0)
                AirsideNamedChildren.Forget(aircraft);
            return batchCount;
        }

        public static bool IsStaticGlazing(Renderer renderer)
        {
            if (renderer == null || renderer.forceRenderingOff || renderer.sharedMaterial == null
                || renderer.sharedMaterial.renderQueue < (int)RenderQueue.Transparent)
                return false;
            var name = renderer.gameObject.name;
            if (name.StartsWith(BatchNamePrefix, StringComparison.Ordinal))
                return false;
            return name == "Cockpit"
                || name == "Cockpit glare"
                || name.StartsWith("Cabin window", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("cabin_window", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Windscreen", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Cockpit side", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("cockpit_side", StringComparison.OrdinalIgnoreCase)
                || (name.StartsWith("glazing_", StringComparison.OrdinalIgnoreCase)
                    && name.EndsWith("_reflection", StringComparison.OrdinalIgnoreCase));
        }

        private static bool TryBuildBatch(
            Transform aircraft,
            Material material,
            List<MeshRenderer> sources,
            int batchNumber)
        {
            var combines = new CombineInstance[sources.Count];
            var vertexCount = 0L;
            for (var i = 0; i < sources.Count; i++)
            {
                var mesh = sources[i].GetComponent<MeshFilter>().sharedMesh;
                vertexCount += mesh.vertexCount;
                combines[i] = new CombineInstance
                {
                    mesh = mesh,
                    subMeshIndex = 0,
                    transform = aircraft.worldToLocalMatrix * sources[i].transform.localToWorldMatrix
                };
            }

            var go = new GameObject($"{BatchNamePrefix} {batchNumber}");
            go.transform.SetParent(aircraft, false);
            var meshFilter = go.AddComponent<MeshFilter>();
            var batchRenderer = go.AddComponent<MeshRenderer>();
            var combined = new Mesh
            {
                name = $"{aircraft.name} glazing {batchNumber}",
                indexFormat = vertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            go.AddComponent<AirsideGeneratedMeshOwner>().Mesh = combined;

            try
            {
                combined.CombineMeshes(combines, true, true, false);
                combined.RecalculateBounds();
                combined.UploadMeshData(true);
                meshFilter.sharedMesh = combined;
                batchRenderer.sharedMaterial = material;
                batchRenderer.shadowCastingMode = ShadowCastingMode.Off;
                batchRenderer.receiveShadows = false;
                batchRenderer.lightProbeUsage = sources[0].lightProbeUsage;
                batchRenderer.reflectionProbeUsage = sources[0].reflectionProbeUsage;
                foreach (var source in sources)
                    source.forceRenderingOff = true;
                AirsideSceneIndex.Remember(go.transform);
                return true;
            }
            catch (Exception)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(go);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
                return false;
            }
        }
    }

}
