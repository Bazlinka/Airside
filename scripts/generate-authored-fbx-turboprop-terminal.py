#!/usr/bin/env python3
"""Generate authored-topology FBX (+ companion glTF) for hero turboprop and terminal.

Decision 0025 item 2 / Batch C AIR-001 + BLD-001: replace lofted/greybox cuboids with
lathed/cylindrical mesh topology at distinct *_authored_v01 ids. FBX is the Unity
ModelImporter source; companion glTF keeps StreamingAssets/ArtGltfLoader working
until Mac Editor bakes Resources prefabs from the imported FBX.

Does not race lofted/v04 filenames.
"""

from __future__ import annotations

import importlib.util
import subprocess
import tempfile
import uuid
from pathlib import Path

import numpy as np

ROOT = Path("/workspace/game/Airside/Assets/Airside/Art")
AIRCRAFT = ROOT / "Models" / "Aircraft"
BUILDINGS = ROOT / "Models" / "Buildings"
VEHICLES = ROOT / "Models" / "Vehicles"
PROPS = ROOT / "Models" / "Props"

_SPEC = importlib.util.spec_from_file_location(
    "batch_c_v01", Path("/workspace/scripts/generate-batch-c-models.py")
)
_v01 = importlib.util.module_from_spec(_SPEC)
assert _SPEC.loader is not None
_SPEC.loader.exec_module(_v01)
box = _v01.box
pack_gltf = _v01.pack_gltf
write_default_meta = _v01.write_default_meta


def new_guid() -> str:
    return uuid.uuid4().hex


def write_fbx_model_meta(path: Path) -> None:
    """Unity ModelImporter stub — keep existing GUID when regenerating."""
    meta_path = Path(str(path) + ".meta")
    if meta_path.exists():
        return
    Path(str(path) + ".meta").write_text(
        f"""fileFormatVersion: 2
guid: {new_guid()}
ModelImporter:
  serializedVersion: 22200
  internalIDToNameTable: []
  externalObjects: {{}}
  materials:
    materialImportMode: 1
    materialName: 0
    materialSearch: 1
    materialLocation: 1
  animations:
    legacyGenerateAnimations: 4
    bakeSimulation: 0
    resampleCurves: 1
    optimizeGameObjects: 0
    motionNodeName: 
    animationImportErrors: 
    animationImportWarnings: 
    animationRetargetingWarnings: 
    animationDoRetargetingWarnings: 0
    importAnimatedCustomProperties: 0
    importConstraints: 0
    animationCompression: 1
    animationRotationError: 0.5
    animationPositionError: 0.5
    animationScaleError: 0.5
    animationWrapMode: 0
    extraExposedTransformPaths: []
    extraUserProperties: []
    clipAnimations: []
    isReadable: 1
  meshes:
    lODScreenPercentages: []
    globalScale: 1
    meshCompression: 0
    addColliders: 0
    useFileUnits: 1
    keepQuads: 0
    weldVertices: 1
    bakeAxisConversion: 0
    preserveHierarchy: 1
    skinWeightsMode: 0
    maxBonesPerVertex: 4
    minBoneWeight: 0.001
    optimizeBones: 1
    meshOptimizationFlags: -1
    indexFormat: 0
    secondaryUVAngleDistortion: 8
    secondaryUVAreaDistortion: 15.000001
    secondaryUVHardAngle: 88
    secondaryUVMarginMethod: 1
    secondaryUVMinLightmapResolution: 40
    secondaryUVMinObjectScale: 1
    secondaryUVPackMargin: 4
    useFileScale: 1
    strictVertexDataChecks: 0
  tangentSpace:
    normalSmoothAngle: 60
    normalImportMode: 0
    tangentImportMode: 3
    normalCalculationMode: 4
    legacyComputeAllNormalsFromSmoothingGroupsWhenMeshHasBlendShapes: 0
    blendShapeNormalImportMode: 1
    normalSmoothingSource: 0
  referencedClips: []
  importAnimation: 0
  humanDescription:
    serializedVersion: 3
    human: []
    skeleton: []
    armTwist: 0.5
    foreArmTwist: 0.5
    upperLegTwist: 0.5
    legTwist: 0.5
    armStretch: 0.05
    legStretch: 0.05
    feetSpacing: 0
    globalScale: 1
    rootMotionBoneName: 
    hasTranslationDoF: 0
    hasExtraRoot: 0
    skeletonHasParents: 1
  lastHumanDescriptionAvatarSource: {{instanceID: 0}}
  autoGenerateAvatarMappingIfUnspecified: 1
  animationType: 0
  humanoidOversampling: 1
  avatarSetup: 0
  addHumanoidExtraRootOnlyWhenUsingAvatar: 1
  importBlendShapeDeformPercent: 1
  remapMaterialsIfMaterialImportModeIsNone: 0
  additionalBone: 0
  userData: 
  assetBundleName: 
  assetBundleVariant: 
""",
        encoding="utf-8",
    )


def cylinder(
    cx: float,
    cy: float,
    cz: float,
    radius: float,
    length: float,
    *,
    axis: str = "z",
    segments: int = 12,
    capped: bool = True,
) -> tuple[np.ndarray, np.ndarray]:
    """Axis-aligned cylinder centred at (cx,cy,cz); length along axis."""
    segs = max(6, segments)
    half = length / 2.0
    rings: list[np.ndarray] = []
    for end in (-half, half):
        ring = []
        for i in range(segs):
            ang = 2.0 * np.pi * i / segs
            x = radius * np.cos(ang)
            y = radius * np.sin(ang)
            if axis == "z":
                ring.append([cx + x, cy + y, cz + end])
            elif axis == "y":
                ring.append([cx + x, cy + end, cz + y])
            else:
                ring.append([cx + end, cy + y, cz + x])
        rings.append(np.asarray(ring, np.float32))

    verts: list = []
    indices: list = []
    for i in range(segs):
        j = (i + 1) % segs
        a, b = rings[0][i], rings[0][j]
        c, d = rings[1][j], rings[1][i]
        base = len(verts)
        verts.extend([a, b, c, d])
        indices.extend([base, base + 1, base + 2, base, base + 2, base + 3])

    if capped:
        for ring, flip in ((rings[0], True), (rings[1], False)):
            center = ring.mean(axis=0)
            for i in range(segs):
                j = (i + 1) % segs
                base = len(verts)
                if flip:
                    verts.extend([center, ring[j], ring[i]])
                else:
                    verts.extend([center, ring[i], ring[j]])
                indices.extend([base, base + 1, base + 2])

    return np.asarray(verts, np.float32), np.asarray(indices, np.uint16)


def lathe_fuselage(
    stations: list[tuple[float, float, float]],
    *,
    segments: int = 14,
) -> tuple[np.ndarray, np.ndarray]:
    """stations: list of (z, radius, y_center)."""
    segs = max(8, segments)
    rings = []
    for z, radius, cy in stations:
        ring = []
        for i in range(segs):
            ang = 2.0 * np.pi * i / segs
            ring.append([radius * np.cos(ang), cy + radius * np.sin(ang), z])
        rings.append(np.asarray(ring, np.float32))

    verts: list = []
    indices: list = []
    for r in range(len(rings) - 1):
        for i in range(segs):
            j = (i + 1) % segs
            a, b = rings[r][i], rings[r][j]
            c, d = rings[r + 1][j], rings[r + 1][i]
            base = len(verts)
            verts.extend([a, b, c, d])
            indices.extend([base, base + 1, base + 2, base, base + 2, base + 3])

    # Nose / tail caps
    for ring, z_sign in ((rings[0], -1.0), (rings[-1], 1.0)):
        tip = ring.mean(axis=0).copy()
        tip[2] += z_sign * 0.08
        for i in range(segs):
            j = (i + 1) % segs
            base = len(verts)
            if z_sign < 0:
                verts.extend([tip, ring[j], ring[i]])
            else:
                verts.extend([tip, ring[i], ring[j]])
            indices.extend([base, base + 1, base + 2])

    return np.asarray(verts, np.float32), np.asarray(indices, np.uint16)


def tapered_wing(
    cx: float,
    cy: float,
    cz: float,
    span: float,
    root_chord: float,
    tip_chord: float,
    thickness: float,
    *,
    side: float,
) -> tuple[np.ndarray, np.ndarray]:
    """side: -1 left, +1 right. Span extends along +X * side."""
    x0, x1 = cx, cx + side * span
    z_root_f, z_root_a = cz + root_chord / 2, cz - root_chord / 2
    z_tip_f, z_tip_a = cz + tip_chord / 2, cz - tip_chord / 2
    y0, y1 = cy - thickness / 2, cy + thickness / 2
    corners = np.array(
        [
            [x0, y0, z_root_a],
            [x0, y0, z_root_f],
            [x1, y0, z_tip_f],
            [x1, y0, z_tip_a],
            [x0, y1, z_root_a],
            [x0, y1, z_root_f],
            [x1, y1, z_tip_f],
            [x1, y1, z_tip_a],
        ],
        dtype=np.float32,
    )
    faces = [
        (0, 1, 2, 3),
        (4, 7, 6, 5),
        (0, 4, 5, 1),
        (3, 2, 6, 7),
        (1, 5, 6, 2),
        (0, 3, 7, 4),
    ]
    verts: list = []
    indices: list = []
    for a, b, c, d in faces:
        base = len(verts)
        verts.extend([corners[a], corners[b], corners[c], corners[d]])
        indices.extend([base, base + 1, base + 2, base, base + 2, base + 3])
    return np.asarray(verts, np.float32), np.asarray(indices, np.uint16)


def prop_blade(
    cx: float,
    cy: float,
    cz: float,
    angle_deg: float,
    *,
    length: float = 1.18,
    root_chord: float = 0.13,
    tip_chord: float = 0.04,
    thickness: float = 0.034,
) -> tuple[np.ndarray, np.ndarray]:
    """Thin tapered blade in the propeller disc (around +Z spin axis)."""
    a = np.deg2rad(angle_deg)
    ca, sa = float(np.cos(a)), float(np.sin(a))

    def pt(u: float, v: float, w: float) -> list[float]:
        # u radial from hub, v chord, w thickness along spin axis
        x = cx + v * ca - u * sa
        y = cy + v * sa + u * ca
        z = cz + w
        return [x, y, z]

    u0, u1 = 0.1, length
    hr, ht = root_chord / 2, tip_chord / 2
    tw = thickness / 2
    corners = np.array(
        [
            pt(u0, -hr, -tw),
            pt(u0, hr, -tw),
            pt(u1, ht, -tw),
            pt(u1, -ht, -tw),
            pt(u0, -hr, tw),
            pt(u0, hr, tw),
            pt(u1, ht, tw),
            pt(u1, -ht, tw),
        ],
        dtype=np.float32,
    )
    faces = [
        (0, 1, 2, 3),
        (4, 7, 6, 5),
        (0, 4, 5, 1),
        (3, 2, 6, 7),
        (1, 5, 6, 2),
        (0, 3, 7, 4),
    ]
    verts: list = []
    indices: list = []
    for a0, b, c, d in faces:
        base = len(verts)
        verts.extend([corners[a0], corners[b], corners[c], corners[d]])
        indices.extend([base, base + 1, base + 2, base, base + 2, base + 3])
    return np.asarray(verts, np.float32), np.asarray(indices, np.uint16)


def write_obj(path: Path, meshes: dict[str, tuple[np.ndarray, np.ndarray]]) -> None:
    lines = ["# Airside authored kit", "mtllib none"]
    v_offset = 1
    for name, (verts, indices) in meshes.items():
        lines.append(f"o {name}")
        for v in verts:
            lines.append(f"v {v[0]:.6f} {v[1]:.6f} {v[2]:.6f}")
        for i in range(0, len(indices), 3):
            a = int(indices[i]) + v_offset
            b = int(indices[i + 1]) + v_offset
            c = int(indices[i + 2]) + v_offset
            lines.append(f"f {a} {b} {c}")
        v_offset += len(verts)
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def export_fbx(meshes: dict[str, tuple[np.ndarray, np.ndarray]], fbx_path: Path) -> None:
    with tempfile.TemporaryDirectory() as tmp:
        obj_path = Path(tmp) / (fbx_path.stem + ".obj")
        write_obj(obj_path, meshes)
        result = subprocess.run(
            ["assimp", "export", str(obj_path), str(fbx_path)],
            capture_output=True,
            text=True,
            check=False,
        )
        if result.returncode != 0 or not fbx_path.exists():
            raise RuntimeError(
                f"assimp export failed for {fbx_path.name}: {result.stderr or result.stdout}"
            )
    write_fbx_model_meta(fbx_path)


