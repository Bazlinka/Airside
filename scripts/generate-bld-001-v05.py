#!/usr/bin/env python3
"""Generate Batch F1 BLD-001 v05 authored regional terminal (FBX + companion glTF).

Decision 0027 / Batch F packet — BLD-001 v05 only. Distinct id
`mdl_terminal_regional_small_v05` — does not overwrite authored_v01 / v04 files.

Architectural intent (REF-001 / REF-005):
- Shallow dual-pitch roof with low ridge (Australian regional silhouette)
- Deep glazed airside curtain wall with mullion hierarchy
- Apron canopy on round posts
- Service wing + baggage dock on landside/right
- Rooftop plant as composed volumes
- Modular rounded end caps

Not a cuboid densify pass: roof is pitched panels, end caps use cylindrical
softening, canopy soffit is tapered, curtain wall recesses into the shell.
"""

from __future__ import annotations

import importlib.util
import subprocess
import tempfile
from pathlib import Path

import numpy as np

REPO = Path(__file__).resolve().parents[1]
ROOT = REPO / "game/Airside/Assets/Airside/Art"
BUILDINGS = ROOT / "Models" / "Buildings"
BASENAME = "mdl_terminal_regional_small_v05"

# Load Batch C primitives without depending on cloud /workspace paths.
_batch_spec = importlib.util.spec_from_file_location(
    "batch_c_v01", REPO / "scripts/generate-batch-c-models.py"
)
_batch = importlib.util.module_from_spec(_batch_spec)
assert _batch_spec.loader is not None
# Patch ROOT before exec so any incidental writes stay in-repo.
import sys

sys.modules["batch_c_v01"] = _batch
_batch_spec.loader.exec_module(_batch)
_batch.ROOT = ROOT

_ascii_spec = importlib.util.spec_from_file_location(
    "ascii_fbx", REPO / "scripts/write_ascii_fbx.py"
)
_ascii = importlib.util.module_from_spec(_ascii_spec)
assert _ascii_spec.loader is not None
_ascii_spec.loader.exec_module(_ascii)

box = _batch.box
pack_gltf = _batch.pack_gltf
write_default_meta = _batch.write_default_meta


def cylinder(
    cx: float,
    cy: float,
    cz: float,
    radius: float,
    length: float,
    *,
    axis: str = "y",
    segments: int = 12,
) -> tuple[np.ndarray, np.ndarray]:
    """Closed cylinder — matches authored FBX kit helper."""
    segs = max(6, segments)
    rings = []
    if axis == "y":
        y0, y1 = cy - length * 0.5, cy + length * 0.5
        for y in (y0, y1):
            ring = []
            for i in range(segs):
                ang = 2.0 * np.pi * i / segs
                ring.append([cx + radius * np.cos(ang), y, cz + radius * np.sin(ang)])
            rings.append(np.asarray(ring, np.float32))
    elif axis == "z":
        z0, z1 = cz - length * 0.5, cz + length * 0.5
        for z in (z0, z1):
            ring = []
            for i in range(segs):
                ang = 2.0 * np.pi * i / segs
                ring.append([cx + radius * np.cos(ang), cy + radius * np.sin(ang), z])
            rings.append(np.asarray(ring, np.float32))
    else:  # x
        x0, x1 = cx - length * 0.5, cx + length * 0.5
        for x in (x0, x1):
            ring = []
            for i in range(segs):
                ang = 2.0 * np.pi * i / segs
                ring.append([x, cy + radius * np.cos(ang), cz + radius * np.sin(ang)])
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


