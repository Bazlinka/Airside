#!/usr/bin/env python3
"""Emit Resources prefabs for authored turboprop + terminal.

Decision 0025 item 2 — Addressables keys airside-prefab/mdl_*_authored_v01 load via
Resources immediately (cylinder/cube hierarchy with motion part names). Companion
FBX remains the Unity ModelImporter upgrade; Mac bake can overwrite these prefabs.
"""

from __future__ import annotations

import math
from pathlib import Path

OUT_DIR = Path("/workspace/game/Airside/Assets/Resources/Airside/Prefabs")
BINDER_GUID = "c4f8a21b9e7d4650a3b1c2d4e5f60718"
CUBE = "{fileID: 10202, guid: 0000000000000000e000000000000000, type: 0}"
CYLINDER = "{fileID: 10206, guid: 0000000000000000e000000000000000, type: 0}"


def write_meta(path: Path, guid: str) -> None:
    path.write_text(
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "PrefabImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n",
        encoding="utf-8",
    )


def euler_to_quat(ex: float, ey: float, ez: float) -> tuple[float, float, float, float]:
    """Degrees → Unity quaternion (x,y,z,w)."""
    rx, ry, rz = map(math.radians, (ex, ey, ez))
    cx, sx = math.cos(rx * 0.5), math.sin(rx * 0.5)
    cy, sy = math.cos(ry * 0.5), math.sin(ry * 0.5)
    cz, sz = math.cos(rz * 0.5), math.sin(rz * 0.5)
    # ZYX order matching Unity localEulerAngles application
    w = cx * cy * cz + sx * sy * sz
    x = sx * cy * cz - cx * sy * sz
    y = cx * sy * cz + sx * cy * sz
    z = cx * cy * sz - sx * sy * cz
    return (x, y, z, w)