def turboprop_meshes() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    # Lathed cabin — denser stations/segments so overview/follow reads rounder (0025 item 2).
    fuselage = {
        "fuselage": lathe_fuselage(
            [
                (5.45, 0.18, 1.0),
                (5.2, 0.28, 1.03),
                (4.9, 0.38, 1.06),
                (4.5, 0.48, 1.1),
                (4.0, 0.56, 1.16),
                (3.4, 0.64, 1.2),
                (2.7, 0.7, 1.2),
                (1.9, 0.73, 1.19),
                (1.1, 0.74, 1.18),
                (0.3, 0.74, 1.18),
                (-0.5, 0.72, 1.16),
                (-1.3, 0.7, 1.15),
                (-2.1, 0.64, 1.12),
                (-2.9, 0.54, 1.07),
                (-3.55, 0.42, 1.02),
                (-4.05, 0.3, 0.98),
                (-4.45, 0.2, 0.94),
            ],
            segments=24,
        ),
        "belly_fairing": cylinder(0, 0.52, 0.2, 0.42, 4.2, axis="z", segments=16),
        "radome": cylinder(0, 1.15, 5.05, 0.22, 0.55, axis="z", segments=14),
        "cockpit": box(0, 1.55, 3.55, 0.95, 0.55, 1.1),
        "cockpit_frame": box(0, 1.82, 3.55, 1.0, 0.06, 1.15),
        "cockpit_glare": box(0, 1.62, 4.05, 0.85, 0.28, 0.12),
        "windscreen_pillar_l": box(-0.42, 1.7, 4.0, 0.06, 0.45, 0.55),
        "windscreen_pillar_r": box(0.42, 1.7, 4.0, 0.06, 0.45, 0.55),
        "cabin_window_band": box(0, 1.38, 0.4, 1.48, 0.12, 4.2),
        "cabin_window_1": box(-0.74, 1.38, 2.0, 0.05, 0.26, 0.48),
        "cabin_window_2": box(-0.74, 1.38, 1.1, 0.05, 0.26, 0.48),
        "cabin_window_3": box(-0.74, 1.38, 0.2, 0.05, 0.26, 0.48),
        "cabin_window_4": box(-0.74, 1.38, -0.7, 0.05, 0.26, 0.48),
        "cabin_window_5": box(-0.74, 1.38, -1.6, 0.05, 0.26, 0.48),
        "cabin_window_6": box(-0.74, 1.38, -2.35, 0.05, 0.22, 0.36),
        "cabin_window_r1": box(0.74, 1.38, 2.0, 0.05, 0.26, 0.48),
        "cabin_window_r2": box(0.74, 1.38, 1.1, 0.05, 0.26, 0.48),
        "cabin_window_r3": box(0.74, 1.38, 0.2, 0.05, 0.26, 0.48),
        "cabin_window_r4": box(0.74, 1.38, -0.7, 0.05, 0.26, 0.48),
        "cabin_window_r5": box(0.74, 1.38, -1.6, 0.05, 0.26, 0.48),
        "cabin_window_r6": box(0.74, 1.38, -2.35, 0.05, 0.22, 0.36),
        "cabin_window_frame_1": box(-0.76, 1.38, 2.0, 0.04, 0.32, 0.55),
        "cabin_window_frame_3": box(-0.76, 1.38, 0.2, 0.04, 0.32, 0.55),
        "cabin_window_frame_5": box(-0.76, 1.38, -1.6, 0.04, 0.32, 0.55),
        "cabin_window_frame_r2": box(0.76, 1.38, 1.1, 0.04, 0.32, 0.55),
        "cabin_window_frame_r4": box(0.76, 1.38, -0.7, 0.04, 0.32, 0.55),
        "livery_stripe": box(0, 1.05, 0.5, 1.52, 0.1, 5.8),
        "livery_stripe_lower": box(0, 0.78, 0.4, 1.5, 0.06, 5.4),
        "door_frame_fwd": box(-0.72, 1.1, 2.1, 0.12, 1.15, 1.35),
        "door_handle_fwd": box(-0.78, 1.05, 2.4, 0.06, 0.12, 0.08),
        "inspection_panel_fwd": box(0.72, 1.0, 1.5, 0.04, 0.45, 0.7),
        "inspection_panel_aft": box(-0.72, 0.95, -2.0, 0.04, 0.4, 0.6),
    }

    wings = {
        "wing_left": tapered_wing(-0.7, 1.05, 0.4, 6.6, 1.9, 0.85, 0.14, side=-1),
        "wing_right": tapered_wing(0.7, 1.05, 0.4, 6.6, 1.9, 0.85, 0.14, side=1),
        "wing_root_left": box(-1.35, 1.08, 0.45, 1.7, 0.22, 1.5),
        "wing_root_right": box(1.35, 1.08, 0.45, 1.7, 0.22, 1.5),
        "wing_fairing_left": box(-1.9, 0.95, 0.55, 1.1, 0.18, 1.1),
        "wing_fairing_right": box(1.9, 0.95, 0.55, 1.1, 0.18, 1.1),
        "flap_left": box(-3.2, 1.0, -0.35, 3.0, 0.07, 0.42),
        "flap_right": box(3.2, 1.0, -0.35, 3.0, 0.07, 0.42),
        "flap_track_l1": box(-2.4, 0.92, -0.55, 0.08, 0.12, 0.35),
        "flap_track_l2": box(-3.6, 0.92, -0.55, 0.08, 0.12, 0.35),
        "flap_track_r1": box(2.4, 0.92, -0.55, 0.08, 0.12, 0.35),
        "flap_track_r2": box(3.6, 0.92, -0.55, 0.08, 0.12, 0.35),
        "flap_fairing_l": box(-3.2, 0.88, -0.2, 2.4, 0.1, 0.25),
        "flap_fairing_r": box(3.2, 0.88, -0.2, 2.4, 0.1, 0.25),
        "spoiler_left": box(-3.4, 1.14, 0.05, 2.4, 0.04, 0.35),
        "spoiler_right": box(3.4, 1.14, 0.05, 2.4, 0.04, 0.35),
        "aileron_left": box(-6.1, 1.02, 0.15, 1.7, 0.06, 0.5),
        "aileron_right": box(6.1, 1.02, 0.15, 1.7, 0.06, 0.5),
        "winglet_left": box(-7.35, 1.4, 0.45, 0.1, 0.6, 0.4),
        "winglet_right": box(7.35, 1.4, 0.45, 0.1, 0.6, 0.4),
        "wing_fence_left": box(-4.6, 1.18, 0.55, 0.06, 0.28, 0.9),
        "wing_fence_right": box(4.6, 1.18, 0.55, 0.06, 0.28, 0.9),
        "wing_fence_mid_l": box(-2.8, 1.16, 0.5, 0.05, 0.22, 0.7),
        "wing_fence_mid_r": box(2.8, 1.16, 0.5, 0.05, 0.22, 0.7),
        "static_wick_left": box(-7.5, 1.05, 0.15, 0.04, 0.04, 0.28),
        "static_wick_right": box(7.5, 1.05, 0.15, 0.04, 0.04, 0.28),
        "pitot": box(0.15, 1.35, 4.6, 0.04, 0.04, 0.35),
        "pitot_b": box(-0.18, 1.32, 4.55, 0.035, 0.035, 0.28),
        "vor_antenna": box(0, 0.35, -1.2, 0.5, 0.05, 0.05),
    }

    engines = {
        "engine_left": cylinder(-2.4, 0.85, 1.0, 0.38, 2.1, axis="z", segments=16),
        "engine_right": cylinder(2.4, 0.85, 1.0, 0.38, 2.1, axis="z", segments=16),
        "pylon_left": box(-2.4, 1.0, 0.7, 0.35, 0.35, 1.4),
        "pylon_right": box(2.4, 1.0, 0.7, 0.35, 0.35, 1.4),
        "nacelle_left": cylinder(-2.4, 0.52, 0.55, 0.28, 1.2, axis="z", segments=14),
        "nacelle_right": cylinder(2.4, 0.52, 0.55, 0.28, 1.2, axis="z", segments=14),
        "intake_left": cylinder(-2.4, 0.95, 2.0, 0.28, 0.35, axis="z", segments=14),
        "intake_right": cylinder(2.4, 0.95, 2.0, 0.28, 0.35, axis="z", segments=14),
        "exhaust_left": cylinder(-2.4, 0.7, -0.2, 0.18, 0.5, axis="z", segments=12),
        "exhaust_right": cylinder(2.4, 0.7, -0.2, 0.18, 0.5, axis="z", segments=12),
        "exhaust_stack_l": box(-2.55, 0.55, -0.35, 0.12, 0.18, 0.35),
        "exhaust_stack_r": box(2.55, 0.55, -0.35, 0.12, 0.18, 0.35),
        "oil_cooler_l": box(-2.4, 0.55, 1.2, 0.45, 0.18, 0.55),
        "oil_cooler_r": box(2.4, 0.55, 1.2, 0.45, 0.18, 0.55),
        "cowl_flap_l": box(-2.4, 0.7, 1.6, 0.5, 0.08, 0.35),
        "cowl_flap_r": box(2.4, 0.7, 1.6, 0.5, 0.08, 0.35),
        # Three tapered blades per hub (REF silhouette) — spin around +Z.
        "propeller_left": prop_blade(-2.4, 0.85, 2.25, 0),
        "propeller_left_b": prop_blade(-2.4, 0.85, 2.25, 120),
        "propeller_left_c": prop_blade(-2.4, 0.85, 2.25, 240),
        "propeller_right": prop_blade(2.4, 0.85, 2.25, 0),
        "propeller_right_b": prop_blade(2.4, 0.85, 2.25, 120),
        "propeller_right_c": prop_blade(2.4, 0.85, 2.25, 240),
        "spinner_left": cylinder(-2.4, 0.85, 2.42, 0.16, 0.36, axis="z", segments=14),
        "spinner_right": cylinder(2.4, 0.85, 2.42, 0.16, 0.36, axis="z", segments=14),
        "spinner_stripe_l": cylinder(-2.4, 0.85, 2.5, 0.17, 0.06, axis="z", segments=14),
        "spinner_stripe_r": cylinder(2.4, 0.85, 2.5, 0.17, 0.06, axis="z", segments=14),
        "prop_hub_left": cylinder(-2.4, 0.85, 2.3, 0.12, 0.18, axis="z", segments=12),
        "prop_hub_right": cylinder(2.4, 0.85, 2.3, 0.12, 0.18, axis="z", segments=12),
        "hub_cap_left": cylinder(-2.4, 0.85, 2.55, 0.08, 0.12, axis="z", segments=10),
        "hub_cap_right": cylinder(2.4, 0.85, 2.55, 0.08, 0.12, axis="z", segments=10),
    }

    empennage = {
        "tail_fin": box(0, 2.45, -3.7, 0.12, 2.2, 1.45),
        "tail_fin_tip": box(0, 3.45, -3.5, 0.1, 0.35, 0.7),
        "tailplane": box(0, 1.75, -3.85, 3.4, 0.1, 1.0),
        "tailplane_tip_l": box(-1.85, 1.78, -3.85, 0.35, 0.12, 0.7),
        "tailplane_tip_r": box(1.85, 1.78, -3.85, 0.35, 0.12, 0.7),
        "elevator_left": box(-1.1, 1.72, -4.25, 1.3, 0.05, 0.4),
        "elevator_right": box(1.1, 1.72, -4.25, 1.3, 0.05, 0.4),
        "rudder": box(0, 2.5, -4.35, 0.09, 1.6, 0.45),
        "dorsal_fin": box(0, 1.8, -2.8, 0.09, 0.5, 0.85),
        "hf_antenna": box(0, 2.15, -2.2, 0.04, 0.04, 1.6),
        "tail_nav_light": box(0, 3.55, -3.2, 0.08, 0.08, 0.08),
    }

    gear = {
        "gear_nose": box(0, 0.38, 3.15, 0.12, 0.72, 0.32),
        "gear_left": box(-1.15, 0.32, -0.35, 0.12, 0.72, 0.42),
        "gear_right": box(1.15, 0.32, -0.35, 0.12, 0.72, 0.42),
        "gear_oleo_nose": cylinder(0, 0.35, 3.15, 0.05, 0.55, axis="y", segments=8),
        "gear_oleo_left": cylinder(-1.15, 0.3, -0.35, 0.05, 0.55, axis="y", segments=8),
        "gear_oleo_right": cylinder(1.15, 0.3, -0.35, 0.05, 0.55, axis="y", segments=8),
        "gear_scissors_nose": box(0, 0.45, 3.0, 0.06, 0.35, 0.2),
        "gear_scissors_left": box(-1.15, 0.4, -0.5, 0.06, 0.35, 0.22),
        "gear_scissors_right": box(1.15, 0.4, -0.5, 0.06, 0.35, 0.22),
        "gear_door_nose": box(0, 0.55, 3.15, 0.5, 0.05, 0.65),
        "gear_door_left": box(-1.15, 0.55, -0.35, 0.6, 0.05, 0.8),
        "gear_door_right": box(1.15, 0.55, -0.35, 0.6, 0.05, 0.8),
        "tire_nose": cylinder(0, 0.12, 3.15, 0.14, 0.2, axis="x", segments=14),
        "tire_left": cylinder(-1.15, 0.12, -0.35, 0.16, 0.18, axis="x", segments=14),
        "tire_right": cylinder(1.15, 0.12, -0.35, 0.16, 0.18, axis="x", segments=14),
        "rim_nose": cylinder(0, 0.12, 3.15, 0.08, 0.12, axis="x", segments=10),
        "rim_left": cylinder(-1.15, 0.12, -0.35, 0.09, 0.1, axis="x", segments=10),
        "rim_right": cylinder(1.15, 0.12, -0.35, 0.09, 0.1, axis="x", segments=10),
        "door_fwd": box(-0.72, 1.1, 2.1, 0.07, 1.0, 1.2),
        "cargo_door": box(0.72, 1.0, -1.5, 0.07, 0.9, 1.5),
        "cargo_door_latch": box(0.78, 1.0, -1.2, 0.05, 0.15, 0.12),
        "antenna": box(0, 2.05, 1.2, 0.05, 0.5, 0.05),
        "antenna_aft": box(0, 1.95, -1.8, 0.04, 0.35, 0.04),
        "nav_light_left": box(-7.35, 1.08, 0.5, 0.1, 0.1, 0.1),
        "nav_light_right": box(7.35, 1.08, 0.5, 0.1, 0.1, 0.1),
        "beacon_top": box(0, 2.95, -3.35, 0.12, 0.12, 0.12),
        "landing_light_l": box(-2.55, 0.95, 2.0, 0.16, 0.1, 0.1),
        "landing_light_r": box(2.55, 0.95, 2.0, 0.16, 0.1, 0.1),
        "taxi_light": box(0, 0.55, 3.55, 0.14, 0.09, 0.1),
    }

    meshes: dict[str, tuple[np.ndarray, np.ndarray]] = {}
    for part in (fuselage, wings, engines, empennage, gear):
        meshes.update(part)
    return meshes


