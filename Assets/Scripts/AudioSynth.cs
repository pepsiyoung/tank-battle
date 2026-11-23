using System.Collections.Generic;
using UnityEngine;

namespace TankBattle
{
    public enum WaveType
    {
        Square,
        Triangle,
        Saw
    }

    // 用采样合成替代网页版的 Web Audio 振荡器，音色与调用时机保持一致
    public static class AudioSynth
    {
        public static bool Enabled = true;

        const int SampleRate = 44100;
        static AudioSource source;
        static readonly Dictionary<string, AudioClip> Cache = new Dictionary<string, AudioClip>();

        public static void Init(AudioSource src)
        {
            source = src;
        }

        static void Play(AudioClip clip)
        {
            if (!Enabled || source == null || clip == null) return;
            source.PlayOneShot(clip, 1f);
        }

        /* ---------------- 合成工具 ---------------- */

        static float Wave(WaveType type, float phase)
        {
            switch (type)
            {
                case WaveType.Triangle:
                    return (2f / Mathf.PI) * Mathf.Asin(Mathf.Sin(phase));
                case WaveType.Saw:
                    return 2f * Mathf.Repeat(phase / (2f * Mathf.PI), 1f) - 1f;
                default:
                    return Mathf.Sin(phase) >= 0f ? 1f : -1f;
            }
        }

