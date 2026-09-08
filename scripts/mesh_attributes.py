"""Shared vertex-attribute derivation for the Airside procedural kits.

Every generator funnels its meshes through ``pack_gltf`` in
``generate-batch-c-models.py``, which historically wrote ``POSITION`` and
nothing else. The runtime then had to guess the rest: ``ArtGltfLoader`` called
``Mesh.RecalculateNormals`` (hard facets on every lathed hull and cylinder,
because Unity's recalculation never smooths) and projected throwaway UVs across
each part's bounding box (so texel density changed from part to part, and no
normal map could work at all without tangents).

Deriving the attributes here fixes every kit at once -- aircraft, buildings,
vehicles, props, characters and environment -- because they all share the one
writer.

The three passes:

* **Normals** are smoothed by angle. Faces meeting under ``smoothing_angle``
  average together, so a fuselage loft or a wheel reads round; faces meeting
  over it stay split, so a box keeps its crisp edges. Positions are welded
  first, because the box builders emit four fresh corners per face and nothing
  would ever share a normal otherwise.
* **UVs** are a per-face planar projection along the dominant normal axis,
  measured in *metres* rather than normalised across the part. That is what
  makes texel density uniform across the whole airport: a metre of corrugated
  wall is the same number of texels on the hangar as on the ops shed.
* **Tangents** follow from those UVs, so ``_BumpMap`` finally has a frame to
  sample in.

Corners are split and re-welded around the result, so a vertex shared by two
smoothing groups (or two projection axes) is duplicated rather than averaged
into mush.
"""

from __future__ import annotations

import numpy as np

# Weld tolerance in metres. The generators author on a millimetre-ish grid, so
# 0.1 mm is far below anything intentional and far above float32 noise.
_WELD_QUANTUM = 1e-4

# Faces meeting under this angle share a normal. 40 degrees keeps a 30-degree
# lathe segment smooth while leaving a 90-degree box corner hard.
DEFAULT_SMOOTHING_ANGLE = 40.0

# One UV unit per metre. Runtime tiling then reads as "tiles per metre" and is
# consistent between parts, instead of "tiles per part" as it was before.
DEFAULT_TEXELS_PER_METRE = 1.0


def _face_normals(tri: np.ndarray) -> np.ndarray:
    """Unit normal per triangle, from an (F, 3, 3) corner array."""
    n = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0])
    length = np.linalg.norm(n, axis=1, keepdims=True)
    # A degenerate triangle has no direction to offer; point it up so it never
    # poisons the average of its neighbours with a zero vector.
    degenerate = length[:, 0] <= 1e-12
    n[degenerate] = (0.0, 1.0, 0.0)
    length[degenerate] = 1.0
    return n / length


def _face_areas(tri: np.ndarray) -> np.ndarray:
    """Triangle area, used to weight normals so tessellation cannot skew them."""
    return 0.5 * np.linalg.norm(
        np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0]), axis=1
    )


def _smoothed_normals(
    corners: np.ndarray, face_normal: np.ndarray, face_area: np.ndarray, angle: float
) -> np.ndarray:
    """Per-corner normals, averaging only across faces within ``angle``."""
    face_count = len(face_normal)
    threshold = np.cos(np.radians(angle))

    # Group corners that sit on the same position, whichever face they came from.
    keys = np.round(corners.reshape(-1, 3) / _WELD_QUANTUM).astype(np.int64)
    _, weld_id = np.unique(keys, axis=0, return_inverse=True)
    weld_id = weld_id.reshape(face_count, 3)

    # Bucket faces by welded position so each corner only tests its true neighbours.
    order = np.argsort(weld_id.ravel(), kind="stable")
    grouped_faces = order // 3
    group_of = weld_id.ravel()[order]
    starts = np.searchsorted(group_of, np.arange(group_of[-1] + 1), side="left")
    ends = np.searchsorted(group_of, np.arange(group_of[-1] + 1), side="right")

    out = np.empty((face_count, 3, 3), dtype=np.float64)
    weighted = face_normal * face_area[:, None]
    for f in range(face_count):
        own = face_normal[f]
        for c in range(3):
            g = weld_id[f, c]
            neighbours = grouped_faces[starts[g] : ends[g]]
            # Only faces facing broadly the same way join the average; the rest
            # belong to a different smoothing group and must stay separate.
            share = neighbours[face_normal[neighbours] @ own >= threshold]
            acc = weighted[share].sum(axis=0)
            norm = np.linalg.norm(acc)
            out[f, c] = own if norm <= 1e-12 else acc / norm
    return out


def _planar_uvs(
    corners: np.ndarray, face_normal: np.ndarray, texels_per_metre: float
) -> np.ndarray:
    """Per-face planar projection along the dominant normal axis, in metres.

    The axis pair is chosen so the projection never collapses, and is ordered by
    the sign of the normal so the winding stays consistent -- a mirrored UV shell
    would flip tangent handedness and light the part inside out.
    """
    dominant = np.argmax(np.abs(face_normal), axis=1)
    sign = np.sign(face_normal[np.arange(len(face_normal)), dominant])
    sign[sign == 0] = 1.0

    # X-dominant -> project ZY, Y-dominant -> XZ, Z-dominant -> XY.
    u_axis = np.select([dominant == 0, dominant == 1], [2, 0], default=0)
    v_axis = np.select([dominant == 0, dominant == 1], [1, 2], default=1)

    idx = np.arange(len(face_normal))[:, None]
    u = corners[idx, np.arange(3)[None, :], u_axis[:, None]] * sign[:, None]
    v = corners[idx, np.arange(3)[None, :], v_axis[:, None]]
    return np.stack([u, v], axis=2) * texels_per_metre