def emit_prefab(
    key: str,
    guid: str,
    parts: list[tuple],
    *,
    base: tuple[float, float, float],
    accent: tuple[float, float, float],
    step: tuple[float, float, float],
) -> None:
    """parts: (name, pos, scale, mesh[, euler_xyz])"""
    root, root_t, root_binder = 100001, 100002, 100003
    numbered = []
    for i, part in enumerate(parts):
        name, pos, scale, mesh = part[0], part[1], part[2], part[3]
        euler = part[4] if len(part) > 4 else (0.0, 0.0, 0.0)
        numbered.append((name, pos, scale, mesh, euler, 200000 + i * 1000))

    lines = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:"]
    lines += [
        f"--- !u!1 &{root}",
        "GameObject:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  serializedVersion: 6",
        "  m_Component:",
        f"  - component: {{fileID: {root_t}}}",
        f"  - component: {{fileID: {root_binder}}}",
        "  m_Layer: 0",
        f"  m_Name: {key}",
        "  m_TagString: Untagged",
        "  m_Icon: {fileID: 0}",
        "  m_NavMeshLayer: 0",
        "  m_StaticEditorFlags: 0",
        "  m_IsActive: 1",
        f"--- !u!4 &{root_t}",
        "Transform:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        f"  m_GameObject: {{fileID: {root}}}",
        "  serializedVersion: 2",
        "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}",
        "  m_LocalPosition: {x: 0, y: 0, z: 0}",
        "  m_LocalScale: {x: 1, y: 1, z: 1}",
        "  m_ConstrainProportionsScale: 0",
        "  m_Children:",
    ]
    for *_, gid in numbered:
        lines.append(f"  - {{fileID: {gid + 1}}}")
    lines += [
        "  m_Father: {fileID: 0}",
        "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}",
        f"--- !u!114 &{root_binder}",
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        f"  m_GameObject: {{fileID: {root}}}",
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        f"  m_Script: {{fileID: 11500000, guid: {BINDER_GUID}, type: 3}}",
        "  m_Name: ",
        "  m_EditorClassIdentifier: ",
        f"  baseColor: {{r: {base[0]}, g: {base[1]}, b: {base[2]}, a: 1}}",
        f"  accentColor: {{r: {accent[0]}, g: {accent[1]}, b: {accent[2]}, a: 1}}",
        f"  stepColor: {{r: {step[0]}, g: {step[1]}, b: {step[2]}, a: 1}}",
    ]

    for name, pos, scale, mesh, euler, gid in numbered:
        tid, mfid, mrid = gid + 1, gid + 2, gid + 3
        qx, qy, qz, qw = euler_to_quat(*euler)
        mesh_ref = CYLINDER if mesh == "cylinder" else CUBE
        lines += [
            f"--- !u!1 &{gid}",
            "GameObject:",
            "  m_ObjectHideFlags: 0",
            "  m_CorrespondingSourceObject: {fileID: 0}",
            "  m_PrefabInstance: {fileID: 0}",
            "  m_PrefabAsset: {fileID: 0}",
            "  serializedVersion: 6",
            "  m_Component:",
            f"  - component: {{fileID: {tid}}}",
            f"  - component: {{fileID: {mfid}}}",
            f"  - component: {{fileID: {mrid}}}",
            "  m_Layer: 0",
            f"  m_Name: {name}",
            "  m_TagString: Untagged",
            "  m_Icon: {fileID: 0}",
            "  m_NavMeshLayer: 0",
            "  m_StaticEditorFlags: 0",
            "  m_IsActive: 1",
            f"--- !u!4 &{tid}",
            "Transform:",
            "  m_ObjectHideFlags: 0",
            "  m_CorrespondingSourceObject: {fileID: 0}",
            "  m_PrefabInstance: {fileID: 0}",
            "  m_PrefabAsset: {fileID: 0}",
            f"  m_GameObject: {{fileID: {gid}}}",
            "  serializedVersion: 2",
            f"  m_LocalRotation: {{x: {qx:.6f}, y: {qy:.6f}, z: {qz:.6f}, w: {qw:.6f}}}",
            f"  m_LocalPosition: {{x: {pos[0]}, y: {pos[1]}, z: {pos[2]}}}",
            f"  m_LocalScale: {{x: {scale[0]}, y: {scale[1]}, z: {scale[2]}}}",
            "  m_ConstrainProportionsScale: 0",
            "  m_Children: []",
            f"  m_Father: {{fileID: {root_t}}}",
            f"  m_LocalEulerAnglesHint: {{x: {euler[0]}, y: {euler[1]}, z: {euler[2]}}}",
            f"--- !u!33 &{mfid}",
            "MeshFilter:",
            "  m_ObjectHideFlags: 0",
            "  m_CorrespondingSourceObject: {fileID: 0}",
            "  m_PrefabInstance: {fileID: 0}",
            "  m_PrefabAsset: {fileID: 0}",
            f"  m_GameObject: {{fileID: {gid}}}",
            f"  m_Mesh: {mesh_ref}",
            f"--- !u!23 &{mrid}",
            "MeshRenderer:",
            "  m_ObjectHideFlags: 0",
            "  m_CorrespondingSourceObject: {fileID: 0}",
            "  m_PrefabInstance: {fileID: 0}",
            "  m_PrefabAsset: {fileID: 0}",
            f"  m_GameObject: {{fileID: {gid}}}",
            "  m_Enabled: 1",
            "  m_CastShadows: 1",
            "  m_ReceiveShadows: 1",
            "  m_DynamicOccludee: 1",
            "  m_StaticShadowCaster: 0",
            "  m_MotionVectors: 1",
            "  m_LightProbeUsage: 1",
            "  m_ReflectionProbeUsage: 1",
            "  m_RayTracingMode: 2",
            "  m_RayTraceProcedural: 0",
            "  m_RenderingLayerMask: 1",
            "  m_RendererPriority: 0",
            "  m_Materials:",
            "  - {fileID: 0}",
            "  m_StaticBatchInfo:",
            "    firstSubMesh: 0",
            "    subMeshCount: 0",
            "  m_StaticBatchRoot: {fileID: 0}",
            "  m_ProbeAnchor: {fileID: 0}",
            "  m_LightProbeVolumeOverride: {fileID: 0}",
            "  m_ScaleInLightmap: 1",
            "  m_ReceiveGI: 1",
            "  m_PreserveUVs: 1",
            "  m_IgnoreNormalsForChartDetection: 0",
            "  m_ImportantGI: 0",
            "  m_StitchLightmapSeams: 1",
            "  m_SelectedEditorRenderState: 3",
            "  m_MinimumChartSize: 4",
            "  m_AutoUVMaxDistance: 0.5",
            "  m_AutoUVMaxAngle: 89",
            "  m_LightmapParameters: {fileID: 0}",
            "  m_SortingLayerID: 0",
            "  m_SortingLayer: 0",
            "  m_SortingOrder: 0",
            "  m_AdditionalVertexStreams: {fileID: 0}",
        ]

    out = OUT_DIR / f"{key}.prefab"
    out.write_text("\n".join(lines) + "\n", encoding="utf-8")
    write_meta(out.with_suffix(".prefab.meta"), guid)
    print(f"wrote {out} ({len(parts)} parts)")


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)

    # Cylinder along Z: rotate 90° on X; Unity cylinder height is local Y → world Z.
    zcyl = (90.0, 0.0, 0.0)

    emit_prefab(
        "mdl_regional_turboprop_01_authored_v01",
        "a1b2c3d4e5f60718293a4b5c6d7e8f90",
        [
            # Lathed-style fuselage stations (round cylinders)
            ("Nose", (0.0, 1.08, 4.8), (0.55, 1.1, 0.55), "cylinder", zcyl),
            ("Cockpit", (0.0, 1.25, 3.6), (0.95, 1.2, 0.95), "cylinder", zcyl),
            ("Fuselage", (0.0, 1.18, 2.2), (1.25, 1.5, 1.25), "cylinder", zcyl),
            ("Fuselage mid", (0.0, 1.18, 0.6), (1.32, 1.7, 1.32), "cylinder", zcyl),
            ("Fuselage aft", (0.0, 1.12, -1.2), (1.2, 1.8, 1.2), "cylinder", zcyl),
            ("Belly fairing", (0.0, 0.52, 0.2), (0.85, 4.0, 0.55), "cylinder", zcyl),
            ("Cabin window band", (0.0, 1.38, 0.4), (1.45, 0.12, 4.0), "cube"),
            ("Wing L", (-4.0, 1.05, 0.4), (6.4, 0.14, 1.7), "cube"),
            ("Wing R", (4.0, 1.05, 0.4), (6.4, 0.14, 1.7), "cube"),
            ("Wing root L", (-1.35, 1.08, 0.45), (1.7, 0.22, 1.5), "cube"),
            ("Wing root R", (1.35, 1.08, 0.45), (1.7, 0.22, 1.5), "cube"),
            ("Engine L", (-2.4, 0.85, 1.0), (0.72, 2.0, 0.72), "cylinder", zcyl),
            ("Engine R", (2.4, 0.85, 1.0), (0.72, 2.0, 0.72), "cylinder", zcyl),
            ("Nacelle L", (-2.4, 0.52, 0.55), (0.5, 1.1, 0.5), "cylinder", zcyl),
            ("Nacelle R", (2.4, 0.52, 0.55), (0.5, 1.1, 0.5), "cylinder", zcyl),
            ("Propeller L", (-2.4, 0.85, 2.25), (0.08, 2.35, 0.16), "cube"),
            ("PropBlade L", (-2.4, 0.85, 2.25), (2.35, 0.08, 0.16), "cube"),
            ("Propeller R", (2.4, 0.85, 2.25), (0.08, 2.35, 0.16), "cube"),
            ("PropBlade R", (2.4, 0.85, 2.25), (2.35, 0.08, 0.16), "cube"),
            ("Spinner L", (-2.4, 0.85, 2.42), (0.3, 0.35, 0.3), "cylinder", zcyl),
            ("Spinner R", (2.4, 0.85, 2.42), (0.3, 0.35, 0.3), "cylinder", zcyl),
            ("Tail", (0.0, 2.45, -3.7), (0.12, 2.2, 1.4), "cube"),
            ("Tailplane", (0.0, 1.75, -3.85), (3.4, 0.1, 1.0), "cube"),
            ("Rudder", (0.0, 2.5, -4.35), (0.09, 1.6, 0.45), "cube"),
            ("Gear nose", (0.0, 0.38, 3.15), (0.12, 0.72, 0.32), "cube"),
            ("Gear L", (-1.15, 0.32, -0.35), (0.12, 0.72, 0.42), "cube"),
            ("Gear R", (1.15, 0.32, -0.35), (0.12, 0.72, 0.42), "cube"),
            ("Gear door nose", (0.0, 0.55, 3.15), (0.5, 0.05, 0.65), "cube"),
            ("Gear door L", (-1.15, 0.55, -0.35), (0.6, 0.05, 0.8), "cube"),
            ("Gear door R", (1.15, 0.55, -0.35), (0.6, 0.05, 0.8), "cube"),
            ("Tire nose", (0.0, 0.12, 3.15), (0.22, 0.22, 0.22), "cylinder", (0.0, 0.0, 90.0)),
            ("Tire L", (-1.15, 0.12, -0.35), (0.26, 0.26, 0.26), "cylinder", (0.0, 0.0, 90.0)),
            ("Tire R", (1.15, 0.12, -0.35), (0.26, 0.26, 0.26), "cylinder", (0.0, 0.0, 90.0)),
            ("CabinDoor", (-0.72, 1.1, 2.1), (0.07, 1.0, 1.2), "cube"),
            ("Cargo door", (0.72, 1.0, -1.5), (0.07, 0.9, 1.5), "cube"),
        ],
        base=(0.93, 0.95, 0.97),
        accent=(0.35, 0.55, 0.72),
        step=(0.25, 0.25, 0.28),
    )

    emit_prefab(
        "mdl_terminal_regional_small_authored_v01",
        "b2c3d4e5f60718293a4b5c6d7e8f901a",
        [
            ("terminal_body", (0.0, 2.15, 0.2), (20.5, 4.1, 4.4), "cube"),
            ("roof", (0.0, 4.35, 0.1), (21.2, 0.28, 5.0), "cube"),
            ("end_cap_left", (-10.8, 2.0, 0.0), (1.1, 3.9, 5.0), "cube"),
            ("end_cap_right", (10.8, 2.0, 0.0), (1.1, 3.9, 5.0), "cube"),
            ("glass_front", (0.0, 2.35, -2.15), (16.5, 2.4, 0.1), "cube"),
            ("window_mullion_1", (-6.0, 2.35, -2.2), (0.12, 2.5, 0.14), "cube"),
            ("window_mullion_2", (-3.0, 2.35, -2.2), (0.12, 2.5, 0.14), "cube"),
            ("window_mullion_3", (0.0, 2.35, -2.2), (0.12, 2.5, 0.14), "cube"),
            ("window_mullion_4", (3.0, 2.35, -2.2), (0.12, 2.5, 0.14), "cube"),
            ("window_mullion_5", (6.0, 2.35, -2.2), (0.12, 2.5, 0.14), "cube"),
            ("entrance", (0.0, 1.35, -2.25), (2.4, 2.4, 0.12), "cube"),
            ("landside_glass", (0.0, 2.2, 2.35), (12.0, 1.8, 0.1), "cube"),
            ("canopy", (0.0, 3.55, -3.1), (14.0, 0.18, 2.2), "cube"),
            ("canopy_post_l", (-6.5, 1.7, -3.6), (0.24, 3.3, 0.24), "cylinder"),
            ("canopy_post_r", (6.5, 1.7, -3.6), (0.24, 3.3, 0.24), "cylinder"),
            ("canopy_post_ml", (-2.2, 1.7, -3.6), (0.24, 3.3, 0.24), "cylinder"),
            ("canopy_post_mr", (2.2, 1.7, -3.6), (0.24, 3.3, 0.24), "cylinder"),
            ("service_wing", (7.5, 1.35, 2.8), (7.5, 2.6, 2.8), "cube"),
            ("service_door", (9.5, 1.1, 4.15), (1.6, 2.0, 0.1), "cube"),
            ("baggage_door", (5.5, 1.0, 4.15), (2.4, 1.8, 0.1), "cube"),
            ("signage_bar", (0.0, 3.9, -2.3), (10.0, 0.35, 0.2), "cube"),
            ("column_l", (-8.5, 2.0, -1.5), (0.44, 3.8, 0.44), "cylinder"),
            ("column_r", (8.5, 2.0, -1.5), (0.44, 3.8, 0.44), "cylinder"),
        ],
        base=(0.68, 0.72, 0.75),
        accent=(0.16, 0.38, 0.5),
        step=(0.55, 0.58, 0.6),
    )


    emit_prefab(
        "mdl_hangar_small_authored_v01",
        "c3d4e5f60718293a4b5c6d7e8f901a2b",
        [
            ("hangar_shell", (0.0, 2.5, 0.0), (14.0, 5.0, 9.0), "cube"),
            ("roof_ridge", (0.0, 5.15, 0.0), (14.4, 0.35, 1.2), "cube"),
            ("roof_panel_l", (-3.5, 4.85, 0.0), (7.2, 0.22, 9.2), "cube"),
            ("roof_panel_r", (3.5, 4.85, 0.0), (7.2, 0.22, 9.2), "cube"),
            ("door_opening", (0.0, 2.2, 4.6), (9.5, 4.2, 0.15), "cube"),
            ("door_panel_l", (-2.4, 2.0, 4.7), (4.6, 3.9, 0.12), "cube"),
            ("door_panel_r", (2.4, 2.0, 4.7), (4.6, 3.9, 0.12), "cube"),
            ("door_track_l", (-4.8, 4.3, 4.55), (0.25, 0.2, 0.5), "cube"),
            ("door_track_r", (4.8, 4.3, 4.55), (0.25, 0.2, 0.5), "cube"),
            ("buttress_l", (-7.2, 1.5, 2.5), (1.0, 3.0, 2.5), "cube"),
            ("buttress_r", (7.2, 1.5, 2.5), (1.0, 3.0, 2.5), "cube"),
            ("personnel_door", (-5.5, 1.1, 4.65), (1.1, 2.1, 0.1), "cube"),
            ("office_lean", (5.8, 1.4, -3.5), (3.5, 2.6, 3.0), "cube"),
            ("office_window", (5.8, 1.8, -5.05), (2.2, 1.2, 0.08), "cube"),
            ("crane_beam", (0.0, 4.4, 0.0), (12.0, 0.2, 0.35), "cube"),
            ("column_l", (-6.2, 2.4, -2.0), (0.4, 4.6, 0.4), "cylinder"),
            ("column_r", (6.2, 2.4, -2.0), (0.4, 4.6, 0.4), "cylinder"),
        ],
        base=(0.45, 0.5, 0.54),
        accent=(0.22, 0.24, 0.26),
        step=(0.4, 0.44, 0.48),
    )


if __name__ == "__main__":
    main()