def write_fbx_model_meta(path: Path) -> None:
    meta_path = Path(str(path) + ".meta")
    if meta_path.exists():
        return
    import uuid

    meta_path.write_text(
        f"""fileFormatVersion: 2
guid: {uuid.uuid4().hex}
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
    importAnimation: 0
  meshes:
    globalScale: 1
    meshCompression: 0
    addColliders: 0
    useFileUnits: 1
    keepQuads: 0
    weldVertices: 1
    bakeAxisConversion: 0
    preserveHierarchy: 1
    isReadable: 1
  importBlendShapes: 0
  importVisibility: 1
  importCameras: 0
  importLights: 0
  normalImportMode: 0
  normalCalculationMode: 4
  tangentImportMode: 3
  materialImportMode: 1
  generateSecondaryUV: 0
  useFileScale: 1
""",
        encoding="utf-8",
    )


def write_obj(path: Path, meshes: dict) -> None:
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


def rotate_x(
    verts: np.ndarray, cx: float, cy: float, cz: float, deg: float
) -> np.ndarray:
    rad = np.deg2rad(deg)
    ca, sa = float(np.cos(rad)), float(np.sin(rad))
    out = verts.copy()
    for i, v in enumerate(verts):
        y, z = v[1] - cy, v[2] - cz
        out[i, 1] = cy + y * ca - z * sa
        out[i, 2] = cz + y * sa + z * ca
    return out


def pitched_panel(
    cx: float,
    cy: float,
    cz: float,
    width: float,
    thickness: float,
    depth: float,
    pitch_deg: float,
) -> tuple[np.ndarray, np.ndarray]:
    """Roof panel pitched about local X through its centre."""
    verts, indices = box(cx, cy, cz, width, thickness, depth)
    return rotate_x(verts, cx, cy, cz, pitch_deg), indices


