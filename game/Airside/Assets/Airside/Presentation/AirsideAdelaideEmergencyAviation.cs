using System;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>ADR 0186 — places AIR-017 on the mapped western rescue helipad.</summary>
    public static class AirsideAdelaideEmergencyAviation
    {
        public const string ArtPath = "Models/Aircraft/mdl_bell_412_rescue_v01.gltf";
        public const string ObjectName = "Adelaide Rescue Helicopter";

        /// <summary>
        /// The parked rescue helicopter drawn for the demo circuit. In an airline game the fleet's own helicopter
        /// (ADR 0207) stands on the pad instead, so presentation hides this one.
        /// </summary>
        public static Transform StaticHelicopter { get; private set; }

        /// <summary>World Y of the pad surface, where a helicopter's skids rest.</summary>
        public static float PadGroundY { get; private set; } = AirsideAdelaideGround.PavementWorldY;

        public static bool TryBuild(Transform parent, Func<float, float, float> groundHeight)
        {
            if (parent == null)
                return false;
            var groundY = groundHeight != null
                ? groundHeight(AdelaideEmergencyAviationGeometry.PadCentreX, AdelaideEmergencyAviationGeometry.PadCentreZ)
                : AirsideAdelaideGround.PavementWorldY;
            Transform helicopter;
            var loaded = ArtPresentationLoader.TryInstantiate(
                ArtPath,
                parent,
                out helicopter,
                part => $"{ObjectName} {part}",
                part => PartColour(part));
            if (!loaded)
                helicopter = BuildFallback(parent);
            if (helicopter == null)
                return false;

            helicopter.name = ObjectName;
            helicopter.localPosition = new Vector3(
                AdelaideEmergencyAviationGeometry.PadCentreX,
                groundY + 0.18f,
                AdelaideEmergencyAviationGeometry.PadCentreZ);
            helicopter.localRotation = Quaternion.Euler(0f, AdelaideEmergencyAviationGeometry.HelicopterYawDegrees, 0f);
            AirsideSceneIndex.Remember(helicopter);
            StaticHelicopter = helicopter;
            PadGroundY = groundY;
            return true;
        }

        /// <summary>Part colours for the Bell 412 kit; <paramref name="rescueRed"/> repaints the rescue-red parts.</summary>
        internal static Color? PartColour(string part, Color? rescueRed = null)
        {
            var key = part.ToLowerInvariant();
            if (key.Contains("glass") || key.Contains("window"))
                return new Color(0.08f, 0.16f, 0.22f, 0.68f);
            if (key.Contains("rescue_red"))
                return rescueRed ?? new Color(0.68f, 0.07f, 0.055f);
            if (key.Contains("rotor_blade") || key.Contains("landing_skid") || key.Contains("skid_strut")
                || key.Contains("step") || key.Contains("wire_strike"))
                return new Color(0.12f, 0.14f, 0.15f);
            if (key.Contains("hub") || key.Contains("exhaust") || key.Contains("mast"))
                return new Color(0.38f, 0.40f, 0.42f);
            if (key.Contains("searchlight"))
                return new Color(0.95f, 0.94f, 0.80f);
            if (key.Contains("camera"))
                return new Color(0.14f, 0.16f, 0.17f);
            if (key.Contains("nav_light_left"))
                return new Color(0.72f, 0.06f, 0.05f);
            if (key.Contains("nav_light_right"))
                return new Color(0.04f, 0.62f, 0.22f);
            if (key.Contains("beacon"))
                return new Color(0.88f, 0.08f, 0.04f);
            if (key.Contains("engine"))
                return new Color(0.86f, 0.87f, 0.86f);
            return new Color(0.94f, 0.94f, 0.90f);
        }

        internal static Transform BuildFallback(Transform parent)
        {
            var root = new GameObject(ObjectName + " fallback").transform;
            root.SetParent(parent, false);
            AddPrimitive(root, "Fuselage", PrimitiveType.Sphere, new Vector3(0f, 1.9f, 0.35f),
                new Vector3(2.65f, 2.65f, 6.1f), new Color(0.94f, 0.94f, 0.90f));
            AddPrimitive(root, "Tail boom", PrimitiveType.Cube, new Vector3(0f, 2.35f, -5.2f),
                new Vector3(0.58f, 0.58f, 7.2f), new Color(0.88f, 0.89f, 0.87f));
            AddPrimitive(root, "Rescue belly", PrimitiveType.Cube, new Vector3(0f, 1.0f, 0.1f),
                new Vector3(2.5f, 0.28f, 4.2f), new Color(0.68f, 0.07f, 0.055f));
            var glass = new Color(0.08f, 0.16f, 0.22f, 0.78f);
            AddPrimitive(root, "Cockpit glass", PrimitiveType.Cube, new Vector3(0f, 2.35f, 2.85f),
                new Vector3(1.8f, 0.7f, 0.10f), glass);
            AddPrimitive(root, "Rotor mast", PrimitiveType.Cylinder, new Vector3(0f, 4.0f, -0.25f),
                new Vector3(0.18f, 0.72f, 0.18f), new Color(0.30f, 0.32f, 0.33f));
            var dark = new Color(0.12f, 0.14f, 0.15f);
            AddPrimitive(root, "Main rotor A", PrimitiveType.Cube, new Vector3(0f, 4.6f, -0.25f),
                new Vector3(13.9f, 0.055f, 0.30f), dark);
            AddPrimitive(root, "Main rotor B", PrimitiveType.Cube, new Vector3(0f, 4.61f, -0.25f),
                new Vector3(0.30f, 0.055f, 13.9f), dark);
            AddPrimitive(root, "Skid left", PrimitiveType.Cube, new Vector3(-1.2f, 0.28f, 0f),
                new Vector3(0.13f, 0.13f, 5.4f), dark);
            AddPrimitive(root, "Skid right", PrimitiveType.Cube, new Vector3(1.2f, 0.28f, 0f),
                new Vector3(0.13f, 0.13f, 5.4f), dark);
            return root;
        }

        private static void AddPrimitive(Transform parent, string name, PrimitiveType type, Vector3 position,
            Vector3 scale, Color colour)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            var collider = go.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.Destroy(collider);
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = AirsideMaterialLibrary.CreateShared(colour,
                    type == PrimitiveType.Cylinder ? AirsideMaterialLibrary.SurfaceKind.Metal
                        : AirsideMaterialLibrary.SurfaceKind.AircraftSkin,
                    useTextures: false);
            AirsideSceneIndex.Remember(go.transform);
        }
    }
}
