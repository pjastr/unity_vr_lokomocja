using System;
using UnityEngine;

namespace VRLocomotion
{
    /// <summary>
    /// Dźwięki generowane w kodzie (synteza) - projekt nie potrzebuje plików audio.
    /// Odtwarzane jednym źródłem 2D (Sfx.Play). Klipy powstają przy pierwszym użyciu albo w Warmup().
    /// </summary>
    public static class Sfx
    {
        const int Rate = 44100;
        const float TwoPi = Mathf.PI * 2f;

        static AudioSource source;
        static AudioClip click, toggle, teleport, invalid, turn, checkpoint, start, finish, splash;

        public static AudioClip Click => click != null ? click : click = Make("sfx_click", 0.05f, ClickWave);
        public static AudioClip Toggle => toggle != null ? toggle : toggle = Make("sfx_toggle", 0.16f, ToggleWave);
        public static AudioClip Teleport => teleport != null ? teleport : teleport = Make("sfx_teleport", 0.3f, TeleportWave);
        public static AudioClip Invalid => invalid != null ? invalid : invalid = Make("sfx_invalid", 0.14f, InvalidWave);
        public static AudioClip Turn => turn != null ? turn : turn = Make("sfx_turn", 0.06f, TurnWave);
        public static AudioClip Checkpoint => checkpoint != null ? checkpoint : checkpoint = Make("sfx_checkpoint", 0.35f, CheckpointWave);
        public static AudioClip Start => start != null ? start : start = Make("sfx_start", 0.45f, StartWave);
        public static AudioClip Finish => finish != null ? finish : finish = Make("sfx_finish", 0.9f, FinishWave);
        public static AudioClip Splash => splash != null ? splash : splash = Make("sfx_splash", 0.6f, SplashWave);

        public static void Warmup()
        {
            _ = Click; _ = Toggle; _ = Teleport; _ = Invalid; _ = Turn; _ = Checkpoint; _ = Start; _ = Finish; _ = Splash;
        }

        public static void Play(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            if (source == null)
            {
                var go = new GameObject("Sfx");
                source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
            }
            source.PlayOneShot(clip, volume);
        }

        // ------------------------------------------------------------------ fale

        static float ClickWave(float t, float n) => Mathf.Sin(TwoPi * 1800f * t) * Mathf.Exp(-t * 120f) * 0.6f;

        static float ToggleWave(float t, float n) => Arp(t, 0.07f, 30f, 880f, 1320f);

        // Teleport: „szum” przesuwający się w górę + rosnący ton.
        static float TeleportWave(float t, float n)
        {
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.3f));
            float tone = Mathf.Sin(SweepPhase(t, 300f, 1200f, 0.3f)) * 0.35f;
            return (tone + n * 0.25f) * env;
        }

        static float InvalidWave(float t, float n) => Square(TwoPi * 180f * t) * 0.25f * Mathf.Exp(-t * 18f);

        static float TurnWave(float t, float n) => n * Mathf.Exp(-t * 80f) * 0.4f + Mathf.Sin(TwoPi * 600f * t) * Mathf.Exp(-t * 60f) * 0.2f;

        static float CheckpointWave(float t, float n) => Arp(t, 0.1f, 12f, 784f, 1175f);

        static float StartWave(float t, float n) => Arp(t, 0.1f, 9f, 523f, 659f, 784f, 1047f);

        static float FinishWave(float t, float n) => Arp(t, 0.12f, 5f, 523f, 659f, 784f, 1047f, 1319f, 1568f);

        // Plusk: szum o opadającej „barwie” (proste filtrowanie przez mnożenie z sinusem).
        static float SplashWave(float t, float n)
        {
            float env = Mathf.Exp(-t * 6f) * Mathf.Clamp01(t * 60f);
            return n * env * (0.5f + 0.5f * Mathf.Sin(SweepPhase(t, 900f, 120f, 0.6f))) * 0.6f;
        }

        // ------------------------------------------------------------------ narzędzia

        static AudioClip Make(string name, float seconds, Func<float, float, float> wave)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            var rng = new System.Random(name.Length * 7919);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)rng.NextDouble() * 2f - 1f;
                float fade = Mathf.Clamp01((n - i) / (Rate * 0.005f)); // bez trzasku na końcu
                data[i] = Mathf.Clamp(wave(t, noise) * fade, -1f, 1f) * 0.8f;
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static float SweepPhase(float t, float f0, float f1, float T) => TwoPi * (f0 * t + (f1 - f0) * t * t / (2f * T));

        static float Square(float phase) => Mathf.Sin(phase) >= 0f ? 1f : -1f;

        static float Arp(float t, float step, float decay, params float[] notes)
        {
            int idx = Mathf.Min((int)(t / step), notes.Length - 1);
            float local = t - idx * step;
            float f = notes[idx];
            return (Mathf.Sin(TwoPi * f * t) * 0.6f + Mathf.Sin(TwoPi * f * 2f * t) * 0.15f) * Mathf.Exp(-local * decay);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            source = null;
            click = toggle = teleport = invalid = turn = checkpoint = start = finish = splash = null;
        }
    }
}
