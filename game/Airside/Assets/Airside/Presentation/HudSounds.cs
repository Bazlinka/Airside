using System;

namespace Airside.Presentation
{
    /// <summary>
    /// ADR 0133 — the HUD's moment sounds, synthesised like the game's other procedural audio (no files,
    /// nothing to register). Pure: each returns mono samples in −1..1 that the runtime wraps in an
    /// AudioClip. Deterministic, so a test can pin their shape.
    /// </summary>
    public static class HudSounds
    {
        public const int SampleRate = 22050;

        /// <summary>A split-flap board settling: a burst of small clacks that slow and quieten.</summary>
        public static float[] FlapRattle()
        {
            var samples = new float[(int)(SampleRate * 0.55f)];
            var noise = new Noise(7);
            var t = 0f;
            var gap = 0.018f;
            var index = 0;
            while (t < 0.5f)
            {
                var start = (int)(t * SampleRate);
                var loud = 0.55f * (1f - t / 0.55f);
                // One clack: a few ms of filtered noise with a hard edge.
                for (var i = 0; i < SampleRate * 0.006f && start + i < samples.Length; i++)
                {
                    var decay = (float)Math.Exp(-i / (SampleRate * 0.0012f));
                    samples[start + i] += noise.Next() * loud * decay;
                }

                t += gap;
                gap *= 1.07f + 0.02f * (index++ % 3);
            }

            return Normalise(samples, 0.7f);
        }

        /// <summary>Money landing: two bright bell partials, a till "ching".</summary>
        public static float[] CashChing()
        {
            var samples = new float[(int)(SampleRate * 0.6f)];
            for (var i = 0; i < samples.Length; i++)
            {
                var time = i / (float)SampleRate;
                var attack = Math.Min(1f, time / 0.004f);
                var body = (float)(Math.Sin(2 * Math.PI * 1567.98 * time) * Math.Exp(-time * 7.0)
                                   + 0.6 * Math.Sin(2 * Math.PI * 2093.0 * time) * Math.Exp(-time * 9.0)
                                   + 0.25 * Math.Sin(2 * Math.PI * 3135.96 * time) * Math.Exp(-time * 14.0));
                // The second strike a beat later, a fifth higher, like a register bell.
                var second = time < 0.07 ? 0f
                    : (float)(0.7 * Math.Sin(2 * Math.PI * 2349.32 * (time - 0.07)) * Math.Exp(-(time - 0.07) * 8.0));
                samples[i] = (body + second) * attack;
            }

            return Normalise(samples, 0.6f);
        }

        /// <summary>A new tier or the finale: a rising three-note major arpeggio with a held top.</summary>
        public static float[] TierSting() => Arpeggio(new[] { 523.25, 659.25, 783.99, 1046.5 }, 0.11, 1.4f, 0.65f);

        /// <summary>A finished contract: a shorter two-note "done".</summary>
        public static float[] ContractChime() => Arpeggio(new[] { 783.99, 1046.5 }, 0.12, 0.9f, 0.55f);

        /// <summary>A panel opening: a short, soft rising air whoosh (ADR 0136).</summary>
        public static float[] PanelWhoosh()
        {
            var samples = new float[(int)(SampleRate * 0.28f)];
            var noise = new Noise(11);
            var low = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                var t = i / (float)samples.Length;
                // A one-pole low-pass whose corner rises: the whoosh brightens as the sheet arrives.
                var alpha = 0.02f + 0.18f * t;
                low += alpha * (noise.Next() - low);
                var envelope = (float)Math.Sin(Math.PI * t) * (1f - t * 0.4f);
                samples[i] = low * envelope;
            }

            return Normalise(samples, 0.35f);
        }

        /// <summary>The terminal PA's three-tone chime before an announcement: a gentle "bing-bong-bing".</summary>
        public static float[] PaChime() => Arpeggio(new[] { 659.25, 523.25, 783.99 }, 0.42, 2.2f, 0.5f);