def terminal_meshes() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    # Practical regional terminal: body shell, glass curtain, caps, service wing, canopy.
    # Mullions sit proud of panes so the curtain wall reads framed (REF-001/002).
    mullion_z = -2.28
    pane_z = -2.18
    meshes: dict[str, tuple[np.ndarray, np.ndarray]] = {
        "terminal_body": box(0, 2.15, 0.2, 20.5, 4.1, 4.4),
        "plinth": box(0, 0.18, 0.1, 21.0, 0.36, 4.8),
        "roof": box(0, 4.35, 0.1, 21.2, 0.28, 5.0),
        "roof_plant": box(-4.5, 4.7, -0.8, 2.2, 0.55, 1.4),
        "roof_plant_b": box(3.8, 4.65, 0.6, 1.8, 0.45, 1.2),
        "roof_plant_c": box(0.5, 4.6, -0.4, 1.4, 0.35, 1.0),
        "roof_vent_a": cylinder(-2.0, 4.85, 0.8, 0.22, 0.45, axis="y", segments=8),
        "roof_vent_b": cylinder(2.5, 4.85, -0.6, 0.22, 0.45, axis="y", segments=8),
        "roof_parapet": box(0, 4.55, 2.45, 20.8, 0.25, 0.12),
        "roof_parapet_back": box(0, 4.55, -2.25, 20.8, 0.25, 0.12),
        "end_cap_left": box(-10.8, 2.0, 0, 1.1, 3.9, 5.0),
        "end_cap_right": box(10.8, 2.0, 0, 1.1, 3.9, 5.0),
        # Keep glass_front as a thin deep pane strip for fallbacks; panes add depth.
        "glass_front": box(0, 2.35, pane_z - 0.02, 16.5, 2.4, 0.04),
        "window_mullion_1": box(-6.0, 2.35, mullion_z, 0.12, 2.5, 0.14),
        "window_mullion_2": box(-3.0, 2.35, mullion_z, 0.12, 2.5, 0.14),
        "window_mullion_3": box(0.0, 2.35, mullion_z, 0.12, 2.5, 0.14),
        "window_mullion_4": box(3.0, 2.35, mullion_z, 0.12, 2.5, 0.14),
        "window_mullion_5": box(6.0, 2.35, mullion_z, 0.12, 2.5, 0.14),
        "window_mullion_6": box(-7.5, 2.35, mullion_z, 0.1, 2.5, 0.12),
        "window_mullion_7": box(7.5, 2.35, mullion_z, 0.1, 2.5, 0.12),
        "window_mullion_8": box(-4.5, 2.35, mullion_z, 0.08, 2.5, 0.1),
        "window_mullion_9": box(4.5, 2.35, mullion_z, 0.08, 2.5, 0.1),
        "window_mullion_10": box(-1.5, 2.35, mullion_z, 0.08, 2.5, 0.1),
        "window_mullion_11": box(1.5, 2.35, mullion_z, 0.08, 2.5, 0.1),
        "window_transom": box(0, 3.35, mullion_z, 16.2, 0.1, 0.12),
        "window_midrail": box(0, 2.35, mullion_z, 16.2, 0.08, 0.1),
        "window_sill": box(0, 1.15, mullion_z, 16.2, 0.08, 0.14),
        "window_header": box(0, 3.55, mullion_z, 16.4, 0.12, 0.14),
        "entrance": box(0, 1.35, -2.25, 2.4, 2.4, 0.12),
        "entrance_door_l": box(-0.55, 1.25, -2.32, 1.0, 2.2, 0.06),
        "entrance_door_r": box(0.55, 1.25, -2.32, 1.0, 2.2, 0.06),
        "entrance_frame": box(0, 1.35, -2.35, 2.7, 2.6, 0.08),
        "entrance_transom": box(0, 2.55, -2.38, 2.5, 0.08, 0.06),
        "entrance_handle_l": box(-0.15, 1.35, -2.4, 0.08, 0.35, 0.08),
        "entrance_handle_r": box(0.15, 1.35, -2.4, 0.08, 0.35, 0.08),
        "landside_glass": box(0, 2.2, 2.35, 12.0, 1.8, 0.04),
        "landside_mullion_1": box(-4.0, 2.2, 2.42, 0.1, 1.9, 0.12),
        "landside_mullion_2": box(0.0, 2.2, 2.42, 0.1, 1.9, 0.12),
        "landside_mullion_3": box(4.0, 2.2, 2.42, 0.1, 1.9, 0.12),
        "landside_mullion_4": box(-2.0, 2.2, 2.42, 0.08, 1.9, 0.1),
        "landside_mullion_5": box(2.0, 2.2, 2.42, 0.08, 1.9, 0.1),
        "landside_mullion_6": box(-5.5, 2.2, 2.42, 0.08, 1.9, 0.1),
        "landside_mullion_7": box(5.5, 2.2, 2.42, 0.08, 1.9, 0.1),
        "landside_transom": box(0, 2.85, 2.42, 11.5, 0.08, 0.1),
        "landside_sill": box(0, 1.35, 2.42, 11.5, 0.08, 0.12),
        "landside_awning": box(0, 3.2, 3.0, 10.0, 0.12, 1.4),
        "canopy": box(0, 3.55, -3.1, 14.0, 0.18, 2.2),
        "canopy_beam": box(0, 3.35, -3.1, 14.0, 0.12, 0.25),
        "canopy_edge": box(0, 3.45, -4.15, 14.0, 0.1, 0.12),
        "canopy_brace_l": box(-4.5, 3.2, -3.4, 0.1, 0.5, 1.4),
        "canopy_brace_r": box(4.5, 3.2, -3.4, 0.1, 0.5, 1.4),
        "canopy_light_l": box(-3.5, 3.4, -3.6, 0.35, 0.08, 0.25),
        "canopy_light_r": box(3.5, 3.4, -3.6, 0.35, 0.08, 0.25),
        "canopy_post_l": cylinder(-6.5, 1.7, -3.6, 0.12, 3.3, axis="y", segments=8),
        "canopy_post_r": cylinder(6.5, 1.7, -3.6, 0.12, 3.3, axis="y", segments=8),
        "canopy_post_ml": cylinder(-2.2, 1.7, -3.6, 0.12, 3.3, axis="y", segments=8),
        "canopy_post_mr": cylinder(2.2, 1.7, -3.6, 0.12, 3.3, axis="y", segments=8),
        "service_wing": box(7.5, 1.35, 2.8, 7.5, 2.6, 2.8),
        "service_door": box(9.5, 1.1, 4.15, 1.6, 2.0, 0.1),
        "baggage_door": box(5.5, 1.0, 4.15, 2.4, 1.8, 0.1),
        "baggage_ramp": box(5.5, 0.2, 4.6, 2.6, 0.25, 0.9),
        "baggage_canopy": box(5.5, 2.0, 4.5, 3.0, 0.12, 1.2),
        "boarding_gate": box(-5.5, 1.2, -2.4, 1.8, 2.2, 0.12),
        "boarding_frame": box(-5.5, 1.2, -2.5, 2.0, 2.4, 0.08),
        "signage_bar": box(0, 3.9, -2.3, 10.0, 0.35, 0.2),
        "signage_cap": box(0, 4.15, -2.3, 10.2, 0.1, 0.22),
        "column_l": cylinder(-8.5, 2.0, -1.5, 0.22, 3.8, axis="y", segments=8),
        "column_r": cylinder(8.5, 2.0, -1.5, 0.22, 3.8, axis="y", segments=8),
        "column_ml": cylinder(-4.0, 2.0, -1.5, 0.18, 3.8, axis="y", segments=8),
        "column_mr": cylinder(4.0, 2.0, -1.5, 0.18, 3.8, axis="y", segments=8),
        "buttress": box(-10.2, 1.2, -1.8, 0.8, 2.2, 1.2),
        "buttress_r": box(10.2, 1.2, -1.8, 0.8, 2.2, 1.2),
        "hvac_duct": box(-6.5, 4.55, 1.2, 3.5, 0.2, 0.35),
        "downpipe_l": cylinder(-10.2, 2.2, 2.2, 0.08, 4.2, axis="y", segments=8),
        "downpipe_r": cylinder(10.2, 2.2, 2.2, 0.08, 4.2, axis="y", segments=8),
        "flag_pole": cylinder(10.2, 3.5, -2.8, 0.05, 3.0, axis="y", segments=8),
        "flag_cloth": box(10.55, 4.6, -2.8, 0.7, 0.45, 0.04),
        # Warm interior silhouettes behind the curtain wall (night glow targets).
        "interior_counter": box(-3.5, 1.15, -1.55, 4.5, 0.9, 0.7),
        "interior_seat_row": box(3.2, 0.75, -1.45, 5.0, 0.55, 0.7),
        "interior_desk_a": box(-5.5, 1.05, -1.5, 2.2, 0.85, 0.65),
        "interior_desk_b": box(5.5, 1.05, -1.5, 2.2, 0.85, 0.65),
        "interior_table_1": box(-2.0, 0.85, -1.35, 1.2, 0.12, 0.7),
        "interior_table_2": box(1.8, 0.85, -1.35, 1.2, 0.12, 0.7),
        "interior_chair_1": box(-2.5, 0.55, -1.2, 0.45, 0.55, 0.45),
        "interior_chair_2": box(-1.5, 0.55, -1.2, 0.45, 0.55, 0.45),
        "interior_chair_3": box(1.3, 0.55, -1.2, 0.45, 0.55, 0.45),
        "interior_chair_4": box(2.3, 0.55, -1.2, 0.45, 0.55, 0.45),
        "interior_figure_a": box(-4.2, 1.2, -1.35, 0.35, 1.4, 0.25),
        "interior_figure_b": box(0.2, 1.15, -1.3, 0.35, 1.35, 0.25),
        "interior_figure_c": box(4.0, 1.2, -1.35, 0.35, 1.4, 0.25),
        "interior_glow_l": box(-5.0, 2.4, -1.7, 3.2, 1.4, 0.08),
        "interior_glow_r": box(5.0, 2.4, -1.7, 3.2, 1.4, 0.08),
        "interior_glow_mid": box(0.0, 2.6, -1.65, 2.8, 1.2, 0.08),
        "interior_glow_desk": box(-5.5, 1.6, -1.75, 2.0, 0.6, 0.06),
        # Shell densify — cladding ribs / fascia / soffit so overview body is not one slab.
        "fascia_front": box(0, 4.05, -2.45, 20.6, 0.22, 0.14),
        "fascia_back": box(0, 4.05, 2.45, 20.6, 0.22, 0.14),
        "soffit_front": box(0, 3.75, -2.55, 18.0, 0.1, 0.55),
        "canopy_soffit": box(0, 3.48, -3.1, 13.5, 0.06, 1.9),
        "canopy_gutter": box(0, 3.42, -4.1, 13.8, 0.08, 0.12),
        "service_wing_roof": box(7.5, 2.75, 2.8, 7.6, 0.18, 2.9),
        "service_wing_fascia": box(7.5, 2.55, 4.15, 7.4, 0.16, 0.12),
        "service_door_frame": box(9.5, 1.1, 4.22, 1.75, 2.15, 0.08),
        "baggage_door_frame": box(5.5, 1.0, 4.22, 2.55, 1.95, 0.08),
        "plinth_step": box(0, 0.32, -2.4, 16.0, 0.12, 0.55),
        "corner_trim_l": box(-10.25, 2.15, -2.3, 0.18, 3.8, 0.18),
        "corner_trim_r": box(10.25, 2.15, -2.3, 0.18, 3.8, 0.18),
    }
    # Pane bays between mullions at x = -7.5..7.5 every 1.5 m (skip entrance bay).
    pane_xs = [-6.75, -5.25, -3.75, -2.25, 2.25, 3.75, 5.25, 6.75, -0.75, 0.75]
    for i, x in enumerate(pane_xs, start=1):
        # Upper + lower panes split by midrail.
        meshes[f"glass_pane_{i}"] = box(x, 2.85, pane_z, 1.35, 0.9, 0.06)
        meshes[f"glass_pane_lo_{i}"] = box(x, 1.75, pane_z, 1.35, 1.05, 0.06)
    # Landside curtain panes between mullions.
    land_xs = [-5.0, -3.0, -1.0, 1.0, 3.0, 5.0]
    land_z = 2.36
    for i, x in enumerate(land_xs, start=1):
        meshes[f"glass_pane_land_{i}"] = box(x, 2.55, land_z, 1.7, 0.55, 0.05)
        meshes[f"glass_pane_land_lo_{i}"] = box(x, 1.75, land_z, 1.7, 0.7, 0.05)
    # Apron-face cladding ribs (skip glass bay).
    for i, x in enumerate([-9.5, -8.5, 8.5, 9.5], start=1):
        meshes[f"wall_rib_end_{i}"] = box(x, 2.15, -2.35, 0.1, 3.6, 0.08)
    for i, x in enumerate([-9.0, -7.0, -5.0, -3.0, 3.0, 5.0, 7.0, 9.0], start=1):
        meshes[f"wall_rib_land_{i}"] = box(x, 2.0, 2.4, 0.1, 3.2, 0.08)
    for i, x in enumerate([4.5, 5.5, 6.5, 7.5, 8.5, 9.5, 10.5], start=1):
        meshes[f"service_rib_{i}"] = box(x, 1.35, 4.12, 0.08, 2.4, 0.06)
    return meshes


