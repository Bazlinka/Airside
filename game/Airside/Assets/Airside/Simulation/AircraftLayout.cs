using System;
using System.Collections.Generic;
using Airside.Domain;

namespace Airside.Simulation
{
    /// <summary>An axis-aligned box in an aircraft's own frame (x right, z forward), metres.</summary>
    public readonly struct LayoutRect
    {
        public LayoutRect(float minX, float maxX, float minZ, float maxZ)
        {
            MinX = minX;
            MaxX = maxX;
            MinZ = minZ;
            MaxZ = maxZ;
        }

        public float MinX { get; }
        public float MaxX { get; }
        public float MinZ { get; }
        public float MaxZ { get; }

        public bool Contains(float x, float z, float margin) =>
            x > MinX - margin && x < MaxX + margin && z > MinZ - margin && z < MaxZ + margin;
    }

    /// <summary>One side's engine; the other side mirrors it. Propeller radius is 0 for a jet.</summary>
    public readonly struct EngineLayout
    {
        public EngineLayout(float x, float zBack, float zFront, float halfWidth, float propellerRadius = 0f,
            float propellerZ = 0f)
        {
            X = x;
            ZBack = zBack;
            ZFront = zFront;
            HalfWidth = halfWidth;
            PropellerRadius = propellerRadius;
            PropellerZ = propellerZ;
        }

        /// <summary>Centreline of the right-hand nacelle (positive); the left one is at -X.</summary>
        public float X { get; }
        public float ZBack { get; }
        public float ZFront { get; }
        public float HalfWidth { get; }
        public float PropellerRadius { get; }
        public float PropellerZ { get; }
        public bool IsPropeller => PropellerRadius > 0f;
    }

    /// <summary>
    /// Where the parts that matter on the ground are on each type, in the frame its art root and
    /// stand datum share: x to the aircraft's right, z forward, origin on the stand stop. Jets
    /// are rooted at the nose stop; the regional turboprops are rooted mid-airframe.
    ///
    /// Measured from the runtime glTF meshes (fuselage, nacelles, propeller blades, wings,
    /// tailplanes, doors). Everything that places people or vehicles beside an aircraft — ramp
    /// crew, service vehicles, the ground router's obstacles — reads this one table, so a crew
    /// member never stands in a propeller arc and a fuel truck never drives through a nacelle.
    ///
    /// Pure data and geometry; no UnityEngine types, so the headless harness checks it.
    /// </summary>
    public sealed class AircraftLayout
    {
        /// <summary>A service vehicle cannot pass under a surface lower than this.</summary>
        public const float VehicleHeadroomMetres = 3.4f;

        /// <summary>A person cannot walk under a surface lower than this.</summary>
        public const float PersonHeadroomMetres = 2.0f;

        private AircraftLayout(float noseZ, float tailZ, float halfWidth, float halfSpan,
            float wingBackZ, float wingFrontZ, float wingUndersideY,
            float tailplaneHalfSpan, float tailplaneBackZ, float tailplaneFrontZ, float tailplaneUndersideY,
            EngineLayout engine, (float X, float Z) passengerDoor, (float X, float Z) cargoDoor,
            (float X, float Z)? cateringDoor)
        {
            NoseZ = noseZ;
            TailZ = tailZ;
            HalfWidth = halfWidth;
            HalfSpan = halfSpan;
            WingBackZ = wingBackZ;
            WingFrontZ = wingFrontZ;
            WingUndersideY = wingUndersideY;
            TailplaneHalfSpan = tailplaneHalfSpan;
            TailplaneBackZ = tailplaneBackZ;
            TailplaneFrontZ = tailplaneFrontZ;
            TailplaneUndersideY = tailplaneUndersideY;
            Engine = engine;
            PassengerDoor = passengerDoor;
            CargoDoor = cargoDoor;
            CateringDoor = cateringDoor;
        }

        public float NoseZ { get; }
        public float TailZ { get; }
        /// <summary>Half the fuselage width.</summary>
        public float HalfWidth { get; }
        public float HalfSpan { get; }
        public float WingBackZ { get; }
        public float WingFrontZ { get; }
        public float WingUndersideY { get; }
        public float TailplaneHalfSpan { get; }
        public float TailplaneBackZ { get; }
        public float TailplaneFrontZ { get; }
        public float TailplaneUndersideY { get; }
        public EngineLayout Engine { get; }
        public EngineLayout? OuterEngine { get; private set; }

        /// <summary>The door passengers use when boarding by stairs (L1 on a jet).</summary>
        public (float X, float Z) PassengerDoor { get; }

