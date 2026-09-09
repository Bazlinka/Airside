using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Airside.Presentation
{
    /// <summary>
    /// One scene scan after the airfield is built. Replaces repeated
    /// <c>GameObject.Find</c> / <c>FindObjectsByType</c> at Awake and in Update.
    /// </summary>
    public static class AirsideSceneIndex
    {
        private static readonly Dictionary<string, Transform> ByName = new(StringComparer.Ordinal);

        public static Renderer[] Renderers { get; private set; } = Array.Empty<Renderer>();

        public static Light[] Lights { get; private set; } = Array.Empty<Light>();

        public static void Remember(Transform transform)
        {
            if (transform == null)
                return;
            ByName.TryAdd(transform.gameObject.name, transform);
        }

        public static void Remember(GameObject go)
        {
            if (go != null)
                Remember(go.transform);
        }

        public static void Capture()
        {
            Renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            ByName.Clear();
            for (var i = 0; i < transforms.Length; i++)
            {
                var transform = transforms[i];
                if (transform == null)
                    continue;
                ByName.TryAdd(transform.gameObject.name, transform);
            }
        }

        public static Transform Find(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            return ByName.TryGetValue(name, out var transform) ? transform : null;
        }

        public static GameObject FindGameObject(string name) => Find(name)?.gameObject;

        public static Light FindLight(string name)
        {
            var transform = Find(name);
            return transform != null ? transform.GetComponent<Light>() : null;
        }
    }
}
