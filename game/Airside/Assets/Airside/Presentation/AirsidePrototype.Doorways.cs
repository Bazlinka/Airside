using System;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Sits on a door leaf and points at the doorway hollow behind it (see <see cref="AircraftDoorwayGeometry"/>).
    /// The hollow is only drawn while the door is away from the hull.
    /// </summary>
    public sealed class AircraftDoorway : MonoBehaviour
    {
        public GameObject Shell;
    }

    /// <summary>
    /// Hollow doorways behind every opening aircraft door. With the leaf shut they are hidden; as it swings
    /// or folds away they show a dark reveal, a darker cabin and a lit vestibule, so people walking up the
    /// stairs or bridge step into a door rather than into the bare fuselage.
    /// </summary>
    public sealed partial class AirsidePrototype
    {
        /// <summary>The leaf must be this far open (0..1) before the hollow is drawn.</summary>
        private const float DoorwayOpenThreshold = 0.02f;

        // scale = how much of the leaf's outline the layer covers; lift = how far proud of the leaf's face it sits.
        // The leaf is folded away whenever these show, so stacking them a few millimetres proud keeps the layers
        // clear of the hull (door fit is audited by scripts/fit-aircraft-doors.py) and apart for the depth
        // buffer, without anything poking through a shut door.
        private static readonly (string Name, float Scale, float Lift, Color Colour)[] DoorwayLayers =
        {
            ("Doorway reveal", 1.00f, 0.0020f, new Color(0.13f, 0.14f, 0.16f)),
            ("Doorway cabin", 0.88f, 0.0060f, new Color(0.05f, 0.05f, 0.06f)),
            ("Doorway light", 0.66f, 0.0100f, new Color(0.62f, 0.55f, 0.42f)),
        };

        /// <summary>
        /// Build the hollow for each passenger and cargo door on the aircraft. Call while the doors are
        /// still shut and before <see cref="ConvertToAirstairDoor"/> moves the passenger door's pivot.
        /// </summary>
        private static void AttachDoorways(Transform aircraft)
        {
            if (aircraft == null)
                return;
            var children = AirsideNamedChildren.Get(aircraft);
            var added = false;
            for (var i = 0; i < children.Length; i++)
            {
                var leaf = children[i];
                if (leaf == null || leaf == aircraft || leaf.GetComponent<AircraftDoorway>() != null)
                    continue;
                // The live name: nesting has already renamed the frame, handle and latch to "Frame" / "Handle" / "Latch".
                if (!leaf.name.StartsWith("CabinDoor", StringComparison.Ordinal)
                    && !leaf.name.StartsWith("Cargo door", StringComparison.Ordinal))
                    continue;
                // Only the leaf's own mesh: the frame, handle and latch nest beneath it.
                var filter = leaf.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable)
                    continue;
                added |= BuildDoorway(aircraft, leaf, filter.sharedMesh);
            }

            if (added)
                AirsideNamedChildren.Forget(aircraft);
        }

        private static bool BuildDoorway(Transform aircraft, Transform leaf, Mesh leafMesh)
        {
            var vertices = leafMesh.vertices;
            var positions = new float[vertices.Length * 3];
            for (var v = 0; v < vertices.Length; v++)
            {
                var local = aircraft.InverseTransformPoint(leaf.TransformPoint(vertices[v]));
                positions[v * 3] = local.x;
                positions[v * 3 + 1] = local.y;
                positions[v * 3 + 2] = local.z;
            }

            var shell = new GameObject("Doorway").transform;
            shell.SetParent(aircraft, false);
            var any = false;
            foreach (var layer in DoorwayLayers)
            {
                if (!AircraftDoorwayGeometry.TryBuild(positions, layer.Scale, layer.Lift, out var copy, out var triangles))
                    break;
                var points = new Vector3[copy.Length / 3];
                for (var v = 0; v < points.Length; v++)
                    points[v] = new Vector3(copy[v * 3], copy[v * 3 + 1], copy[v * 3 + 2]);
                var mesh = new Mesh { name = leafMesh.name + " " + layer.Name };
                mesh.vertices = points;
                mesh.triangles = triangles;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                var part = new GameObject(layer.Name);
                part.transform.SetParent(shell, false);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = part.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = CreateMaterial(layer.Colour);
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                any = true;
            }

            if (!any)
            {
                Debug.LogWarning($"Doorway: {leaf.name} on {aircraft.name} is not a front/back door shell ({vertices.Length} vertices); it opens onto bare hull.");
                DestroyPresentationObject(shell.gameObject);
                return false;
            }

            shell.gameObject.SetActive(false);
            leaf.gameObject.AddComponent<AircraftDoorway>().Shell = shell.gameObject;
            HingeLeafOnItsOutwardEdge(aircraft, leaf, positions);
            return true;
        }

        /// <summary>
        /// The leaf's pivot starts at its centre, so a swung-open door stood edge-on across the middle of the
        /// opening with passengers walking through it. Hinge it on the vertical edge that swings it out beside
        /// the doorway instead. <see cref="UpdateCabinDoor"/> turns a passenger door -85 deg and a cargo door
        /// +70 deg about Y, so the hinge goes at the aft edge when that turn is toward the door's own side.
        /// </summary>
        private static void HingeLeafOnItsOutwardEdge(Transform aircraft, Transform leaf, float[] positions)
        {
            var half = positions.Length / 6;
            float minY = float.MaxValue, maxY = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue, sumX = 0f;
            for (var v = 0; v < half; v++)
            {
                minY = Mathf.Min(minY, positions[v * 3 + 1]);
                maxY = Mathf.Max(maxY, positions[v * 3 + 1]);
                minZ = Mathf.Min(minZ, positions[v * 3 + 2]);
                maxZ = Mathf.Max(maxZ, positions[v * 3 + 2]);
                sumX += positions[v * 3];
            }

            var side = sumX < 0f ? -1f : 1f;
            var turn = leaf.name.StartsWith("CabinDoor", StringComparison.Ordinal) ? -1f : 1f;
            var edgeZ = turn == side ? minZ : maxZ;
            float edgeX = 0f;
            var edgeCount = 0;
            for (var v = 0; v < half; v++)
            {
                if (Mathf.Abs(positions[v * 3 + 2] - edgeZ) > 0.03f)
                    continue;
                edgeX += positions[v * 3];
                edgeCount++;
            }

            if (edgeCount == 0)
                return;
            RebakePartPivot(leaf, aircraft.TransformPoint(new Vector3(edgeX / edgeCount, (minY + maxY) * 0.5f, edgeZ)));
        }

        private static void ShowDoorway(GameObject shell, float open)
        {
            if (shell == null)
                return;
            var show = open > DoorwayOpenThreshold;
            if (shell.activeSelf != show)
                shell.SetActive(show);
        }
    }
}
