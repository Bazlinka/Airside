using System.Collections.Generic;
using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// The bones a held item follows on a CHR-003 Quaternius figure (one shared 62-bone rig).
    /// Positions are read after the clip is sampled, so equipment moves with the hands.
    /// </summary>
    public sealed class FigureRig
    {
        private readonly Transform _root;
        private readonly Transform[] _lowerArm = new Transform[2];
        private readonly Transform[] _wrist = new Transform[2];
        private readonly Transform[] _middle1 = new Transform[2];
        private readonly Transform[] _middle2 = new Transform[2];
        private readonly Transform[] _thumb = new Transform[2];
        private readonly Transform _head;
        private readonly Transform _headEnd;

        private FigureRig(Transform root, Dictionary<string, Transform> bones)
        {
            _root = root;
            for (var side = 0; side < 2; side++)
            {
                var suffix = side == 0 ? ".L" : ".R";
                bones.TryGetValue("LowerArm" + suffix, out _lowerArm[side]);
                bones.TryGetValue("Wrist" + suffix, out _wrist[side]);
                bones.TryGetValue("Middle1" + suffix, out _middle1[side]);
                bones.TryGetValue("Middle2" + suffix, out _middle2[side]);
                bones.TryGetValue("Thumb2" + suffix, out _thumb[side]);
            }

            bones.TryGetValue("Head", out _head);
            bones.TryGetValue("Head_end", out _headEnd);
        }

        public Transform Root => _root;

        /// <summary>Null when the figure is not the shared rig (no hands to hold anything).</summary>
        public static FigureRig Find(GameObject figure)
        {
            var bones = new Dictionary<string, Transform>();
            foreach (var bone in figure.GetComponentsInChildren<Transform>(true))
                bones.TryAdd(bone.name, bone);
            var rig = new FigureRig(figure.transform, bones);
            return rig.HasHand(true) && rig.HasHand(false) ? rig : null;
        }

        private bool HasHand(bool right)
        {
            var side = right ? 1 : 0;
            return _lowerArm[side] != null && _wrist[side] != null && _middle1[side] != null && _thumb[side] != null;
        }

        /// <summary>
        /// The closed fist: position inside the curled fingers; +Z runs down the forearm, +Y out of
        /// the thumb side — the way a gripped stick leaves the hand.
        /// </summary>
        public void Grip(bool right, out Vector3 position, out Quaternion rotation)
        {
            var side = right ? 1 : 0;
            var wrist = _wrist[side].position;
            var knuckle = _middle1[side].position;
            var fingers = _middle2[side] != null ? _middle2[side].position : knuckle + (knuckle - wrist);
            position = Vector3.Lerp(knuckle, fingers, 0.35f);
            var forearm = wrist - _lowerArm[side].position;
            if (forearm.sqrMagnitude < 1e-8f)
                forearm = -_root.up;
            forearm.Normalize();
            var thumbSide = _thumb[side].position - knuckle;
            thumbSide -= Vector3.Dot(thumbSide, forearm) * forearm;
            if (thumbSide.sqrMagnitude < 1e-8f)
                thumbSide = Vector3.Cross(forearm, _root.right);
            rotation = Quaternion.LookRotation(forearm, thumbSide.normalized);
        }

        /// <summary>Crown-to-chin axis and the scale of this figure relative to the 1.83 m source.</summary>
        public bool Head(out Vector3 neck, out Vector3 up, out float scale)
        {
            neck = up = default;
            scale = 1f;
            if (_head == null || _headEnd == null)
                return false;
            neck = _head.position;
            var span = _headEnd.position - neck;
            // The source head bone is 0.255 m long on a 1.83 m figure.
            scale = span.magnitude / 0.255f;
            if (scale < 1e-4f)
                return false;
            up = span / (scale * 0.255f);
            return true;
        }
    }

    /// <summary>
    /// Equipment carried by ramp crew and passengers (ADR 0174): built once per job, posed in
    /// world space from the figure's hands after every clip sample, so wands are in the fists,
    /// cases hang from them and the fuel hose runs from the nozzle to the apron.
    /// </summary>
    public static class HandTools
    {
        public sealed class Kit
        {
            public Transform Root;
            /// <summary>Grip-space items in each fist (see <see cref="FigureRig.Grip"/>).</summary>
            public Transform Left;
            public Transform Right;
            /// <summary>Hangs level from the right fist, facing the way the body faces.</summary>
            public Transform RightHanging;
            /// <summary>Held upright in the right fist (a radio), whatever the wrist is doing.</summary>
            public Transform RightUpright;
            /// <summary>On the apron, in the body's frame (a trolley being pushed).</summary>
            public Transform Ground;
            public Transform Headset;
            public Transform[] Hose;
            /// <summary>Passenger roller bag: wheels on the ground, handle up to the right fist.</summary>
            public Transform Roller;
            public Transform RollerHandle;
            /// <summary>Held in front in both hands, level (a galley box).</summary>
            public Transform Front;
            /// <summary>Items shown only while the worker is carrying them (<see cref="SetCarried"/>).</summary>
            public readonly Dictionary<CarriedItem, Transform> Items = new();
            /// <summary>Where the fuel hose comes from (the truck's reel); the hose shows only while set.</summary>
            public Vector3? HoseAnchor;
        }

        private static readonly Color WandGlow = new(1f, 0.26f, 0.04f);
        private static readonly Color Handle = new(0.08f, 0.08f, 0.09f);
        private static readonly Color ConeOrange = new(0.96f, 0.36f, 0.06f);
        private static readonly Color Reflective = new(0.93f, 0.94f, 0.92f);
        private static readonly Color Hose = new(0.07f, 0.07f, 0.075f);
        private static readonly Color Steel = new(0.62f, 0.64f, 0.66f);
        private static readonly Color Aluminium = new(0.78f, 0.8f, 0.82f);
        private static readonly Color RadioBody = new(0.11f, 0.12f, 0.13f);
        private static readonly Color EarCup = new(0.85f, 0.14f, 0.1f);

        private static readonly Color[] BagColours =
        {
            new(0.1f, 0.14f, 0.26f), new(0.55f, 0.1f, 0.12f), new(0.1f, 0.1f, 0.11f),
            new(0.2f, 0.36f, 0.4f), new(0.42f, 0.44f, 0.46f), new(0.44f, 0.3f, 0.18f)
        };

        private const int HoseSegments = 7;
        private static readonly Vector3 RollerWheels = new(0f, -0.52f, -0.15f);
        private static readonly Dictionary<Color, Material> GlowMaterials = new();
        private static readonly Dictionary<(float, float, float), Mesh> Frustums = new();

        // ---- Build -----------------------------------------------------------------------------

        /// <summary>Equipment for a ramp job. Every worker also wears red ear defenders.</summary>
        public static Kit BuildRampKit(RampTask task, Transform parent)
        {
            var kit = NewKit($"{task} equipment", parent);
            kit.Headset = Child(kit.Root, "Ear defenders");
            for (var side = -1; side <= 1; side += 2)
                Part(kit.Headset, "Ear cup", PrimitiveType.Cylinder, new Vector3(side * 0.1f, 0f, 0f),
                    new Vector3(0.085f, 0.022f, 0.085f), Mat(EarCup), Quaternion.Euler(0f, 0f, 90f));

            switch (task)
            {
                case RampTask.MarshalArrival:
                case RampTask.WingWalk:
                    Wand(kit.Left);
                    Wand(kit.Right);
                    break;
                case RampTask.PlaceSafetyEquipment:
                case RampTask.EquipmentRunner:
                    // Carried by its tip and hidden after placement.
                    Cone(kit.Items[CarriedItem.Cone] = Child(kit.RightHanging, "Safety cone"));
                    break;
                case RampTask.FuelCoupling:
                    Nozzle(kit.Items[CarriedItem.Nozzle] = Child(kit.Right, "Nozzle"));
                    kit.Hose = new Transform[HoseSegments];
                    for (var i = 0; i < HoseSegments; i++)
                        kit.Hose[i] = Part(kit.Root, "Fuel hose", PrimitiveType.Cylinder, Vector3.zero,
                            Vector3.one, Mat(Hose, AirsideMaterialLibrary.SurfaceKind.Rubber));
                    break;
                case RampTask.CateringLoader:
                case RampTask.CateringDoor:
                    GalleyTrolley(kit.Items[CarriedItem.Trolley] = Child(kit.Ground, "Trolley"));
                    GalleyBox(kit.Items[CarriedItem.Canister] = Child(kit.Front, "Galley box"));
                    break;
                case RampTask.BaggageHold:
                case RampTask.BaggageCart:
                    Suitcase(kit.Items[CarriedItem.Bag] = Child(kit.RightHanging, "Bag"),
                        BagColours[(int)task % BagColours.Length], 0.56f);
                    break;
                case RampTask.BoardingSupervision:
                case RampTask.PushbackHeadset:
                    Radio(kit.RightUpright);
                    break;
            }

            return kit;
        }

        /// <summary>
        /// Hand luggage for about three passengers in five, fixed by their look: a roller bag
        /// (carried up and down stairs) or a holdall. Null for empty-handed passengers.
        /// </summary>
        /// <summary>True for a passenger whose look gives them a roller bag.</summary>
        public static bool TowsRollerBag(int look) => look % 5 is 0 or 2;

        public static Kit BuildPassengerBag(int look, Transform parent)
        {
            var style = look % 5;
            if (style >= 3)
                return null;
            var colour = BagColours[look / 5 % BagColours.Length];
            var kit = NewKit("Hand luggage", parent);
            if (style == 1)
            {
                Holdall(kit.RightHanging, colour);
                return kit;
            }

            kit.Roller = Child(kit.Root, "Roller bag");
            RollerBag(kit.Roller, colour);
            kit.RollerHandle = Part(kit.Root, "Roller handle", PrimitiveType.Cylinder, Vector3.zero, Vector3.one,
                Mat(Handle, AirsideMaterialLibrary.SurfaceKind.Metal));
            // On stairs the same bag is lifted: a second copy hangs from the hand.
            RollerBag(Child(kit.RightHanging, "Lifted roller bag", new Vector3(0.06f, -0.03f, 0f)), colour);
            return kit;
        }

        private static Kit NewKit(string name, Transform parent)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            return new Kit
            {
                Root = root,
                Left = Child(root, "Left fist"),
                Right = Child(root, "Right fist"),
                RightHanging = Child(root, "Right hand (hanging)"),
                RightUpright = Child(root, "Right hand (upright)"),
                Front = Child(root, "Both hands (front)"),
                Ground = Child(root, "Pushed")
            };
        }

        private static void Wand(Transform fist)
        {
            // Carried on as an extension of the forearm, as marshallers hold them: up when the
            // arm signals, down at rest. Slightly off the forearm line, out of the thumb side.
            var wand = Child(fist, "Marshalling wand", new Vector3(0f, 0f, -0.04f), Quaternion.Euler(78f, 0f, 0f));
            Part(wand, "Grip", PrimitiveType.Cylinder, new Vector3(0f, 0.01f, 0f),
                new Vector3(0.04f, 0.075f, 0.04f), Mat(Handle, AirsideMaterialLibrary.SurfaceKind.Rubber));
            Part(wand, "Light tube", PrimitiveType.Cylinder, new Vector3(0f, 0.3f, 0f),
                new Vector3(0.052f, 0.22f, 0.052f), Glow(WandGlow));
            Part(wand, "Tip", PrimitiveType.Sphere, new Vector3(0f, 0.52f, 0f),
                new Vector3(0.052f, 0.03f, 0.052f), Glow(WandGlow));
        }

        private static void Cone(Transform hanging)
        {
            var cone = Child(hanging, "Safety cone", new Vector3(0f, -0.04f, 0f), Quaternion.Euler(0f, 0f, 180f));
            var orange = Mat(ConeOrange, AirsideMaterialLibrary.SurfaceKind.Plastic);
            // Built tip-down from the fist: y is measured from the tip.
            Frustum(cone, "Cone top", 0.02f, 0.075f, 0f, 0.17f, orange);
            Frustum(cone, "Reflective collar", 0.077f, 0.105f, 0.17f, 0.12f, Mat(Reflective, AirsideMaterialLibrary.SurfaceKind.Plastic));
            Frustum(cone, "Cone skirt", 0.105f, 0.15f, 0.29f, 0.2f, orange);
            Part(cone, "Base", PrimitiveType.Cube, new Vector3(0f, 0.505f, 0f), new Vector3(0.36f, 0.03f, 0.36f), orange);
        }

        private static void Nozzle(Transform fist)
        {
            var steel = Mat(Steel, AirsideMaterialLibrary.SurfaceKind.Metal);
            Part(fist, "Nozzle body", PrimitiveType.Cube, new Vector3(0f, -0.03f, 0.03f), new Vector3(0.07f, 0.1f, 0.16f), steel);
            Part(fist, "Nozzle spout", PrimitiveType.Cylinder, new Vector3(0f, -0.01f, 0.16f),
                new Vector3(0.035f, 0.06f, 0.035f), steel, Quaternion.Euler(90f, 0f, 0f));
        }

        private static void GalleyTrolley(Transform ground)
        {
            var aluminium = Mat(Aluminium, AirsideMaterialLibrary.SurfaceKind.Metal);
            // Standard full-size galley cart, narrow end to the person pushing it.
            Part(ground, "Galley trolley", PrimitiveType.Cube, new Vector3(0f, 0.57f, 0.72f), new Vector3(0.3f, 0.98f, 0.8f), aluminium);
            Part(ground, "Trolley door seam", PrimitiveType.Cube, new Vector3(0f, 0.57f, 0.315f),
                new Vector3(0.24f, 0.86f, 0.012f), Mat(new Color(0.55f, 0.57f, 0.6f), AirsideMaterialLibrary.SurfaceKind.Metal));
            Part(ground, "Trolley handle", PrimitiveType.Cube, new Vector3(0f, 1.0f, 0.3f), new Vector3(0.26f, 0.035f, 0.035f),
                Mat(Handle, AirsideMaterialLibrary.SurfaceKind.Plastic));
            var wheel = Mat(Handle, AirsideMaterialLibrary.SurfaceKind.Rubber);
            foreach (var x in new[] { -0.11f, 0.11f })
            foreach (var z in new[] { 0.38f, 1.06f })
                Part(ground, "Castor", PrimitiveType.Cylinder, new Vector3(x, 0.04f, z), new Vector3(0.08f, 0.015f, 0.08f),
                    wheel, Quaternion.Euler(0f, 0f, 90f));
        }

        private static void Suitcase(Transform hanging, Color colour, float height)
        {
            var body = Mat(colour, AirsideMaterialLibrary.SurfaceKind.Plastic);
            Part(hanging, "Handle", PrimitiveType.Cube, new Vector3(0f, -0.02f, 0f), new Vector3(0.025f, 0.03f, 0.13f), Mat(Handle));
            Part(hanging, "Suitcase", PrimitiveType.Cube, new Vector3(0.05f, -0.04f - height * 0.5f, 0f),
                new Vector3(0.21f, height, 0.4f), body);
        }

        private static void Holdall(Transform hanging, Color colour)
        {
            Part(hanging, "Strap", PrimitiveType.Cube, new Vector3(0.03f, -0.05f, 0f), new Vector3(0.02f, 0.1f, 0.2f), Mat(Handle));
            Part(hanging, "Holdall", PrimitiveType.Capsule, new Vector3(0.06f, -0.2f, 0f),
                new Vector3(0.24f, 0.25f, 0.24f), Mat(colour, AirsideMaterialLibrary.SurfaceKind.Plastic), Quaternion.Euler(90f, 0f, 0f));
        }

        private static void RollerBag(Transform bag, Color colour)
        {
            // Origin at the top of the case; the case hangs below it.
            Part(bag, "Case", PrimitiveType.Cube, new Vector3(0f, -0.26f, 0f), new Vector3(0.22f, 0.5f, 0.36f),
                Mat(colour, AirsideMaterialLibrary.SurfaceKind.Plastic));
            var wheel = Mat(Handle, AirsideMaterialLibrary.SurfaceKind.Rubber);
            foreach (var x in new[] { -0.08f, 0.08f })
                Part(bag, "Wheel", PrimitiveType.Cylinder, new Vector3(x, -0.52f, -0.15f), new Vector3(0.05f, 0.012f, 0.05f),
                    wheel, Quaternion.Euler(0f, 0f, 90f));
        }

        private static void GalleyBox(Transform front)
        {
            // Standard galley container, carried in front in both hands.
            Part(front, "Galley box", PrimitiveType.Cube, new Vector3(0f, 0f, 0.14f), new Vector3(0.3f, 0.26f, 0.4f),
                Mat(Aluminium, AirsideMaterialLibrary.SurfaceKind.Metal));
            Part(front, "Galley box latch", PrimitiveType.Cube, new Vector3(0f, 0.02f, 0.345f), new Vector3(0.12f, 0.05f, 0.012f),
                Mat(Handle));
        }

        private static void Radio(Transform upright)
        {
            var body = Mat(RadioBody, AirsideMaterialLibrary.SurfaceKind.Plastic);
            Part(upright, "Radio", PrimitiveType.Cube, new Vector3(0f, 0.02f, 0.02f), new Vector3(0.055f, 0.13f, 0.035f), body);
            Part(upright, "Antenna", PrimitiveType.Cylinder, new Vector3(0.012f, 0.13f, 0.02f), new Vector3(0.013f, 0.045f, 0.013f), body);
        }

        /// <summary>Show only the item the worker has in hand now (tools that are always held stay).</summary>
        public static void SetCarried(Kit kit, CarriedItem item)
        {
            if (kit == null)
                return;
            foreach (var pair in kit.Items)
                if (pair.Value.gameObject.activeSelf != (pair.Key == item))
                    pair.Value.gameObject.SetActive(pair.Key == item);
        }

        /// <summary>A loose bag, galley box or trolley (one on its way into the aircraft), origin at its base.</summary>
        public static Transform BuildItem(CarriedItem kind, Transform parent, int variant = 0)
        {
            var root = Child(parent, $"{kind} (loose)");
            switch (kind)
            {
                case CarriedItem.Cone:
                    Cone(Child(root, "Cone", new Vector3(0f, 0.55f, 0f)));
                    break;
                case CarriedItem.Bag:
                    Suitcase(Child(root, "Bag", new Vector3(0f, 0.6f, 0f)), BagColours[variant % BagColours.Length], 0.56f);
                    break;
                case CarriedItem.Canister:
                    GalleyBox(Child(root, "Box", new Vector3(0f, 0.15f, 0f)));
                    break;
                case CarriedItem.Trolley:
                    GalleyTrolley(Child(root, "Trolley", new Vector3(0f, 0f, -0.72f)));
                    break;
            }

            return root;
        }

        // ---- Pose ------------------------------------------------------------------------------

        /// <summary>Put every item of a kit where this frame's pose puts the hands and head.</summary>
        public static void Pose(Kit kit, FigureRig rig, bool lifted = false)
        {
            if (kit == null || rig == null)
                return;
            var body = rig.Root;
            var forward = Vector3.ProjectOnPlane(body.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-6f)
                forward = Vector3.forward;
            var level = Quaternion.LookRotation(forward.normalized, Vector3.up);

            rig.Grip(false, out var leftPosition, out var leftRotation);
            rig.Grip(true, out var rightPosition, out var rightRotation);
            kit.Left.SetPositionAndRotation(leftPosition, leftRotation);
            kit.Right.SetPositionAndRotation(rightPosition, rightRotation);
            kit.RightHanging.SetPositionAndRotation(rightPosition, level);
            kit.RightUpright.SetPositionAndRotation(rightPosition, level);
            kit.Front.SetPositionAndRotation((leftPosition + rightPosition) * 0.5f + forward.normalized * 0.12f, level);
            kit.Ground.SetPositionAndRotation(body.position, level);

            if (kit.Headset != null && rig.Head(out var neck, out var headUp, out var scale))
            {
                kit.Headset.SetPositionAndRotation(neck + headUp * (0.085f * scale),
                    Quaternion.LookRotation(Vector3.ProjectOnPlane(forward, headUp).normalized, headUp));
                kit.Headset.localScale = Vector3.one * scale;
            }

            if (kit.Hose != null)
            {
                var showHose = kit.HoseAnchor.HasValue;
                foreach (var segment in kit.Hose)
                    if (segment.gameObject.activeSelf != showHose)
                        segment.gameObject.SetActive(showHose);
                if (showHose)
                    PoseHose(kit.Hose, rightPosition + rightRotation * new Vector3(0f, -0.07f, -0.04f), kit.HoseAnchor.Value,
                        body.position.y);
            }

            if (kit.Roller != null)
            {
                kit.Roller.gameObject.SetActive(!lifted);
                kit.RollerHandle.gameObject.SetActive(!lifted);
                kit.RightHanging.gameObject.SetActive(lifted);
                if (!lifted)
                    PoseRoller(kit, rightPosition, forward, body.position.y);
            }
        }

        private static void PoseRoller(Kit kit, Vector3 grip, Vector3 forward, float groundY)
        {
            // Towed behind and outboard of the right hand, wheels on the apron, leaning to the hand.
            var right = Vector3.Cross(Vector3.up, forward);
            var wheels = grip - forward * 0.72f + right * 0.1f;
            wheels.y = groundY + 0.025f;
            var axis = (grip - wheels).normalized;
            var rotation = Quaternion.LookRotation(Vector3.Cross(right, axis), axis);
            // Case origin is its top centre; the wheels are on its back bottom edge.
            var origin = wheels - rotation * RollerWheels;
            kit.Roller.SetPositionAndRotation(origin, rotation);
            Stretch(kit.RollerHandle, origin + rotation * new Vector3(0f, 0f, RollerWheels.z), grip, 0.018f);
        }

        private static void PoseHose(Transform[] segments, Vector3 from, Vector3 to, float groundY)
        {
            // Drops from the nozzle to the apron, lies along it, and rises to the truck's reel.
            var bend = new Vector3(Mathf.Lerp(from.x, to.x, 0.5f), groundY - 0.35f, Mathf.Lerp(from.z, to.z, 0.5f));
            var previous = from;
            for (var i = 0; i < segments.Length; i++)
            {
                var t = (i + 1f) / segments.Length;
                var a = Vector3.Lerp(from, bend, t);
                var b = Vector3.Lerp(bend, to, t);
                var next = Vector3.Lerp(a, b, t);
                Stretch(segments[i], previous, next, 0.065f);
                previous = next;
            }
        }

        /// <summary>Lay a unit primitive cylinder between two points.</summary>
        private static void Stretch(Transform cylinder, Vector3 from, Vector3 to, float diameter)
        {
            var span = to - from;
            var length = span.magnitude;
            if (length < 1e-4f)
            {
                cylinder.localScale = Vector3.zero;
                return;
            }

            cylinder.SetPositionAndRotation((from + to) * 0.5f, Quaternion.FromToRotation(Vector3.up, span / length));
            cylinder.localScale = new Vector3(diameter, length * 0.5f + diameter * 0.3f, diameter);
        }

        // ---- Parts -----------------------------------------------------------------------------

        private static Transform Child(Transform parent, string name, Vector3 position = default, Quaternion? rotation = null)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = position;
            child.localRotation = rotation ?? Quaternion.identity;
            return child;
        }

        private static Transform Part(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale,
            Material material, Quaternion? rotation = null)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            AirsideRuntimeQuality.StripVisualCollider(part);
            var t = part.transform;
            t.SetParent(parent, false);
            t.localPosition = position;
            t.localRotation = rotation ?? Quaternion.identity;
            t.localScale = scale;
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return t;
        }

        private static void Frustum(Transform parent, string name, float topRadius, float bottomRadius, float fromY,
            float height, Material material)
        {
            var key = (topRadius, bottomRadius, height);
            if (!Frustums.TryGetValue(key, out var mesh) || mesh == null)
                Frustums[key] = mesh = BuildFrustum(topRadius, bottomRadius, height);
            var part = new GameObject(name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = new Vector3(0f, fromY, 0f);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        /// <summary>Open-ended frustum from y=0 (radius <paramref name="r0"/>) to y=h (<paramref name="r1"/>), flat-shaded.</summary>
        private static Mesh BuildFrustum(float r0, float r1, float h)
        {
            const int sides = 12;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (var i = 0; i < sides; i++)
            {
                var a0 = i * Mathf.PI * 2f / sides;
                var a1 = (i + 1) * Mathf.PI * 2f / sides;
                var d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
                var d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));
                var start = vertices.Count;
                vertices.Add(d0 * r0);
                vertices.Add(d1 * r0);
                vertices.Add(d1 * r1 + Vector3.up * h);
                vertices.Add(d0 * r1 + Vector3.up * h);
                triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }

            // Cap the wide end so the carried cone is not hollow from below.
            var wide = r0 > r1 ? 0f : h;
            var radius = Mathf.Max(r0, r1);
            var centre = vertices.Count;
            vertices.Add(Vector3.up * wide);
            for (var i = 0; i < sides; i++)
            {
                var a0 = i * Mathf.PI * 2f / sides;
                var a1 = (i + 1) * Mathf.PI * 2f / sides;
                var start = vertices.Count;
                vertices.Add(new Vector3(Mathf.Cos(a0) * radius, wide, Mathf.Sin(a0) * radius));
                vertices.Add(new Vector3(Mathf.Cos(a1) * radius, wide, Mathf.Sin(a1) * radius));
                if (wide > 0f)
                    triangles.AddRange(new[] { centre, start + 1, start });
                else
                    triangles.AddRange(new[] { centre, start, start + 1 });
            }

            var mesh = new Mesh { name = "Frustum" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material Mat(Color colour, AirsideMaterialLibrary.SurfaceKind kind = AirsideMaterialLibrary.SurfaceKind.Plastic) =>
            AirsideMaterialLibrary.CreateShared(colour, kind);

        /// <summary>A self-lit copy for wand tubes (never the shared material itself).</summary>
        private static Material Glow(Color colour)
        {
            if (GlowMaterials.TryGetValue(colour, out var cached) && cached != null)
                return cached;
            var material = new Material(Mat(colour)) { name = "Wand glow" };
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", colour * 1.1f);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            GlowMaterials[colour] = material;
            return material;
        }
    }
}
