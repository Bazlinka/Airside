using System.Collections.Generic;
using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>Airport-only presentation sleeps outside its geographic frame; simulation stays live.</summary>
    public sealed class FlightWorldActorVisibility
    {
        private readonly Dictionary<Transform, bool> _hidden = new();

        public void Hide(Transform root)
        {
            if (root == null || _hidden.ContainsKey(root)) return;
            _hidden.Add(root, root.gameObject.activeSelf);
            root.gameObject.SetActive(false);
        }

        public void Restore()
        {
            foreach (var actor in _hidden)
                if (actor.Key != null) actor.Key.gameObject.SetActive(actor.Value);
            _hidden.Clear();
        }
    }
}
