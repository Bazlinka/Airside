using System.Collections.Generic;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// P1 — mark runtime scenery static and combine shared-material slabs so the
    /// SRP batcher / GPU resident drawer see one airfield, not thousands of cubes.
    /// Aircraft, GSE, lights, weather and hold-short pulse targets stay dynamic.
    /// </summary>
    public static class AirsideStaticWorld
    {
        public static Transform WorldRoot { get; set; }

        public static void Attach(Transform transform)
        {
            if (transform == null || WorldRoot == null || transform == WorldRoot)
                return;
            if (transform.parent == WorldRoot)
                return;
            transform.SetParent(WorldRoot, true);
        }

        public static void Finalize(Transform airfieldRoot)
        {
            WorldRoot = airfieldRoot;
            if (airfieldRoot == null)
                return;

            var batch = new List<GameObject>(64);
            Collect(airfieldRoot, batch);
            if (batch.Count == 0)
                return;

            try
            {
                StaticBatchingUtility.Combine(batch.ToArray(), airfieldRoot.gameObject);
            }
            catch
            {
                // Combine can refuse some runtime meshes; instancing still applies.
            }

            AttachDistantLod(airfieldRoot);
        }

        private static void Collect(Transform root, List<GameObject> batch)
        {
            var go = root.gameObject;
            // Moving roots keep their whole subtree off the static batch so drift,
            // GSE, birds and clouds still translate after Combine.
            if (IsDynamic(go))
                return;

            for (var i = 0; i < root.childCount; i++)
                Collect(root.GetChild(i), batch);

            go.isStatic = true;
            if (go.GetComponent<MeshRenderer>() != null)
                batch.Add(go);
        }

        public static bool IsDynamic(GameObject go)
        {
            if (go == null)
                return true;
            if (go.GetComponent<Light>() != null)
                return true;
            var n = go.name;
            return n.StartsWith("Hold short", System.StringComparison.Ordinal)
                || n.StartsWith("Commercial ", System.StringComparison.Ordinal)
                || n.StartsWith("Ground traffic", System.StringComparison.Ordinal)
                || n.StartsWith("Fuel truck", System.StringComparison.Ordinal)
                || n.StartsWith("Baggage cart", System.StringComparison.Ordinal)
                || n.StartsWith("Passenger ", System.StringComparison.Ordinal)
                || n.StartsWith("GPU ", System.StringComparison.Ordinal)
                || n.StartsWith("Wheel chock", System.StringComparison.Ordinal)
                || n.StartsWith("Pushback", System.StringComparison.Ordinal)
                || n.StartsWith("Rain", System.StringComparison.Ordinal)
                || n.StartsWith("Touchdown", System.StringComparison.Ordinal)
                || n.StartsWith("Taxi spray", System.StringComparison.Ordinal)
                || n.StartsWith("Skid", System.StringComparison.Ordinal)
                || n.StartsWith("Wet ", System.StringComparison.Ordinal)
                || n.StartsWith("interior_glow", System.StringComparison.Ordinal)
                || n.StartsWith("flag_", System.StringComparison.Ordinal)
                || n.StartsWith("door_panel", System.StringComparison.Ordinal)
                || n.StartsWith("Propeller", System.StringComparison.Ordinal)
                || n.StartsWith("Star ", System.StringComparison.Ordinal)
                || n.StartsWith("Bird ", System.StringComparison.Ordinal)
                || n.StartsWith("Cloud", System.StringComparison.Ordinal)
                || n.StartsWith("Windsock sock", System.StringComparison.Ordinal)
                || n.StartsWith("Terminal window glow", System.StringComparison.Ordinal)
                || n.StartsWith("Coast boat", System.StringComparison.Ordinal)
                || n.StartsWith("Coast foam", System.StringComparison.Ordinal)
                || n == "GroundShadow"
                || n == "Hangar door"
                || n == "Windsock"
                || n == "Horizon dome"
                || n == "Sun disc"
                || n == "Moon disc"
                || n == "Apron life"
                || n == "Star field"
                || n == "Jetty deck";
        }

        private static void AttachDistantLod(Transform airfieldRoot)
        {
            var lods = new List<Renderer>();
            foreach (var renderer in airfieldRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                    continue;
                var n = renderer.gameObject.name;
                if (n.StartsWith("Hill far", System.StringComparison.Ordinal)
                    || n.StartsWith("Context hill", System.StringComparison.Ordinal)
                    || n.StartsWith("Context paddock", System.StringComparison.Ordinal)
                    || n.StartsWith("Airfield terrain base", System.StringComparison.Ordinal))
                    lods.Add(renderer);
            }

            if (lods.Count == 0)
                return;

            var group = airfieldRoot.gameObject.GetComponent<LODGroup>();
            if (group == null)
                group = airfieldRoot.gameObject.AddComponent<LODGroup>();
            group.SetLODs(new[]
            {
                new LOD(0.015f, lods.ToArray()),
                new LOD(0f, System.Array.Empty<Renderer>())
            });
            group.RecalculateBounds();
        }
    }
}