        /// <summary>The hold door the baggage team works.</summary>
        public (float X, float Z) CargoDoor { get; }

        /// <summary>
        /// The service door a catering hi-loader docks at. Null for the regional turboprops:
        /// they are catered by hand through the passenger door, with no truck.
        /// </summary>
        public (float X, float Z)? CateringDoor { get; }

        public bool IsTurboprop => Engine.IsPropeller;
        public bool CateredByTruck => CateringDoor.HasValue;
        public float WingMidZ => (WingBackZ + WingFrontZ) * 0.5f;
        public float Length => NoseZ - TailZ;

        // ---- Footprint ------------------------------------------------------------------------

        /// <summary>
        /// Everything on this airframe a vehicle (or, with <paramref name="forVehicles"/> false, a
        /// person) must stay out of: fuselage, both nacelles, propeller arcs, and any wing or
        /// tailplane too low to pass beneath.
        /// </summary>
        public void Footprint(List<LayoutRect> into, bool forVehicles)
        {
            into.Clear();
            into.Add(new LayoutRect(-HalfWidth, HalfWidth, TailZ, NoseZ));
            var e = Engine;
            foreach (var side in Sides)
            {
                var x = side * e.X;
                into.Add(new LayoutRect(x - e.HalfWidth, x + e.HalfWidth, e.ZBack, e.ZFront));
                if (e.IsPropeller)
                    into.Add(new LayoutRect(x - e.PropellerRadius, x + e.PropellerRadius,
                        e.PropellerZ - 0.3f, e.PropellerZ + 0.3f));
            }

            if (OuterEngine is { } outer)
                foreach (var side in Sides)
                    into.Add(new LayoutRect(side * outer.X - outer.HalfWidth,
                        side * outer.X + outer.HalfWidth, outer.ZBack, outer.ZFront));

            var headroom = forVehicles ? VehicleHeadroomMetres : PersonHeadroomMetres;
            if (WingUndersideY < headroom)
                into.Add(new LayoutRect(-HalfSpan, HalfSpan, WingBackZ, WingFrontZ));
            if (TailplaneUndersideY < headroom)
                into.Add(new LayoutRect(-TailplaneHalfSpan, TailplaneHalfSpan, TailplaneBackZ, TailplaneFrontZ));
        }

