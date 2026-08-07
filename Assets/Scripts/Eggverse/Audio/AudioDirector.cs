using System.Collections;
using UnityEngine;

namespace Eggverse
{
    /// <summary>Plays the synthesised clips: one-shot effects and a cross-faded music bed.</summary>
    public class AudioDirector : MonoBehaviour
    {
        public static AudioDirector Instance { get; private set; }

        AudioSource sfx;
        AudioSource musicA, musicB;
        bool usingA = true;
        MusicTrack current = MusicTrack.None;
        Coroutine fade;

        public float SfxVolume = 0.55f;
        public float MusicVolume = 0.30f;
        public bool Muted { get; private set; }

        // Stops rapid-fire identical blips from stacking into a buzz.
        float lastTalkTime;

        public void Build()
        {
            Instance = this;

            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;

            musicA = gameObject.AddComponent<AudioSource>();
            musicB = gameObject.AddComponent<AudioSource>();
            foreach (var source in new[] { musicA, musicB })
            {
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0f;
                source.volume = 0f;
            }
        }

        // ------------------------------------------------------------------

        public void Play(Sfx id, float volumeScale = 1f)
        {
            if (Muted || sfx == null) return;

            if (id == Sfx.Talk)
            {
                // The dialogue tick fires per character; thin it out.
                if (Time.unscaledTime - lastTalkTime < 0.035f) return;
                lastTalkTime = Time.unscaledTime;
            }

            var clip = ProcAudio.Get(id);
            if (clip != null) sfx.PlayOneShot(clip, Mathf.Clamp01(SfxVolume * volumeScale));
        }

        /// <summary>Plays the right hit sound for how effective the move was.</summary>
        public void PlayHit(float typeMultiplier, bool critical)
        {
            if (critical) Play(Sfx.Crit);
            else if (typeMultiplier > 1.2f) Play(Sfx.HitStrong);
            else if (typeMultiplier < 0.8f) Play(Sfx.HitWeak);
            else Play(Sfx.Hit);
        }

        public void SetMusic(MusicTrack track, float fadeSeconds = 1.1f)
        {
            if (track == current) return;
            current = track;

            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(CrossFade(track, fadeSeconds));
        }

        IEnumerator CrossFade(MusicTrack track, float seconds)
        {
            AudioSource from = usingA ? musicA : musicB;
            AudioSource to = usingA ? musicB : musicA;
            usingA = !usingA;

            var clip = ProcAudio.Music(track);
            if (clip != null)
            {
                to.clip = clip;
                to.volume = 0f;
                to.Play();
            }

            float fromStart = from.volume;
            float target = (Muted || clip == null) ? 0f : MusicVolume;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / seconds);
                from.volume = Mathf.Lerp(fromStart, 0f, k);
                to.volume = Mathf.Lerp(0f, target, k);
                yield return null;
            }

            from.Stop();
            from.volume = 0f;
            to.volume = target;
            fade = null;
        }

        public void ToggleMute()
        {
            Muted = !Muted;
            ApplyMusicVolume();
        }

        /// <summary>Pushes MusicVolume (and mute) to whichever source is currently playing.</summary>
        public void ApplyMusicVolume()
        {
            float target = Muted ? 0f : MusicVolume;
            if (fade != null) return;   // a cross-fade is already driving the volumes
            if (musicA != null && musicA.isPlaying) musicA.volume = target;
            if (musicB != null && musicB.isPlaying) musicB.volume = target;
        }
    }
}
