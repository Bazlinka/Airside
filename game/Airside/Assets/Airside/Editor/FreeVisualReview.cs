using System;
using System.IO;
using System.Reflection;
using Airside.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Reusable native material/asset board under a single daylight reference.</summary>
public static class FreeVisualReview
{
    [MenuItem("Airside/Review/Free visual upgrade reference")]
    public static void BuildReferenceScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.72f,.78f,.86f);
        RenderSettings.ambientEquatorColor=new Color(.55f,.60f,.65f);
        RenderSettings.ambientGroundColor=new Color(.3f,.31f,.32f);
        RenderSettings.fog=false;
        var sun=new GameObject("Reference sun").AddComponent<Light>();
        sun.type=LightType.Directional;sun.intensity=1.15f;sun.shadows=LightShadows.Soft;
        sun.transform.rotation=Quaternion.Euler(45,-35,0);
        var camera=new GameObject("Reference camera").AddComponent<Camera>();
        camera.tag="MainCamera";camera.transform.position=new Vector3(18,16,20);camera.transform.LookAt(new Vector3(0,1,0));
        camera.orthographic=true;camera.orthographicSize=12;camera.backgroundColor=new Color(.22f,.27f,.30f);
        var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.name="Dry apron reference";
        ground.transform.localScale=new Vector3(3,1,3);
        ground.GetComponent<Renderer>().sharedMaterial=AirsideMaterialLibrary.CreateShared(new Color(.48f,.49f,.49f),AirsideMaterialLibrary.SurfaceKind.Concrete);
        var asphalt=GameObject.CreatePrimitive(PrimitiveType.Plane);asphalt.name="Scanned asphalt";
        asphalt.transform.position=new Vector3(-7,.01f,-5);asphalt.transform.localScale=new Vector3(.8f,1,.8f);
        asphalt.GetComponent<Renderer>().sharedMaterial=AirsideMaterialLibrary.CreateShared(new Color(.30f,.31f,.32f),AirsideMaterialLibrary.SurfaceKind.Asphalt);
        var wet=GameObject.CreatePrimitive(PrimitiveType.Plane);wet.name="Rain-wet concrete";
        wet.transform.position=new Vector3(5,.015f,-5);wet.transform.localScale=new Vector3(.8f,1,.8f);
        var wetMat=AirsideMaterialLibrary.Create(new Color(.48f,.49f,.49f),AirsideMaterialLibrary.SurfaceKind.Concrete);
        AirsideMaterialLibrary.ApplyWetness(wetMat,1,new Color(.48f,.49f,.49f),.18f,0,.46f,preferWetConcreteAlbedo:true);
        wet.GetComponent<Renderer>().sharedMaterial=wetMat;
        var build=typeof(AirsidePrototype).GetMethod("BuildServiceVehicle",BindingFlags.NonPublic|BindingFlags.Static);
        foreach(var entry in new[]{("Fuel truck","mdl_fuel_truck_small_v07",-6f),("Catering truck","mdl_catering_truck_v02",5f)})
        {
            var root=(Transform)build.Invoke(null,new object[]{entry.Item1,new Color(.80f,.64f,.16f),Vector3.one,"Models/Vehicles/"+entry.Item2+".gltf"});
            root.gameObject.SetActive(true);root.position=new Vector3(entry.Item3,.55f,2);
        }
        var parts=new[]{("tree_trunk_0",new Color(.39f,.36f,.29f)),("tree_foliage_0",new Color(.29f,.35f,.23f))};
        ArtGltfLoader.TryPlaceCombined("Models/Environment/mdl_local_foliage_v01.gltf",parts,new Vector3(-9,0,7),Quaternion.identity,"Sourced local tree",out _,Vector3.one*3);
        var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);sphere.name="Aircraft paint dielectric reference";
        sphere.transform.position=new Vector3(0,1.3f,5);sphere.transform.localScale=Vector3.one*2;
        sphere.GetComponent<Renderer>().sharedMaterial=AirsideMaterialLibrary.CreateShared(new Color(.13f,.46f,.49f),AirsideMaterialLibrary.SurfaceKind.AircraftSkin);
    }

    public static void Render()
    {
        ShaderUtil.allowAsyncCompilation=false;
        BuildReferenceScene();
        var output=Path.GetFullPath("../../work/free-visual-review");
        var args=Environment.GetCommandLineArgs();var index=Array.IndexOf(args,"-freeVisualOutput");
        if(index>=0 && index+1<args.Length) output=Path.GetFullPath(args[index+1]);
        Directory.CreateDirectory(output);
        var camera=Camera.main;var target=new RenderTexture(1600,1100,24){antiAliasing=4};camera.targetTexture=target;
        foreach(var angle in new[]{("reference",new Vector3(18,16,20),12f),("vehicles",new Vector3(11,5,12),8f)})
        {
            camera.transform.position=angle.Item2;camera.transform.LookAt(new Vector3(0,1,2));camera.orthographicSize=angle.Item3;
            camera.Render();camera.Render();RenderTexture.active=target;
            var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,angle.Item1+".png"),image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
        }
        camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(target);
        // Reference scene is rebuilt from code: generated meshes/materials need no saved .unity asset.
        Debug.Log("Free visual reference captured: "+output);
    }
}
