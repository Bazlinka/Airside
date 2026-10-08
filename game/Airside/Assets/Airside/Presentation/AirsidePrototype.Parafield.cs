using Airside.Simulation;
using UnityEngine;

namespace Airside.Presentation
{
    public sealed partial class AirsidePrototype
    {
        private AirsideParafieldAirport _parafield;
        private readonly HudDrawList _parafieldHud=new();

        private bool WatchingParafield => _cameraController!=null && _cameraController.WatchingIndependentAirport;
        private Rect ParafieldPanelRect(float width,float height) => new(width*.5f-190,height-152,380,64);

        private void DrawParafieldWatchPanel(HudLayout layout)
        {
            if(!WatchingParafield || _menuOpen || _activeWorkspace!=HudWorkspace.None)return;
            var width=Screen.width/HudLayout.ScaleFor(Screen.width,Screen.height);
            var height=Screen.height/HudLayout.ScaleFor(Screen.width,Screen.height);
            var box=Box(ParafieldPanelRect(width,height));
            _parafieldHud.Clear();_parafieldHud.Surface(box);
            _parafieldHud.Text(new HudBox(box.X+14,box.Y+10,218,20),"Parafield · YPPF",16,HudTone.Default,HudTextStyle.Bold);
            _parafieldHud.Text(new HudBox(box.X+14,box.Y+34,218,16),"Independent training traffic",11,HudTone.Muted);
            _parafieldHud.Button(new HudBox(box.Right-130,box.Y+15,116,32),"ADELAIDE","parafield:return",HudButtonStyle.Secondary);
            if(_hudPainter.Draw(_parafieldHud)=="parafield:return")ResetView();
        }

        private void BuildParafield()
        {
            if(AirsideBareField.Enabled && _airfieldRoot!=null)
                _parafield=AirsideParafieldAirport.Create(_airfieldRoot,_clock);
        }

        private void UpdateParafield()
        {
            if(_parafield==null)return;
            _parafield.Tick(_preciseTime,AirportPresentationVisible,1f-CurrentDaylight);
        }

        private void WatchParafield()
        {
            if(_parafield==null || _cameraController==null)return;
            if(InCockpit) ExitCockpit(true);
            ResetFlightWorld();
            _selectedAircraftId=null;
            ClearAircraftSelection(releaseFollow:true);
            _activeWorkspace=HudWorkspace.None;
            _cameraController.WatchAirport(new Vector3((float)ParafieldLayout.CentreX,AirsideParafieldAirport.GroundY,
                (float)ParafieldLayout.CentreZ),2800,52,140);
            PlayUiClick();
        }
    }
}
