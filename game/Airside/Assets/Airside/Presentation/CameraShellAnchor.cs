using System.Collections.Generic;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Keeps camera-centred scenery (star shell, sun and moon discs, stratus deck, horizon band) locked to
    /// the camera. That scenery is placed from <c>AirsidePrototype.Update</c>, but the camera moves in
    /// <c>AirsideCameraController.LateUpdate</c>, so a position taken in Update is one frame stale and the
    /// stars and sky slid against the view whenever the camera panned, orbited or zoomed. This runs after
    /// the camera has moved and re-applies each registered offset to its final position.
    /// </summary>
    [DefaultExecutionOrder(500)]
    public sealed class CameraShellAnchor : MonoBehaviour
    {
        private struct Entry
        {
            public Transform Target;
            public Vector3 Offset;
            public bool AbsoluteHeight;
        }

        private readonly List<Entry> _entries = new();
        private readonly Dictionary<Transform, int> _index = new();

        /// <summary>The position an anchored object takes for a camera at <paramref name="camera"/>.</summary>
        public static Vector3 Resolve(Vector3 camera, Vector3 offset, bool absoluteHeight) =>
            new Vector3(camera.x + offset.x, absoluteHeight ? offset.y : camera.y + offset.y, camera.z + offset.z);

        /// <summary>
        /// Anchors <paramref name="target"/> at <paramref name="offset"/> from the camera and places it now.
        /// With <paramref name="absoluteHeight"/> the offset's Y is a world height, so only horizontal
        /// travel is followed.
        /// </summary>
        public static void Place(Camera camera, Transform target, Vector3 offset, bool absoluteHeight = false)
        {
            if (camera == null || target == null)
                return;
            var anchor = camera.GetComponent<CameraShellAnchor>();
            if (anchor == null)
                anchor = camera.gameObject.AddComponent<CameraShellAnchor>();
            anchor.Set(target, offset, absoluteHeight);
            target.position = Resolve(camera.transform.position, offset, absoluteHeight);
        }

        private void Set(Transform target, Vector3 offset, bool absoluteHeight)
        {
            var entry = new Entry { Target = target, Offset = offset, AbsoluteHeight = absoluteHeight };
            if (_index.TryGetValue(target, out var i))
                _entries[i] = entry;
            else
            {
                _index[target] = _entries.Count;
                _entries.Add(entry);
            }
        }

        private void LateUpdate()
        {
            var camera = transform.position;
            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (entry.Target != null)
                    entry.Target.position = Resolve(camera, entry.Offset, entry.AbsoluteHeight);
            }
        }
    }
}