def terminal_v05_meshes() -> dict[str, tuple[np.ndarray, np.ndarray]]:
    """Authored regional terminal — prefer silhouette over mesh count."""
    mullion_z = -2.42
    pane_z = -2.28
    meshes: dict[str, tuple[np.ndarray, np.ndarray]] = {}

    # --- Shell & plinth -------------------------------------------------
    meshes["terminal_body"] = box(0, 2.05, 0.15, 20.2, 3.85, 4.2)
    meshes["plinth"] = box(0, 0.16, 0.05, 21.0, 0.32, 4.9)
    meshes["plinth_step"] = box(0, 0.3, -2.45, 16.5, 0.1, 0.55)
    meshes["plinth_kerb_l"] = box(-10.1, 0.22, -2.55, 0.7, 0.28, 0.35)
    meshes["plinth_kerb_r"] = box(10.1, 0.22, -2.55, 0.7, 0.28, 0.35)
    meshes["girth_band_1"] = box(0, 0.95, 0.15, 20.3, 0.09, 4.25)
    meshes["girth_band_2"] = box(0, 2.95, 0.15, 20.3, 0.09, 4.25)

    # --- Shallow dual-pitch roof (REF regional silhouette) --------------
    # Left / right panels pitch ~8° toward a low central ridge.
    meshes["roof_panel_l"] = pitched_panel(-5.2, 4.35, 0.05, 10.6, 0.16, 5.15, 8.0)
    meshes["roof_panel_r"] = pitched_panel(5.2, 4.35, 0.05, 10.6, 0.16, 5.15, -8.0)
    meshes["roof_ridge"] = box(0, 4.72, 0.05, 0.55, 0.18, 5.0)
    meshes["roof_eave_front"] = box(0, 4.12, -2.5, 21.0, 0.1, 0.22)
    meshes["roof_eave_back"] = box(0, 4.12, 2.55, 21.0, 0.1, 0.22)
    meshes["roof_parapet"] = box(0, 4.55, -2.45, 20.6, 0.22, 0.1)
    meshes["roof_parapet_back"] = box(0, 4.55, 2.4, 20.6, 0.22, 0.1)
    meshes["fascia_front"] = box(0, 3.95, -2.5, 20.5, 0.18, 0.12)
    meshes["fascia_back"] = box(0, 3.95, 2.5, 20.5, 0.18, 0.12)
    meshes["soffit_front"] = box(0, 3.7, -2.55, 18.0, 0.08, 0.5)
    meshes["roof_flash_front"] = box(0, 4.48, -2.35, 20.4, 0.06, 0.16)
    meshes["roof_flash_back"] = box(0, 4.48, 2.35, 20.4, 0.06, 0.16)

    # Rooftop plant — composed HVAC volumes (not one slab)
    meshes["roof_plant"] = box(-4.6, 4.95, -0.55, 2.4, 0.55, 1.5)
    meshes["roof_plant_b"] = box(3.6, 4.9, 0.45, 2.0, 0.48, 1.25)
    meshes["roof_plant_c"] = box(0.2, 4.85, -0.2, 1.5, 0.38, 1.05)
    meshes["roof_vent_a"] = cylinder(-2.2, 5.15, 0.7, 0.24, 0.5, axis="y", segments=12)
    meshes["roof_vent_b"] = cylinder(2.3, 5.15, -0.5, 0.24, 0.5, axis="y", segments=12)
    meshes["roof_vent_c"] = cylinder(-5.5, 5.05, 0.9, 0.16, 0.35, axis="y", segments=10)
    meshes["hvac_duct"] = box(-6.2, 4.85, 1.15, 3.2, 0.18, 0.32)
    meshes["hvac_duct_b"] = box(5.0, 4.8, -0.9, 2.4, 0.16, 0.28)

    # --- Modular end caps (softened cylinders + faces) ------------------
    meshes["end_cap_left"] = box(-10.7, 2.0, 0, 1.0, 3.85, 4.85)
    meshes["end_cap_right"] = box(10.7, 2.0, 0, 1.0, 3.85, 4.85)
    # Soft vertical edges on modular caps (readable from overview).
    meshes["end_cap_soft_l_f"] = cylinder(-10.7, 2.0, -2.2, 0.38, 3.7, axis="y", segments=12)
    meshes["end_cap_soft_l_b"] = cylinder(-10.7, 2.0, 2.2, 0.38, 3.7, axis="y", segments=12)
    meshes["end_cap_soft_r_f"] = cylinder(10.7, 2.0, -2.2, 0.38, 3.7, axis="y", segments=12)
    meshes["end_cap_soft_r_b"] = cylinder(10.7, 2.0, 2.2, 0.38, 3.7, axis="y", segments=12)
    meshes["corner_trim_l"] = box(-10.2, 2.1, -2.35, 0.16, 3.7, 0.16)
    meshes["corner_trim_r"] = box(10.2, 2.1, -2.35, 0.16, 3.7, 0.16)
    meshes["corner_trim_bl"] = box(-10.2, 2.1, 2.35, 0.16, 3.7, 0.16)
    meshes["corner_trim_br"] = box(10.2, 2.1, 2.35, 0.16, 3.7, 0.16)
    meshes["buttress"] = box(-10.15, 1.15, -1.7, 0.75, 2.1, 1.15)
    meshes["buttress_r"] = box(10.15, 1.15, -1.7, 0.75, 2.1, 1.15)
    meshes["downpipe_l"] = cylinder(-10.15, 2.15, 2.25, 0.07, 4.1, axis="y", segments=8)
    meshes["downpipe_r"] = cylinder(10.15, 2.15, 2.25, 0.07, 4.1, axis="y", segments=8)
    meshes["flag_pole"] = cylinder(10.25, 3.55, -2.85, 0.045, 3.0, axis="y", segments=8)
    meshes["flag_cloth"] = box(10.6, 4.65, -2.85, 0.7, 0.42, 0.035)

    # --- Airside curtain wall (recessed glazed face) --------------------
    meshes["glass_front"] = box(0, 2.3, pane_z - 0.04, 16.2, 2.35, 0.035)
    for i, x in enumerate((-7.5, -6.0, -4.5, -3.0, -1.5, 0.0, 1.5, 3.0, 4.5, 6.0, 7.5), start=1):
        w = 0.11 if i % 3 == 0 else 0.08
        meshes[f"window_mullion_{i}"] = box(x, 2.3, mullion_z, w, 2.45, 0.16)
    meshes["window_transom"] = box(0, 3.3, mullion_z, 16.0, 0.09, 0.14)
    meshes["window_midrail"] = box(0, 2.3, mullion_z, 16.0, 0.07, 0.12)
    meshes["window_sill"] = box(0, 1.12, mullion_z, 16.0, 0.08, 0.16)
    meshes["window_header"] = box(0, 3.5, mullion_z, 16.2, 0.12, 0.16)
    # Pane bays (skip centre entrance)
    pane_xs = (-6.75, -5.25, -3.75, -2.25, 2.25, 3.75, 5.25, 6.75, -0.75, 0.75, -8.0, 8.0)
    for i, x in enumerate(pane_xs, start=1):
        meshes[f"glass_pane_{i}"] = box(x, 2.8, pane_z, 1.3, 0.85, 0.05)
        meshes[f"glass_pane_lo_{i}"] = box(x, 1.7, pane_z, 1.3, 1.0, 0.05)

    # Primary entrance
    meshes["entrance"] = box(0, 1.3, -2.35, 2.5, 2.35, 0.1)
    meshes["entrance_frame"] = box(0, 1.35, -2.42, 2.8, 2.55, 0.08)
    meshes["entrance_door_l"] = box(-0.55, 1.2, -2.4, 1.05, 2.15, 0.05)
    meshes["entrance_door_r"] = box(0.55, 1.2, -2.4, 1.05, 2.15, 0.05)
    meshes["entrance_transom"] = box(0, 2.5, -2.45, 2.55, 0.08, 0.05)
    meshes["entrance_handle_l"] = box(-0.12, 1.3, -2.48, 0.07, 0.32, 0.07)
    meshes["entrance_handle_r"] = box(0.12, 1.3, -2.48, 0.07, 0.32, 0.07)

    # Boarding gate (left airside)
    meshes["boarding_gate"] = box(-5.6, 1.15, -2.45, 1.85, 2.15, 0.1)
    meshes["boarding_frame"] = box(-5.6, 1.15, -2.55, 2.05, 2.35, 0.07)
    meshes["boarding_glass"] = box(-5.6, 1.5, -2.48, 1.25, 1.05, 0.04)
    meshes["boarding_canopy"] = box(-5.6, 2.4, -2.95, 2.3, 0.09, 1.05)

    # Signage
    meshes["signage_bar"] = box(0, 3.85, -2.38, 10.2, 0.32, 0.18)
    meshes["signage_cap"] = box(0, 4.08, -2.38, 10.4, 0.09, 0.2)
    meshes["signage_glyph_a"] = box(-2.6, 3.85, -2.3, 1.5, 0.18, 0.035)
    meshes["signage_glyph_b"] = box(2.6, 3.85, -2.3, 1.5, 0.18, 0.035)

    # Structural columns (airside)
    for key, x in (
        ("column_l", -8.4),
        ("column_r", 8.4),
        ("column_ml", -4.0),
        ("column_mr", 4.0),
    ):
        meshes[key] = cylinder(x, 1.95, -1.45, 0.2, 3.7, axis="y", segments=12)

    # End wall ribs
    for i, x in enumerate((-9.5, -8.6, 8.6, 9.5, -9.05, 9.05), start=1):
        meshes[f"wall_rib_end_{i}"] = box(x, 2.1, -2.4, 0.09, 3.5, 0.07)

    # --- Apron canopy ---------------------------------------------------
    meshes["canopy"] = box(0, 3.5, -3.25, 14.4, 0.14, 2.35)
    meshes["canopy_beam"] = box(0, 3.3, -3.25, 14.4, 0.1, 0.22)
    meshes["canopy_edge"] = box(0, 3.4, -4.35, 14.4, 0.09, 0.1)
    meshes["canopy_soffit"] = box(0, 3.42, -3.25, 13.8, 0.05, 2.05)
    meshes["canopy_gutter"] = box(0, 3.35, -4.3, 14.2, 0.07, 0.1)
    meshes["canopy_flash"] = box(0, 3.55, -2.25, 14.4, 0.07, 0.18)
    for key, x in (
        ("canopy_post_l", -6.6),
        ("canopy_post_r", 6.6),
        ("canopy_post_ml", -2.2),
        ("canopy_post_mr", 2.2),
    ):
        meshes[key] = cylinder(x, 1.65, -3.75, 0.11, 3.25, axis="y", segments=12)
    for key, x in (
        ("canopy_brace_l", -4.5),
        ("canopy_brace_r", 4.5),
        ("canopy_brace_ml", -2.2),
        ("canopy_brace_mr", 2.2),
    ):
        meshes[key] = box(x, 3.15, -3.5, 0.09, 0.45, 1.35)
    meshes["canopy_light_l"] = box(-3.5, 3.35, -3.7, 0.38, 0.07, 0.22)
    meshes["canopy_light_r"] = box(3.5, 3.35, -3.7, 0.38, 0.07, 0.22)
    meshes["canopy_light_mid"] = box(0, 3.35, -3.7, 0.5, 0.07, 0.22)

    # --- Landside face --------------------------------------------------
    meshes["landside_glass"] = box(0, 2.15, 2.4, 12.2, 1.75, 0.035)
    for i, x in enumerate((-5.5, -4.0, -2.0, 0.0, 2.0, 4.0, 5.5, -6.5, 6.5), start=1):
        meshes[f"landside_mullion_{i}"] = box(x, 2.15, 2.48, 0.09, 1.85, 0.12)
    meshes["landside_transom"] = box(0, 2.8, 2.48, 11.6, 0.07, 0.1)
    meshes["landside_sill"] = box(0, 1.3, 2.48, 11.6, 0.07, 0.12)
    land_xs = (-5.0, -3.0, -1.0, 1.0, 3.0, 5.0, -6.5, 6.5)
    for i, x in enumerate(land_xs, start=1):
        meshes[f"glass_pane_land_{i}"] = box(x, 2.5, 2.42, 1.65, 0.5, 0.045)
        meshes[f"glass_pane_land_lo_{i}"] = box(x, 1.7, 2.42, 1.65, 0.65, 0.045)
    meshes["landside_awning"] = box(0, 3.15, 3.05, 10.2, 0.1, 1.45)
    meshes["landside_awning_brace_l"] = box(-4.0, 2.95, 2.75, 0.07, 0.32, 0.7)
    meshes["landside_awning_brace_r"] = box(4.0, 2.95, 2.75, 0.07, 0.32, 0.7)
    for i, x in enumerate((-9.0, -7.0, -5.0, -3.0, 3.0, 5.0, 7.0, 9.0, -1.5, 1.5), start=1):
        meshes[f"wall_rib_land_{i}"] = box(x, 1.95, 2.45, 0.09, 3.1, 0.07)

    # --- Service wing + baggage dock ------------------------------------
    meshes["service_wing"] = box(7.6, 1.3, 2.85, 7.6, 2.5, 2.85)
    meshes["service_wing_roof"] = box(7.6, 2.7, 2.85, 7.7, 0.16, 2.95)
    meshes["service_wing_fascia"] = box(7.6, 2.5, 4.2, 7.5, 0.14, 0.1)
    meshes["service_door"] = box(9.6, 1.05, 4.2, 1.55, 1.95, 0.09)
    meshes["service_door_frame"] = box(9.6, 1.05, 4.28, 1.7, 2.1, 0.07)
    meshes["service_window"] = box(7.6, 1.65, 4.2, 1.15, 0.75, 0.045)
    meshes["service_window_frame"] = box(7.6, 1.65, 4.26, 1.3, 0.9, 0.055)
    meshes["baggage_door"] = box(5.5, 0.95, 4.2, 2.45, 1.75, 0.09)
    meshes["baggage_door_frame"] = box(5.5, 0.95, 4.28, 2.6, 1.9, 0.07)
    meshes["baggage_ramp"] = box(5.5, 0.18, 4.7, 2.7, 0.22, 0.95)
    meshes["baggage_canopy"] = box(5.5, 1.95, 4.55, 3.1, 0.1, 1.25)
    for i, x in enumerate((4.2, 5.2, 6.2, 7.2, 8.2, 9.2, 10.2, 11.0), start=1):
        meshes[f"service_rib_{i}"] = box(x, 1.3, 4.18, 0.07, 2.35, 0.055)

    # --- Warm interior read (night glow targets; kept for color map) ----
    meshes["interior_counter"] = box(-3.5, 1.1, -1.5, 4.4, 0.85, 0.65)
    meshes["interior_seat_row"] = box(3.2, 0.7, -1.4, 5.0, 0.5, 0.65)
    meshes["interior_desk_a"] = box(-5.5, 1.0, -1.45, 2.1, 0.8, 0.6)
    meshes["interior_desk_b"] = box(5.5, 1.0, -1.45, 2.1, 0.8, 0.6)
    meshes["interior_table_1"] = box(-2.0, 0.8, -1.3, 1.15, 0.1, 0.65)
    meshes["interior_table_2"] = box(1.8, 0.8, -1.3, 1.15, 0.1, 0.65)
    for i, x in enumerate((-2.5, -1.5, 1.3, 2.3), start=1):
        meshes[f"interior_chair_{i}"] = box(x, 0.5, -1.15, 0.42, 0.5, 0.42)
    meshes["interior_figure_a"] = box(-4.2, 1.15, -1.3, 0.32, 1.35, 0.22)
    meshes["interior_figure_b"] = box(0.2, 1.1, -1.25, 0.32, 1.3, 0.22)
    meshes["interior_figure_c"] = box(4.0, 1.15, -1.3, 0.32, 1.35, 0.22)
    meshes["interior_glow_l"] = box(-5.0, 2.35, -1.65, 3.1, 1.35, 0.07)
    meshes["interior_glow_r"] = box(5.0, 2.35, -1.65, 3.1, 1.35, 0.07)
    meshes["interior_glow_mid"] = box(0.0, 2.55, -1.6, 2.7, 1.15, 0.07)
    meshes["interior_glow_desk"] = box(-5.5, 1.55, -1.7, 1.9, 0.55, 0.055)

    return meshes