        /// <summary>
        /// True inside a propeller's danger zone: the disc and <paramref name="margin"/> metres
        /// around it, reaching further forward (where the blast and suction are) than behind.
        /// </summary>
        public bool InPropellerZone(float x, float z, float margin)
        {
            if (!Engine.IsPropeller)
                return false;
            foreach (var side in Sides)
            {
                var cx = side * Engine.X;
                if (Math.Abs(x - cx) < Engine.PropellerRadius + margin
                    && z < Engine.PropellerZ + margin * 1.5f && z > Engine.PropellerZ - margin)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Nudge a point to the nearest spot outside the footprint (by <paramref name="margin"/>)
        /// and outside every propeller zone. Used for every crew and vehicle anchor.
        /// </summary>
        public (float X, float Z) Clear(float x, float z, float margin, bool forVehicles)
        {
            var rects = new List<LayoutRect>(8);
            Footprint(rects, forVehicles);
            if (Engine.IsPropeller)
                foreach (var side in Sides)
                {
                    var cx = side * Engine.X;
                    rects.Add(new LayoutRect(cx - Engine.PropellerRadius, cx + Engine.PropellerRadius,
                        Engine.PropellerZ - 0.3f, Engine.PropellerZ + margin * 0.5f + 0.3f));
                }

            for (var pass = 0; pass < 6; pass++)
            {
                var moved = false;
                foreach (var r in rects)
                {
                    if (!r.Contains(x, z, margin))
                        continue;
                    // Out through the nearest face.
                    var left = x - (r.MinX - margin);
                    var right = r.MaxX + margin - x;
                    var back = z - (r.MinZ - margin);
                    var front = r.MaxZ + margin - z;
                    var least = Math.Min(Math.Min(left, right), Math.Min(back, front));
                    if (least == left) x = r.MinX - margin - 0.01f;
                    else if (least == right) x = r.MaxX + margin + 0.01f;
                    else if (least == back) z = r.MinZ - margin - 0.01f;
                    else z = r.MaxZ + margin + 0.01f;
                    moved = true;
                }

                if (!moved)
                    break;
            }

            return (x, z);
        }

        // ---- Service anchors ------------------------------------------------------------------

        /// <summary>Side of the aircraft a door is on: -1 left, +1 right.</summary>
        public static float SideOf((float X, float Z) door) => door.X < 0f ? -1f : 1f;

        /// <summary>Where the fuel truck parks: beside the right wing, clear of nacelle and propeller.</summary>
        public (float X, float Z) FuelTruck => IsTurboprop
            ? Clear(Engine.X + Engine.PropellerRadius + 4.5f, WingBackZ - 4.0f, 2.2f, true)
            : Clear(Engine.X + Engine.HalfWidth + 4.5f, Engine.ZBack - 2.5f, 2.2f, true);

        /// <summary>The refuel coupling under the right wing, where the hose is connected.</summary>
        public (float X, float Z) FuelCoupling => IsTurboprop
            ? Clear(Engine.X + 1.7f, WingBackZ - 0.9f, 0.6f, false)
            : Clear(Engine.X + Engine.HalfWidth + 1.3f, Engine.ZBack - 1.5f, 0.6f, false);

        /// <summary>Where the catering hi-loader docks, beside its service door. Null without a truck.</summary>
        public (float X, float Z)? CateringTruck => CateringDoor is { } door
            ? Clear(door.X + SideOf(door) * 3.0f, door.Z, 2.2f, true)
            : null;

        /// <summary>Where the baggage train stops: beside the hold door, staged away from the wing.</summary>
        public (float X, float Z) BaggageTrain =>
            Clear(CargoDoor.X + SideOf(CargoDoor) * 6.5f,
                CargoDoor.Z + AwayFromWing(CargoDoor.Z) * (IsTurboprop ? 3.0f : 5.5f), 2.2f, true);

        /// <summary>+1 for a door ahead of the wing, -1 behind it: the direction with room to work.</summary>
        public float AwayFromWing(float z) => z >= WingMidZ ? 1f : -1f;

        // ---- Table ----------------------------------------------------------------------------

        private static readonly float[] Sides = { -1f, 1f };
        private static readonly Dictionary<string, AircraftLayout> ById = new(StringComparer.Ordinal);

        /// <summary>The layout of <paramref name="type"/>; an unknown type gets the 737-8's.</summary>
        public static AircraftLayout For(AircraftType type)
        {
            lock (ById)
            {
                if (ById.Count == 0)
                    Build();
                return type != null && ById.TryGetValue(type.Id, out var layout) ? layout : ById[AircraftType.Boeing7378.Id];
            }
        }

        private static void Build()
        {
            const float High = 99f;

            // Regional turboprops: root mid-airframe. The ATR's passenger door is its aft-left
            // airstair and its hold door is forward left, as on the real aircraft (its mesh
            // parts are moved to match when the model is loaded).
            ById[AircraftType.Atr42.Id] = new AircraftLayout(11.33f, -11.33f, 1.40f, 12.28f,
                -1.84f, 1.66f, 2.87f, 3.94f, -9.62f, -6.61f, 7.37f,
                new EngineLayout(3.99f, -1.84f, 5.17f, 0.67f, 1.96f, 5.58f),
                AtrPassengerDoor, AtrCargoDoor, null);
            ById[AircraftType.Saab340.Id] = new AircraftLayout(9.86f, -9.86f, 1.14f, 10.72f,
                -1.30f, 1.55f, 1.76f, 4.55f, -9.30f, -7.65f, 2.55f,
                new EngineLayout(3.55f, -2.67f, 2.97f, 0.62f, 1.67f, 2.95f),
                (-0.92f, 6.55f), (1.03f, -5.60f), null);
            ById[AircraftType.Dash8Q400.Id] = new AircraftLayout(16.42f, -16.42f, 1.36f, 14.21f,
                -1.10f, 3.25f, 4.32f, 4.2f, -16.2f, -13.0f, 8.0f,
                new EngineLayout(4.35f, -5.16f, 5.68f, 0.84f, 2.05f, 5.62f),
                (-1.20f, 11.05f), (1.26f, -9.40f), null);

            // Jets: root at the nose stop. Wings and tailplanes are high enough to drive under;
            // the nacelles are not. Catering docks at the aft right service door (R4 on a widebody).
            Jet(AircraftType.EmbraerE190, -36.24f, 1.50f, 14.36f, new EngineLayout(4.05f, -19.17f, -14.06f, 0.79f),
                (1.26f, -11.72f), (1.27f, -28.22f));
            Jet(AircraftType.AirbusA220300, -38.70f, 1.75f, 17.55f, new EngineLayout(4.85f, -19.98f, -14.61f, 1.20f),
                (1.48f, -12.35f), (1.49f, -30.65f));
            Jet(AircraftType.AirbusA320200, -37.57f, 1.98f, 17.90f, new EngineLayout(5.33f, -19.84f, -13.44f, 1.16f),
                (1.70f, -11.27f), (1.81f, -30.11f));
            Jet(AircraftType.Boeing737800, -39.47f, 1.88f, 17.96f, new EngineLayout(5.35f, -20.84f, -14.12f, 1.16f),
                (1.62f, -11.84f), (1.73f, -31.64f));
            Jet(AircraftType.Boeing7378, -39.47f, 1.88f, 17.96f, new EngineLayout(5.35f, -20.84f, -14.12f, 1.16f),
                (1.62f, -11.84f), (1.73f, -31.64f));
            Jet(AircraftType.AirbusA321Neo, -44.51f, 1.87f, 17.90f, new EngineLayout(5.33f, -23.51f, -15.92f, 1.16f),
                (1.61f, -13.35f), (1.72f, -35.68f));
            Jet(AircraftType.AirbusA350900, -66.80f, 2.98f, 32.38f, new EngineLayout(10.75f, -35.72f, -25.22f, 1.92f),
                (2.90f, -15.5f), (2.69f, -59.60f));
            Jet(AircraftType.Boeing78710, -68.30f, 2.88f, 30.06f, new EngineLayout(9.98f, -36.52f, -25.79f, 1.78f),
                (2.80f, -16.0f), (2.60f, -60.94f));
            Jet(AircraftType.AirbusA330900, -63.67f, 2.82f, 32.00f, new EngineLayout(10.63f, -34.04f, -24.03f, 1.90f),
                (2.70f, -15.0f), (2.55f, -56.80f));
            Jet(AircraftType.Boeing7879, -62.81f, 2.88f, 30.06f, new EngineLayout(9.98f, -33.59f, -23.71f, 1.78f),
                (2.80f, -15.0f), (2.60f, -56.04f));

            Jet(AircraftType.Boeing7478, -76.25f, 3.05f, 34.2f,
                new EngineLayout(12f, -38f, -30f, 1.45f), (2.9f, -20f), (2.9f, -64f));
            ById["B748"].OuterEngine = new EngineLayout(22f, -44f, -36f, 1.45f);
            Jet(AircraftType.AirbusA380800, -72.73f, 3.57f, 39.875f,
                new EngineLayout(13f, -36f, -28f, 1.65f), (3.4f, -19f), (3.4f, -61f));
            ById["A388"].OuterEngine = new EngineLayout(25f, -42f, -34f, 1.65f);

            void Jet(AircraftType type, float tailZ, float halfWidth, float halfSpan, EngineLayout engine,
                (float, float) cargo, (float, float) catering)
            {
                var door = AircraftDoors.L1(type);
                // Wing box: from the pylon (just aft of the nacelle front) back about a fifth of the length.
                var wingFront = engine.ZBack + 2.0f;
                var wingBack = engine.ZBack + 0.2f * tailZ;
                ById[type.Id] = new AircraftLayout(0f, tailZ, halfWidth, halfSpan, wingBack, wingFront, High,
                    halfSpan * 0.35f, tailZ + 6f, tailZ + 1f, High, engine,
                    (door.LocalX, door.LocalZ), cargo, catering);
            }
        }

        /// <summary>ATR 42 door positions after the mesh relocation (see <see cref="AtrDoorShiftMetres"/>).</summary>
        public static readonly (float X, float Z) AtrPassengerDoor = (-1.33f, -5.90f);

        public static readonly (float X, float Z) AtrCargoDoor = (-1.31f, 8.00f);

        /// <summary>
        /// The ATR mesh has its passenger door forward left and its hold door aft right — the
        /// real aircraft is the other way round. Presentation moves the passenger door parts aft
        /// by this much, and turns the hold door parts half round and forward by
        /// <see cref="AtrCargoDoorShiftMetres"/>, so the model, this table and the boarding
        /// agree.
        /// </summary>
        public const float AtrDoorShiftMetres = -12.15f;

        public const float AtrCargoDoorShiftMetres = 4.75f;

        /// <summary>World (x, z) of a point in this layout's frame on a stand or ground pose.</summary>
        public static (float X, float Z) ToWorld(GroundPose pose, float x, float z) =>
            (pose.X + pose.NoseX * z + pose.NoseZ * x, pose.Z + pose.NoseZ * z - pose.NoseX * x);

        /// <summary>This layout's frame from a world point on a pose (inverse of <see cref="ToWorld"/>).</summary>
        public static (float X, float Z) ToLocal(GroundPose pose, float worldX, float worldZ)
        {
            var dx = worldX - pose.X;
            var dz = worldZ - pose.Z;
            return (dx * pose.NoseZ - dz * pose.NoseX, dx * pose.NoseX + dz * pose.NoseZ);
        }
    }
}
