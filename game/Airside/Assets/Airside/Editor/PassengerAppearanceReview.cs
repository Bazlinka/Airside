using System;
using System.IO;
using System.Reflection;
using Airside.Domain;
using Airside.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

public static class PassengerAppearanceReview
{
    public static void Run()
    {
        System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(AirsidePrototype).TypeHandle);
        var args=Environment.GetCommandLineArgs();var index=Array.IndexOf(args,"-passengerReviewOutput");
        var output=index>=0 && index+1<args.Length ? args[index+1] : "work/passenger-review";
        Directory.CreateDirectory(output);
        foreach(var p in PassengerCabinProfile.All)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.7f,.77f,.85f);
            RenderSettings.ambientEquatorColor=new Color(.52f,.58f,.64f);
            RenderSettings.ambientGroundColor=new Color(.27f,.29f,.3f);
            RenderSettings.fog=false;
            var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.3f;
            sun.transform.rotation=Quaternion.Euler(35,-25,0);
            var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.transform.localScale=Vector3.one*1000;
            var groundMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            groundMaterial.color=new Color(.38f,.46f,.30f);ground.GetComponent<Renderer>().sharedMaterial=groundMaterial;
            AircraftType.TryFromId(p.TypeId,out var type);
            var aircraft=(Transform)typeof(AirsidePrototype).GetMethod("BuildAircraftForType",BindingFlags.Static|BindingFlags.NonPublic)
                .Invoke(null,new object[]{p.TypeId+" review",type,Color.blue,null});
            aircraft.position=new Vector3(0,150,0);aircraft.rotation=Quaternion.Euler(-3,0,8);
            foreach(var lod in aircraft.GetComponentsInChildren<LODGroup>())lod.ForceLOD(0);
            var cabin=PassengerCabinInterior.Build(aircraft,type,false);cabin.Enter();
            var camera=new GameObject("Camera").AddComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.57f,.74f,.85f);camera.fieldOfView=65;camera.nearClipPlane=.035f;camera.farClipPlane=55000;
            var target=new RenderTexture(1000,650,24);camera.targetTexture=target;
            foreach(var right in new[]{false,true})
            {
                cabin.SelectSide(right);camera.transform.SetPositionAndRotation(cabin.Seat.position,cabin.Seat.rotation);
                Capture(camera,target,Path.Combine(output,p.TypeId+(right ? "_right.png" : "_left.png")));
            }
            camera.transform.SetPositionAndRotation(cabin.Seat.position,aircraft.rotation);
            Capture(camera,target,Path.Combine(output,p.TypeId+"_cabin.png"));
            cabin.Leave();
            var profile=AircraftVisualProfiles.For(type);var centre=aircraft.TransformPoint(profile.VisualCentreOffsetMetres+Vector3.up*profile.PickCentreYMetres);
            var radius=Mathf.Max(28,Mathf.Max(profile.PickSizeMetres.x,profile.PickSizeMetres.z)*1.15f);
            camera.transform.position=centre+Quaternion.Euler(18,-35,0)*Vector3.back*radius;
            camera.transform.LookAt(centre);camera.fieldOfView=48;
            Capture(camera,target,Path.Combine(output,p.TypeId+"_outside.png"));
            camera.targetTexture=null;Object.DestroyImmediate(target);Object.DestroyImmediate(groundMaterial);
        }
        Debug.Log("Passenger/exterior native review complete: "+output);
    }
    private static void Capture(Camera camera,RenderTexture target,string path)
    {
        camera.Render();camera.Render();RenderTexture.active=target;
        var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
        Object.DestroyImmediate(image);RenderTexture.active=null;
    }
}