        /// <summary>
        /// ADR 0136 — the working apron under everything: distant rumble, a faint mains hum from ground
        /// power, and now and then a far-off reversing beep. A 12 s loop, level at both ends.
        /// </summary>
        public static float[] ApronBed()
        {
            var length = SampleRate * 12;
            var samples = new float[length];
            var noise = new Noise(23);
            var rumble = 0f;
            var rumble2 = 0f;
            for (var i = 0; i < length; i++)
            {
                var time = i / (double)SampleRate;
                rumble += 0.01f * (noise.Next() - rumble);
                rumble2 += 0.05f * (rumble - rumble2);
                var hum = 0.18 * Math.Sin(2 * Math.PI * 100 * time) + 0.08 * Math.Sin(2 * Math.PI * 200 * time);
                // A slow swell so the bed breathes, periodic over the loop so it joins cleanly.
                var swell = 0.8 + 0.2 * Math.Sin(2 * Math.PI * time / 12.0);
                samples[i] = (float)((rumble2 * 6.0 + hum * 0.25) * swell);
            }

            // Reversing beeps from a distant tug: two bursts of three, well inside the loop.
            foreach (var start in new[] { 3.2, 8.7 })
                for (var b = 0; b < 3; b++)
                {
                    var from = (int)((start + b * 0.9) * SampleRate);
                    for (var k = 0; k < SampleRate * 0.35 && from + k < length; k++)
                    {
                        var t = k / (double)SampleRate;
                        var edge = Math.Min(1.0, Math.Min(t, 0.35 - t) / 0.01);
                        samples[from + k] += (float)(0.06 * edge * Math.Sin(2 * Math.PI * 1150 * t));
                    }
                }

            // Crossfade the ends so the loop is seamless.
            var fade = SampleRate / 2;
            for (var i = 0; i < fade; i++)
            {
                var t = i / (float)fade;
                samples[i] = samples[i] * t + samples[length - fade + i] * (1f - t);
            }

            var looped = new float[length - fade];
            Array.Copy(samples, looped, looped.Length);
            return Normalise(looped, 0.5f);
        }

        private static float[] Arpeggio(double[] notes, double step, float seconds, float peak)
        {
            var samples = new float[(int)(SampleRate * seconds)];
            for (var n = 0; n < notes.Length; n++)
            {
                var start = (int)(n * step * SampleRate);
                var last = n == notes.Length - 1;
                for (var i = start; i < samples.Length; i++)
                {
                    var time = (i - start) / (double)SampleRate;
                    var envelope = Math.Min(1.0, time / 0.01) * Math.Exp(-time * (last ? 2.2 : 6.0));
                    var tone = Math.Sin(2 * Math.PI * notes[n] * time) + 0.3 * Math.Sin(2 * Math.PI * notes[n] * 2 * time);
                    samples[i] += (float)(tone * envelope);
                }
            }

            // A short fade so the tail never ends on a click.
            var fade = (int)(SampleRate * 0.08f);
            for (var i = 0; i < fade && i < samples.Length; i++)
                samples[samples.Length - 1 - i] *= i / (float)fade;
            return Normalise(samples, peak);
        }

        private static float[] Normalise(float[] samples, float peak)
        {
            var max = 0f;
            foreach (var sample in samples)
                max = Math.Max(max, Math.Abs(sample));
            if (max <= 0f)
                return samples;
            var scale = peak / max;
            for (var i = 0; i < samples.Length; i++)
                samples[i] *= scale;
            return samples;
        }

        /// <summary>Small deterministic noise source (xorshift), so the rattle is the same every run.</summary>
        private struct Noise
        {
            private uint _state;
            public Noise(uint seed) => _state = seed == 0 ? 1u : seed;

            public float Next()
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return (_state & 0xFFFF) / 32767.5f - 1f;
            }
        }
    }
}
