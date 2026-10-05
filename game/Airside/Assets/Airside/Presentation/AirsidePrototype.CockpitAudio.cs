using UnityEngine;

namespace Airside.Presentation
{
    public sealed partial class AirsidePrototype
    {
        private AudioSource _cockpitAirflow;
        private AudioClip _cockpitAirflowClip;

        private void UpdateCockpitAirflow()
        {
            if (!InCockpit || _cockpitInterior == null) return;
            if (_audioMuted)
            {
                if (_cockpitAirflow != null) { _cockpitAirflow.Stop(); _cockpitAirflow.volume = 0f; }
                return;
            }
            if (_cockpitAirflow == null)
            {
                var samples = CockpitAirflow.Samples();
                _cockpitAirflowClip = AudioClip.Create("Cockpit filtered airflow", samples.Length,
                    1, CockpitAirflow.SampleRate, false);
                _cockpitAirflowClip.SetData(samples, 0);
                _cockpitAirflow = _cockpitInterior.gameObject.AddComponent<AudioSource>();
                _cockpitAirflow.playOnAwake = false;
                _cockpitAirflow.loop = true;
                _cockpitAirflow.spatialBlend = 0f;
                _cockpitAirflow.dopplerLevel = 0f;
                _cockpitAirflow.priority = 145;
                _cockpitAirflow.volume = 0f;
                _cockpitAirflow.clip = _cockpitAirflowClip;
            }
            _cockpitAirflow.volume = AircraftAudioMix.SmoothTowards(_cockpitAirflow.volume,
                CockpitAirflow.Volume(_cockpitGroundKnots), Time.unscaledDeltaTime, 0.75f);
            if (!_cockpitAirflow.isPlaying && CanStartAudio(_cockpitAirflow)) _cockpitAirflow.Play();
        }

        private void ReleaseCockpitAirflow()
        {
            if (_cockpitAirflow != null)
            {
                _cockpitAirflow.Stop();
                _cockpitAirflow.clip = null;
                DestroyPresentationObject(_cockpitAirflow);
            }
            if (_cockpitAirflowClip != null) DestroyPresentationObject(_cockpitAirflowClip);
            _cockpitAirflow = null;
            _cockpitAirflowClip = null;
        }
    }
}
