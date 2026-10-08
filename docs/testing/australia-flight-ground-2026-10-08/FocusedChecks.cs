using System;
using System.IO;
using Airside.Presentation;
class Program
{
 static int Main(string[] args)
 {
  var root=args[0];var count=0;
  void Check(bool condition,string name){if(!condition)throw new Exception(name);count++;}
  foreach(var path in Directory.GetFiles(root,"dem_*australia_v01.bin")) Check(FlightWorldHeights.Parse(File.ReadAllBytes(path))!=null,path);
  foreach(var path in Directory.GetFiles(root,"dem_approach_*.bin")) Check(FlightWorldHeights.Parse(File.ReadAllBytes(path))!=null,path);
  foreach(var path in Directory.GetFiles(root,"landcover_*australia_v01.bin")) Check(FlightWorldLandCover.Parse(File.ReadAllBytes(path))!=null,path);
  foreach(var path in Directory.GetFiles(root,"landcover_approach_*.bin")) Check(FlightWorldLandCover.Parse(File.ReadAllBytes(path))!=null,path);
  foreach(var d in Airside.Domain.DestinationCatalogue.Australia) Check(FlightWorldGrid.Covered(d.Latitude,d.Longitude),d.Code);
  Check(RegionalRunways.All.Length>=18,"Australia runway coverage");
  foreach(var r in RegionalRunways.All)
  {
   RegionalFlightPath.Landing(r,0,0,0,out var x,out var y,out var z);
   Check(Math.Abs(y-r.Elevation-RegionalFlightPath.AirsideFlightPathDatum)<.001,r.Code+" field elevation");
   Check(FlightWorldDetail.RunwayDistance(x,z,r)<.01,r.Code+" touchdown on strip");
   for(var i=0;i<=20;i++)
   {
    x=r.Ax+(r.Bx-r.Ax)*i/20;z=r.Az+(r.Bz-r.Az)*i/20;
    var cx=Math.Floor(x/250)*250;var cz=Math.Floor(z/250)*250;
    for(var dx=0;dx<=1;dx++)for(var dz=0;dz<=1;dz++) Check(Math.Abs(RegionalRunways.Ground(cx+dx*250,cz+dz*250,900)-r.Elevation)<.001,r.Code+" approach cells flat");
   }
  }
  Check(!FlightWorldDetail.Cruise(4400,false),"cruise entry threshold");Check(FlightWorldDetail.Cruise(3600,true),"cruise hysteresis");
  Check(!FlightWorldDetail.Cruise(3499,true),"cruise exit");
  var detail=0;for(var x=-8;x<=8;x++)for(var z=-8;z<=8;z++)if(FlightWorldDetail.ApproachTile(x,z,0,0))detail++;
  Check(detail==9,"bounded detailed ring");
  var national=FlightWorldHeights.Parse(File.ReadAllBytes(Path.Combine(root,"dem_australia_v01.bin")));
  Check(!national.TryHeight(-60,130,out _),"no edge clamp");Check(!national.TryHeight(double.NaN,130,out _),"nonfinite bounds");
  Console.WriteLine("PASS "+count+" focused checks: actual SATG/SALC parsing, all Australian destination coverage, mapped runway touchdown/datum/approach cells, cruise hysteresis, nine-tile detail bound.");
  return 0;
 }
}
