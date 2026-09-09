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

        public static Transform[] Get(Transform root)
        {
            if (root == null)
                return Array.Empty<Transform>();

            var id = root.GetInstanceID();
            if (Cache.TryGetValue(id, out var cached) && cached != null)
                return cached;

            cached = root.GetComponentsInChildren<Transform>(true);
            Cache[id] = cached;
            return cached;
        }

        public static void Forget(Transform root)
        {
            if (root == null)
                return;
            Cache.Remove(root.GetInstanceID());
        }

        public static bool HasName(Transform root, string name)
        {
            if (root == null || string.IsNullOrEmpty(name))
                return false;
            foreach (var child in Get(root))
            {
                if (child != null && child != root && child.name == name)
                    return true;
            }

            return false;
        }

        public static Transform FindContains(Transform root, string fragment)
        {
            if (root == null || string.IsNullOrEmpty(fragment))
                return null;
            foreach (var child in Get(root))
            {
                if (child != null && child.name.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
                    return child;
            }

            return null;
        }
    }
}
