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
    """Unity ModelImporter stub — Editor regenerates detail on first open."""
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
    # Lathed cabin — round silhouette from overview/follow.
    fuselage = {
        "fuselage": lathe_fuselage(
            [
                (5.2, 0.28, 1.05),
                (4.6, 0.48, 1.08),
                (3.7, 0.58, 1.2),
                (2.6, 0.68, 1.18),
                (1.0, 0.72, 1.18),
                (-0.6, 0.7, 1.15),
                (-2.2, 0.62, 1.12),
                (-3.4, 0.48, 1.05),
                (-4.2, 0.32, 1.0),
            ],
            segments=16,
        ),
        "belly_fairing": cylinder(0, 0.52, 0.2, 0.42, 4.2, axis="z", segments=12),
        "cockpit": box(0, 1.55, 3.55, 0.95, 0.55, 1.1),
        "cockpit_frame": box(0, 1.82, 3.55, 1.0, 0.06, 1.15),
        "cabin_window_band": box(0, 1.38, 0.4, 1.48, 0.12, 4.2),
        "cabin_window_1": box(-0.74, 1.38, 2.0, 0.05, 0.26, 0.48),
        "cabin_window_2": box(-0.74, 1.38, 1.1, 0.05, 0.26, 0.48),
        "cabin_window_3": box(-0.74, 1.38, 0.2, 0.05, 0.26, 0.48),
        "cabin_window_4": box(-0.74, 1.38, -0.7, 0.05, 0.26, 0.48),
        "cabin_window_5": box(-0.74, 1.38, -1.6, 0.05, 0.26, 0.48),
        "cabin_window_r1": box(0.74, 1.38, 2.0, 0.05, 0.26, 0.48),
        "cabin_window_r2": box(0.74, 1.38, 1.1, 0.05, 0.26, 0.48),
        "cabin_window_r3": box(0.74, 1.38, 0.2, 0.05, 0.26, 0.48),
        "cabin_window_r4": box(0.74, 1.38, -0.7, 0.05, 0.26, 0.48),
        "cabin_window_r5": box(0.74, 1.38, -1.6, 0.05, 0.26, 0.48),
    }

    wings = {
        "wing_left": tapered_wing(-0.7, 1.05, 0.4, 6.6, 1.9, 0.85, 0.14, side=-1),
        "wing_right": tapered_wing(0.7, 1.05, 0.4, 6.6, 1.9, 0.85, 0.14, side=1),
        "wing_root_left": box(-1.35, 1.08, 0.45, 1.7, 0.22, 1.5),
        "wing_root_right": box(1.35, 1.08, 0.45, 1.7, 0.22, 1.5),
        "flap_left": box(-3.2, 1.0, -0.35, 3.0, 0.07, 0.42),
        "flap_right": box(3.2, 1.0, -0.35, 3.0, 0.07, 0.42),
        "aileron_left": box(-6.1, 1.02, 0.15, 1.7, 0.06, 0.5),
        "aileron_right": box(6.1, 1.02, 0.15, 1.7, 0.06, 0.5),
        "winglet_left": box(-7.35, 1.4, 0.45, 0.1, 0.6, 0.4),
        "winglet_right": box(7.35, 1.4, 0.45, 0.1, 0.6, 0.4),
    }

    engines = {
        "engine_left": cylinder(-2.4, 0.85, 1.0, 0.38, 2.1, axis="z", segments=12),
        "engine_right": cylinder(2.4, 0.85, 1.0, 0.38, 2.1, axis="z", segments=12),
        "nacelle_left": cylinder(-2.4, 0.52, 0.55, 0.28, 1.2, axis="z", segments=10),
        "nacelle_right": cylinder(2.4, 0.52, 0.55, 0.28, 1.2, axis="z", segments=10),
        "intake_left": cylinder(-2.4, 0.95, 2.0, 0.28, 0.35, axis="z", segments=10),
        "intake_right": cylinder(2.4, 0.95, 2.0, 0.28, 0.35, axis="z", segments=10),
        "exhaust_left": cylinder(-2.4, 0.7, -0.2, 0.18, 0.5, axis="z", segments=8),
        "exhaust_right": cylinder(2.4, 0.7, -0.2, 0.18, 0.5, axis="z", segments=8),
        "propeller_left": box(-2.4, 0.85, 2.25, 0.08, 2.4, 0.16),
        "propeller_left_b": box(-2.4, 0.85, 2.25, 2.4, 0.08, 0.16),
        "propeller_right": box(2.4, 0.85, 2.25, 0.08, 2.4, 0.16),
        "propeller_right_b": box(2.4, 0.85, 2.25, 2.4, 0.08, 0.16),
        "spinner_left": cylinder(-2.4, 0.85, 2.42, 0.16, 0.36, axis="z", segments=10),
        "spinner_right": cylinder(2.4, 0.85, 2.42, 0.16, 0.36, axis="z", segments=10),
    }

    empennage = {
        "tail_fin": box(0, 2.45, -3.7, 0.12, 2.2, 1.45),
        "tail_fin_tip": box(0, 3.45, -3.5, 0.1, 0.35, 0.7),
        "tailplane": box(0, 1.75, -3.85, 3.4, 0.1, 1.0),
        "elevator_left": box(-1.1, 1.72, -4.25, 1.3, 0.05, 0.4),
        "elevator_right": box(1.1, 1.72, -4.25, 1.3, 0.05, 0.4),
        "rudder": box(0, 2.5, -4.35, 0.09, 1.6, 0.45),
        "dorsal_fin": box(0, 1.8, -2.8, 0.09, 0.5, 0.85),
    }

    gear = {
        "gear_nose": box(0, 0.38, 3.15, 0.12, 0.72, 0.32),
        "gear_left": box(-1.15, 0.32, -0.35, 0.12, 0.72, 0.42),
        "gear_right": box(1.15, 0.32, -0.35, 0.12, 0.72, 0.42),
        "gear_door_nose": box(0, 0.55, 3.15, 0.5, 0.05, 0.65),
        "gear_door_left": box(-1.15, 0.55, -0.35, 0.6, 0.05, 0.8),
        "gear_door_right": box(1.15, 0.55, -0.35, 0.6, 0.05, 0.8),
        "tire_nose": cylinder(0, 0.12, 3.15, 0.14, 0.2, axis="x", segments=10),
        "tire_left": cylinder(-1.15, 0.12, -0.35, 0.16, 0.18, axis="x", segments=10),
        "tire_right": cylinder(1.15, 0.12, -0.35, 0.16, 0.18, axis="x", segments=10),
        "door_fwd": box(-0.72, 1.1, 2.1, 0.07, 1.0, 1.2),
        "cargo_door": box(0.72, 1.0, -1.5, 0.07, 0.9, 1.5),
        "antenna": box(0, 2.05, 1.2, 0.05, 0.5, 0.05),
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
    return {
        "terminal_body": box(0, 2.15, 0.2, 20.5, 4.1, 4.4),
        "roof": box(0, 4.35, 0.1, 21.2, 0.28, 5.0),
        "roof_plant": box(-4.5, 4.7, -0.8, 2.2, 0.55, 1.4),
        "roof_plant_b": box(3.8, 4.65, 0.6, 1.8, 0.45, 1.2),
        "end_cap_left": box(-10.8, 2.0, 0, 1.1, 3.9, 5.0),
        "end_cap_right": box(10.8, 2.0, 0, 1.1, 3.9, 5.0),
        "glass_front": box(0, 2.35, -2.15, 16.5, 2.4, 0.1),
        "window_mullion_1": box(-6.0, 2.35, -2.2, 0.12, 2.5, 0.14),
        "window_mullion_2": box(-3.0, 2.35, -2.2, 0.12, 2.5, 0.14),
        "window_mullion_3": box(0.0, 2.35, -2.2, 0.12, 2.5, 0.14),
        "window_mullion_4": box(3.0, 2.35, -2.2, 0.12, 2.5, 0.14),
        "window_mullion_5": box(6.0, 2.35, -2.2, 0.12, 2.5, 0.14),
        "entrance": box(0, 1.35, -2.25, 2.4, 2.4, 0.12),
        "entrance_frame": box(0, 1.35, -2.35, 2.7, 2.6, 0.08),
        "landside_glass": box(0, 2.2, 2.35, 12.0, 1.8, 0.1),
        "canopy": box(0, 3.55, -3.1, 14.0, 0.18, 2.2),
        "canopy_beam": box(0, 3.35, -3.1, 14.0, 0.12, 0.25),
        "canopy_post_l": cylinder(-6.5, 1.7, -3.6, 0.12, 3.3, axis="y", segments=8),
        "canopy_post_r": cylinder(6.5, 1.7, -3.6, 0.12, 3.3, axis="y", segments=8),
        "canopy_post_ml": cylinder(-2.2, 1.7, -3.6, 0.12, 3.3, axis="y", segments=8),
        "canopy_post_mr": cylinder(2.2, 1.7, -3.6, 0.12, 3.3, axis="y", segments=8),
        "service_wing": box(7.5, 1.35, 2.8, 7.5, 2.6, 2.8),
        "service_door": box(9.5, 1.1, 4.15, 1.6, 2.0, 0.1),
        "baggage_door": box(5.5, 1.0, 4.15, 2.4, 1.8, 0.1),
        "signage_bar": box(0, 3.9, -2.3, 10.0, 0.35, 0.2),
        "column_l": cylinder(-8.5, 2.0, -1.5, 0.22, 3.8, axis="y", segments=8),
        "column_r": cylinder(8.5, 2.0, -1.5, 0.22, 3.8, axis="y", segments=8),
        "buttress": box(-10.2, 1.2, -1.8, 0.8, 2.2, 1.2),
    }


def hangar_meshes() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    return {
        "hangar_shell": box(0, 2.5, 0, 14, 5, 9),
        "roof_ridge": box(0, 5.15, 0, 14.4, 0.35, 1.2),
        "roof_panel_l": box(-3.5, 4.85, 0, 7.2, 0.22, 9.2),
        "roof_panel_r": box(3.5, 4.85, 0, 7.2, 0.22, 9.2),
        "roof_rib_1": box(-5.0, 4.7, 0, 0.18, 0.45, 9.0),
        "roof_rib_2": box(-2.5, 4.7, 0, 0.18, 0.45, 9.0),
        "roof_rib_3": box(0.0, 4.7, 0, 0.18, 0.45, 9.0),
        "roof_rib_4": box(2.5, 4.7, 0, 0.18, 0.45, 9.0),
        "roof_rib_5": box(5.0, 4.7, 0, 0.18, 0.45, 9.0),
        "door_opening": box(0, 2.2, 4.6, 9.5, 4.2, 0.15),
        "door_panel_l": box(-2.4, 2.0, 4.7, 4.6, 3.9, 0.12),
        "door_panel_r": box(2.4, 2.0, 4.7, 4.6, 3.9, 0.12),
        "door_rib_l": box(-2.4, 2.0, 4.78, 0.12, 3.9, 0.08),
        "door_rib_r": box(2.4, 2.0, 4.78, 0.12, 3.9, 0.08),
        "door_track_l": box(-4.8, 4.3, 4.55, 0.25, 0.2, 0.5),
        "door_track_r": box(4.8, 4.3, 4.55, 0.25, 0.2, 0.5),
        "buttress_l": box(-7.2, 1.5, 2.5, 1.0, 3.0, 2.5),
        "buttress_r": box(7.2, 1.5, 2.5, 1.0, 3.0, 2.5),
        "side_vent": box(-7.05, 3.2, -1.5, 0.15, 1.2, 2.0),
        "side_vent_b": box(7.05, 3.2, -1.5, 0.15, 1.2, 2.0),
        "personnel_door": box(-5.5, 1.1, 4.65, 1.1, 2.1, 0.1),
        "office_lean": box(5.8, 1.4, -3.5, 3.5, 2.6, 3.0),
        "office_window": box(5.8, 1.8, -5.05, 2.2, 1.2, 0.08),
        "crane_beam": box(0, 4.4, 0, 12.0, 0.2, 0.35),
        "column_l": cylinder(-6.2, 2.4, -2.0, 0.2, 4.6, axis="y", segments=8),
        "column_r": cylinder(6.2, 2.4, -2.0, 0.2, 4.6, axis="y", segments=8),
    }


def main() -> None:
    AIRCRAFT.mkdir(parents=True, exist_ok=True)
    BUILDINGS.mkdir(parents=True, exist_ok=True)

    air = turboprop_meshes()
    air_gltf = AIRCRAFT / "mdl_regional_turboprop_01_authored_v01.gltf"
    air_fbx = AIRCRAFT / "mdl_regional_turboprop_01_authored_v01.fbx"
    pack_gltf(air_gltf, air)
    write_default_meta(air_gltf)
    write_default_meta(air_gltf.with_suffix(".bin"))
    export_fbx(air, air_fbx)
    print(f"Wrote {air_gltf.name} + {air_fbx.name} ({len(air)} meshes)")

    bld = terminal_meshes()
    bld_gltf = BUILDINGS / "mdl_terminal_regional_small_authored_v01.gltf"
    bld_fbx = BUILDINGS / "mdl_terminal_regional_small_authored_v01.fbx"
    pack_gltf(bld_gltf, bld)
    write_default_meta(bld_gltf)
    write_default_meta(bld_gltf.with_suffix(".bin"))
    export_fbx(bld, bld_fbx)
    print(f"Wrote {bld_gltf.name} + {bld_fbx.name} ({len(bld)} meshes)")

    hangar = hangar_meshes()
    hangar_gltf = BUILDINGS / "mdl_hangar_small_authored_v01.gltf"
    hangar_fbx = BUILDINGS / "mdl_hangar_small_authored_v01.fbx"
    pack_gltf(hangar_gltf, hangar)
    write_default_meta(hangar_gltf)
    write_default_meta(hangar_gltf.with_suffix(".bin"))
    export_fbx(hangar, hangar_fbx)
    print(f"Wrote {hangar_gltf.name} + {hangar_fbx.name} ({len(hangar)} meshes)")


if __name__ == "__main__":
    main()
