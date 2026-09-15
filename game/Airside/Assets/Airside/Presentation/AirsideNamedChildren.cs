using System;
using System.Collections.Generic;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Caches <c>GetComponentsInChildren</c> for presentation hierarchies that
    /// do not grow after spawn (aircraft, GSE). Per-frame scans were allocating
    /// a Transform array for every propeller / gear / glow pass.
    /// </summary>
    public static class AirsideNamedChildren
    {
        private static readonly Dictionary<int, Transform[]> Cache = new();
        private static readonly Dictionary<int, string[]> NameCache = new();

        public static Transform[] Get(Transform root)
        {
            if (root == null)
                return Array.Empty<Transform>();

            var id = root.GetInstanceID();
            if (Cache.TryGetValue(id, out var cached) && cached != null)
                return cached;

            cached = root.GetComponentsInChildren<Transform>(true);
            Cache[id] = cached;
            NameCache[id] = CaptureNames(cached);
            return cached;
        }

        /// <summary>
        /// Names of <see cref="Get"/>'s transforms, index for index, read once. Every
        /// <c>Transform.name</c> read allocates a new string, and the per-frame gear, light,
        /// door and propeller passes read hundreds of them per aircraft.
        /// </summary>
        public static string[] Names(Transform root)
        {
            if (root == null)
                return Array.Empty<string>();
            var children = Get(root);
            var id = root.GetInstanceID();
            if (!NameCache.TryGetValue(id, out var names) || names == null || names.Length != children.Length)
                NameCache[id] = names = CaptureNames(children);
            return names;
        }

        private static string[] CaptureNames(Transform[] children)
        {
            var names = new string[children.Length];
            for (var i = 0; i < children.Length; i++)
                names[i] = children[i] != null ? children[i].name : string.Empty;
            return names;
        }

        public static void Forget(Transform root)
        {
            if (root == null)
                return;
            var id = root.GetInstanceID();
            Cache.Remove(id);
            NameCache.Remove(id);
        }

        public static bool HasName(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
                return false;
            var children = Get(root);
            var names = Names(root);
            for (var i = 0; i < children.Length; i++)
            {
                if (children[i] != null && children[i] != root && names[i] == name)
                    return true;
            }

            return false;
        }

        public static Transform FindContains(Transform root, string fragment)
        {
            if (root == null || string.IsNullOrEmpty(fragment))
                return null;
            var children = Get(root);
            var names = Names(root);
            for (var i = 0; i < children.Length; i++)
            {
                if (children[i] != null && names[i].IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                    return children[i];
            }

            return null;
        }
    }
}
