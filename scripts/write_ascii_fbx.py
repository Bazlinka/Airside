#!/usr/bin/env python3
"""Minimal ASCII FBX 7.4 mesh exporter for Airside authored kits.

Used when the `assimp` CLI is unavailable. Unity ModelImporter accepts ASCII FBX
with Geometry/Model/Connections nodes. Coordinates are metres, Y-up.
"""

from __future__ import annotations

import importlib.util
from pathlib import Path
from typing import Dict, Tuple

import numpy as np

_SCRIPTS = Path(__file__).resolve().parent
_ATTR_SPEC = importlib.util.spec_from_file_location(
    "mesh_attributes", _SCRIPTS / "mesh_attributes.py"
)
mesh_attributes = importlib.util.module_from_spec(_ATTR_SPEC)
assert _ATTR_SPEC.loader is not None
_ATTR_SPEC.loader.exec_module(mesh_attributes)


def write_ascii_fbx(
    path: Path,
    meshes: Dict[str, Tuple[np.ndarray, np.ndarray]],
) -> None:
    """Write a Kaydara ASCII FBX containing one Model+Geometry per mesh part."""
    lines: list[str] = [
        "; FBX 7.4.0 project file",
        "; Airside ASCII FBX exporter (no assimp)",
        "FBXHeaderExtension:  {",
        "    FBXHeaderVersion: 1003",
        "    FBXVersion: 7400",
        '    Creator: "Airside write_ascii_fbx.py"',
        "}",
        "GlobalSettings:  {",
        "    Version: 1000",
        "    Properties70:  {",
        '        P: "UpAxis", "int", "Integer", "",1',
        '        P: "UpAxisSign", "int", "Integer", "",1',
        '        P: "FrontAxis", "int", "Integer", "",2',
        '        P: "FrontAxisSign", "int", "Integer", "",1',
        '        P: "CoordAxis", "int", "Integer", "",0',
        '        P: "CoordAxisSign", "int", "Integer", "",1',
        # FBX expresses this value in centimetres per authored unit. Airside
        # geometry is authored in metres, so 100 prevents Unity from shrinking
        # every imported kit to one percent of its intended size.
        '        P: "UnitScaleFactor", "double", "Number", "",100',
        '        P: "OriginalUnitScaleFactor", "double", "Number", "",100',
        "    }",
        "}",
        "Documents:  {",
        "    Count: 1",
        '    Document: 1000000000, "Scene", "Scene" {',
        '        RootNode: 0',
        "    }",
        "}",
        "References:  {",
        "}",
        "Definitions:  {",
        "    Version: 100",
        f"    Count: {2 * len(meshes) + 1}",
        '    ObjectType: "GlobalSettings" {',
        "        Count: 1",
        "    }",
        '    ObjectType: "Model" {',
        f"        Count: {len(meshes)}",
        "    }",
        '    ObjectType: "Geometry" {',
        f"        Count: {len(meshes)}",
        "    }",
        "}",
        "Objects:  {",
    ]

    # Stable positive IDs for Unity.
    geo_base = 2000000000
    model_base = 3000000000
    connections: list[str] = []

    for i, (name, (verts, indices)) in enumerate(meshes.items()):
        safe = "".join(c if c.isalnum() or c in "_-" else "_" for c in name)
        gid = geo_base + i
        mid = model_base + i
        # Shared derivation, so the FBX and the glTF describe the same surface.
        # This used to average every adjacent face unconditionally, which rounded
        # off box corners that should have stayed hard, and wrote no UVs at all --
        # leaving Unity's Mikktspace tangent step with nothing to work from.
        verts, normals, uvs, _tangents, indices = mesh_attributes.build_attributes(
            verts, indices
        )
        verts = np.asarray(verts, dtype=np.float64)
        indices = np.asarray(indices, dtype=np.int64)

        # FBX polygon indices: last index of each triangle is bitwise NOT.
        poly: list[str] = []
        for t in range(0, len(indices), 3):
            a, b, c = int(indices[t]), int(indices[t + 1]), int(indices[t + 2])
            poly.extend([str(a), str(b), str(~c)])

        nflat = ", ".join(f"{v:.6f}" for xyz in normals for v in xyz)
        n_count = int(normals.size)
        uvflat = ", ".join(f"{v:.6f}" for uv in uvs for v in uv)
        uv_count = int(uvs.size)

        vflat = ", ".join(f"{v:.6f}" for xyz in verts for v in xyz)
        lines += [
            f'    Geometry: {gid}, "Geometry::{safe}", "Mesh" {{',
            f"        Vertices: *{verts.size} {{",
            f"            a: {vflat}",
            "        }",
            f"        PolygonVertexIndex: *{len(poly)} {{",
            f"            a: {', '.join(poly)}",
            "        }",
            "        GeometryVersion: 124",
            "        LayerElementNormal: 0 {",
            "            Version: 101",
            '            Name: ""',
            '            MappingInformationType: "ByControlPoint"',
            '            ReferenceInformationType: "Direct"',
            f"            Normals: *{n_count} {{",
            f"                a: {nflat}",
            "            }",
            "        }",
            "        LayerElementUV: 0 {",
            "            Version: 101",
            '            Name: "UVMap"',
            '            MappingInformationType: "ByControlPoint"',
            '            ReferenceInformationType: "Direct"',
            f"            UV: *{uv_count} {{",
            f"                a: {uvflat}",
            "            }",
            "        }",
            "        Layer: 0 {",
            "            Version: 100",
            "            LayerElement:  {",
            '                Type: "LayerElementNormal"',
            "                TypedIndex: 0",
            "            }",
            "            LayerElement:  {",
            '                Type: "LayerElementUV"',
            "                TypedIndex: 0",
            "            }",
            "        }",
            "    }",
            f'    Model: {mid}, "Model::{safe}", "Mesh" {{',
            "        Version: 232",
            "        Properties70:  {",
            '            P: "InheritType", "enum", "", "",1',
            '            P: "DefaultAttributeIndex", "int", "Integer", "",0',
            "        }",
            "        Shading: Y",
            '        Culling: "CullingOff"',
            "    }",
        ]
        connections.append(f'    C: "OO",{gid},{mid}')
        connections.append(f'    C: "OO",{mid},0')

    lines += [
        "}",
        "Connections:  {",
        *connections,
        "}",
    ]
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")