def hangar_meshes() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    meshes: dict[str, tuple[np.ndarray, np.ndarray]] = {
        "hangar_shell": box(0, 2.5, 0, 14, 5, 9),
        "roof_ridge": box(0, 5.15, 0, 14.4, 0.35, 1.2),
        "roof_panel_l": box(-3.5, 4.85, 0, 7.2, 0.22, 9.2),
        "roof_panel_r": box(3.5, 4.85, 0, 7.2, 0.22, 9.2),
        "roof_rib_1": box(-5.0, 4.7, 0, 0.18, 0.45, 9.0),
        "roof_rib_2": box(-2.5, 4.7, 0, 0.18, 0.45, 9.0),
        "roof_rib_3": box(0.0, 4.7, 0, 0.18, 0.45, 9.0),
        "roof_rib_4": box(2.5, 4.7, 0, 0.18, 0.45, 9.0),
        "roof_rib_5": box(5.0, 4.7, 0, 0.18, 0.45, 9.0),
        "roof_rib_6": box(-6.5, 4.7, 0, 0.16, 0.4, 9.0),
        "roof_rib_7": box(6.5, 4.7, 0, 0.16, 0.4, 9.0),
        "side_window": box(-7.05, 2.0, 0.5, 0.04, 1.0, 1.6),
        "side_window_b": box(7.05, 2.0, 0.5, 0.04, 1.0, 1.6),
        "skylight_l": box(-3.0, 5.05, -1.5, 2.2, 0.04, 1.4),
        "skylight_r": box(3.0, 5.05, -1.5, 2.2, 0.04, 1.4),
        "skylight_mid": box(0.0, 5.05, 1.2, 2.0, 0.04, 1.2),
        "gutter_front": box(0, 4.55, 4.5, 14.0, 0.1, 0.12),
        "door_opening": box(0, 2.2, 4.6, 9.5, 4.2, 0.15),
        "door_panel_l": box(-2.4, 2.0, 4.7, 4.6, 3.9, 0.12),
        "door_panel_r": box(2.4, 2.0, 4.7, 4.6, 3.9, 0.12),
        "door_rib_l": box(-2.4, 2.0, 4.78, 0.12, 3.9, 0.08),
        "door_rib_r": box(2.4, 2.0, 4.78, 0.12, 3.9, 0.08),
        "door_bar_l1": box(-2.4, 1.2, 4.76, 4.4, 0.08, 0.06),
        "door_bar_l2": box(-2.4, 2.8, 4.76, 4.4, 0.08, 0.06),
        "door_bar_l3": box(-2.4, 2.0, 4.76, 4.4, 0.06, 0.05),
        "door_bar_r1": box(2.4, 1.2, 4.76, 4.4, 0.08, 0.06),
        "door_bar_r2": box(2.4, 2.8, 4.76, 4.4, 0.08, 0.06),
        "door_bar_r3": box(2.4, 2.0, 4.76, 4.4, 0.06, 0.05),
        "door_handle_l": box(-3.8, 2.0, 4.82, 0.35, 0.12, 0.1),
        "door_handle_r": box(3.8, 2.0, 4.82, 0.35, 0.12, 0.1),
        "door_track_l": box(-4.8, 4.3, 4.55, 0.25, 0.2, 0.5),
        "door_track_r": box(4.8, 4.3, 4.55, 0.25, 0.2, 0.5),
        "door_track_mid": box(0, 4.35, 4.55, 9.6, 0.12, 0.2),
        "buttress_l": box(-7.2, 1.5, 2.5, 1.0, 3.0, 2.5),
        "buttress_r": box(7.2, 1.5, 2.5, 1.0, 3.0, 2.5),
        "side_vent": box(-7.05, 3.2, -1.5, 0.15, 1.2, 2.0),
        "side_vent_b": box(7.05, 3.2, -1.5, 0.15, 1.2, 2.0),
        "personnel_door": box(-5.5, 1.1, 4.65, 1.1, 2.1, 0.1),
        "personnel_frame": box(-5.5, 1.1, 4.72, 1.25, 2.25, 0.06),
        "office_lean": box(5.8, 1.4, -3.5, 3.5, 2.6, 3.0),
        "office_window": box(5.8, 1.8, -5.02, 2.2, 1.2, 0.04),
        "office_door": box(4.4, 1.1, -5.05, 0.9, 2.0, 0.08),
        "office_mullion": box(5.8, 1.8, -5.08, 0.06, 1.2, 0.06),
        "office_mullion_2": box(5.2, 1.8, -5.08, 0.05, 1.2, 0.05),
        "office_mullion_3": box(6.4, 1.8, -5.08, 0.05, 1.2, 0.05),
        "office_sill": box(5.8, 1.18, -5.08, 2.25, 0.06, 0.1),
        "office_header": box(5.8, 2.42, -5.08, 2.25, 0.06, 0.1),
        "skylight_frame_l": box(-3.0, 5.08, -1.5, 2.3, 0.06, 1.5),
        "skylight_frame_r": box(3.0, 5.08, -1.5, 2.3, 0.06, 1.5),
        "skylight_frame_mid": box(0.0, 5.08, 1.2, 2.1, 0.06, 1.3),
        "side_mullion_l": box(-7.08, 2.0, 0.5, 0.06, 1.0, 0.06),
        "side_mullion_r": box(7.08, 2.0, 0.5, 0.06, 1.0, 0.06),
        "side_sill_l": box(-7.08, 1.48, 0.5, 0.08, 0.06, 1.65),
        "side_sill_r": box(7.08, 1.48, 0.5, 0.08, 0.06, 1.65),
        "crane_beam": box(0, 4.4, 0, 12.0, 0.2, 0.35),
        "crane_trolley": box(1.5, 4.25, 0, 0.8, 0.35, 0.6),
        "crane_hook": box(1.5, 3.85, 0, 0.15, 0.35, 0.15),
        "column_l": cylinder(-6.2, 2.4, -2.0, 0.2, 4.6, axis="y", segments=10),
        "column_r": cylinder(6.2, 2.4, -2.0, 0.2, 4.6, axis="y", segments=10),
        "column_ml": cylinder(-2.0, 2.4, -3.5, 0.16, 4.6, axis="y", segments=8),
        "column_mr": cylinder(2.0, 2.4, -3.5, 0.16, 4.6, axis="y", segments=8),
        "downpipe_l": cylinder(-6.8, 2.2, 4.4, 0.06, 4.2, axis="y", segments=8),
        "downpipe_r": cylinder(6.8, 2.2, 4.4, 0.06, 4.2, axis="y", segments=8),
        "flood_can_l": box(-6.5, 4.6, 4.3, 0.35, 0.2, 0.3),
        "flood_can_r": box(6.5, 4.6, 4.3, 0.35, 0.2, 0.3),
        "plinth": box(0, 0.12, 0, 14.4, 0.24, 9.4),
        "door_bar_l4": box(-2.4, 3.4, 4.76, 4.4, 0.06, 0.05),
        "door_bar_r4": box(2.4, 3.4, 4.76, 4.4, 0.06, 0.05),
        "door_warning_l": box(-3.6, 3.6, 4.8, 1.2, 0.18, 0.04),
        "door_warning_r": box(3.6, 3.6, 4.8, 1.2, 0.18, 0.04),
        "side_louvre_l": box(-7.05, 1.2, -1.5, 0.12, 0.8, 1.8),
        "side_louvre_r": box(7.05, 1.2, -1.5, 0.12, 0.8, 1.8),
        "rear_vent": box(0, 3.5, -4.5, 2.4, 0.9, 0.12),
        "rear_door": box(-2.5, 1.1, -4.5, 1.0, 2.0, 0.1),
        "gutter_back": box(0, 4.55, -4.5, 14.0, 0.1, 0.12),
        "gutter_end_l": box(-7.0, 4.4, 0, 0.12, 0.12, 9.0),
        "gutter_end_r": box(7.0, 4.4, 0, 0.12, 0.12, 9.0),
        "sign_board": box(0, 4.7, 4.4, 3.5, 0.4, 0.12),
        "workbench": box(5.0, 0.7, -1.5, 2.2, 0.8, 1.0),
        "tool_cabinet": box(-5.5, 0.8, -2.5, 1.2, 1.4, 0.7),
        "floor_drain": box(0, 0.05, 1.5, 0.8, 0.06, 0.8),
        "cladding_face_l": box(-7.05, 2.5, 0, 0.08, 4.6, 8.6),
        "cladding_face_r": box(7.05, 2.5, 0, 0.08, 4.6, 8.6),
        "girth_band_1": box(0, 1.4, 0, 14.1, 0.12, 9.05),
        "girth_band_2": box(0, 3.2, 0, 14.1, 0.12, 9.05),
        "girth_band_3": box(0, 4.4, 0, 14.1, 0.1, 9.05),
        "fascia_front": box(0, 4.75, 4.55, 14.2, 0.18, 0.14),
        "fascia_back": box(0, 4.75, -4.55, 14.2, 0.18, 0.14),
        "door_track_brace_l": box(-4.8, 3.8, 4.5, 0.12, 0.9, 0.12),
        "door_track_brace_r": box(4.8, 3.8, 4.5, 0.12, 0.9, 0.12),
        "office_roof": box(5.8, 2.8, -3.5, 3.6, 0.16, 3.1),
        "office_fascia": box(5.8, 2.65, -5.05, 3.4, 0.12, 0.1),
        "office_downpipe": cylinder(7.4, 1.5, -5.0, 0.05, 2.6, axis="y", segments=8),
        "sign_glyph": box(0, 4.7, 4.48, 2.4, 0.22, 0.04),
        "crane_rail_l": box(-5.5, 4.35, 0, 0.12, 0.12, 8.0),
        "crane_rail_r": box(5.5, 4.35, 0, 0.12, 0.12, 8.0),
        "floor_mark_bay": box(0, 0.04, 2.5, 8.0, 0.03, 0.25),
        "corner_trim_fl": box(-7.0, 2.5, 4.4, 0.14, 4.6, 0.14),
        "corner_trim_fr": box(7.0, 2.5, 4.4, 0.14, 4.6, 0.14),
    }
    # Vertical corrugation ribs on ±X faces.
    for i, z in enumerate((-3.5, -2.5, -1.5, -0.5, 0.5, 1.5, 2.5, 3.5, -4.0, 4.0), start=1):
        meshes[f"wall_rib_l_{i}"] = box(-7.12, 2.5, z, 0.1, 4.5, 0.18)
        meshes[f"wall_rib_r_{i}"] = box(7.12, 2.5, z, 0.1, 4.5, 0.18)
    # Side wall glass panes between ribs around the office-side windows.
    for i, z in enumerate((0.0, 0.35, 0.7, 1.05), start=1):
        meshes[f"glass_pane_side_l_{i}"] = box(-7.06, 2.15, z, 0.05, 0.7, 0.3)
        meshes[f"glass_pane_side_r_{i}"] = box(7.06, 2.15, z, 0.05, 0.7, 0.3)
        meshes[f"glass_pane_side_lo_l_{i}"] = box(-7.06, 1.75, z, 0.05, 0.45, 0.3)
        meshes[f"glass_pane_side_lo_r_{i}"] = box(7.06, 1.75, z, 0.05, 0.45, 0.3)
    # Office lean-to curtain panes.
    for i, x in enumerate((5.05, 5.5, 5.95, 6.4), start=1):
        meshes[f"glass_pane_office_{i}"] = box(x, 2.15, -5.04, 0.4, 0.5, 0.05)
        meshes[f"glass_pane_office_lo_{i}"] = box(x, 1.55, -5.04, 0.4, 0.55, 0.05)
    # Roof skylight panes.
    for i, (x, z) in enumerate(((-3.5, -1.5), (-2.5, -1.5), (2.5, -1.5), (3.5, -1.5), (-0.5, 1.2), (0.5, 1.2)), start=1):
        meshes[f"glass_pane_sky_{i}"] = box(x, 5.06, z, 0.85, 0.05, 1.0)
    return meshes


def ops_shed_meshes() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    # Corrugation ribs + split glass panes so mullions read metal, not glass tint.
    pane_z = 2.02
    mullion_z = 2.08
    meshes: dict[str, tuple[np.ndarray, np.ndarray]] = {
        "shed_body": box(0, 1.4, 0, 6, 2.8, 4),
        "porch": box(0, 1.0, 2.3, 3, 2.0, 1.2),
        "porch_roof": box(0, 2.15, 2.4, 3.4, 0.18, 1.5),
        "porch_beam": box(0, 1.95, 2.85, 3.1, 0.1, 0.12),
        "porch_post_l": cylinder(-1.2, 1.0, 2.85, 0.08, 1.9, axis="y", segments=8),
        "porch_post_r": cylinder(1.2, 1.0, 2.85, 0.08, 1.9, axis="y", segments=8),
        "door": box(0, 1.0, 2.85, 1.1, 1.9, 0.1),
        "door_frame": box(0, 1.0, 2.92, 1.25, 2.05, 0.06),
        "door_knob": cylinder(0.4, 1.05, 2.95, 0.04, 0.06, axis="z", segments=8),
        # Keep named slabs as deep fallbacks; panes carry the visible glass.
        "window_l": box(-1.8, 1.6, pane_z - 0.02, 1.0, 0.9, 0.04),
        "window_r": box(1.8, 1.6, pane_z - 0.02, 1.0, 0.9, 0.04),
        "window_side": box(-3.05, 1.6, 0, 0.04, 0.9, 1.4),
        "window_side_b": box(3.05, 1.6, 0, 0.04, 0.9, 1.4),
        "window_mullion_l": box(-1.8, 1.6, mullion_z, 0.06, 0.9, 0.08),
        "window_mullion_r": box(1.8, 1.6, mullion_z, 0.06, 0.9, 0.08),
        "window_mullion_l2": box(-2.05, 1.6, mullion_z, 0.05, 0.9, 0.06),
        "window_mullion_l3": box(-1.55, 1.6, mullion_z, 0.05, 0.9, 0.06),
        "window_mullion_r2": box(1.55, 1.6, mullion_z, 0.05, 0.9, 0.06),
        "window_mullion_r3": box(2.05, 1.6, mullion_z, 0.05, 0.9, 0.06),
        "window_sill_l": box(-1.8, 1.12, mullion_z, 1.05, 0.08, 0.12),
        "window_sill_r": box(1.8, 1.12, mullion_z, 1.05, 0.08, 0.12),
        "window_transom_l": box(-1.8, 2.0, mullion_z, 1.0, 0.05, 0.06),
        "window_transom_r": box(1.8, 2.0, mullion_z, 1.0, 0.05, 0.06),
        "roof_ridge": box(0, 2.95, 0, 6.2, 0.25, 1.0),
        "roof_panel": box(0, 2.85, 0, 6.0, 0.12, 3.8),
        "roof_gutter": box(0, 2.55, 2.0, 6.0, 0.08, 0.12),
        "roof_fascia": box(0, 2.7, 2.05, 6.1, 0.18, 0.08),
        "roof_downpipe_l": cylinder(-2.9, 1.4, 2.0, 0.05, 2.6, axis="y", segments=8),
        "roof_downpipe_r": cylinder(2.9, 1.4, 2.0, 0.05, 2.6, axis="y", segments=8),
        "antenna_mast": cylinder(1.8, 3.6, -0.5, 0.05, 1.2, axis="y", segments=8),
        "antenna_dish": cylinder(1.8, 4.15, -0.5, 0.22, 0.1, axis="y", segments=12),
        "antenna_boom": box(1.8, 4.0, -0.35, 0.06, 0.06, 0.45),
        "antenna_guy": box(1.8, 3.4, -0.2, 0.03, 0.9, 0.03),
        "ac_unit": box(-1.5, 3.15, -0.8, 1.2, 0.45, 0.9),
        "ac_unit_b": box(0.5, 3.1, -1.0, 0.9, 0.35, 0.7),
        "ac_grille": box(-1.5, 3.15, -1.25, 1.0, 0.35, 0.08),
        "radio_rack": box(-2.2, 1.4, -1.6, 0.8, 1.6, 0.5),
        "vent_pipe": cylinder(-2.4, 3.2, 0.8, 0.08, 0.7, axis="y", segments=8),
        "wall_vent": box(2.6, 1.2, -2.05, 0.55, 0.35, 0.08),
        "step": box(0, 0.15, 2.9, 1.4, 0.25, 0.5),
        "step_rail_l": box(-0.65, 0.55, 2.95, 0.06, 0.7, 0.06),
        "step_rail_r": box(0.65, 0.55, 2.95, 0.06, 0.7, 0.06),
        "signage": box(0, 2.55, 2.1, 1.6, 0.35, 0.08),
        "flood_can": box(-2.4, 2.7, 2.05, 0.25, 0.18, 0.2),
        "plinth": box(0, 0.1, 0, 6.2, 0.2, 4.2),
        "porch_light": box(0, 2.05, 2.95, 0.2, 0.12, 0.2),
        "window_header_l": box(-1.8, 2.1, mullion_z, 1.05, 0.06, 0.1),
        "window_header_r": box(1.8, 2.1, mullion_z, 1.05, 0.06, 0.1),
        "side_louvre": box(-3.05, 1.0, -1.2, 0.1, 0.7, 1.0),
        "side_louvre_b": box(3.05, 1.0, -1.2, 0.1, 0.7, 1.0),
        "roof_vent_a": cylinder(-1.0, 3.15, 0.5, 0.15, 0.35, axis="y", segments=8),
        "roof_vent_b": cylinder(1.2, 3.15, -0.8, 0.15, 0.35, axis="y", segments=8),
        "antenna_guy_b": box(1.5, 3.3, -0.2, 0.03, 0.8, 0.03),
        "mailbox": box(2.6, 0.85, 2.6, 0.35, 0.4, 0.25),
        "bench": box(-2.2, 0.35, 2.6, 1.2, 0.35, 0.4),
        "flood_can_b": box(2.4, 2.7, 2.05, 0.25, 0.18, 0.2),
        "door_kick": box(0, 0.25, 2.9, 1.0, 0.2, 0.08),
        "girth_band_1": box(0, 0.85, 0, 6.05, 0.08, 4.05),
        "girth_band_2": box(0, 1.95, 0, 6.05, 0.08, 4.05),
        "cladding_face_l": box(-3.02, 1.4, 0, 0.06, 2.6, 3.9),
        "cladding_face_r": box(3.02, 1.4, 0, 0.06, 2.6, 3.9),
        "interior_desk": box(-1.2, 1.05, -0.6, 1.6, 0.85, 0.7),
        "interior_glow": box(0, 1.55, 1.7, 3.2, 1.0, 0.06),
        "porch_fascia": box(0, 2.05, 3.1, 3.3, 0.12, 0.1),
        "porch_soffit": box(0, 2.0, 2.5, 3.2, 0.06, 1.3),
        "roof_eave_back": box(0, 2.7, -2.05, 6.1, 0.14, 0.1),
        "ac_pipe": box(-1.5, 2.85, -0.4, 0.08, 0.08, 0.7),
        "ac_pipe_b": box(0.5, 2.8, -0.6, 0.08, 0.08, 0.55),
        "shed_corner_l": box(-3.05, 1.4, 2.0, 0.12, 2.7, 0.12),
        "shed_corner_r": box(3.05, 1.4, 2.0, 0.12, 2.7, 0.12),
        "window_ledge_l": box(-1.8, 1.08, 2.15, 1.1, 0.06, 0.25),
        "window_ledge_r": box(1.8, 1.08, 2.15, 1.1, 0.06, 0.25),
        "radio_antenna_whip": cylinder(-2.2, 2.4, -1.6, 0.03, 0.9, axis="y", segments=6),
        "signage_glyph": box(0, 2.55, 2.15, 1.1, 0.18, 0.04),
    }
    # Front bay panes (split by mullions).
    for i, x in enumerate([-2.05, -1.55, 1.55, 2.05], start=1):
        meshes[f"glass_pane_{i}"] = box(x, 1.85, pane_z, 0.42, 0.42, 0.05)
        meshes[f"glass_pane_lo_{i}"] = box(x, 1.35, pane_z, 0.42, 0.42, 0.05)
    # Side curtain panes.
    for i, z in enumerate([-0.45, 0.0, 0.45], start=1):
        meshes[f"glass_pane_side_l_{i}"] = box(-3.04, 1.6, z, 0.05, 0.75, 0.4)
        meshes[f"glass_pane_side_r_{i}"] = box(3.04, 1.6, z, 0.05, 0.75, 0.4)
    for i, x in enumerate([-2.4, -1.6, -0.8, 0.0, 0.8, 1.6, 2.4, -2.8, 2.8], start=1):
        meshes[f"wall_rib_{i}"] = box(x, 1.4, 2.02, 0.08, 2.5, 0.06)
    for i, z in enumerate([-1.5, -0.75, 0.0, 0.75, 1.5, -1.1, 1.1], start=1):
        meshes[f"wall_rib_l_{i}"] = box(-3.04, 1.4, z, 0.06, 2.5, 0.08)
        meshes[f"wall_rib_r_{i}"] = box(3.04, 1.4, z, 0.06, 2.5, 0.08)
    return meshes


