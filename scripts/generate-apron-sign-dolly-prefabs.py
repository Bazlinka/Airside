#!/usr/bin/env python3
"""Emit apron sign / dolly / windsock-pole Resources prefabs.

Decision 0025 item 1 — Unity YAML prefabs + AirsideRuntimeMaterialBinder.
"""

from __future__ import annotations

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


def emit_prefab(
    key: str,
    guid: str,
    parts: list[tuple[str, tuple[float, float, float], tuple[float, float, float], str]],
    *,
    base: tuple[float, float, float],
    accent: tuple[float, float, float],
    step: tuple[float, float, float],
) -> None:
    root, root_t, root_binder = 100001, 100002, 100003
    numbered = [(n, p, s, m, 200000 + i * 1000) for i, (n, p, s, m) in enumerate(parts)]

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

    for name, pos, scale, mesh, gid in numbered:
        tid, mfid, mrid = gid + 1, gid + 2, gid + 3
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
            "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}",
            f"  m_LocalPosition: {{x: {pos[0]}, y: {pos[1]}, z: {pos[2]}}}",
            f"  m_LocalScale: {{x: {scale[0]}, y: {scale[1]}, z: {scale[2]}}}",
            "  m_ConstrainProportionsScale: 0",
            "  m_Children: []",
            f"  m_Father: {{fileID: {root_t}}}",
            "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}",
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
    print(f"wrote {out}")


def main() -> None:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    emit_prefab(
        "mdl_airside_sign_v01",
        "f30415263748596a7b8c9d0e1f202122",
        [
            ("Sign post", (0.0, 1.1, 0.0), (0.12, 2.0, 0.12), "cube"),
            ("Sign back", (0.0, 1.35, 0.0), (0.12, 1.0, 1.2), "cube"),
            ("Sign face", (0.08, 1.35, 0.0), (0.04, 0.9, 1.1), "cube"),
            ("Sign accent bar", (0.1, 1.7, 0.0), (0.05, 0.08, 1.0), "cube"),
        ],
        base=(0.12, 0.35, 0.55),
        accent=(0.95, 0.95, 0.92),
        step=(0.35, 0.36, 0.38),
    )
    emit_prefab(
        "mdl_baggage_dolly_v01",
        "0415263748596a7b8c9d0e1f20212223",
        [
            ("Dolly bed", (0.0, 0.35, 0.0), (1.6, 0.35, 0.9), "cube"),
            ("Dolly rail L", (-0.7, 0.55, 0.0), (0.08, 0.55, 0.85), "cube"),
            ("Dolly rail R", (0.7, 0.55, 0.0), (0.08, 0.55, 0.85), "cube"),
            ("Dolly wheel FL", (0.55, 0.12, 0.35), (0.22, 0.22, 0.14), "cube"),
            ("Dolly wheel FR", (0.55, 0.12, -0.35), (0.22, 0.22, 0.14), "cube"),
            ("Dolly wheel RL", (-0.55, 0.12, 0.35), (0.22, 0.22, 0.14), "cube"),
            ("Dolly wheel RR", (-0.55, 0.12, -0.35), (0.22, 0.22, 0.14), "cube"),
            ("Dolly cargo", (0.0, 0.7, 0.0), (1.1, 0.4, 0.65), "cube"),
        ],
        base=(0.55, 0.35, 0.18),
        accent=(0.75, 0.55, 0.2),
        step=(0.15, 0.15, 0.16),
    )
    emit_prefab(
        "mdl_windsock_pole_v01",
        "15263748596a7b8c9d0e1f2021222324",
        [
            ("Windsock pole", (0.0, 1.6, 0.0), (0.12, 3.2, 0.12), "cylinder"),
            ("Windsock hinge", (0.0, 3.15, 0.0), (0.22, 0.22, 0.22), "cube"),
            ("Windsock base plate", (0.0, 0.04, 0.0), (0.55, 0.08, 0.55), "cube"),
        ],
        base=(0.75, 0.75, 0.72),
        accent=(0.55, 0.55, 0.52),
        step=(0.35, 0.36, 0.38),
    )


if __name__ == "__main__":
    main()
