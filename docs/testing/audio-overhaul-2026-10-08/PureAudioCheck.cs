using System;
using System.Linq;
using Airside.Domain;
using Airside.Presentation;

static class Runner
{
    static int checks;
    static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    static void Main()
    {
        foreach (var spec in AircraftCatalogue.All)
        {
            var profile = AircraftAudioProfiles.For(spec.Type);
            Check(profile.Resource("core").EndsWith("core_v02"), spec.Id + " bank version");
            var mix = AircraftAudioMix.For(spec.Type, "audition", 1, 1, 1, 1, 1, 70, true);
            Check(float.IsFinite(mix.Power) && mix.Power > 0 && mix.Reverse > 0, spec.Id + " loaded/reverse");
            var cold = AircraftAudioMix.For(spec.Type, "audition", 0, 0, 0, 0, 0, 0, true);
            Check(cold.Idle + cold.Power + cold.Wheels == 0, spec.Id + " cold silence");
        }
        Check(AircraftAudioDynamics.Starter(.4f, true) > .1f, "starter rise");
        Check(AircraftAudioDynamics.Starter(.4f, false) == 0, "shutdown is not a starter");
        Check(AircraftAudioDynamics.Starter(1, true) == 0, "full engine no starter");
        Check(AircraftAudioDynamics.CorePitch(EngineClass.Narrowbody, .95f, 1) > AircraftAudioDynamics.CorePitch(EngineClass.Narrowbody, .26f, 1) * 1.8, "jet independent core sweep");
        Check(AircraftAudioDynamics.InteriorEngineGain(true,true) > AircraftAudioDynamics.InteriorEngineGain(true,false), "passenger insulation differs");
        Check(AircraftAudioDynamics.AirflowGain(400,true) > AircraftAudioDynamics.AirflowGain(20,true)*3, "airflow follows dynamic pressure proxy");
        foreach (var make in new Func<float[]>[]{HudSounds.UiClick,HudSounds.PanelWhoosh,HudSounds.PaChime,HudSounds.TierSting,HudSounds.ContractChime})
        {
            var x = make();
            Check(x.All(float.IsFinite) && x.Max(v=>Math.Abs(v)) <= .75f, "UI headroom");
            Check(Math.Abs(x[^1]) < .05f, "UI tail fade");
            Check(x.SequenceEqual(make()), "UI reproducible");
        }
        var click = HudSounds.UiClick();
        Check(click[0]==0 && click.Max(v=>Math.Abs(v)) <= .35f, "click attack and peak");
        var energy = click.Select(v=>(double)v*v).ToArray();
        Check(energy.Take(882).Sum() > energy.Skip(882).Sum()*3, "click is a tick not a beep");
        var limiter = new AudioPeakLimiter(48000);
        var samples = new float[48000];
        for(int i=0;i<samples.Length;i+=2) { samples[i]=(float)Math.Sin(i*.033)*3.5f;samples[i+1]=samples[i]*.4f; }
        limiter.Process(samples,2);
        Check(samples.All(float.IsFinite) && samples.Max(v=>Math.Abs(v)) <= AudioPeakLimiter.Ceiling+.00001f,"bus stress below ceiling");
        Check(Enumerable.Range(0,samples.Length/2).All(i=>Math.Abs(samples[i*2+1]-samples[i*2]*.4f)<.00001f),"linked stereo image");
        var ordinary = new[]{.2f,-.1f,.7f,-.6f};
        new AudioPeakLimiter(22050).Process(ordinary,2);
        Check(ordinary.SequenceEqual(new[]{.2f,-.1f,.7f,-.6f}),"normal audio transparent");
        var corrupt=new[]{float.NaN,float.PositiveInfinity,2f,-2f};limiter.Process(corrupt,2);
        Check(corrupt.All(float.IsFinite),"finite bus protection");
        var recovery=Enumerable.Repeat(.2f,96000).ToArray();limiter.Process(recovery,1);
        Check(Math.Abs(recovery[^1]-.2f)<.0001f,"release recovers gain");
        Console.WriteLine($"PASS: {checks} package-free aircraft mixer, UI waveform and stereo limiter assertions (no Unity runtime).");
    }
}
