using UnityEngine;

namespace Airside.Presentation
{
    /// <summary>
    /// Invisible collider used only for clicking an on-field aircraft. Kept as a
    /// dedicated child so aircraft meshes never need physics colliders.
    /// </summary>
    public sealed class AircraftPickProxy : MonoBehaviour
    {
        [SerializeField] private string _aircraftId;

        public string AircraftId
        {
            get => _aircraftId;
            set => _aircraftId = value;
        }

        /// <summary>Create or refresh the pick volume under an aircraft view.</summary>
        public static AircraftPickProxy Ensure(Transform aircraft, string aircraftId)
        {
            if (aircraft == null || string.IsNullOrEmpty(aircraftId))
                return null;

            var existing = aircraft.Find(AircraftPickRouting.ProxyChildName);
            AircraftPickProxy proxy;
            if (existing == null)
            {
                var go = new GameObject(AircraftPickRouting.ProxyChildName);
                go.transform.SetParent(aircraft, false);
                go.transform.localPosition = new Vector3(0f, AircraftPickRouting.ProxyCentreYMetres, 0f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;

                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(
                    AircraftPickRouting.ProxyWidthMetres,
                    AircraftPickRouting.ProxyHeightMetres,
                    AircraftPickRouting.ProxyLengthMetres);
                box.isTrigger = true;

                var layer = LayerMask.NameToLayer(AircraftPickRouting.PickLayerName);
                if (layer >= 0)
                    go.layer = layer;

                proxy = go.AddComponent<AircraftPickProxy>();
            }
            else
            {
                proxy = existing.GetComponent<AircraftPickProxy>()
                        ?? existing.gameObject.AddComponent<AircraftPickProxy>();
            }

            proxy.AircraftId = aircraftId;
            return proxy;
        }
    }
}
