using System;
using Airside.Domain;

namespace Airside.Presentation
{
    /// <summary>How an aircraft's engines sound by size (ADR 0136).</summary>
    public enum EngineClass
    {
        Turboprop,
        RegionalJet,
        Narrowbody,
        Widebody
    }

    /// <summary>
    /// ADR 0136 — the engine voice as numbers: pitch, loudness, how far it carries and how muffled it is
    /// with distance. The three recorded beds (ADR 0087, level-matched in 0136) are shared across types;
    /// this is what makes an E190 sound lighter than a 737 and an A350 deeper and bigger. Pure.
    /// </summary>
    public static class EngineVoice
    {
        /// <summary>Source volume for a narrowbody at full power, for the −20 dBFS beds.</summary>
        public const float RunningVolume = 0.24f;

        public static EngineClass ClassOf(AircraftType type)
        {
            if (type == null || !AircraftCatalogue.TryFor(type, out var spec))
                return EngineClass.Narrowbody;
            if (spec.StandClass == StandClass.RegionalBay)
                return EngineClass.Turboprop;
            if (AircraftCatalogue.IsWidebody(type))
                return EngineClass.Widebody;
            return spec.Role != null && spec.Role.StartsWith("Regional jet", StringComparison.Ordinal)
                ? EngineClass.RegionalJet
                : EngineClass.Narrowbody;
        }

        public static float BasePitch(EngineClass kind) => kind switch
        {
            EngineClass.RegionalJet => 1.08f,
            EngineClass.Widebody => 0.86f,
            _ => 1f
        };

        public static float Gain(EngineClass kind) => kind switch
        {
            EngineClass.Turboprop => 0.8f,
            EngineClass.RegionalJet => 0.9f,
            EngineClass.Widebody => 1.15f,
            _ => 1f
        };

        /// <summary>How far away (metres) the engines stop being heard, and where they start to fall off.</summary>
        public static (float Min, float Max) Range(EngineClass kind) => kind switch
        {
            EngineClass.Turboprop => (12f, 260f),
            EngineClass.RegionalJet => (14f, 320f),
            EngineClass.Widebody => (24f, 520f),
            _ => (16f, 380f)
        };

        /// <summary>
        /// A steady ±3% offset per aircraft, from its id, so two identical jets in a queue do not sit on the
        /// same note and phase against each other.
        /// </summary>
        public static float Detune(string id)
        {
            if (string.IsNullOrEmpty(id))
                return 1f;
            unchecked
            {
                var hash = 2166136261u;
                foreach (var c in id)
                    hash = (hash ^ c) * 16777619u;
                return 0.97f + (hash % 1000) / 1000f * 0.06f;
            }
        }

        /// <summary>Pitch for 0..1 power and spool (spool bends the note down while starting or stopping).</summary>
        public static float Pitch(EngineClass kind, float power, float spool, float detune)
        {
            power = Clamp01(power);
            spool = Clamp01(spool);
            return BasePitch(kind) * detune * Lerp(0.94f, 1.08f, power) * Lerp(0.78f, 1f, spool);
        }

        public static float Volume(EngineClass kind, float power, float spool) =>
            RunningVolume * Gain(kind) * Lerp(0.35f, 1f, Clamp01(power)) * Lerp(0.35f, 1f, Clamp01(spool));

        /// <summary>
        /// Low-pass cutoff (Hz): close and at power the engine is bright; far off only the rumble carries,
        /// the way a distant jet sounds.
        /// </summary>
        public static float LowPassHz(float distance, float maxDistance, float power)
        {
            var closeness = maxDistance <= 0f ? 1f : 1f - Clamp01(distance / maxDistance);
            var cutoff = Lerp(900f, 22000f, (float)Math.Pow(closeness, 1.6));
            return cutoff * Lerp(0.7f, 1f, Clamp01(power));
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
