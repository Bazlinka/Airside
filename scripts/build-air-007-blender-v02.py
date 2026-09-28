#!/usr/bin/env python3
"""Build and review the Blender AIR-007 Saab 340B candidate.

Run with Blender, not the system Python:

    /Applications/Blender.app/Contents/MacOS/Blender --background \
      --python scripts/build-air-007-blender-v02.py

The script imports the approved project-owned v01 FBX, preserves its exact
19.73 m x 21.44 m x 6.97 m envelope and runtime-facing part names, then creates
a non-runtime v02 candidate with better material response, propeller blades,
nacelle lips, edge highlights, and restrained landing-gear detail. It writes
an editable .blend, review renders, FBX/GLB interchange files, and a machine-
readable manifest under docs/art/candidates. It does not replace runtime art.
"""

from __future__ import annotations

import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


REPO = Path(__file__).resolve().parent.parent
SOURCE_FBX = (
    REPO
    / "game"
    / "Airside"
    / "Assets"
    / "Airside"
    / "Art"
    / "Models"
    / "Aircraft"
    / "mdl_saab_340b_v01.fbx"
)
OUTPUT = REPO / "docs" / "art" / "candidates" / "air-007-saab-340b-v02"
RENDERS = OUTPUT / "renders"
BLEND_PATH = OUTPUT / "mdl_saab_340b_v02_candidate.blend"
FBX_PATH = OUTPUT / "mdl_saab_340b_v02_candidate.fbx"
GLB_PATH = OUTPUT / "mdl_saab_340b_v02_candidate.glb"

EXPECTED_DIMS = Vector((21.44, 19.73, 6.97))  # Blender X, Y, Z.
EXPECTED_MIN_Z = 0.0
PROP_RADIUS = 1.675
PROP_HUBS = {
    "left": Vector((-3.55, -2.95, 1.74)),
    "right": Vector((3.55, -2.95, 1.74)),
}


def clear_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials):
        for block in list(datablocks):
            if block.users == 0:
                datablocks.remove(block)


