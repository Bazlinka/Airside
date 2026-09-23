"""Import the CC0 Quaternius people used for boarding and ramp crew (ADR 0114).

Sources (CC0 1.0, Quaternius — recorded in docs/data/ASSET_AND_DATA_REGISTER.md):
  Ultimate Modular Men   https://quaternius.com/packs/ultimatemodularcharacters.html
  Ultimate Modular Women https://quaternius.com/packs/ultimatemodularwomen.html
Download the packs' "Individual Characters/FBX" folders into SOURCE (default
work/quaternius/{men,women}), then run with Blender's Python module:

  pip install bpy && python3 scripts/import-quaternius-people.py [SOURCE]

Every character shares one 62-bone rig. Each is re-exported with only the clips the
airport uses (Walk, Idle, Idle_Neutral, Interact, Wave) so the runtime FBX stays small
and each character's clips bind to its own bones. Output:
game/Airside/Assets/Resources/Airside/Characters/<id>.fbx (Unity FBX axes: -Z fwd, Y up).
"""
import bpy  # noqa: E402  (must be the first import — Blender's module owns logging)
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "game/Airside/Assets/Resources/Airside/Characters")
SOURCE = sys.argv[1] if len(sys.argv) > 1 else os.path.join(ROOT, "work/quaternius")
KEEP = ("Walk", "Idle", "Idle_Neutral", "Interact", "Wave")

# (pack, source FBX, runtime id) — everyday clothes for passengers, the worker outfit
# (hi-vis vest, cap) for ramp crew.
CHARACTERS = [
    ("men", "Casual_2", "chr_passenger_m_casual"),
    ("men", "Casual_Hoodie", "chr_passenger_m_hoodie"),
    ("men", "Suit", "chr_passenger_m_suit"),
    ("men", "Beach", "chr_passenger_m_holiday"),
    ("men", "Worker", "chr_ramp_m_worker"),
    ("women", "Casual", "chr_passenger_f_casual"),
    ("women", "Formal", "chr_passenger_f_formal"),
    ("women", "Suit", "chr_passenger_f_suit"),
    ("women", "Worker", "chr_ramp_f_worker"),
]


def clip_name(action_name):
    return action_name.split("|")[-1]


def convert(pack, name, runtime_id):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    source = os.path.join(SOURCE, pack, "Individual Characters", "FBX", f"{name}.fbx")
    bpy.ops.import_scene.fbx(filepath=source)
    for action in list(bpy.data.actions):
        if clip_name(action.name) not in KEEP:
            bpy.data.actions.remove(action)
        else:
            action.name = clip_name(action.name)
            action.use_fake_user = True
    for obj in bpy.data.objects:
        if obj.animation_data is not None:
            obj.animation_data.action = None
    # The men's pack ships every material at alpha 0 (a source bug): engines honour that and
    # draw the body invisible. People are opaque.
    for material in bpy.data.materials:
        if material.use_nodes:
            for node in material.node_tree.nodes:
                if node.type == "BSDF_PRINCIPLED":
                    node.inputs["Alpha"].default_value = 1.0
        material.blend_method = "OPAQUE"
    kept = sorted(a.name for a in bpy.data.actions)
    if kept != sorted(KEEP):
        raise SystemExit(f"{name}: expected clips {sorted(KEEP)}, found {kept}")
    out = os.path.join(OUT, f"{runtime_id}.fbx")
    bpy.ops.export_scene.fbx(
        filepath=out, object_types={"ARMATURE", "MESH"}, add_leaf_bones=False,
        axis_forward="-Z", axis_up="Y", apply_unit_scale=True, bake_space_transform=False,
        bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=1.0, path_mode="STRIP")
    triangles = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in bpy.data.objects if o.type == "MESH")
    print(f"{runtime_id}: {triangles} triangles, clips {kept}, {os.path.getsize(out) // 1024} KB", flush=True)


def main():
    os.makedirs(OUT, exist_ok=True)
    for pack, name, runtime_id in CHARACTERS:
        convert(pack, name, runtime_id)


main()
