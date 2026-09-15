using System;
using System.IO;
using Airside.Domain;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// True-scale check for runtime aircraft models (ADR 0048): the extent of every POSITION
    /// accessor in a runtime glTF kit, compared with the catalogue's real dimensions. The kits
    /// bake geometry in model space (no node transforms), so accessor min/max are the bounds.
    /// </summary>
    public static class AircraftModelBounds
    {
        /// <summary>Allowed difference from the catalogue dimension.</summary>
        public const double Tolerance = 0.05;

        [Serializable]
        private sealed class Gltf
        {
            public Accessor[] accessors;
            public Mesh[] meshes;
            public Node[] nodes;
        }

        [Serializable]
        private sealed class Node
        {
            public string name;
            public int mesh = -1;
        }

        [Serializable]
        private sealed class Accessor
        {
            public float[] min;
            public float[] max;
        }

        [Serializable]
        private sealed class Mesh
        {
            public Primitive[] primitives;
        }

        [Serializable]
        private sealed class Primitive
        {
            public Attributes attributes;
        }

        [Serializable]
        private sealed class Attributes
        {
            public int POSITION = -1;
        }

        /// <summary>Length (Z), wingspan (X) and height (Y) of a glTF kit, in metres.</summary>
        public static bool TryMeasure(string gltfJson, out double lengthMetres, out double wingspanMetres, out double heightMetres)
        {
            lengthMetres = wingspanMetres = heightMetres = 0;
            var gltf = JsonUtility.FromJson<Gltf>(gltfJson);
            if (gltf?.meshes == null || gltf.accessors == null)
                return false;

            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            var any = false;
            foreach (var mesh in gltf.meshes)
            foreach (var primitive in mesh.primitives ?? Array.Empty<Primitive>())
            {
                var index = primitive.attributes?.POSITION ?? -1;
                if (index < 0 || index >= gltf.accessors.Length)
                    continue;
                var accessor = gltf.accessors[index];
                if (accessor.min == null || accessor.max == null || accessor.min.Length < 3 || accessor.max.Length < 3)
                    continue;
                min = Vector3.Min(min, new Vector3(accessor.min[0], accessor.min[1], accessor.min[2]));
                max = Vector3.Max(max, new Vector3(accessor.max[0], accessor.max[1], accessor.max[2]));
                any = true;
            }

            if (!any)
                return false;
            wingspanMetres = max.x - min.x;
            heightMetres = max.y - min.y;
            lengthMetres = max.z - min.z;
            return true;
        }

        /// <summary>
        /// Model-space bounds of one named part (x = across, y = up, z = forward), for plan-view
        /// clearance checks such as parked wingtips.
        /// </summary>
        public static bool TryMeasurePart(string gltfJson, string partName, out Vector3 min, out Vector3 max)
        {
            min = max = Vector3.zero;
            var gltf = JsonUtility.FromJson<Gltf>(gltfJson);
            if (gltf?.nodes == null || gltf.meshes == null || gltf.accessors == null)
                return false;
            foreach (var node in gltf.nodes)
            {
                if (node.name != partName || node.mesh < 0 || node.mesh >= gltf.meshes.Length)
                    continue;
                var any = false;
                min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                foreach (var primitive in gltf.meshes[node.mesh].primitives ?? Array.Empty<Primitive>())
                {
                    var index = primitive.attributes?.POSITION ?? -1;
                    if (index < 0 || index >= gltf.accessors.Length || gltf.accessors[index].min == null)
                        continue;
                    var accessor = gltf.accessors[index];
                    min = Vector3.Min(min, new Vector3(accessor.min[0], accessor.min[1], accessor.min[2]));
                    max = Vector3.Max(max, new Vector3(accessor.max[0], accessor.max[1], accessor.max[2]));
                    any = true;
                }

                return any;
            }

            return false;
        }

        /// <summary>Measure a catalogue type's runtime model from the project art folder.</summary>
        public static bool TryMeasure(AircraftSpec spec, out double lengthMetres, out double wingspanMetres, out double heightMetres)
        {
            lengthMetres = wingspanMetres = heightMetres = 0;
            var path = spec?.RuntimeModelPath == null ? null : ArtRuntimePaths.ResolveExisting(spec.RuntimeModelPath);
            return path != null && TryMeasure(File.ReadAllText(path), out lengthMetres, out wingspanMetres, out heightMetres);
        }

        public static bool WithinTolerance(double measured, double reference) =>
            reference > 0 && Math.Abs(measured - reference) / reference <= Tolerance;
    }
}