def make_material(
    name: str,
    colour: tuple[float, float, float, float],
    metallic: float = 0.0,
    roughness: float = 0.42,
    emission: tuple[float, float, float, float] | None = None,
    emission_strength: float = 0.0,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.diffuse_color = colour
    material.use_nodes = True
    bsdf = material.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = colour
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = roughness
    if emission is not None:
        bsdf.inputs["Emission Color"].default_value = emission
        bsdf.inputs["Emission Strength"].default_value = emission_strength
    return material


def materials() -> dict[str, bpy.types.Material]:
    return {
        "airframe": make_material("Airside Warm White", (0.78, 0.80, 0.79, 1.0), 0.08, 0.30),
        "wing": make_material("Painted Alloy", (0.50, 0.53, 0.54, 1.0), 0.42, 0.28),
        "blue": make_material("Coastal Blue", (0.025, 0.15, 0.24, 1.0), 0.12, 0.24),
        "sand": make_material("Sand Pinstripe", (0.58, 0.43, 0.22, 1.0), 0.08, 0.30),
        "glass": make_material("Flight Deck Glass", (0.012, 0.045, 0.065, 1.0), 0.18, 0.12),
        "uniform": make_material("Pilot Uniform", (0.018, 0.070, 0.11, 1.0), 0.05, 0.48),
        "skin": make_material("Pilot Skin", (0.46, 0.25, 0.16, 1.0), 0.0, 0.58),
        "tyre": make_material("Tyre Rubber", (0.018, 0.020, 0.021, 1.0), 0.0, 0.82),
        "metal": make_material("Gear Metal", (0.31, 0.34, 0.35, 1.0), 0.72, 0.22),
        "dark_metal": make_material("Exhaust Dark Metal", (0.055, 0.060, 0.064, 1.0), 0.70, 0.36),
        "prop": make_material("Propeller Charcoal", (0.018, 0.024, 0.027, 1.0), 0.16, 0.24),
        "yellow": make_material("Propeller Safety Yellow", (0.93, 0.60, 0.035, 1.0), 0.0, 0.32),
        "red_light": make_material(
            "Red Navigation Light", (0.36, 0.008, 0.006, 1.0), 0.0, 0.20,
            (1.0, 0.012, 0.006, 1.0), 5.0,
        ),
        "green_light": make_material(
            "Green Navigation Light", (0.008, 0.30, 0.055, 1.0), 0.0, 0.20,
            (0.01, 1.0, 0.08, 1.0), 5.0,
        ),
        "white_light": make_material(
            "White Aircraft Light", (0.75, 0.78, 0.72, 1.0), 0.0, 0.16,
            (1.0, 0.96, 0.82, 1.0), 4.0,
        ),
    }


def assign_material(obj: bpy.types.Object, material: bpy.types.Material) -> None:
    obj.data.materials.clear()
    obj.data.materials.append(material)


def material_for(name: str, mats: dict[str, bpy.types.Material]) -> bpy.types.Material:
    lower = name.lower()
    if "nav_light_left" in lower:
        return mats["red_light"]
    if "nav_light_right" in lower:
        return mats["green_light"]
    if any(token in lower for token in ("landing_light", "taxi_light", "tail_nav_light")):
        return mats["white_light"]
    if "beacon" in lower:
        return mats["red_light"]
    if "pilot_" in lower and "uniform" in lower:
        return mats["uniform"]
    if "pilot_" in lower and "head" in lower:
        return mats["skin"]
    if any(token in lower for token in ("windscreen", "cockpit_side", "cabin_window", "glazing_")):
        return mats["glass"]
    if "livery_secondary" in lower:
        return mats["sand"]
    if "livery_emblem" in lower:
        return mats["airframe"]
    if any(token in lower for token in ("livery_stripe", "livery_cowl", "door_outline")):
        return mats["blue"]
    if "tire_" in lower:
        return mats["tyre"]
    if any(token in lower for token in ("wheel_", "rim_", "gear_oleo")):
        return mats["metal"]
    if "propeller_" in lower:
        return mats["yellow"] if "_tip" in lower else mats["prop"]
    if any(token in lower for token in ("exhaust", "intake")):
        return mats["dark_metal"]
    if any(
        token in lower
        for token in (
            "wing_", "flap_", "aileron_", "tailplane", "elevator_", "tail_fin",
            "rudder", "pylon_", "static_wick",
        )
    ):
        return mats["wing"]
    return mats["airframe"]


def aircraft_meshes() -> list[bpy.types.Object]:
    return [obj for obj in bpy.context.scene.objects if obj.type == "MESH" and not obj.name.startswith("RENDER_")]


def import_source() -> list[bpy.types.Object]:
    if not SOURCE_FBX.exists():
        raise FileNotFoundError(SOURCE_FBX)
    # Blender 5.2's native importer accepts the repository's intentionally
    # human-readable ASCII FBX; the legacy add-on importer does not.
    if hasattr(bpy.ops.wm, "fbx_import"):
        bpy.ops.wm.fbx_import(filepath=str(SOURCE_FBX))
    else:
        bpy.ops.import_scene.fbx(filepath=str(SOURCE_FBX))
    meshes = aircraft_meshes()
    if not meshes:
        raise RuntimeError("Source FBX imported no mesh objects")
    return meshes


def set_surface_finish(meshes: list[bpy.types.Object], mats: dict[str, bpy.types.Material]) -> None:
    curved = (
        "fuselage", "radome", "engine_", "spinner_", "prop_hub_", "gear_fairing_",
        "belly_fairing", "tail_root_fairing", "tire_", "wheel_", "rim_", "gear_oleo_",
    )
    # The authored aerofoil meshes already carry their profile. Bevel only the
    # visibly box-built support parts; broad beveling of every generated wing
    # edge costs tens of thousands of triangles for no game-camera benefit.
    bevelled = ("pylon_", "gear_door_")
    for obj in meshes:
        assign_material(obj, material_for(obj.name, mats))
        lower = obj.name.lower()
        if any(token in lower for token in curved):
            for polygon in obj.data.polygons:
                polygon.use_smooth = True
        if any(token in lower for token in bevelled):
            modifier = obj.modifiers.new("Airside edge softness", "BEVEL")
            modifier.width = 0.012
            modifier.segments = 1
            modifier.limit_method = "ANGLE"
            modifier.angle_limit = math.radians(38.0)


def make_blade_mesh(name: str, angle_deg: float, start: float, end: float) -> bpy.types.Mesh:
    is_tip = start > 1.3
    if is_tip:
        station_data = [
            (start, 0.086, 0.030, 13.0),
            ((start + end) * 0.5, 0.058, 0.020, 10.0),
            (end, 0.016, 0.008, 8.0),
        ]
    else:
        station_data = [
            (start, 0.17, 0.085, 34.0),
            (0.46, 0.25, 0.075, 29.0),
            (0.84, 0.205, 0.058, 23.0),
            (1.18, 0.145, 0.044, 17.0),
            (end, 0.086, 0.030, 13.0),
        ]

    angle = math.radians(angle_deg)
    radial = Vector((math.sin(angle), 0.0, math.cos(angle)))
    tangent = Vector((math.cos(angle), 0.0, -math.sin(angle)))
    axis = Vector((0.0, 1.0, 0.0))
    verts: list[tuple[float, float, float]] = []
    for radius, chord, thickness, twist_deg in station_data:
        twist = math.radians(twist_deg)
        width_dir = tangent * math.cos(twist) + axis * math.sin(twist)
        thick_dir = -tangent * math.sin(twist) + axis * math.cos(twist)
        centre = radial * radius
        for width_sign, thick_sign in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            point = centre + width_dir * (width_sign * chord * 0.5) + thick_dir * (thick_sign * thickness * 0.5)
            verts.append(tuple(point))

    faces: list[tuple[int, ...]] = [(0, 3, 2, 1)]
    for station in range(len(station_data) - 1):
        a = station * 4
        b = (station + 1) * 4
        faces.extend(
            [
                (a, a + 1, b + 1, b),
                (a + 1, a + 2, b + 2, b + 1),
                (a + 2, a + 3, b + 3, b + 2),
                (a + 3, a, b, b + 3),
            ]
        )
    last = (len(station_data) - 1) * 4
    faces.append((last, last + 1, last + 2, last + 3))
    mesh = bpy.data.meshes.new(f"{name}_mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    return mesh


def replace_propellers(mats: dict[str, bpy.types.Material]) -> None:
    suffixes = ("", "_b", "_c", "_d")
    for side, hub in PROP_HUBS.items():
        for index, suffix in enumerate(suffixes):
            main_name = f"propeller_{side}{suffix}"
            tip_name = f"propeller_{side}_tip{suffix}"
            for object_name in (main_name, tip_name):
                old = bpy.data.objects.get(object_name)
                if old is None:
                    raise RuntimeError(f"Required runtime part is missing: {object_name}")
                bpy.data.objects.remove(old, do_unlink=True)

            main = bpy.data.objects.new(main_name, make_blade_mesh(main_name, index * 90.0, 0.20, 1.43))
            main.location = hub
            bpy.context.collection.objects.link(main)
            assign_material(main, mats["prop"])

            tip = bpy.data.objects.new(tip_name, make_blade_mesh(tip_name, index * 90.0, 1.43, PROP_RADIUS))
            tip.location = hub
            bpy.context.collection.objects.link(tip)
            assign_material(tip, mats["yellow"])


def cylinder_between(
    name: str,
    start: Vector,
    end: Vector,
    radius: float,
    material: bpy.types.Material,
    vertices: int = 16,
) -> bpy.types.Object:
    direction = end - start
    midpoint = (start + end) * 0.5
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=direction.length, location=midpoint)
    obj = bpy.context.object
    obj.name = name
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = direction.to_track_quat("Z", "Y")
    assign_material(obj, material)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    bevel = obj.modifiers.new("Machined edge", "BEVEL")
    bevel.width = min(0.012, radius * 0.28)
    bevel.segments = 2
    return obj


def add_detail(mats: dict[str, bpy.types.Material]) -> None:
    # Polished intake lips sit inside the existing nacelle envelope.
    for side, x in (("left", -3.55), ("right", 3.55)):
        bpy.ops.mesh.primitive_torus_add(
            align="WORLD",
            major_segments=48,
            minor_segments=10,
            location=(x, -2.57, 1.74),
            rotation=(math.radians(90.0), 0.0, 0.0),
            major_radius=0.315,
            minor_radius=0.026,
        )
        lip = bpy.context.object
        lip.name = f"intake_lip_{side}"
        assign_material(lip, mats["metal"])
        for polygon in lip.data.polygons:
            polygon.use_smooth = True

        side_sign = -1.0 if side == "left" else 1.0
        cylinder_between(
            f"gear_drag_brace_{side}",
            Vector((x, 0.28, 0.82)),
            Vector((x + side_sign * 0.28, 0.30, 1.32)),
            0.032,
            mats["metal"],
        )
        cylinder_between(
            f"gear_axle_{side}",
            Vector((x - 0.27, 0.55, 0.38)),
            Vector((x + 0.27, 0.55, 0.38)),
            0.038,
            mats["dark_metal"],
        )

    # Twin fork detail helps the small nose assembly read at follow-camera range.
    cylinder_between(
        "gear_fork_nose_left", Vector((-0.12, -7.15, 0.30)), Vector((-0.08, -7.15, 0.78)),
        0.026, mats["metal"],
    )
    cylinder_between(
        "gear_fork_nose_right", Vector((0.12, -7.15, 0.30)), Vector((0.08, -7.15, 0.78)),
        0.026, mats["metal"],
    )


def create_aircraft_root() -> bpy.types.Object:
    root = bpy.data.objects.new("AIR007_Saab340B_Candidate_v02", None)
    bpy.context.collection.objects.link(root)
    for obj in aircraft_meshes():
        obj.parent = root
        obj.matrix_parent_inverse = root.matrix_world.inverted()
    return root


def world_bounds(meshes: list[bpy.types.Object]) -> tuple[Vector, Vector]:
    points: list[Vector] = []
    depsgraph = bpy.context.evaluated_depsgraph_get()
    for obj in meshes:
        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        points.extend(evaluated.matrix_world @ vertex.co for vertex in mesh.vertices)
        evaluated.to_mesh_clear()
    minimum = Vector(tuple(min(point[i] for point in points) for i in range(3)))
    maximum = Vector(tuple(max(point[i] for point in points) for i in range(3)))
    return minimum, maximum


def triangle_count(meshes: list[bpy.types.Object]) -> int:
    triangles = 0
    depsgraph = bpy.context.evaluated_depsgraph_get()
    for obj in meshes:
        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        mesh.calc_loop_triangles()
        triangles += len(mesh.loop_triangles)
        evaluated.to_mesh_clear()
    return triangles


def validate(meshes: list[bpy.types.Object], baseline_triangles: int) -> dict[str, object]:
    names = {obj.name for obj in meshes}
    required = {
        "fuselage", "wing_left", "wing_right", "engine_left", "engine_right",
        "gear_nose", "gear_left", "gear_right", "door_fwd", "cargo_door",
        "flap_left", "flap_right", "aileron_left", "aileron_right", "rudder",
        "elevator_left", "elevator_right",
    }
    for side in PROP_HUBS:
        for suffix in ("", "_b", "_c", "_d"):
            required.add(f"propeller_{side}{suffix}")
            required.add(f"propeller_{side}_tip{suffix}")
    missing = sorted(required - names)
    if missing:
        raise RuntimeError(f"Candidate lost required runtime-facing parts: {missing}")

    minimum, maximum = world_bounds(meshes)
    dimensions = maximum - minimum
    if any(abs(dimensions[i] - EXPECTED_DIMS[i]) > 0.025 for i in range(3)):
        raise RuntimeError(f"Candidate bounds drifted: {tuple(round(v, 4) for v in dimensions)}")
    if abs(minimum.z - EXPECTED_MIN_Z) > 0.02:
        raise RuntimeError(f"Candidate tyre contact drifted: min Z {minimum.z:.4f}")

    for side, hub in PROP_HUBS.items():
        for suffix in ("", "_b", "_c", "_d"):
            for name in (f"propeller_{side}{suffix}", f"propeller_{side}_tip{suffix}"):
                if (bpy.data.objects[name].location - hub).length > 0.0001:
                    raise RuntimeError(f"{name} pivot is not at its propeller hub")

    triangles = triangle_count(meshes)
    added_triangles = triangles - baseline_triangles
    maximum_added = max(4000, round(baseline_triangles * 0.07))
    if added_triangles > maximum_added:
        raise RuntimeError(
            "Candidate triangle budget exceeded: "
            f"+{added_triangles} triangles over source; budget +{maximum_added}"
        )
    return {
        "mesh_objects": len(meshes),
        "triangles": triangles,
        "source_triangles": baseline_triangles,
        "added_triangles": added_triangles,
        "triangle_increase_percent": round(added_triangles / baseline_triangles * 100.0, 2),
        "bounds_min_blender_xyz": [round(v, 5) for v in minimum],
        "bounds_max_blender_xyz": [round(v, 5) for v in maximum],
        "dimensions_blender_xyz_m": [round(v, 5) for v in dimensions],
    }


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def setup_render_rig() -> tuple[bpy.types.Object, list[bpy.types.Object]]:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = 1400
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.render.image_settings.color_mode = "RGB"
    scene.render.image_settings.compression = 90
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.world.color = (0.025, 0.032, 0.041)

    rig_objects: list[bpy.types.Object] = []
    bpy.ops.mesh.primitive_plane_add(size=90.0, location=(0.0, 0.0, -0.018))
    ground = bpy.context.object
    ground.name = "RENDER_Ground"
    ground_mat = make_material("Render Ground", (0.055, 0.065, 0.070, 1.0), 0.0, 0.66)
    assign_material(ground, ground_mat)
    rig_objects.append(ground)

    bpy.ops.object.camera_add(location=(24.0, -29.0, 14.0))
    camera = bpy.context.object
    camera.name = "RENDER_Camera"
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 26.5
    camera.data.lens = 52
    scene.camera = camera
    rig_objects.append(camera)

    light_specs = (
        ("RENDER_Key", (8.0, -15.0, 21.0), 2100.0, 10.0, (1.0, 0.86, 0.70)),
        ("RENDER_Fill", (-15.0, -2.0, 11.0), 1300.0, 9.0, (0.53, 0.72, 1.0)),
        ("RENDER_Rim", (5.0, 15.0, 17.0), 1800.0, 8.0, (0.72, 0.84, 1.0)),
    )
    for name, location, energy, size, colour in light_specs:
        bpy.ops.object.light_add(type="AREA", location=location)
        light = bpy.context.object
        light.name = name
        light.data.energy = energy
        light.data.shape = "DISK"
        light.data.size = size
        light.data.color = colour
        look_at(light, Vector((0.0, 0.0, 2.0)))
        rig_objects.append(light)
    return camera, rig_objects


def render_views(camera: bpy.types.Object, prefix: str) -> list[str]:
    scene = bpy.context.scene
    target = Vector((0.0, 0.0, 2.25))
    views = {
        "front_left_high": ((20.0, -27.0, 14.0), 27.0),
        "side_left": ((30.0, 0.0, 5.2), 23.5),
        "front": ((0.0, -31.0, 4.8), 24.0),
        "top": ((0.0, 0.0, 35.0), 25.0),
    }
    paths: list[str] = []
    for name, (location, scale) in views.items():
        camera.location = location
        camera.data.ortho_scale = scale
        look_at(camera, target)
        path = RENDERS / f"{prefix}_{name}.png"
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        paths.append(str(path.relative_to(REPO)))
    return paths


def render_detail_views(camera: bpy.types.Object, prefix: str) -> list[str]:
    scene = bpy.context.scene
    views = {
        "prop_close": (Vector((-3.55, -8.2, 2.05)), Vector((-3.55, -2.95, 1.65)), 4.25),
        "gear_close": (Vector((-8.0, -2.8, 2.5)), Vector((-3.55, 0.1, 0.72)), 4.2),
    }
    paths: list[str] = []
    for name, (location, target, scale) in views.items():
        camera.location = location
        camera.data.ortho_scale = scale
        look_at(camera, target)
        path = RENDERS / f"{prefix}_{name}.png"
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        paths.append(str(path.relative_to(REPO)))
    return paths


def set_modifiers_visible(visible: bool) -> None:
    for obj in aircraft_meshes():
        for modifier in obj.modifiers:
            modifier.show_render = visible
            modifier.show_viewport = visible


def select_aircraft(root: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    root.select_set(True)
    for obj in aircraft_meshes():
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root


def export_candidate(root: bpy.types.Object) -> None:
    select_aircraft(root)
    bpy.ops.export_scene.fbx(
        filepath=str(FBX_PATH),
        use_selection=True,
        object_types={"EMPTY", "MESH"},
        apply_unit_scale=True,
        bake_space_transform=False,
        axis_forward="-Z",
        axis_up="Y",
        mesh_smooth_type="FACE",
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        path_mode="AUTO",
    )
    select_aircraft(root)
    bpy.ops.export_scene.gltf(
        filepath=str(GLB_PATH),
        export_format="GLB",
        use_selection=True,
        export_apply=True,
        export_yup=True,
        export_materials="EXPORT",
    )


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def write_manifest(stats: dict[str, object], renders: list[str]) -> None:
    manifest = {
        "asset_id": "AIR-007",
        "candidate_version": "v02",
        "status": "candidate-not-runtime",
        "source": str(SOURCE_FBX.relative_to(REPO)),
        "generator": "Blender 5.2.2 LTS + scripts/build-air-007-blender-v02.py",
        "dimensions_m": {"length": 19.73, "span": 21.44, "height": 6.97},
        "validation": stats,
        "outputs": {
            "blend": str(BLEND_PATH.relative_to(REPO)),
            "fbx": str(FBX_PATH.relative_to(REPO)),
            "glb": str(GLB_PATH.relative_to(REPO)),
            "renders": renders,
        },
        "sha256": {
            "fbx": sha256(FBX_PATH),
            "glb": sha256(GLB_PATH),
        },
        "runtime_replacement": False,
        "fallback": "game/Airside/Assets/Airside/Art/Models/Aircraft/mdl_saab_340b_v01.fbx",
    }
    (OUTPUT / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")


def main() -> None:
    OUTPUT.mkdir(parents=True, exist_ok=True)
    RENDERS.mkdir(parents=True, exist_ok=True)
    clear_scene()
    meshes = import_source()
    baseline_triangles = triangle_count(meshes)
    mats = materials()
    set_surface_finish(meshes, mats)
    camera, _rig = setup_render_rig()

    # Baseline and candidate share the same rig and base materials so the review
    # isolates the modelling improvements rather than a lighting trick.
    set_modifiers_visible(False)
    render_views(camera, "baseline")
    render_detail_views(camera, "baseline")
    set_modifiers_visible(True)

    replace_propellers(mats)
    add_detail(mats)
    root = create_aircraft_root()
    stats = validate(aircraft_meshes(), baseline_triangles)
    candidate_renders = render_views(camera, "candidate")
    candidate_renders.extend(render_detail_views(camera, "candidate"))

    backup_path = Path(str(BLEND_PATH) + "1")
    if backup_path.exists():
        backup_path.unlink()
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH), compress=True)
    if backup_path.exists():
        backup_path.unlink()
    export_candidate(root)
    write_manifest(stats, candidate_renders)
    print(
        "AIR-007 Blender candidate ready: "
        f"{stats['mesh_objects']} meshes, {stats['triangles']} triangles, "
        f"{stats['dimensions_blender_xyz_m']} m Blender XYZ"
    )


if __name__ == "__main__":
    main()
