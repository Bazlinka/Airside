#!/usr/bin/env python3
"""Emit the first Airside Resources prefab: mdl_passenger_stairs_v01.

Decision 0025 item 1 — Unity YAML prefab using built-in cube meshes plus
AirsideRuntimeMaterialBinder so Lit materials apply at runtime without authored
.mat assets. Bailey can replace this with an imported FBX prefab later.
"""

from __future__ import annotations

from pathlib import Path

OUT = Path("/workspace/game/Airside/Assets/Resources/Airside/Prefabs/mdl_passenger_stairs_v01.prefab")
BINDER_GUID = "c4f8a21b9e7d4650a3b1c2d4e5f60718"
# Unity built-in cube mesh.
CUBE = "{fileID: 10202, guid: 0000000000000000e000000000000000, type: 0}"

# Stable local fileIDs for the prefab contents.
ROOT = 100001
ROOT_T = 100002
ROOT_BINDER = 100003
parts = []  # (name, pos, scale, id_base)


def add_cube(name: str, pos: tuple[float, float, float], scale: tuple[float, float, float], id_base: int) -> None:
    parts.append((name, pos, scale, id_base))


def main() -> None:
    add_cube("Stairs base", (0.0, 0.0, 0.0), (1.1, 0.2, 2.4), 200000)
    add_cube("Stairs rail L", (-0.45, 0.55, 0.0), (0.08, 1.0, 2.2), 210000)
    add_cube("Stairs rail R", (0.45, 0.55, 0.0), (0.08, 1.0, 2.2), 220000)
    for i in range(5):
        add_cube(
            f"Step {i}",
            (0.0, 0.15 + i * 0.18, -0.9 + i * 0.35),
            (0.95, 0.08, 0.32),
            300000 + i * 1000,
        )

    child_file_ids = [p[3] for p in parts]  # GameObject ids
    lines: list[str] = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:"]

    # Root GameObject
    lines += [
        f"--- !u!1 &{ROOT}",
        "GameObject:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  serializedVersion: 6",
        "  m_Component:",
        f"  - component: {{fileID: {ROOT_T}}}",
        f"  - component: {{fileID: {ROOT_BINDER}}}",
        "  m_Layer: 0",
        "  m_Name: mdl_passenger_stairs_v01",
        "  m_TagString: Untagged",
        "  m_Icon: {fileID: 0}",
        "  m_NavMeshLayer: 0",
        "  m_StaticEditorFlags: 0",
        "  m_IsActive: 1",
        f"--- !u!4 &{ROOT_T}",
        "Transform:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        f"  m_GameObject: {{fileID: {ROOT}}}",
        "  serializedVersion: 2",
        "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}",
        "  m_LocalPosition: {x: 0, y: 0, z: 0}",
        "  m_LocalScale: {x: 1, y: 1, z: 1}",
        "  m_ConstrainProportionsScale: 0",
        "  m_Children:",
    ]
    for gid in child_file_ids:
        # Transform of each child is gid+1
        lines.append(f"  - {{fileID: {gid + 1}}}")
    lines += [
        "  m_Father: {fileID: 0}",
        "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}",
        f"--- !u!114 &{ROOT_BINDER}",
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        f"  m_GameObject: {{fileID: {ROOT}}}",
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        f"  m_Script: {{fileID: 11500000, guid: {BINDER_GUID}, type: 3}}",
        "  m_Name: ",
        "  m_EditorClassIdentifier: ",
        "  baseColor: {r: 0.7, g: 0.72, b: 0.74, a: 1}",
        "  accentColor: {r: 0.85, g: 0.55, b: 0.15, a: 1}",
        "  stepColor: {r: 0.55, g: 0.56, b: 0.58, a: 1}",
    ]

    for name, pos, scale, gid in parts:
        tid, mfid, mrid = gid + 1, gid + 2, gid + 3
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
            "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}",
            f"  m_LocalPosition: {{x: {pos[0]}, y: {pos[1]}, z: {pos[2]}}}",
            f"  m_LocalScale: {{x: {scale[0]}, y: {scale[1]}, z: {scale[2]}}}",
            "  m_ConstrainProportionsScale: 0",
            "  m_Children: []",
            f"  m_Father: {{fileID: {ROOT_T}}}",
            "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}",
            f"--- !u!33 &{mfid}",
            "MeshFilter:",
            "  m_ObjectHideFlags: 0",
            "  m_CorrespondingSourceObject: {fileID: 0}",
            "  m_PrefabInstance: {fileID: 0}",
            "  m_PrefabAsset: {fileID: 0}",
            f"  m_GameObject: {{fileID: {gid}}}",
            f"  m_Mesh: {CUBE}",
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

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text("\n".join(lines) + "\n", encoding="utf-8")
    meta = OUT.with_suffix(".prefab.meta")
    meta.write_text(
        "fileFormatVersion: 2\n"
        "guid: d1e2f3a4b5c60718293a4b5c6d7e8f01\n"
        "PrefabImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n",
        encoding="utf-8",
    )
    print(f"wrote {OUT}")


if __name__ == "__main__":
    main()
