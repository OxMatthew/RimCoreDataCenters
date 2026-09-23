using System;
using System.IO;

namespace AssetGen
{
    /// <summary>Synthesises the mod's original placeholder sounds as 16-bit mono PCM WAV files.</summary>
    internal static class Audio
    {
        private const int Rate = 44100;

        // Loops are built from sinusoids whose frequencies are integer multiples of 1/duration,
        // so the waveform is exactly periodic and loops with no click.
        public static float[] ServerHum()
        {
            const float seconds = 2.0f;
            int n = (int)(Rate * seconds);
            float[] s = new float[n];
            Random rng = new Random(1337);
            float[] baseHz = { 50f, 100f, 150f, 200f, 300f };
            float[] baseAmp = { 0.55f, 0.35f, 0.18f, 0.12f, 0.06f };
            for (int b = 0; b < baseHz.Length; b++)
            {
                AddSine(s, baseHz[b], baseAmp[b], 0f);
            }
            // fan / airflow noise: many random-phase partials in the 350-2600 Hz band
            for (int i = 0; i < 260; i++)
            {
                float hz = (float)(350 + rng.NextDouble() * 2250);
                hz = (float)Math.Round(hz * seconds) / seconds;
                float amp = 0.012f * (float)(1.0 - (hz - 350) / 2600.0 * 0.6);
                AddSine(s, hz, amp, (float)(rng.NextDouble() * Math.PI * 2));
            }
            Normalize(s, 0.42f);
            return s;
        }

        public static float[] CoolerFan()
        {
            const float seconds = 2.0f;
            int n = (int)(Rate * seconds);
            float[] s = new float[n];
            Random rng = new Random(4242);
            for (int i = 0; i < 420; i++)
            {
                float hz = (float)(160 + rng.NextDouble() * 3600);
                hz = (float)Math.Round(hz * seconds) / seconds;
                float amp = 0.010f * (float)(1.0 - (hz - 160) / 3800.0 * 0.75);
                AddSine(s, hz, amp, (float)(rng.NextDouble() * Math.PI * 2));
            }
            AddSine(s, 88f, 0.22f, 0f);
            AddSine(s, 176f, 0.09f, 1.1f);
            // slow periodic swell (2 cycles per loop) to mimic fan blade pass
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / Rate;
                s[i] *= (float)(0.86 + 0.14 * Math.Sin(2 * Math.PI * (2.0 / seconds) * t));
            }
            Normalize(s, 0.4f);
            return s;
        }

        public static float[] CartridgeReady()
        {
            const float seconds = 0.55f;
            int n = (int)(Rate * seconds);
            float[] s = new float[n];
            AddNote(s, 0.00f, 880f, 0.38f);
            AddNote(s, 0.11f, 1318.5f, 0.38f);
            Normalize(s, 0.55f);
            return s;
        }

        public static float[] RackAlarm()
        {
            const float seconds = 0.95f;
            int n = (int)(Rate * seconds);
            float[] s = new float[n];
            AddBeep(s, 0.00f, 0.20f, 760f);
            AddBeep(s, 0.30f, 0.20f, 760f);
            AddBeep(s, 0.60f, 0.26f, 560f);
            Normalize(s, 0.6f);
            return s;
        }

        /// <summary>A soft three-note rising chime: the AI has something to say.</summary>
        public static float[] AiChime()
        {
            const float seconds = 0.9f;
            int n = (int)(Rate * seconds);
            float[] s = new float[n];
            AddNote(s, 0.00f, 659.3f, 0.5f);
            AddNote(s, 0.14f, 880f, 0.5f);
            AddNote(s, 0.28f, 1174.7f, 0.55f);
            Normalize(s, 0.5f);
            return s;
        }

