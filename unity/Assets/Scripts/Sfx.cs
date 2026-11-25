using System;
using UnityEngine;

namespace SnakeGame
{
    /// <summary>
    /// 全部音效都是开跑时用波形合成出来的，仓库里不带任何音频文件。
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        const int SampleRate = 44100;

        AudioSource source;
        AudioClip eatClip;
        AudioClip bonusClip;
        AudioClip dieClip;
        AudioClip uiClip;

        public void Init()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = 0.9f;

            eatClip = Build("eat", 0.09f, t =>
                Mathf.Sin(2f * Mathf.PI * (520f + 900f * t) * t) * Env(t, 0.09f, 2f) * 0.55f);

            bonusClip = Build("bonus", 0.22f, t =>
            {
                float f = t < 0.08f ? 880f : 1320f;
                return Mathf.Sin(2f * Mathf.PI * f * t) * Env(t, 0.22f, 1.6f) * 0.5f;
            });

            dieClip = Build("die", 0.55f, t =>
            {
                float f = Mathf.Lerp(420f, 70f, t / 0.55f);
                float saw = Mathf.Sin(2f * Mathf.PI * f * t) + 0.35f * Mathf.Sin(2f * Mathf.PI * f * 2.01f * t);
                return saw * Env(t, 0.55f, 1.4f) * 0.5f;
            });

            uiClip = Build("ui", 0.05f, t =>
                Mathf.Sin(2f * Mathf.PI * 660f * t) * Env(t, 0.05f, 3f) * 0.35f);
        }

        static float Env(float t, float dur, float power)
        {
            float k = Mathf.Clamp01(1f - t / dur);
            return Mathf.Pow(k, power);
        }

        static AudioClip Build(string name, float dur, Func<float, float> shape)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(dur * SampleRate));
            float[] data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float v = shape((float)i / SampleRate);
                data[i] = Mathf.Clamp(v, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create(name, n, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public void PlayEat() { if (source != null) source.PlayOneShot(eatClip, 0.9f); }
        public void PlayBonus() { if (source != null) source.PlayOneShot(bonusClip, 1f); }
        public void PlayDie() { if (source != null) source.PlayOneShot(dieClip, 1f); }
        public void PlayUi() { if (source != null) source.PlayOneShot(uiClip, 0.8f); }
    }
}