def fuel_truck_meshes() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    meshes: dict[str, tuple[np.ndarray, np.ndarray]] = {
        "cab": box(1.05, 0.95, 0, 1.5, 1.5, 1.55),
        "cab_window": box(1.55, 1.25, 0, 0.04, 0.7, 1.2),
        "cab_door": box(1.05, 0.95, 0.78, 1.2, 1.2, 0.08),
        "cab_door_r": box(1.05, 0.95, -0.78, 1.2, 1.2, 0.08),
        "cab_roof": box(1.05, 1.75, 0, 1.45, 0.12, 1.45),
        "cab_visor": box(1.55, 1.55, 0, 0.35, 0.08, 1.3),
        "tank": cylinder(-0.45, 0.9, 0, 0.65, 2.5, axis="x", segments=16),
        "tank_end_f": cylinder(0.78, 0.9, 0, 0.62, 0.22, axis="x", segments=16),
        "tank_end_r": cylinder(-1.68, 0.9, 0, 0.62, 0.22, axis="x", segments=16),
        "tank_band": box(-0.45, 0.9, 0, 2.55, 0.2, 1.4),
        "tank_band_2": box(-0.45, 0.9, 0, 0.15, 1.35, 1.35),
        "tank_band_3": box(0.4, 0.9, 0, 0.12, 1.3, 1.3),
        "tank_band_4": box(-1.2, 0.9, 0, 0.12, 1.3, 1.3),
        "tank_cap": cylinder(-0.45, 1.55, 0, 0.22, 0.2, axis="y", segments=10),
        "tank_cap_b": cylinder(0.35, 1.55, 0, 0.18, 0.16, axis="y", segments=10),
        "tank_ladder": box(-1.5, 1.1, 0.75, 0.08, 1.0, 0.35),
        "tank_walkway": box(-0.45, 1.5, 0.55, 2.2, 0.06, 0.35),
        "tank_rail_l": box(-0.45, 1.55, 0.72, 2.0, 0.05, 0.05),
        "tank_rail_r": box(-0.45, 1.55, -0.72, 2.0, 0.05, 0.05),
        "chassis": box(0.1, 0.35, 0, 3.6, 0.25, 1.1),
        "fender_fl": box(1.35, 0.45, 0.7, 0.7, 0.2, 0.2),
        "fender_fr": box(1.35, 0.45, -0.7, 0.7, 0.2, 0.2),
        "fender_rl": box(-1.15, 0.45, 0.7, 0.7, 0.2, 0.2),
        "fender_rr": box(-1.15, 0.45, -0.7, 0.7, 0.2, 0.2),
        "wheel_fl": cylinder(1.35, 0.28, 0.58, 0.28, 0.22, axis="z", segments=14),
        "wheel_fr": cylinder(1.35, 0.28, -0.58, 0.28, 0.22, axis="z", segments=14),
        "wheel_rl": cylinder(-1.15, 0.28, 0.58, 0.28, 0.22, axis="z", segments=14),
        "wheel_rr": cylinder(-1.15, 0.28, -0.58, 0.28, 0.22, axis="z", segments=14),
        "hub_fl": cylinder(1.35, 0.28, 0.58, 0.12, 0.1, axis="z", segments=8),
        "hub_fr": cylinder(1.35, 0.28, -0.58, 0.12, 0.1, axis="z", segments=8),
        "hose_mount": box(-1.55, 0.7, 0.75, 0.4, 0.4, 0.4),
        "hose_reel": cylinder(-1.55, 0.85, 0.35, 0.28, 0.35, axis="z", segments=12),
        "hose_nozzle": box(-1.85, 0.55, 0.75, 0.25, 0.2, 0.2),
        "hose": cylinder(-1.7, 0.55, 0.55, 0.06, 0.7, axis="z", segments=8),
        "hose_guard": box(-1.7, 0.7, 0.75, 0.5, 0.5, 0.08),
        "hose_tray": box(-1.55, 0.55, 0.55, 0.55, 0.12, 0.55),
        "pump_cabinet": box(-1.55, 0.85, -0.55, 0.55, 0.7, 0.55),
        "pump_gauge": box(-1.55, 1.1, -0.82, 0.25, 0.2, 0.06),
        "pump_valve": cylinder(-1.55, 0.55, -0.55, 0.08, 0.25, axis="y", segments=8),
        "hose_coil_a": cylinder(-1.75, 0.7, 0.2, 0.12, 0.2, axis="z", segments=10),
        "hose_coil_b": cylinder(-1.75, 0.7, 0.0, 0.12, 0.2, axis="z", segments=10),
        "mirror_l": box(1.7, 1.35, 0.85, 0.12, 0.25, 0.18),
        "mirror_r": box(1.7, 1.35, -0.85, 0.12, 0.25, 0.18),
        "beacon": box(1.05, 1.85, 0, 0.25, 0.2, 0.25),
        "beacon_guard": box(1.05, 1.95, 0, 0.35, 0.06, 0.35),
        "bumper": box(1.85, 0.4, 0, 0.2, 0.35, 1.4),
        "bumper_rear": box(-1.85, 0.4, 0, 0.2, 0.35, 1.3),
        "step": box(1.55, 0.45, 0.85, 0.35, 0.15, 0.35),
        "step_r": box(1.55, 0.45, -0.85, 0.35, 0.15, 0.35),
        "light_bar": box(1.8, 0.55, 0, 0.12, 0.18, 1.2),
        "grill": box(1.85, 0.85, 0, 0.08, 0.45, 1.0),
        "headlight_l": box(1.95, 0.65, 0.4, 0.1, 0.12, 0.14),
        "headlight_r": box(1.95, 0.65, -0.4, 0.1, 0.12, 0.14),
        "taillight_l": box(-1.95, 0.55, 0.45, 0.08, 0.1, 0.1),
        "taillight_r": box(-1.95, 0.55, -0.45, 0.08, 0.1, 0.1),
        "exhaust": cylinder(-1.6, 1.35, -0.55, 0.06, 0.9, axis="y", segments=8),
        "mudflap_l": box(-1.15, 0.25, 0.75, 0.35, 0.35, 0.04),
        "mudflap_r": box(-1.15, 0.25, -0.75, 0.35, 0.35, 0.04),
        "number_plate": box(1.95, 0.55, 0, 0.04, 0.18, 0.45),
        "fuel_hazard": box(-0.45, 1.2, 0.72, 0.55, 0.35, 0.04),
        "window_mullion": box(1.56, 1.25, 0, 0.04, 0.7, 0.05),
        "window_mullion_2": box(1.56, 1.25, 0.4, 0.04, 0.7, 0.05),
        "window_mullion_3": box(1.56, 1.25, -0.4, 0.04, 0.7, 0.05),
        "window_sill": box(1.56, 0.88, 0, 0.05, 0.05, 1.15),
        "window_header": box(1.56, 1.62, 0, 0.05, 0.05, 1.15),
        "door_glass": box(1.05, 1.2, 0.8, 0.7, 0.55, 0.04),
        "door_glass_r": box(1.05, 1.2, -0.8, 0.7, 0.55, 0.04),
        "hub_rl": cylinder(-1.15, 0.28, 0.58, 0.12, 0.1, axis="z", segments=8),
        "hub_rr": cylinder(-1.15, 0.28, -0.58, 0.12, 0.1, axis="z", segments=8),
        "door_handle_l": box(1.35, 0.95, 0.85, 0.08, 0.18, 0.06),
        "door_handle_r": box(1.35, 0.95, -0.85, 0.08, 0.18, 0.06),
        "tank_valve_top": cylinder(-0.1, 1.65, 0, 0.08, 0.18, axis="y", segments=8),
        "chassis_rail_l": box(0.1, 0.45, 0.5, 3.4, 0.1, 0.08),
        "chassis_rail_r": box(0.1, 0.45, -0.5, 3.4, 0.1, 0.08),
        "cab_stripe": box(1.05, 0.7, 0.8, 1.35, 0.1, 0.04),
        "tank_stripe": box(-0.45, 0.7, 0.72, 2.3, 0.1, 0.04),
        "pump_hose_out": box(-1.85, 0.55, -0.55, 0.35, 0.1, 0.1),
        "spare_wheel": cylinder(-1.7, 0.85, 0.0, 0.22, 0.12, axis="z", segments=12),
        "wiper": box(1.7, 1.5, 0, 0.06, 0.05, 0.7),
    }
    # Cab windshield panes between mullions.
    for i, z in enumerate([-0.45, -0.15, 0.15, 0.45], start=1):
        meshes[f"glass_pane_{i}"] = box(1.57, 1.4, z, 0.04, 0.35, 0.28)
        meshes[f"glass_pane_lo_{i}"] = box(1.57, 1.1, z, 0.04, 0.28, 0.28)
    return meshes