        static float[] ToneBuf(float freq, float dur, WaveType type, float vol, float slideTo)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(dur * SampleRate));
            float[] data = new float[n];
            float phase = 0f;
            float v = Mathf.Max(0.0002f, vol);

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float f = slideTo > 0f ? freq * Mathf.Pow(slideTo / freq, t) : freq;
                phase += 2f * Mathf.PI * f / SampleRate;
                if (phase > Mathf.PI * 2f) phase -= Mathf.PI * 2f;
                float env = v * Mathf.Pow(0.0001f / v, t);
                data[i] = Wave(type, phase) * env;
            }
            return data;
        }

        static float[] NoiseBuf(float dur, float vol, float cutoff)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(dur * SampleRate));
            float[] data = new float[n];
            float dt = 1f / SampleRate;
            float rc = 1f / (2f * Mathf.PI * cutoff);
            float alpha = dt / (rc + dt);
            float prev = 0f;
            var rnd = new System.Random(9137);

            for (int i = 0; i < n; i++)
            {
                float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
                prev += alpha * (white - prev);
                float env = vol * (1f - i / (float)n);
                data[i] = prev * env;
            }
            return data;
        }

        static float[] Mix(float[] a, float[] b)
        {
            int len = Mathf.Max(a.Length, b.Length);
            float[] data = new float[len];
            for (int i = 0; i < len; i++)
            {
                float v = 0f;
                if (i < a.Length) v += a[i];
                if (i < b.Length) v += b[i];
                data[i] = Mathf.Clamp(v, -1f, 1f);
            }
            return data;
        }

        static float[] Overlap(float[][] notes, float stepSec, float totalSec)
        {
            int total = Mathf.Max(1, Mathf.RoundToInt(totalSec * SampleRate));
            float[] data = new float[total];
            for (int k = 0; k < notes.Length; k++)
            {
                int offset = Mathf.RoundToInt(k * stepSec * SampleRate);
                for (int i = 0; i < notes[k].Length; i++)
                {
                    int idx = offset + i;
                    if (idx >= total) break;
                    data[idx] += notes[k][i];
                }
            }
            for (int i = 0; i < total; i++) data[i] = Mathf.Clamp(data[i], -1f, 1f);
            return data;
        }

        static AudioClip Clip(string key, float[] data)
        {
            AudioClip cached;
            if (Cache.TryGetValue(key, out cached)) return cached;

            AudioClip clip = AudioClip.Create(key, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            Cache[key] = clip;
            return clip;
        }

        /* ---------------- 音效 ---------------- */

        public static void Shoot()
        {
            Play(Clip("shoot", ToneBuf(880f, 0.07f, WaveType.Square, 0.09f, 300f)));
        }

        public static void EnemyShoot()
        {
            Play(Clip("enemyShoot", ToneBuf(500f, 0.08f, WaveType.Square, 0.06f, 220f)));
        }

        public static void HitBrick()
        {
            Play(Clip("hitBrick", NoiseBuf(0.07f, 0.12f, 1400f)));
        }

        public static void HitSteel()
        {
            Play(Clip("hitSteel", ToneBuf(190f, 0.06f, WaveType.Square, 0.09f, 0f)));
        }

        public static void Explode()
        {
            Play(Clip("explode", Mix(
                NoiseBuf(0.25f, 0.22f, 650f),
                ToneBuf(130f, 0.25f, WaveType.Saw, 0.1f, 40f))));
        }

        public static void TankExplode()
        {
            Play(Clip("tankExplode", Mix(
                NoiseBuf(0.38f, 0.3f, 520f),
                ToneBuf(110f, 0.35f, WaveType.Saw, 0.14f, 35f))));
        }

        public static void Pickup()
        {
            Play(Clip("pickup", ToneBuf(660f, 0.09f, WaveType.Square, 0.11f, 1320f)));
        }

        public static void Spawn()
        {
            Play(Clip("spawn", ToneBuf(300f, 0.1f, WaveType.Triangle, 0.09f, 620f)));
        }

        public static void StageClear()
        {
            if (Cache.ContainsKey("stageClear"))
            {
                Play(Cache["stageClear"]);
                return;
            }
            float[][] notes =
            {
                ToneBuf(523f, 0.16f, WaveType.Square, 0.11f, 0f),
                ToneBuf(659f, 0.16f, WaveType.Square, 0.11f, 0f),
                ToneBuf(784f, 0.16f, WaveType.Square, 0.11f, 0f),
                ToneBuf(1046f, 0.16f, WaveType.Square, 0.11f, 0f)
            };
            Play(Clip("stageClear", Overlap(notes, 0.13f, 0.55f)));
        }

        public static void GameOver()
        {
            if (Cache.ContainsKey("gameOver"))
            {
                Play(Cache["gameOver"]);
                return;
            }
            float[][] notes =
            {
                ToneBuf(420f, 0.28f, WaveType.Saw, 0.1f, 0f),
                ToneBuf(330f, 0.28f, WaveType.Saw, 0.1f, 0f),
                ToneBuf(240f, 0.28f, WaveType.Saw, 0.1f, 0f),
                ToneBuf(150f, 0.28f, WaveType.Saw, 0.1f, 0f)
            };
            Play(Clip("gameOver", Overlap(notes, 0.2f, 0.88f)));
        }

        public static void BaseDown()
        {
            Play(Clip("baseDown", Mix(
                NoiseBuf(0.6f, 0.4f, 420f),
                ToneBuf(80f, 0.6f, WaveType.Saw, 0.18f, 30f))));
        }

        public static void ChargeTick()
        {
            Play(Clip("chargeTick", ToneBuf(380f, 0.05f, WaveType.Square, 0.05f, 520f)));
        }

        public static void ChargeFull()
        {
            Play(Clip("chargeFull", ToneBuf(760f, 0.22f, WaveType.Square, 0.1f, 1520f)));
        }

        public static void Beam()
        {
            Play(Clip("beam", Mix(
                NoiseBuf(0.55f, 0.32f, 1100f),
                ToneBuf(110f, 0.5f, WaveType.Saw, 0.16f, 880f))));
        }

        public static void Pause()
        {
            Play(Clip("pause", ToneBuf(700f, 0.07f, WaveType.Square, 0.07f, 0f)));
        }

        /* ---------------- 背景音乐 ---------------- */

        // 红白机坦克大战风格战斗循环 BGM：168 BPM、16 分音符网格、8 小节（约 11.4 秒）
        // 风格参照：小调快速进行曲——八分音符根音八度推进低音 + 方波主旋律 + 琶音和声 + 进行鼓点
        public static AudioClip BgmClip()
        {
            AudioClip cached;
            if (Cache.TryGetValue("bgm", out cached)) return cached;

            const float beat = 60f / 168f;       // 168 BPM
            const float step = beat * 0.25f;     // 16 分音符
            const float total = 8f * 4f * beat;  // 8 小节

            float[] mel =
            {
                440f, 0f, 523f, 659f, 880f, 0f, 784f, 659f, 523f, 0f, 440f, 0f, 392f, 440f, 523f, 0f,
                659f, 0f, 0f, 880f, 784f, 659f, 587f, 0f, 523f, 0f, 440f, 0f, 494f, 523f, 587f, 659f,
                349f, 0f, 440f, 523f, 698f, 0f, 659f, 523f, 440f, 0f, 349f, 0f, 392f, 440f, 523f, 0f,
                392f, 0f, 494f, 587f, 784f, 0f, 698f, 587f, 494f, 0f, 392f, 0f, 440f, 494f, 523f, 587f,
                440f, 0f, 523f, 659f, 880f, 0f, 988f, 880f, 659f, 0f, 784f, 0f, 880f, 0f, 0f, 0f,
                784f, 659f, 523f, 659f, 880f, 0f, 784f, 0f, 659f, 587f, 523f, 494f, 440f, 0f, 0f, 0f,
                349f, 0f, 440f, 523f, 698f, 0f, 659f, 523f, 587f, 0f, 523f, 0f, 440f, 523f, 698f, 0f,
                392f, 0f, 494f, 587f, 784f, 0f, 880f, 784f, 698f, 587f, 494f, 587f, 784f, 0f, 0f, 0f
            };
            var melNotes = new float[mel.Length][];
            for (int i = 0; i < mel.Length; i++)
                melNotes[i] = ToneBuf(mel[i] > 0f ? mel[i] : 1f, step, WaveType.Square, mel[i] > 0f ? 0.13f : 0.0002f, 0f);

            float[] bass =
            {
                110f, 220f, 110f, 220f, 110f, 220f, 110f, 220f,
                110f, 220f, 110f, 220f, 110f, 220f, 110f, 220f,
                87f, 175f, 87f, 175f, 87f, 175f, 87f, 175f,
                98f, 196f, 98f, 196f, 98f, 196f, 98f, 196f,
                110f, 220f, 110f, 220f, 110f, 220f, 110f, 220f,
                110f, 220f, 110f, 220f, 110f, 220f, 110f, 220f,
                87f, 175f, 87f, 175f, 87f, 175f, 87f, 175f,
                98f, 196f, 98f, 196f, 98f, 196f, 98f, 196f
            };
            var bassNotes = new float[bass.Length][];
            for (int i = 0; i < bass.Length; i++)
                bassNotes[i] = ToneBuf(bass[i], step * 2f, WaveType.Triangle, 0.17f, 0f);

            float[] arp =
            {
                220f, 262f, 330f, 262f, 220f, 262f, 330f, 262f,
                220f, 262f, 330f, 262f, 220f, 262f, 330f, 262f,
                175f, 220f, 262f, 220f, 175f, 220f, 262f, 220f,
                196f, 247f, 294f, 247f, 196f, 247f, 294f, 247f,
                220f, 262f, 330f, 262f, 220f, 262f, 330f, 262f,
                220f, 262f, 330f, 262f, 220f, 262f, 330f, 262f,
                175f, 220f, 262f, 220f, 175f, 220f, 262f, 220f,
                196f, 247f, 294f, 247f, 196f, 247f, 294f, 247f
            };
            var arpNotes = new float[arp.Length][];
            for (int i = 0; i < arp.Length; i++)
                arpNotes[i] = ToneBuf(arp[i], step * 2f, WaveType.Square, 0.05f, 0f);

            // 鼓点：底鼓（1、3 拍）+ 军鼓（2、4 拍）+ 八分音符轻踩镲
            var percNotes = new float[32][];
            for (int barIdx = 0; barIdx < 8; barIdx++)
            {
                for (int k = 0; k < 4; k++)
                {
                    if (k == 0 || k == 2)
                        percNotes[barIdx * 4 + k] = ToneBuf(55f, 0.08f, WaveType.Triangle, 0.2f, 0f);
                    else
                        percNotes[barIdx * 4 + k] = NoiseBuf(0.05f, 0.06f, 3400f);
                }
            }

            var hatNotes = new float[64][];
            for (int i = 0; i < hatNotes.Length; i++)
                hatNotes[i] = NoiseBuf(0.02f, 0.025f, 6500f);

            float[] music = Mix(
                Overlap(melNotes, step, total),
                Mix(
                    Overlap(bassNotes, step * 2f, total),
                    Mix(
                        Overlap(arpNotes, step * 2f, total),
                        Mix(Overlap(percNotes, beat, total), Overlap(hatNotes, beat * 0.5f, total)))));

            cached = Clip("bgm", music);
            return cached;
        }
    }
}
