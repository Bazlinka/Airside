using System;
using System.IO;
using Airside.Presentation;

// Usage: dotnet run -- <out-dir>
var dir = args.Length > 0 ? args[0] : ".";
Directory.CreateDirectory(dir);
void Write(string name, float[] samples)
{
    using var file = File.Create(Path.Combine(dir, name + ".wav"));
    using var w = new BinaryWriter(file);
    var bytes = samples.Length * 2;
    w.Write("RIFF"u8); w.Write(36 + bytes); w.Write("WAVE"u8);
    w.Write("fmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1);
    w.Write(HudSounds.SampleRate); w.Write(HudSounds.SampleRate * 2); w.Write((short)2); w.Write((short)16);
    w.Write("data"u8); w.Write(bytes);
    foreach (var s in samples)
        w.Write((short)Math.Round(Math.Clamp(s, -1f, 1f) * 32767));
    Console.WriteLine($"{name}.wav  {samples.Length / (float)HudSounds.SampleRate:0.00} s");
}

Write("flap-rattle", HudSounds.FlapRattle());
Write("cash-ching", HudSounds.CashChing());
Write("tier-sting", HudSounds.TierSting());
Write("contract-chime", HudSounds.ContractChime());
Write("panel-whoosh", HudSounds.PanelWhoosh());
Write("pa-chime", HudSounds.PaChime());
// Three passes of the loop, to hear the join.
var bed = HudSounds.ApronBed();
var three = new float[bed.Length * 3];
for (var i = 0; i < 3; i++) Array.Copy(bed, 0, three, i * bed.Length, bed.Length);
Write("apron-bed-x3", three);