def baggage_tug_meshes() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    meshes: dict[str, tuple[np.ndarray, np.ndarray]] = {
        "tug": box(3.6, 0.55, 0, 1.7, 0.95, 1.15),
        "tug_cab": box(4.1, 0.95, 0, 0.9, 0.85, 1.05),
        "tug_window": box(4.5, 1.15, 0, 0.04, 0.45, 0.85),
        "tug_bumper": box(4.55, 0.4, 0, 0.2, 0.35, 1.0),
        "tug_seat": box(3.85, 0.85, 0, 0.45, 0.35, 0.7),
        "tug_rollbar": box(3.7, 1.35, 0, 0.08, 0.7, 0.9),
        "tug_floor": box(3.9, 0.35, 0, 0.9, 0.08, 1.0),
        "tug_steering": box(4.25, 1.05, 0.15, 0.12, 0.35, 0.12),
        "cart_1": box(1.8, 0.5, 0, 1.5, 0.75, 1.05),
        "cart_2": box(0.2, 0.5, 0, 1.5, 0.75, 1.05),
        "cart_3": box(-1.4, 0.5, 0, 1.5, 0.75, 1.05),
        "cart_bed_1": box(1.8, 0.22, 0, 1.45, 0.08, 1.0),
        "cart_bed_2": box(0.2, 0.22, 0, 1.45, 0.08, 1.0),
        "cart_bed_3": box(-1.4, 0.22, 0, 1.45, 0.08, 1.0),
        "cart_rail_1": box(1.8, 0.85, 0.48, 1.4, 0.08, 0.08),
        "cart_rail_2": box(0.2, 0.85, 0.48, 1.4, 0.08, 0.08),
        "cart_rail_3": box(-1.4, 0.85, 0.48, 1.4, 0.08, 0.08),
        "cart_rail_1b": box(1.8, 0.85, -0.48, 1.4, 0.08, 0.08),
        "cart_rail_2b": box(0.2, 0.85, -0.48, 1.4, 0.08, 0.08),
        "cart_rail_3b": box(-1.4, 0.85, -0.48, 1.4, 0.08, 0.08),
        "cart_gate_1": box(1.8, 0.7, -0.55, 1.3, 0.45, 0.06),
        "cart_gate_2": box(0.2, 0.7, -0.55, 1.3, 0.45, 0.06),
        "cart_gate_3": box(-1.4, 0.7, -0.55, 1.3, 0.45, 0.06),
        "cart_canopy_1": box(1.8, 1.25, 0, 1.35, 0.06, 0.95),
        "cart_canopy_2": box(0.2, 1.25, 0, 1.35, 0.06, 0.95),
        "cart_canopy_3": box(-1.4, 1.25, 0, 1.35, 0.06, 0.95),
        "cart_post_1l": box(1.2, 0.95, 0.42, 0.05, 0.55, 0.05),
        "cart_post_1r": box(1.2, 0.95, -0.42, 0.05, 0.55, 0.05),
        "cart_post_2l": box(-0.4, 0.95, 0.42, 0.05, 0.55, 0.05),
        "cart_post_2r": box(-0.4, 0.95, -0.42, 0.05, 0.55, 0.05),
        "cart_post_3l": box(-2.0, 0.95, 0.42, 0.05, 0.55, 0.05),
        "cart_post_3r": box(-2.0, 0.95, -0.42, 0.05, 0.55, 0.05),
        "cargo_1": box(1.8, 0.95, 0, 1.2, 0.45, 0.85),
        "cargo_2": box(0.2, 0.95, 0, 1.2, 0.45, 0.85),
        "cargo_3": box(-1.4, 0.95, 0, 1.2, 0.45, 0.85),
        "cargo_bag_1a": box(1.5, 1.05, 0.2, 0.45, 0.28, 0.35),
        "cargo_bag_1b": box(2.1, 1.05, -0.15, 0.4, 0.25, 0.3),
        "cargo_bag_2a": box(-0.1, 1.05, 0.15, 0.45, 0.28, 0.35),
        "cargo_bag_2b": box(0.5, 1.05, -0.2, 0.4, 0.25, 0.3),
        "cargo_bag_3a": box(-1.7, 1.05, 0.1, 0.45, 0.28, 0.35),
        "cargo_bag_3b": box(-1.1, 1.05, -0.15, 0.4, 0.25, 0.3),
        "cargo_tag_1": box(1.8, 1.15, 0.4, 0.35, 0.12, 0.04),
        "cargo_tag_2": box(0.2, 1.15, 0.4, 0.35, 0.12, 0.04),
        "hitch_1": box(2.7, 0.35, 0, 0.45, 0.2, 0.2),
        "hitch_2": box(1.0, 0.35, 0, 0.45, 0.2, 0.2),
        "hitch_3": box(-0.6, 0.35, 0, 0.45, 0.2, 0.2),
        "wheel_fl": cylinder(4.0, 0.22, 0.5, 0.2, 0.18, axis="z", segments=12),
        "wheel_fr": cylinder(4.0, 0.22, -0.5, 0.2, 0.18, axis="z", segments=12),
        "wheel_rl": cylinder(3.2, 0.22, 0.5, 0.2, 0.18, axis="z", segments=12),
        "wheel_rr": cylinder(3.2, 0.22, -0.5, 0.2, 0.18, axis="z", segments=12),
        "cart_wheel_1l": cylinder(1.8, 0.18, 0.48, 0.16, 0.14, axis="z", segments=10),
        "cart_wheel_1r": cylinder(1.8, 0.18, -0.48, 0.16, 0.14, axis="z", segments=10),
        "cart_wheel_2l": cylinder(0.2, 0.18, 0.48, 0.16, 0.14, axis="z", segments=10),
        "cart_wheel_2r": cylinder(0.2, 0.18, -0.48, 0.16, 0.14, axis="z", segments=10),
        "cart_wheel_3l": cylinder(-1.4, 0.18, 0.48, 0.16, 0.14, axis="z", segments=10),
        "cart_wheel_3r": cylinder(-1.4, 0.18, -0.48, 0.16, 0.14, axis="z", segments=10),
        "beacon": box(4.1, 1.5, 0, 0.2, 0.15, 0.2),
        "headlight_l": box(4.55, 0.65, 0.35, 0.1, 0.12, 0.14),
        "headlight_r": box(4.55, 0.65, -0.35, 0.1, 0.12, 0.14),
        "taillight_l": box(3.0, 0.55, 0.55, 0.08, 0.1, 0.1),
        "taillight_r": box(3.0, 0.55, -0.55, 0.08, 0.1, 0.1),
        "counterweight": box(3.15, 0.55, 0, 0.45, 0.55, 0.9),
        "number_plate": box(4.65, 0.5, 0, 0.04, 0.14, 0.35),
        "glass_pane_1": box(4.51, 1.25, -0.25, 0.04, 0.28, 0.35),
        "glass_pane_2": box(4.51, 1.25, 0.25, 0.04, 0.28, 0.35),
        "glass_pane_lo_1": box(4.51, 1.0, -0.25, 0.04, 0.2, 0.35),
        "glass_pane_lo_2": box(4.51, 1.0, 0.25, 0.04, 0.2, 0.35),
        "window_mullion": box(4.51, 1.15, 0, 0.04, 0.45, 0.04),
        "tug_hub_fl": cylinder(4.0, 0.22, 0.5, 0.1, 0.1, axis="z", segments=8),
        "tug_hub_fr": cylinder(4.0, 0.22, -0.5, 0.1, 0.1, axis="z", segments=8),
        "tug_hub_rl": cylinder(3.2, 0.22, 0.5, 0.1, 0.1, axis="z", segments=8),
        "tug_hub_rr": cylinder(3.2, 0.22, -0.5, 0.1, 0.1, axis="z", segments=8),
        "cart_hub_1l": cylinder(1.8, 0.18, 0.48, 0.08, 0.08, axis="z", segments=8),
        "cart_hub_1r": cylinder(1.8, 0.18, -0.48, 0.08, 0.08, axis="z", segments=8),
        "cart_hub_2l": cylinder(0.2, 0.18, 0.48, 0.08, 0.08, axis="z", segments=8),
        "cart_hub_2r": cylinder(0.2, 0.18, -0.48, 0.08, 0.08, axis="z", segments=8),
        "cart_hub_3l": cylinder(-1.4, 0.18, 0.48, 0.08, 0.08, axis="z", segments=8),
        "cart_hub_3r": cylinder(-1.4, 0.18, -0.48, 0.08, 0.08, axis="z", segments=8),
        "tug_mirror_l": box(4.4, 1.2, 0.55, 0.1, 0.18, 0.08),
        "tug_mirror_r": box(4.4, 1.2, -0.55, 0.1, 0.18, 0.08),
        "tug_stripe": box(3.6, 0.55, 0.58, 1.5, 0.12, 0.04),
        "cargo_bag_1c": box(1.8, 1.15, 0.0, 0.35, 0.22, 0.28),
        "cargo_bag_2c": box(0.2, 1.15, 0.0, 0.35, 0.22, 0.28),
        "cargo_bag_3c": box(-1.4, 1.15, 0.0, 0.35, 0.22, 0.28),
        "hitch_pin_1": cylinder(2.7, 0.45, 0, 0.04, 0.18, axis="y", segments=6),
        "hitch_pin_2": cylinder(1.0, 0.45, 0, 0.04, 0.18, axis="y", segments=6),
        "hitch_pin_3": cylinder(-0.6, 0.45, 0, 0.04, 0.18, axis="y", segments=6),
        "tug_exhaust": box(3.05, 0.7, -0.45, 0.25, 0.1, 0.1),
    }
    return meshes


def passenger_bus_meshes() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    meshes: dict[str, tuple[np.ndarray, np.ndarray]] = {
        "bus_body": box(0, 0.95, 0, 4.4, 1.65, 1.65),
        "cabin_roof": box(0, 1.9, 0, 4.2, 0.28, 1.55),
        # Keep a thin deep slab for older binders; side panes carry visible glass.
        "windows": box(0, 1.35, 0, 3.8, 0.55, 1.62),
        "window_mullion": box(0, 1.35, 0.84, 0.08, 0.55, 0.06),
        "window_mullion_2": box(-1.2, 1.35, 0.84, 0.08, 0.55, 0.06),
        "window_mullion_3": box(1.2, 1.35, 0.84, 0.08, 0.55, 0.06),
        "window_mullion_4": box(-2.0, 1.35, 0.84, 0.08, 0.55, 0.06),
        "window_mullion_5": box(2.0, 1.35, 0.84, 0.08, 0.55, 0.06),
        "window_mullion_6": box(-0.6, 1.35, 0.84, 0.06, 0.55, 0.06),
        "window_mullion_7": box(0.6, 1.35, 0.84, 0.06, 0.55, 0.06),
        "window_mullion_8": box(-1.2, 1.35, -0.84, 0.08, 0.55, 0.06),
        "window_mullion_9": box(1.2, 1.35, -0.84, 0.08, 0.55, 0.06),
        "window_mullion_10": box(0, 1.35, -0.84, 0.08, 0.55, 0.06),
        "window_sill": box(0, 1.05, 0.86, 3.8, 0.06, 0.06),
        "window_sill_b": box(0, 1.05, -0.86, 3.8, 0.06, 0.06),
        "window_header": box(0, 1.65, 0.86, 3.8, 0.06, 0.06),
        "window_header_b": box(0, 1.65, -0.86, 3.8, 0.06, 0.06),
        "door": box(0.25, 0.95, 0.85, 1.05, 1.35, 0.1),
        "door_frame": box(0.25, 0.95, 0.92, 1.15, 1.45, 0.06),
        "door_glass": box(0.25, 1.25, 0.9, 0.7, 0.55, 0.05),
        "door_handle": box(0.55, 1.0, 0.95, 0.08, 0.2, 0.06),
        "bumper_front": box(2.25, 0.45, 0, 0.25, 0.45, 1.5),
        "bumper_rear": box(-2.25, 0.45, 0, 0.25, 0.45, 1.5),
        "wheel_fl": cylinder(1.45, 0.3, 0.72, 0.28, 0.24, axis="z", segments=14),
        "wheel_fr": cylinder(1.45, 0.3, -0.72, 0.28, 0.24, axis="z", segments=14),
        "wheel_rl": cylinder(-1.45, 0.3, 0.72, 0.28, 0.24, axis="z", segments=14),
        "wheel_rr": cylinder(-1.45, 0.3, -0.72, 0.28, 0.24, axis="z", segments=14),
        "wheel_arch_fl": box(1.45, 0.55, 0.78, 0.7, 0.35, 0.12),
        "wheel_arch_fr": box(1.45, 0.55, -0.78, 0.7, 0.35, 0.12),
        "wheel_arch_rl": box(-1.45, 0.55, 0.78, 0.7, 0.35, 0.12),
        "wheel_arch_rr": box(-1.45, 0.55, -0.78, 0.7, 0.35, 0.12),
        "beacon": box(0, 2.15, 0, 0.28, 0.2, 0.28),
        "mirror_l": box(2.1, 1.55, 0.9, 0.12, 0.3, 0.2),
        "mirror_r": box(2.1, 1.55, -0.9, 0.12, 0.3, 0.2),
        "step": box(0.25, 0.35, 0.95, 0.9, 0.15, 0.35),
        "headlight_l": box(2.3, 0.7, 0.55, 0.12, 0.18, 0.2),
        "headlight_r": box(2.3, 0.7, -0.55, 0.12, 0.18, 0.2),
        "taillight_l": box(-2.3, 0.7, 0.55, 0.1, 0.16, 0.16),
        "taillight_r": box(-2.3, 0.7, -0.55, 0.1, 0.16, 0.16),
        "stripe": box(0, 0.75, 0.82, 4.0, 0.12, 0.06),
        "stripe_b": box(0, 0.75, -0.82, 4.0, 0.12, 0.06),
        "stripe_upper": box(0, 1.75, 0.84, 4.0, 0.08, 0.04),
        "wiper": box(2.15, 1.55, 0, 0.08, 0.06, 0.9),
        "wiper_b": box(2.15, 1.45, 0.2, 0.06, 0.05, 0.7),
        "roof_rack": box(0, 2.15, 0, 3.2, 0.08, 0.9),
        "roof_vent": box(-0.8, 2.12, 0, 0.45, 0.12, 0.45),
        "grill": box(2.35, 0.85, 0, 0.08, 0.35, 0.9),
        "number_plate": box(2.4, 0.5, 0, 0.04, 0.16, 0.4),
        "mudflap_l": box(-1.45, 0.25, 0.85, 0.4, 0.35, 0.04),
        "mudflap_r": box(-1.45, 0.25, -0.85, 0.4, 0.35, 0.04),
        "destination_board": box(2.15, 1.85, 0, 0.08, 0.22, 0.9),
        "destination_board_hood": box(2.12, 1.98, 0, 0.12, 0.06, 0.95),
        "destination_digit": box(2.2, 1.85, 0, 0.04, 0.14, 0.55),
        "rear_window": box(-2.22, 1.35, 0, 0.05, 0.55, 1.2),
        "seat_row_1": box(-0.8, 0.85, 0.35, 1.8, 0.45, 0.45),
        "seat_row_2": box(-0.8, 0.85, -0.35, 1.8, 0.45, 0.45),
        "seat_row_3": box(0.6, 0.85, 0.35, 1.2, 0.45, 0.45),
        "seat_row_4": box(0.6, 0.85, -0.35, 1.2, 0.45, 0.45),
        "seat_back_1": box(-0.8, 1.15, 0.5, 1.8, 0.35, 0.08),
        "seat_back_2": box(-0.8, 1.15, -0.5, 1.8, 0.35, 0.08),
        "windshield": box(2.18, 1.35, 0, 0.05, 0.7, 1.35),
        "body_panel_l": box(0, 0.55, 0.84, 4.0, 0.35, 0.05),
        "body_panel_r": box(0, 0.55, -0.84, 4.0, 0.35, 0.05),
        "wheel_hub_fl": cylinder(1.45, 0.3, 0.72, 0.14, 0.14, axis="z", segments=10),
        "wheel_hub_fr": cylinder(1.45, 0.3, -0.72, 0.14, 0.14, axis="z", segments=10),
        "wheel_hub_rl": cylinder(-1.45, 0.3, 0.72, 0.14, 0.14, axis="z", segments=10),
        "wheel_hub_rr": cylinder(-1.45, 0.3, -0.72, 0.14, 0.14, axis="z", segments=10),
        "door_hinge_t": box(-0.2, 1.45, 0.93, 0.08, 0.12, 0.08),
        "door_hinge_b": box(-0.2, 0.55, 0.93, 0.08, 0.12, 0.08),
        "exhaust_pipe": box(-2.15, 0.35, -0.55, 0.35, 0.1, 0.1),
        "fuel_filler": box(-2.15, 0.95, 0.7, 0.12, 0.12, 0.12),
        "skirt_l": box(0, 0.28, 0.78, 3.6, 0.12, 0.06),
        "skirt_r": box(0, 0.28, -0.78, 3.6, 0.12, 0.06),
    }
    # Side curtain panes between mullions (skip door bay around x=0.25).
    pane_xs = [-1.8, -1.5, -0.9, -0.3, 0.9, 1.5, 1.8]
    for i, x in enumerate(pane_xs, start=1):
        meshes[f"glass_pane_{i}"] = box(x, 1.35, 0.855, 0.5, 0.48, 0.04)
        meshes[f"glass_pane_lo_{i}"] = box(x, 1.35, -0.855, 0.5, 0.48, 0.04)
    for i, z in enumerate([-0.45, 0.0, 0.45], start=1):
        meshes[f"glass_pane_front_{i}"] = box(2.2, 1.35, z, 0.04, 0.55, 0.4)
    return meshes