def _tangents(
    corners: np.ndarray, uvs: np.ndarray, normals: np.ndarray
) -> np.ndarray:
    """glTF VEC4 tangents (xyz + handedness) derived from the UV gradient."""
    e1 = corners[:, 1] - corners[:, 0]
    e2 = corners[:, 2] - corners[:, 0]
    d1 = uvs[:, 1] - uvs[:, 0]
    d2 = uvs[:, 2] - uvs[:, 0]

    det = d1[:, 0] * d2[:, 1] - d2[:, 0] * d1[:, 1]
    safe = np.abs(det) > 1e-12
    r = np.zeros_like(det)
    r[safe] = 1.0 / det[safe]
    tangent = (e1 * d2[:, 1, None] - e2 * d1[:, 1, None]) * r[:, None]
    bitangent = (e2 * d1[:, 0, None] - e1 * d2[:, 0, None]) * r[:, None]

    # A face with no usable UV gradient still needs some frame; any vector
    # perpendicular to the normal will do.
    fallback = np.cross(normals[:, 0], (0.0, 0.0, 1.0))
    weak = np.linalg.norm(fallback, axis=1) < 1e-6
    fallback[weak] = np.cross(normals[weak, 0], (0.0, 1.0, 0.0))
    tangent[~safe] = fallback[~safe]
    bitangent[~safe] = np.cross(normals[~safe, 0], fallback[~safe])

    per_corner = np.repeat(tangent[:, None, :], 3, axis=1)
    # Gram-Schmidt against the (already smoothed) normal, so the frame stays
    # orthonormal even where neighbouring faces pulled the normal around.
    per_corner -= normals * np.sum(per_corner * normals, axis=2, keepdims=True)
    length = np.linalg.norm(per_corner, axis=2, keepdims=True)
    length[length <= 1e-12] = 1.0
    per_corner /= length

    # glTF stores handedness in .w: -1 when the UV shell is mirrored, so the
    # shader reconstructs the bitangent as cross(N, T) * w.
    reference = np.repeat(bitangent[:, None, :], 3, axis=1)
    handedness = np.where(
        np.sum(np.cross(normals, per_corner) * reference, axis=2) < 0.0, -1.0, 1.0
    )
    return np.concatenate([per_corner, handedness[:, :, None]], axis=2)


def build_attributes(
    verts: np.ndarray,
    indices: np.ndarray,
    smoothing_angle: float = DEFAULT_SMOOTHING_ANGLE,
    texels_per_metre: float = DEFAULT_TEXELS_PER_METRE,
) -> tuple[np.ndarray, np.ndarray, np.ndarray, np.ndarray, np.ndarray]:
    """Derive normals, UVs and tangents for one mesh.

    Returns ``(positions, normals, uvs, tangents, indices)``, re-welded so that
    identical corners share a vertex and differing ones do not. The caller gets
    float32 attributes and an int64 index array; picking the glTF index width is
    the writer's job.
    """
    v = np.asarray(verts, dtype=np.float64).reshape(-1, 3)
    i = np.asarray(indices, dtype=np.int64).ravel()
    # Trailing indices that do not complete a triangle would misalign every
    # later face, so drop them rather than reshaping into garbage.
    i = i[: (len(i) // 3) * 3]
    if len(i) == 0:
        empty = np.zeros((0, 3), dtype=np.float32)
        return (
            empty,
            empty,
            np.zeros((0, 2), dtype=np.float32),
            np.zeros((0, 4), dtype=np.float32),
            np.zeros(0, dtype=np.int64),
        )

    corners = v[i].reshape(-1, 3, 3)
    face_normal = _face_normals(corners)
    face_area = _face_areas(corners)

    normals = _smoothed_normals(corners, face_normal, face_area, smoothing_angle)
    uvs = _planar_uvs(corners, face_normal, texels_per_metre)
    tangents = _tangents(corners, uvs, normals)

    # Re-weld: a corner is only shared when position, normal, UV and tangent all
    # agree, which keeps smoothing-group and projection-axis seams intact.
    flat = np.concatenate(
        [
            corners.reshape(-1, 3),
            normals.reshape(-1, 3),
            uvs.reshape(-1, 2),
            tangents.reshape(-1, 4),
        ],
        axis=1,
    )
    keys = np.round(flat / _WELD_QUANTUM).astype(np.int64)
    _, first, inverse = np.unique(keys, axis=0, return_index=True, return_inverse=True)

    return (
        flat[first, 0:3].astype(np.float32),
        flat[first, 3:6].astype(np.float32),
        flat[first, 6:8].astype(np.float32),
        flat[first, 8:12].astype(np.float32),
        inverse.astype(np.int64).ravel(),
    )