def export_fbx(meshes: dict, fbx_path: Path) -> None:
    """Prefer assimp when present; otherwise write ASCII FBX Unity can import."""
    assimp = subprocess.run(["which", "assimp"], capture_output=True, text=True)
    if assimp.returncode == 0 and assimp.stdout.strip():
        with tempfile.TemporaryDirectory() as tmp:
            obj_path = Path(tmp) / (fbx_path.stem + ".obj")
            write_obj(obj_path, meshes)
            result = subprocess.run(
                ["assimp", "export", str(obj_path), str(fbx_path)],
                capture_output=True,
                text=True,
                check=False,
            )
            if result.returncode == 0 and fbx_path.exists():
                write_fbx_model_meta(fbx_path)
                return
    _ascii.write_ascii_fbx(fbx_path, meshes)
    write_fbx_model_meta(fbx_path)


def write_kit(folder: Path, basename: str, meshes: dict) -> None:
    gltf = folder / f"{basename}.gltf"
    fbx = folder / f"{basename}.fbx"
    bin_path = gltf.with_suffix(".bin")
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
    BUILDINGS.mkdir(parents=True, exist_ok=True)
    meshes = terminal_v05_meshes()
    write_kit(BUILDINGS, BASENAME, meshes)
    print(f"BLD-001 v05 ready: {BASENAME} ({len(meshes)} meshes)")


if __name__ == "__main__":
    main()