        /// <summary>A sharp two-tone alternating klaxon: an espionage raid has been spotted.</summary>
        public static float[] EspionageAlert()
        {
            const float seconds = 1.3f;
            int n = (int)(Rate * seconds);
            float[] s = new float[n];
            for (int i = 0; i < 4; i++)
            {
                AddBeep(s, i * 0.3f, 0.24f, i % 2 == 0 ? 880f : 660f);
            }
            Normalize(s, 0.65f);
            return s;
        }

        /// <summary>Two short low buzzes, the second lower: "access denied".</summary>
        public static float[] AccessDenied()
        {
            const float seconds = 0.62f;
            int n = (int)(Rate * seconds);
            float[] s = new float[n];
            AddBuzz(s, 0.00f, 0.22f, 300f);
            AddBuzz(s, 0.28f, 0.30f, 210f);
            Normalize(s, 0.6f);
            return s;
        }

        private static void AddBuzz(float[] s, float startSec, float lengthSec, float hz)
        {
            int start = (int)(startSec * Rate);
            int len = (int)(lengthSec * Rate);
            for (int i = 0; i < len && start + i < s.Length; i++)
            {
                double t = (double)i / Rate;
                double env = Math.Min(1.0, t / 0.008) * Math.Min(1.0, (lengthSec - t) / 0.03);
                // a square-ish wave (odd harmonics) so it reads as a harsh electronic buzz
                double v = Math.Sin(2 * Math.PI * hz * t) + 0.5 * Math.Sin(2 * Math.PI * hz * 3 * t) + 0.3 * Math.Sin(2 * Math.PI * hz * 5 * t);
                s[start + i] += (float)(v * env);
            }
        }

        private static void AddSine(float[] s, float hz, float amp, float phase)
        {
            double step = 2.0 * Math.PI * hz / Rate;
            for (int i = 0; i < s.Length; i++)
            {
                s[i] += amp * (float)Math.Sin(step * i + phase);
            }
        }

        private static void AddNote(float[] s, float startSec, float hz, float lengthSec)
        {
            int start = (int)(startSec * Rate);
            int len = (int)(lengthSec * Rate);
            for (int i = 0; i < len && start + i < s.Length; i++)
            {
                double t = (double)i / Rate;
                double attack = Math.Min(1.0, t / 0.004);
                double decay = Math.Exp(-t * 9.0);
                double v = Math.Sin(2 * Math.PI * hz * t) + 0.25 * Math.Sin(2 * Math.PI * hz * 2 * t);
                s[start + i] += (float)(v * attack * decay);
            }
        }

        private static void AddBeep(float[] s, float startSec, float lengthSec, float hz)
        {
            int start = (int)(startSec * Rate);
            int len = (int)(lengthSec * Rate);
            for (int i = 0; i < len && start + i < s.Length; i++)
            {
                double t = (double)i / Rate;
                double env = Math.Min(1.0, t / 0.012) * Math.Min(1.0, (lengthSec - t) / 0.02);
                double v = Math.Sin(2 * Math.PI * hz * t) + 0.3 * Math.Sin(2 * Math.PI * hz * 3 * t);
                s[start + i] += (float)(v * env);
            }
        }

        private static void Normalize(float[] s, float peak)
        {
            float max = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                max = Math.Max(max, Math.Abs(s[i]));
            }
            if (max <= 0f)
            {
                return;
            }
            float k = peak / max;
            for (int i = 0; i < s.Length; i++)
            {
                s[i] *= k;
            }
        }

        public static void WriteWav(string path, float[] samples)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (FileStream fs = new FileStream(path, FileMode.Create))
            using (BinaryWriter w = new BinaryWriter(fs))
            {
                int dataBytes = samples.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + dataBytes);
                w.Write(new[] { 'W', 'A', 'V', 'E' });
                w.Write(new[] { 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);      // PCM
                w.Write((short)1);      // mono
                w.Write(Rate);
                w.Write(Rate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(dataBytes);
                for (int i = 0; i < samples.Length; i++)
                {
                    float v = Math.Max(-1f, Math.Min(1f, samples[i]));
                    w.Write((short)Math.Round(v * 32767f));
                }
            }
        }
    }
}
