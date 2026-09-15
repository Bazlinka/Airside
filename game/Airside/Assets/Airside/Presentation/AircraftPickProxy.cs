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
            GameObject go;
            if (existing == null)
            {
                go = new GameObject(AircraftPickRouting.ProxyChildName);
                go.transform.SetParent(aircraft, false);
                proxy = go.AddComponent<AircraftPickProxy>();
            }
            else
            {
                go = existing.gameObject;
                proxy = existing.GetComponent<AircraftPickProxy>();
                if (proxy == null)
                    proxy = existing.gameObject.AddComponent<AircraftPickProxy>();
            }

            // AIR-005 and later aircraft carry their actual silhouette metrics. Old
            // ATR-based views deliberately preserve the previous forgiving default.
            var profile = aircraft.GetComponent<AircraftVisualProfileComponent>();
            var size = profile != null
                ? profile.PickSizeMetres
                : new Vector3(
                    AircraftPickRouting.ProxyWidthMetres,
                    AircraftPickRouting.ProxyHeightMetres,
                    AircraftPickRouting.ProxyLengthMetres);
            var centreY = profile != null
                ? profile.PickCentreYMetres
                : AircraftPickRouting.ProxyCentreYMetres;

            var centre = profile != null
                ? profile.VisualCentreOffsetMetres
                : Vector3.zero;
            go.transform.localPosition = new Vector3(centre.x, centreY, centre.z);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            var box = go.GetComponent<BoxCollider>();
            if (box == null)
                box = go.AddComponent<BoxCollider>();
            box.size = size;
            box.isTrigger = true;
            var layer = LayerMask.NameToLayer(AircraftPickRouting.PickLayerName);
            if (layer >= 0)
                go.layer = layer;
            proxy.AircraftId = aircraftId;
            return proxy;
        }
    }
}