def service_equipment_meshes() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    """PRP-001 — keep extract names stairs/chocks/gpu used by presentation helpers."""
    meshes: dict[str, tuple[np.ndarray, np.ndarray]] = {
        "stairs": box(0, 0.9, 0, 1.2, 1.8, 2.4),
        "stairs_base": box(0, 0.12, 0, 1.3, 0.2, 2.5),
        "stairs_rail_l": box(-0.55, 1.0, 0, 0.08, 1.6, 2.3),
        "stairs_rail_r": box(0.55, 1.0, 0, 0.08, 1.6, 2.3),
        "stairs_rail_mid": box(0, 1.55, -0.4, 1.1, 0.06, 0.06),
        "stairs_tread_1": box(0, 0.35, 0.8, 1.1, 0.08, 0.4),
        "stairs_tread_2": box(0, 0.7, 0.3, 1.1, 0.08, 0.4),
        "stairs_tread_3": box(0, 1.05, -0.2, 1.1, 0.08, 0.4),
        "stairs_tread_4": box(0, 1.4, -0.7, 1.1, 0.08, 0.4),
        "stairs_tread_5": box(0, 1.55, -0.95, 1.1, 0.08, 0.35),
        "stairs_platform": box(0, 1.7, -1.1, 1.2, 0.12, 0.7),
        "stairs_wheel_l": cylinder(-0.5, 0.2, 1.0, 0.12, 0.15, axis="z", segments=10),
        "stairs_wheel_r": cylinder(0.5, 0.2, 1.0, 0.12, 0.15, axis="z", segments=10),
        "stairs_wheel_rl": cylinder(-0.5, 0.2, -0.9, 0.12, 0.15, axis="z", segments=10),
        "stairs_wheel_rr": cylinder(0.5, 0.2, -0.9, 0.12, 0.15, axis="z", segments=10),
        "stairs_handle": box(0, 1.85, -1.1, 0.7, 0.08, 0.08),
        "stairs_brace": box(0, 0.9, 0.2, 1.15, 0.06, 0.06),
        "stairs_post_1l": box(-0.55, 0.55, 0.8, 0.06, 0.7, 0.06),
        "stairs_post_1r": box(0.55, 0.55, 0.8, 0.06, 0.7, 0.06),
        "stairs_post_2l": box(-0.55, 0.9, 0.2, 0.06, 0.7, 0.06),
        "stairs_post_2r": box(0.55, 0.9, 0.2, 0.06, 0.7, 0.06),
        "stairs_post_3l": box(-0.55, 1.25, -0.4, 0.06, 0.7, 0.06),
        "stairs_post_3r": box(0.55, 1.25, -0.4, 0.06, 0.7, 0.06),
        "stairs_nosing_1": box(0, 0.4, 0.95, 1.05, 0.04, 0.08),
        "stairs_nosing_2": box(0, 0.75, 0.45, 1.05, 0.04, 0.08),
        "stairs_nosing_3": box(0, 1.1, -0.05, 1.05, 0.04, 0.08),
        "chocks": box(0, 0.15, 0, 0.6, 0.3, 0.35),
        "chock_a": box(-0.4, 0.12, 0, 0.35, 0.24, 0.2),
        "chock_b": box(0.4, 0.12, 0, 0.35, 0.24, 0.2),
        "chock_rope": box(0, 0.08, 0, 0.7, 0.04, 0.04),
        "gpu": box(0, 0.55, 0, 1.4, 1.1, 0.9),
        "gpu_body": box(0, 0.55, 0, 1.4, 0.9, 0.85),
        "gpu_cab": box(0.4, 0.85, 0, 0.55, 0.55, 0.7),
        "gpu_vent": box(-0.4, 0.9, 0, 0.45, 0.25, 0.7),
        "gpu_panel": box(-0.55, 0.7, 0.42, 0.5, 0.45, 0.06),
        "gpu_cable": box(0.85, 0.45, 0, 0.35, 0.2, 0.2),
        "gpu_cable_reel": cylinder(0.95, 0.55, 0.25, 0.14, 0.2, axis="z", segments=10),
        "gpu_hitch": box(0.85, 0.35, 0, 0.25, 0.15, 0.15),
        "gpu_beacon": box(0.15, 1.2, 0, 0.18, 0.14, 0.18),
        "gpu_grille": box(-0.55, 0.55, 0.45, 0.7, 0.35, 0.04),
        "gpu_grille_2": box(-0.55, 0.55, -0.45, 0.7, 0.35, 0.04),
        "gpu_panel_b": box(-0.55, 0.7, -0.42, 0.5, 0.45, 0.06),
        "gpu_slot_1": box(-0.7, 0.75, 0.46, 0.2, 0.08, 0.04),
        "gpu_slot_2": box(-0.7, 0.6, 0.46, 0.2, 0.08, 0.04),
        "gpu_wheel_fl": cylinder(0.45, 0.15, 0.35, 0.1, 0.12, axis="z", segments=10),
        "gpu_wheel_fr": cylinder(0.45, 0.15, -0.35, 0.1, 0.12, axis="z", segments=10),
        "gpu_wheel_rl": cylinder(-0.45, 0.15, 0.35, 0.1, 0.12, axis="z", segments=10),
        "gpu_wheel_rr": cylinder(-0.45, 0.15, -0.35, 0.1, 0.12, axis="z", segments=10),
        "cone": cylinder(1.2, 0.35, 0, 0.16, 0.7, axis="y", segments=10),
        "cone_stripe": cylinder(1.2, 0.35, 0, 0.17, 0.12, axis="y", segments=10),
        "towbar": box(0, 0.2, -1.5, 2.5, 0.12, 0.12),
        "towbar_head": box(1.2, 0.25, -1.5, 0.25, 0.2, 0.2),
        "towbar_wheel": cylinder(-1.0, 0.12, -1.5, 0.08, 0.1, axis="z", segments=8),
        "bin": box(-1.2, 0.45, 0.8, 0.7, 0.9, 0.7),
        "bin_lid": box(-1.2, 0.95, 0.8, 0.72, 0.08, 0.72),
        "bin_handle": box(-1.2, 1.02, 0.8, 0.35, 0.06, 0.06),
        "stairs_tread_6": box(0, 1.65, -1.25, 1.1, 0.08, 0.3),
        "stairs_rail_cross": box(0, 1.2, 0.5, 1.1, 0.05, 0.05),
        "gpu_exhaust": cylinder(-0.55, 1.15, 0, 0.08, 0.35, axis="y", segments=8),
        "gpu_light": box(0.4, 1.15, 0, 0.12, 0.1, 0.12),
        "towbar_handle": box(-1.2, 0.35, -1.5, 0.35, 0.08, 0.08),
        "cone_base": box(1.2, 0.04, 0, 0.4, 0.08, 0.4),
        "belt_loader_chassis": box(2.8, 0.35, 0, 1.6, 0.45, 0.85),
        "belt_loader_cab": box(3.35, 0.75, 0, 0.55, 0.55, 0.7),
        "belt_loader_boom": box(2.2, 0.95, 0, 2.4, 0.18, 0.35),
        "belt_loader_belt": box(2.2, 1.05, 0, 2.2, 0.06, 0.28),
        "belt_loader_rail_l": box(2.2, 1.15, 0.18, 2.2, 0.08, 0.05),
        "belt_loader_rail_r": box(2.2, 1.15, -0.18, 2.2, 0.08, 0.05),
        "belt_loader_wheel_fl": cylinder(3.3, 0.15, 0.35, 0.12, 0.14, axis="z", segments=10),
        "belt_loader_wheel_fr": cylinder(3.3, 0.15, -0.35, 0.12, 0.14, axis="z", segments=10),
        "belt_loader_wheel_rl": cylinder(2.3, 0.15, 0.35, 0.12, 0.14, axis="z", segments=10),
        "belt_loader_wheel_rr": cylinder(2.3, 0.15, -0.35, 0.12, 0.14, axis="z", segments=10),
        "belt_loader_hitch": box(3.7, 0.35, 0, 0.25, 0.15, 0.2),
        "belt_loader_light": box(3.4, 1.05, 0, 0.12, 0.1, 0.12),
        "belt_loader_hinge": box(3.1, 0.85, 0, 0.25, 0.25, 0.4),
        "belt_loader_support": box(2.5, 0.65, 0, 0.12, 0.45, 0.12),
        "belt_loader_roller_1": cylinder(1.4, 1.05, 0, 0.06, 0.3, axis="z", segments=8),
        "belt_loader_roller_2": cylinder(2.0, 1.05, 0, 0.06, 0.3, axis="z", segments=8),
        "belt_loader_roller_3": cylinder(2.6, 1.05, 0, 0.06, 0.3, axis="z", segments=8),
        "belt_loader_bumper": box(3.55, 0.35, 0, 0.12, 0.25, 0.75),
        "stairs_hub_fl": cylinder(-0.5, 0.2, 1.0, 0.06, 0.08, axis="z", segments=8),
        "stairs_hub_fr": cylinder(0.5, 0.2, 1.0, 0.06, 0.08, axis="z", segments=8),
        "stairs_hub_rl": cylinder(-0.5, 0.2, -0.9, 0.06, 0.08, axis="z", segments=8),
        "stairs_hub_rr": cylinder(0.5, 0.2, -0.9, 0.06, 0.08, axis="z", segments=8),
        "stairs_nosing_4": box(0, 1.45, -0.55, 1.05, 0.04, 0.08),
        "stairs_nosing_5": box(0, 1.6, -0.85, 1.05, 0.04, 0.08),
        "stairs_side_panel_l": box(-0.58, 0.9, 0, 0.04, 1.4, 2.0),
        "stairs_side_panel_r": box(0.58, 0.9, 0, 0.04, 1.4, 2.0),
        "gpu_hub_fl": cylinder(0.45, 0.15, 0.35, 0.05, 0.06, axis="z", segments=8),
        "gpu_hub_fr": cylinder(0.45, 0.15, -0.35, 0.05, 0.06, axis="z", segments=8),
        "gpu_hub_rl": cylinder(-0.45, 0.15, 0.35, 0.05, 0.06, axis="z", segments=8),
        "gpu_hub_rr": cylinder(-0.45, 0.15, -0.35, 0.05, 0.06, axis="z", segments=8),
        "gpu_stripe": box(0, 0.45, 0.44, 1.2, 0.1, 0.04),
        "gpu_handle": box(0.7, 0.85, 0, 0.08, 0.25, 0.35),
        "belt_loader_roller_4": cylinder(3.0, 1.05, 0, 0.06, 0.3, axis="z", segments=8),
        "belt_loader_hub_fl": cylinder(3.3, 0.15, 0.35, 0.06, 0.08, axis="z", segments=8),
        "belt_loader_hub_fr": cylinder(3.3, 0.15, -0.35, 0.06, 0.08, axis="z", segments=8),
        "belt_loader_cab_glass": box(3.5, 0.85, 0, 0.04, 0.3, 0.5),
        "belt_loader_stripe": box(2.8, 0.45, 0.44, 1.4, 0.08, 0.04),
        "cone_tip": cylinder(1.2, 0.65, 0, 0.08, 0.18, axis="y", segments=8),
        "towbar_eye": cylinder(1.35, 0.25, -1.5, 0.08, 0.06, axis="x", segments=8),
        "bin_stripe": box(-1.2, 0.55, 1.12, 0.55, 0.12, 0.04),
        "chock_handle": box(0, 0.22, 0, 0.08, 0.2, 0.08),
    }
    return meshes


