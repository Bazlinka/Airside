using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Airside.Editor
{
    /// <summary>
    /// Decision 0025 item 2 — after Unity imports authored FBX kits, bake them into
    /// <c>Resources/Airside/Prefabs</c> so <c>airside-prefab/&lt;key&gt;</c> serves
    /// Unity-imported meshes. StreamingAssets glTF remains the fallback until baked.
    /// </summary>
    public static class AirsideAuthoredFbxPrefabMenu
    {
        private const string MenuPath = "Airside/Art/Bake Authored FBX Prefabs";

        private static readonly string[] AuthoredFbxPaths =
        {
            "Assets/Airside/Art/Models/Aircraft/mdl_regional_turboprop_01_v06.fbx",
            "Assets/Airside/Art/Models/Aircraft/mdl_regional_turboprop_01_v05.fbx",
            "Assets/Airside/Art/Models/Buildings/mdl_terminal_regional_small_v05.fbx",
            "Assets/Airside/Art/Models/Aircraft/mdl_regional_turboprop_01_authored_v01.fbx",
            "Assets/Airside/Art/Models/Buildings/mdl_terminal_regional_small_authored_v01.fbx",
            "Assets/Airside/Art/Models/Buildings/mdl_hangar_small_v05.fbx",
            "Assets/Airside/Art/Models/Buildings/mdl_hangar_small_authored_v01.fbx",
            "Assets/Airside/Art/Models/Buildings/mdl_operations_shed_v05.fbx",
            "Assets/Airside/Art/Models/Buildings/mdl_operations_shed_authored_v01.fbx",
            "Assets/Airside/Art/Models/Vehicles/mdl_fuel_truck_small_v06.fbx",
            "Assets/Airside/Art/Models/Vehicles/mdl_baggage_tug_train_v06.fbx",
            "Assets/Airside/Art/Models/Vehicles/mdl_passenger_bus_apron_v06.fbx",
            "Assets/Airside/Art/Models/Vehicles/mdl_pushback_tug_v03.fbx",
            "Assets/Airside/Art/Models/Vehicles/mdl_parked_car_v02.fbx",
            "Assets/Airside/Art/Models/Vehicles/mdl_fuel_truck_small_v05.fbx",
            "Assets/Airside/Art/Models/Vehicles/mdl_baggage_tug_train_v05.fbx",
            "Assets/Airside/Art/Models/Vehicles/mdl_passenger_bus_apron_v05.fbx",
            "Assets/Airside/Art/Models/Vehicles/mdl_pushback_tug_v02.fbx",
            "Assets/Airside/Art/Models/Characters/mdl_ramp_crew_kit_v02.fbx",
            "Assets/Airside/Art/Models/Characters/mdl_ramp_crew_kit_v01.fbx",
            "Assets/Airside/Art/Models/Characters/mdl_passenger_kit_v02.fbx",
            "Assets/Airside/Art/Models/Characters/mdl_passenger_kit_v01.fbx",
            "Assets/Airside/Art/Models/Environment/mdl_eucalyptus_kit_v01.fbx",
            "Assets/Airside/Art/Models/Environment/mdl_kingscote_scrub_kit_v01.fbx",
            "Assets/Airside/Art/Models/Environment/mdl_kingscote_context_terrain_v01.fbx",
            "Assets/Airside/Art/Models/Props/mdl_airfield_fence_gate_kit_v02.fbx",
            "Assets/Airside/Art/Models/Props/mdl_airfield_fence_gate_kit_v01.fbx",
            "Assets/Airside/Art/Models/Props/mdl_terminal_forecourt_kit_v01.fbx",
            "Assets/Airside/Art/Models/Vehicles/mdl_fuel_truck_small_authored_v01.fbx",
            "Assets/Airside/Art/Models/Vehicles/mdl_baggage_tug_train_authored_v01.fbx",
            "Assets/Airside/Art/Models/Vehicles/mdl_passenger_bus_apron_authored_v01.fbx",
            "Assets/Airside/Art/Models/Props/mdl_service_equipment_kit_authored_v01.fbx",
            "Assets/Airside/Art/Models/Props/mdl_airfield_lighting_kit_authored_v01.fbx",
            "Assets/Airside/Art/Models/Props/mdl_airfield_props_kit_authored_v01.fbx",
        };

        [MenuItem(MenuPath)]
        public static void BakePrefabs()
        {
            var prefabDir = "Assets/Resources/Airside/Prefabs";
            Directory.CreateDirectory(
                Path.Combine(Application.dataPath, "Resources/Airside/Prefabs"));

            var log = new StringBuilder();
            var baked = 0;
            for (var i = 0; i < AuthoredFbxPaths.Length; i++)
            {
                var fbxPath = AuthoredFbxPaths[i];
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (model == null)
                {
                    log.AppendLine($"MISSING import: {fbxPath}");
                    continue;
                }

                var key = Path.GetFileNameWithoutExtension(fbxPath);
                var prefabPath = $"{prefabDir}/{key}.prefab";
                var instance = Object.Instantiate(model);
                instance.name = key;
                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                Object.DestroyImmediate(instance);
                baked++;
                log.AppendLine($"Baked {prefabPath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Airside.Presentation.AirsidePrefabAddressables.EnsureRegistered();
            var keys = Airside.Presentation.AirsidePrefabAddressables.RegisteredKeyCount;
            Debug.Log($"[Airside] Authored FBX prefab bake: {baked} prefabs. Addressables keys: {keys}\n{log}");
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "Airside Authored FBX",
                    $"Baked {baked}/{AuthoredFbxPaths.Length} Resources prefabs.\n" +
                    $"Addressables keys now: {keys}\n\n" +
                    "Play will prefer airside-prefab/<key> over StreamingAssets glTF.",
                    "OK");
            }
        }
    }
}
