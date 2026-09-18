using Airside.Domain;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Presentation measurements for one aircraft silhouette. They never influence
    /// schedules, ground reservations, motion timing or save data.
    /// </summary>
    public readonly struct AircraftVisualProfile
    {
        public AircraftVisualProfile(
            string artRelativePath,
            float modelGroundOffsetMetres,
            Vector3 visualCentreOffsetMetres,
            Vector3 pickSizeMetres,
            float pickCentreYMetres,
            float shadowWidthMetres,
            float shadowDepthMetres,
            float selectionMarkerDiameterMetres,
            float followDistanceMultiplier,
            float mainTireRadiusMetres,
            float noseTireRadiusMetres)
        {
            ArtRelativePath = artRelativePath;
            ModelGroundOffsetMetres = modelGroundOffsetMetres;
            VisualCentreOffsetMetres = visualCentreOffsetMetres;
            PickSizeMetres = pickSizeMetres;
            PickCentreYMetres = pickCentreYMetres;
            ShadowWidthMetres = shadowWidthMetres;
            ShadowDepthMetres = shadowDepthMetres;
            SelectionMarkerDiameterMetres = selectionMarkerDiameterMetres;
            FollowDistanceMultiplier = followDistanceMultiplier;
            MainTireRadiusMetres = mainTireRadiusMetres;
            NoseTireRadiusMetres = noseTireRadiusMetres;
        }

        public string ArtRelativePath { get; }
        /// <summary>Local Y offset from an aircraft motion root on <see cref="AirsideFlightPath.GroundY"/>.</summary>
        public float ModelGroundOffsetMetres { get; }
        /// <summary>Aircraft-space offset from its route/nose datum to the visual centre.</summary>
        public Vector3 VisualCentreOffsetMetres { get; }
        public Vector3 PickSizeMetres { get; }
        public float PickCentreYMetres { get; }
        public float ShadowWidthMetres { get; }
        public float ShadowDepthMetres { get; }
        public float SelectionMarkerDiameterMetres { get; }
        public float FollowDistanceMultiplier { get; }
        /// <summary>Authoritative kit radius for main-wheel roll animation.</summary>
        public float MainTireRadiusMetres { get; }
        /// <summary>Authoritative kit radius for nose-wheel roll animation.</summary>
        public float NoseTireRadiusMetres { get; }
    }

    /// <summary>Aircraft-class profile catalogue for type-aware presentation.</summary>
    public static class AircraftVisualProfiles
    {
        // Existing AIR-001 ATR 42-class mesh/profile. This preserves its framing and
        // forgiving click target while distinct types are introduced incrementally.
        public static readonly AircraftVisualProfile RegionalTurboprop = new(
            artRelativePath: null,
            modelGroundOffsetMetres: -0.7f,
            visualCentreOffsetMetres: Vector3.zero,
            pickSizeMetres: new Vector3(30f, 10f, 28f),
            pickCentreYMetres: 3.5f,
            shadowWidthMetres: 22.5f,
            shadowDepthMetres: 16.5f,
            selectionMarkerDiameterMetres: 18f,
            followDistanceMultiplier: 1f,
            mainTireRadiusMetres: 0.37f,
            noseTireRadiusMetres: 0.31f);

        // AIR-005: original 737-8-class model. Its authored root is the parking
        // position's nose stop, its tyres sit at local y=0, and its nose faces +Z.
        public static readonly AircraftVisualProfile Boeing7378 = new(
            artRelativePath: "Models/Aircraft/mdl_737_8_narrowbody_v01.gltf",
            modelGroundOffsetMetres: -0.68f,
            visualCentreOffsetMetres: new Vector3(0f, 0f, -19.735f),
            pickSizeMetres: new Vector3(40f, 14f, 43f),
            pickCentreYMetres: 6.2f,
            shadowWidthMetres: 34f,
            shadowDepthMetres: 39f,
            selectionMarkerDiameterMetres: 41f,
            followDistanceMultiplier: 1.55f,
            mainTireRadiusMetres: 0.62f,
            noseTireRadiusMetres: 0.55f);

        // AIR-008: A321neo-class international narrowbody. Like the 737 kit its
        // authored origin is the nose-stop datum and its tyres sit at local y=0.
        public static readonly AircraftVisualProfile AirbusA321Neo = new(
            artRelativePath: "Models/Aircraft/mdl_a321neo_v01.gltf",
            modelGroundOffsetMetres: -0.68f,
            visualCentreOffsetMetres: new Vector3(0f, 0f, -22.255f),
            pickSizeMetres: new Vector3(40f, 14f, 48f),
            pickCentreYMetres: 5.9f,
            shadowWidthMetres: 34f,
            shadowDepthMetres: 44f,
            selectionMarkerDiameterMetres: 46f,
            followDistanceMultiplier: 1.7f,
            mainTireRadiusMetres: 0.59f,
            noseTireRadiusMetres: 0.52f);

        public static readonly AircraftVisualProfile AirbusA350900 = new(
            artRelativePath: "Models/Aircraft/mdl_a350_900_v01.gltf",
            modelGroundOffsetMetres: -0.68f,
            visualCentreOffsetMetres: new Vector3(0f, 0f, -33.40f),
            pickSizeMetres: new Vector3(68f, 20f, 72f),
            pickCentreYMetres: 8.5f,
            shadowWidthMetres: 63f,
            shadowDepthMetres: 66f,
            selectionMarkerDiameterMetres: 69f,
            followDistanceMultiplier: 2.4f,
            mainTireRadiusMetres: 0.70f,
            noseTireRadiusMetres: 0.55f);

        public static readonly AircraftVisualProfile Boeing78710 = new(
            artRelativePath: "Models/Aircraft/mdl_787_10_v01.gltf",
            modelGroundOffsetMetres: -0.68f,
            visualCentreOffsetMetres: new Vector3(0f, 0f, -34.15f),
            pickSizeMetres: new Vector3(64f, 20f, 74f),
            pickCentreYMetres: 8.5f,
            shadowWidthMetres: 59f,
            shadowDepthMetres: 68f,
            selectionMarkerDiameterMetres: 65f,
            followDistanceMultiplier: 2.45f,
            mainTireRadiusMetres: 0.70f,
            noseTireRadiusMetres: 0.55f);

        // AIR-007: original Saab 340B-class model. Low-wing regional turboprop with a
        // conventional tail; centred airframe root and tyres at local y=0, like the other
        // regional types, but framed to the compact 19.73 × 21.44 m envelope.
        public static readonly AircraftVisualProfile Saab340 = new(
            artRelativePath: "Models/Aircraft/mdl_saab_340b_v01.gltf",
            modelGroundOffsetMetres: -0.7f,
            visualCentreOffsetMetres: Vector3.zero,
            pickSizeMetres: new Vector3(24f, 9f, 23f),
            pickCentreYMetres: 3.2f,
            shadowWidthMetres: 20.5f,
            shadowDepthMetres: 18.5f,
            selectionMarkerDiameterMetres: 22f,
            followDistanceMultiplier: 0.92f,
            mainTireRadiusMetres: 0.38f,
            noseTireRadiusMetres: 0.28f);

        // AIR-006: original Dash 8-400-class model. Like AIR-001, the authored
        // regional-aircraft root is centred on the airframe and its tyres sit at
        // local y=0; the profile expands framing to the Q400's longer fuselage.
        public static readonly AircraftVisualProfile Dash8Q400 = new(
            artRelativePath: "Models/Aircraft/mdl_dash8_q400_v01.gltf",
            modelGroundOffsetMetres: -0.7f,
            visualCentreOffsetMetres: Vector3.zero,
            pickSizeMetres: new Vector3(31f, 10f, 36f),
            pickCentreYMetres: 4.1f,
            shadowWidthMetres: 28f,
            shadowDepthMetres: 32f,
            selectionMarkerDiameterMetres: 33f,
            followDistanceMultiplier: 1.28f,
            mainTireRadiusMetres: 0.50f,
            noseTireRadiusMetres: 0.34f);

        public static AircraftVisualProfile For(AircraftType type)
        {
            if (type != null && type.Id == AircraftType.Boeing7378.Id)
                return Boeing7378;
            if (type != null && type.Id == AircraftType.AirbusA321Neo.Id)
                return AirbusA321Neo;
            if (type != null && type.Id == AircraftType.AirbusA350900.Id)
                return AirbusA350900;
            if (type != null && type.Id == AircraftType.Boeing78710.Id)
                return Boeing78710;
            if (type != null && type.Id == AircraftType.Dash8Q400.Id)
                return Dash8Q400;
            if (type != null && type.Id == AircraftType.Saab340.Id)
                return Saab340;
            return RegionalTurboprop;
        }

        public static bool IsBoeing7378(AircraftType type) =>
            type != null && type.Id == AircraftType.Boeing7378.Id;

        public static bool IsAirbusA321Neo(AircraftType type) =>
            type != null && type.Id == AircraftType.AirbusA321Neo.Id;

        public static bool IsAirbusA350900(AircraftType type) =>
            type != null && type.Id == AircraftType.AirbusA350900.Id;

        public static bool IsBoeing78710(AircraftType type) =>
            type != null && type.Id == AircraftType.Boeing78710.Id;

        public static bool IsDash8Q400(AircraftType type) =>
            type != null && type.Id == AircraftType.Dash8Q400.Id;

        public static bool IsSaab340(AircraftType type) =>
            type != null && type.Id == AircraftType.Saab340.Id;
    }

    /// <summary>
    /// Stores the selected profile on an instantiated aircraft so generic pick,
    /// shadow and camera code can frame the actual silhouette instead of assuming
    /// every aircraft is the original ATR.
    /// </summary>
    public sealed class AircraftVisualProfileComponent : MonoBehaviour
    {
        [SerializeField] private Vector3 _pickSizeMetres;
        [SerializeField] private Vector3 _visualCentreOffsetMetres;
        [SerializeField] private float _pickCentreYMetres;
        [SerializeField] private float _shadowWidthMetres;
        [SerializeField] private float _shadowDepthMetres;
        [SerializeField] private float _selectionMarkerDiameterMetres;
        [SerializeField] private float _followDistanceMultiplier = 1f;
        [SerializeField] private float _mainTireRadiusMetres = AirsideReusableMotion.MainTireRadiusMetres;
        [SerializeField] private float _noseTireRadiusMetres = AirsideReusableMotion.NoseTireRadiusMetres;

        public Vector3 PickSizeMetres => _pickSizeMetres;
        public Vector3 VisualCentreOffsetMetres => _visualCentreOffsetMetres;
        public float PickCentreYMetres => _pickCentreYMetres;
        public float ShadowWidthMetres => _shadowWidthMetres;
        public float ShadowDepthMetres => _shadowDepthMetres;
        public float SelectionMarkerDiameterMetres => _selectionMarkerDiameterMetres;
        public float FollowDistanceMultiplier => _followDistanceMultiplier;
        public float MainTireRadiusMetres => _mainTireRadiusMetres;
        public float NoseTireRadiusMetres => _noseTireRadiusMetres;

        public static AircraftVisualProfileComponent Ensure(Transform aircraft, AircraftVisualProfile profile)
        {
            if (aircraft == null)
                return null;

            var component = aircraft.GetComponent<AircraftVisualProfileComponent>();
            if (component == null)
                component = aircraft.gameObject.AddComponent<AircraftVisualProfileComponent>();
            component.Apply(profile);
            return component;
        }

        private void Apply(AircraftVisualProfile profile)
        {
            _pickSizeMetres = profile.PickSizeMetres;
            _visualCentreOffsetMetres = profile.VisualCentreOffsetMetres;
            _pickCentreYMetres = profile.PickCentreYMetres;
            _shadowWidthMetres = profile.ShadowWidthMetres;
            _shadowDepthMetres = profile.ShadowDepthMetres;
            _selectionMarkerDiameterMetres = profile.SelectionMarkerDiameterMetres;
            _followDistanceMultiplier = profile.FollowDistanceMultiplier;
            _mainTireRadiusMetres = profile.MainTireRadiusMetres;
            _noseTireRadiusMetres = profile.NoseTireRadiusMetres;
        }
    }
}