def airfield_props_meshes() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    return {
        "windsock_pole": cylinder(0, 1.5, 0, 0.05, 3.0, axis="y", segments=8),
        "cone": cylinder(0, 0.28, 0, 0.16, 0.55, axis="y", segments=8),
        "barrier": box(0, 0.55, 0, 1.6, 1.1, 0.1),
        "sign_board": box(0, 1.1, 0, 1.3, 0.9, 0.08),
        "baggage_dolly": box(0, 0.4, 0, 1.5, 0.55, 0.85),
        "sock_pole": cylinder(0, 1.6, 0, 0.05, 3.2, axis="y", segments=8),
        "sock_base": box(0, 0.08, 0, 0.45, 0.16, 0.45),
        "sock_frame": box(0, 3.1, 0.35, 0.08, 0.08, 0.7),
        "sock_fabric": box(0, 3.05, 0.85, 0.35, 0.35, 1.1),
        "cone_base": box(0, 0.05, 0, 0.4, 0.08, 0.4),
        "cone_body": cylinder(0, 0.35, 0, 0.14, 0.55, axis="y", segments=8),
        "cone_stripe": cylinder(0, 0.35, 0, 0.15, 0.12, axis="y", segments=8),
        "cone_tip": cylinder(0, 0.7, 0, 0.06, 0.18, axis="y", segments=8),
        "barrier_rail": box(0, 0.85, 0, 1.7, 0.12, 0.1),
        "barrier_rail_low": box(0, 0.35, 0, 1.7, 0.12, 0.1),
        "barrier_stripe": box(0, 0.85, 0.06, 1.5, 0.08, 0.04),
        "barrier_leg_l": box(-0.7, 0.45, 0, 0.12, 0.9, 0.12),
        "barrier_leg_r": box(0.7, 0.45, 0, 0.12, 0.9, 0.12),
        "barrier_foot_l": box(-0.7, 0.06, 0, 0.28, 0.08, 0.28),
        "barrier_foot_r": box(0.7, 0.06, 0, 0.28, 0.08, 0.28),
        "sign_post": cylinder(0, 1.15, 0, 0.06, 2.3, axis="y", segments=8),
        "sign_face": box(0.08, 1.45, 0, 0.06, 0.95, 1.2),
        "sign_cap": box(0.08, 1.95, 0, 0.08, 0.08, 1.25),
        "sign_brace": box(0.04, 0.9, 0, 0.08, 0.08, 0.45),
        "dolly_bed": box(0, 0.45, 0, 1.5, 0.18, 0.85),
        "dolly_rail_l": box(-0.7, 0.7, 0, 0.06, 0.4, 0.8),
        "dolly_rail_r": box(0.7, 0.7, 0, 0.06, 0.4, 0.8),
        "dolly_rail_mid": box(0, 0.7, 0.35, 1.35, 0.05, 0.05),
        "dolly_handle": box(0, 0.85, -0.55, 0.9, 0.08, 0.08),
        "dolly_cargo": box(0, 0.7, 0, 1.1, 0.35, 0.65),
        "dolly_wheel_fl": cylinder(-0.55, 0.15, 0.3, 0.1, 0.12, axis="z", segments=10),
        "dolly_wheel_fr": cylinder(0.55, 0.15, 0.3, 0.1, 0.12, axis="z", segments=10),
        "dolly_wheel_rl": cylinder(-0.55, 0.15, -0.3, 0.1, 0.12, axis="z", segments=10),
        "dolly_wheel_rr": cylinder(0.55, 0.15, -0.3, 0.1, 0.12, axis="z", segments=10),
        "dolly_hitch": box(0, 0.35, -0.55, 0.35, 0.15, 0.2),
        "dolly_rail_end": box(0, 0.7, -0.4, 1.35, 0.05, 0.05),
        "dolly_bag_a": box(-0.25, 0.85, 0.1, 0.45, 0.28, 0.35),
        "dolly_bag_b": box(0.3, 0.85, -0.1, 0.4, 0.25, 0.3),
        "dolly_bag_c": box(0.0, 0.95, 0.15, 0.35, 0.2, 0.28),
        "dolly_post_l": box(-0.7, 0.55, -0.35, 0.05, 0.35, 0.05),
        "dolly_post_r": box(0.7, 0.55, -0.35, 0.05, 0.35, 0.05),
        "barrier_stripe_b": box(0, 0.35, 0.06, 1.5, 0.08, 0.04),
        "barrier_brace": box(0, 0.6, 0, 0.08, 0.08, 0.9),
        "barrier_brace_b": box(0, 0.6, 0, 1.4, 0.06, 0.06),
        "sign_reflector": box(0.1, 1.45, 0, 0.02, 0.85, 1.1),
        "sign_base": box(0, 0.06, 0, 0.35, 0.12, 0.35),
        "sign_glyph_bar": box(0.12, 1.55, 0, 0.02, 0.12, 0.7),
        "sign_glyph_dot": box(0.12, 1.3, 0.25, 0.02, 0.12, 0.12),
        "cone_collar": cylinder(0, 0.2, 0, 0.16, 0.08, axis="y", segments=8),
        "sock_guy_l": box(-0.2, 1.5, 0, 0.03, 2.5, 0.03),
        "sock_guy_r": box(0.2, 1.5, 0, 0.03, 2.5, 0.03),
        "dolly_hub_fl": cylinder(-0.55, 0.15, 0.3, 0.05, 0.06, axis="z", segments=8),
        "dolly_hub_fr": cylinder(0.55, 0.15, 0.3, 0.05, 0.06, axis="z", segments=8),
        "dolly_hub_rl": cylinder(-0.55, 0.15, -0.3, 0.05, 0.06, axis="z", segments=8),
        "dolly_hub_rr": cylinder(0.55, 0.15, -0.3, 0.05, 0.06, axis="z", segments=8),
        "dolly_hitch_pin": cylinder(0, 0.42, -0.55, 0.035, 0.12, axis="y", segments=6),
        "sign_glyph_bar_b": box(0.12, 1.4, -0.2, 0.02, 0.1, 0.45),
        "barrier_top_cap": box(0, 1.15, 0, 1.65, 0.06, 0.12),
        "cone_handle": box(0, 0.55, 0.12, 0.04, 0.2, 0.04),
        "sock_counterweight": box(0, 0.25, 0.15, 0.2, 0.2, 0.2),
        "sock_light": box(0, 3.2, 0, 0.12, 0.1, 0.12),
        "sock_ring": cylinder(0, 3.1, 0.05, 0.12, 0.06, axis="y", segments=10),
        "sock_fabric_mid": box(0, 3.05, 1.2, 0.28, 0.28, 0.7),
        "sock_fabric_tip": box(0, 3.05, 1.7, 0.18, 0.18, 0.45),
        "sock_swivel": cylinder(0, 3.1, 0, 0.08, 0.12, axis="y", segments=8),
    }


def airfield_lighting_meshes() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    """WLD lighting — keep PlaceWorldLighting extract names."""
    return {
        "runway_edge_light": cylinder(0, 0.18, 0, 0.07, 0.36, axis="y", segments=10),
        "taxiway_light": cylinder(0, 0.14, 0, 0.06, 0.28, axis="y", segments=10),
        "apron_floodlight": cylinder(0, 4.6, 0, 0.16, 9.0, axis="y", segments=10),
        "obstruction_light": cylinder(0, 0.45, 0, 0.09, 0.9, axis="y", segments=10),
        "edge_base": box(0, 0.04, 0, 0.28, 0.08, 0.28),
        "edge_stem": cylinder(0, 0.18, 0, 0.05, 0.28, axis="y", segments=10),
        "edge_lens": cylinder(0, 0.36, 0, 0.08, 0.1, axis="y", segments=12),
        "edge_collar": cylinder(0, 0.28, 0, 0.09, 0.06, axis="y", segments=10),
        "edge_glare": cylinder(0, 0.4, 0, 0.11, 0.04, axis="y", segments=10),
        "taxi_base": box(0, 0.04, 0, 0.22, 0.06, 0.22),
        "taxi_stem": cylinder(0, 0.16, 0, 0.045, 0.24, axis="y", segments=10),
        "taxi_lens": cylinder(0, 0.3, 0, 0.07, 0.08, axis="y", segments=12),
        "taxi_collar": cylinder(0, 0.22, 0, 0.08, 0.05, axis="y", segments=10),
        "taxi_glare": cylinder(0, 0.34, 0, 0.09, 0.03, axis="y", segments=10),
        "obst_base": box(0, 0.05, 0, 0.24, 0.08, 0.24),
        "obst_stem": cylinder(0, 0.35, 0, 0.05, 0.55, axis="y", segments=10),
        "obst_lens": cylinder(0, 0.7, 0, 0.1, 0.14, axis="y", segments=12),
        "obst_guard": box(0, 0.55, 0, 0.22, 0.08, 0.22),
        "obst_ring": cylinder(0, 0.62, 0, 0.14, 0.04, axis="y", segments=10),
        # Tall multi-head apron flood (~9 m) — SpotLights sync at y≈9.2 (REF-002).
        "flood_base": box(0, 0.1, 0, 0.7, 0.16, 0.7),
        "flood_pole": cylinder(0, 4.6, 0, 0.15, 9.0, axis="y", segments=12),
        "flood_brace": box(0.3, 5.8, 0, 0.6, 0.1, 0.1),
        "flood_brace_b": box(0.24, 3.9, 0, 0.48, 0.08, 0.08),
        "flood_crossarm": box(0, 8.7, 0, 2.6, 0.14, 0.2),
        "flood_platform": box(0, 8.35, 0, 1.3, 0.12, 0.9),
        "flood_arm": box(0.85, 8.95, 0, 1.6, 0.14, 0.2),
        "flood_arm_b": box(-0.7, 8.75, 0.2, 1.2, 0.12, 0.16),
        "flood_head": box(1.4, 8.9, 0, 0.5, 0.32, 0.42),
        "flood_head_b": box(1.05, 8.7, 0.28, 0.45, 0.28, 0.35),
        "flood_head_c": box(-1.05, 8.7, 0.12, 0.45, 0.28, 0.35),
        "flood_lamp": box(1.55, 8.8, 0, 0.3, 0.18, 0.3),
        "flood_lamp_b": box(1.2, 8.6, 0.28, 0.28, 0.16, 0.28),
        "flood_lamp_c": box(-1.2, 8.6, 0.12, 0.28, 0.16, 0.28),
        "flood_visor": box(1.55, 9.0, 0, 0.36, 0.08, 0.42),
        "flood_visor_b": box(1.2, 8.8, 0.28, 0.32, 0.08, 0.35),
        "flood_ladder": box(-0.22, 4.2, 0, 0.08, 7.8, 0.08),
        "flood_guy": box(0.42, 4.4, 0, 0.045, 8.0, 0.045),
        "edge_gasket": cylinder(0, 0.1, 0, 0.1, 0.04, axis="y", segments=10),
        "taxi_gasket": cylinder(0, 0.08, 0, 0.08, 0.03, axis="y", segments=10),
        "obst_cap": cylinder(0, 0.78, 0, 0.08, 0.06, axis="y", segments=10),
        "flood_base_bolt": box(0.22, 0.14, 0.22, 0.1, 0.06, 0.1),
        # Extra mesh-only detail (no new Lights) — denser flood/edge/taxi silhouette.
        "flood_base_bolt_b": box(-0.22, 0.14, -0.22, 0.1, 0.06, 0.1),
        "flood_base_bolt_c": box(0.22, 0.14, -0.22, 0.1, 0.06, 0.1),
        "flood_base_bolt_d": box(-0.22, 0.14, 0.22, 0.1, 0.06, 0.1),
        "flood_junction": box(0, 6.6, 0, 0.28, 0.22, 0.28),
        "flood_cable_tray": box(0.18, 7.4, 0, 0.08, 1.6, 0.08),
        "flood_handrail": box(0, 8.55, 0.35, 1.1, 0.06, 0.06),
        "flood_transformer": box(0.45, 0.55, 0.35, 0.45, 0.7, 0.35),
        "edge_reflector": box(0, 0.42, 0.08, 0.12, 0.08, 0.04),
        "taxi_reflector": box(0, 0.36, 0.07, 0.1, 0.06, 0.035),
        "obst_beacon_ring": cylinder(0, 0.72, 0, 0.16, 0.03, axis="y", segments=10),
        "obst_cable": box(0.08, 0.35, 0, 0.03, 0.5, 0.03),
        "flood_lamp_d": box(-1.4, 8.85, -0.15, 0.28, 0.16, 0.28),
        "flood_visor_c": box(-1.05, 8.85, 0.12, 0.32, 0.08, 0.35),
        "flood_crossarm_brace": box(0, 8.55, 0, 0.9, 0.1, 0.1),
    }


def write_kit(
    folder: Path,
    basename: str,
    meshes: dict[str, tuple[np.ndarray, np.ndarray]],
) -> None:
    gltf = folder / f"{basename}.gltf"
    fbx = folder / f"{basename}.fbx"
    bin_path = gltf.with_suffix(".bin")
    # pack_gltf always rewrites .meta — preserve Unity GUIDs across regenerates.
    preserved = {}
    for p in (gltf, bin_path, fbx):
        meta = Path(str(p) + ".meta")
        if meta.exists():
            preserved[meta] = meta.read_text(encoding="utf-8")
    pack_gltf(gltf, meshes)
    export_fbx(meshes, fbx)
    for meta, text in preserved.items():
        meta.write_text(text, encoding="utf-8")
    if not Path(str(gltf) + ".meta").exists():
        write_default_meta(gltf)
    if not Path(str(bin_path) + ".meta").exists():
        write_default_meta(bin_path)
    print(f"Wrote {gltf.name} + {fbx.name} ({len(meshes)} meshes)")


def main() -> None:
    AIRCRAFT.mkdir(parents=True, exist_ok=True)
    BUILDINGS.mkdir(parents=True, exist_ok=True)
    VEHICLES.mkdir(parents=True, exist_ok=True)
    PROPS.mkdir(parents=True, exist_ok=True)

    write_kit(AIRCRAFT, "mdl_regional_turboprop_01_authored_v01", turboprop_meshes())
    write_kit(BUILDINGS, "mdl_terminal_regional_small_authored_v01", terminal_meshes())
    write_kit(BUILDINGS, "mdl_hangar_small_authored_v01", hangar_meshes())
    write_kit(BUILDINGS, "mdl_operations_shed_authored_v01", ops_shed_meshes())
    write_kit(VEHICLES, "mdl_fuel_truck_small_authored_v01", fuel_truck_meshes())
    write_kit(VEHICLES, "mdl_baggage_tug_train_authored_v01", baggage_tug_meshes())
    write_kit(VEHICLES, "mdl_passenger_bus_apron_authored_v01", passenger_bus_meshes())
    write_kit(PROPS, "mdl_service_equipment_kit_authored_v01", service_equipment_meshes())
    write_kit(PROPS, "mdl_airfield_lighting_kit_authored_v01", airfield_lighting_meshes())
    write_kit(PROPS, "mdl_airfield_props_kit_authored_v01", airfield_props_meshes())


if __name__ == "__main__":
    main()
