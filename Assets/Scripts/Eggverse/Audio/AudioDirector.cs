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

        /// <summary>What is playing now, so a caller can pace the fade by where it is coming from
        /// as well as where it is going.</summary>
        public MusicTrack CurrentTrack => current;
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
        /// <summary>
        /// A species' own voice. Twenty-eight creatures arrived on the same three-note sting for
        /// the whole of the game's life; the sting says something is here, and this says what.
        /// </summary>
        /// <summary>
        /// How close together two cries may land. Running the record cursor down twenty-eight
        /// entries would otherwise stack twenty-eight voices into one noise; at this spacing it
        /// is a chirping sweep, which is what flicking through a record of living things should
        /// sound like.
        /// </summary>
        // 0.16, not 0.11. At 0.11 a cursor held down the record kept eight voices alive at
        // once - not loud, the sweep peaks at 0.39, but eight overlapping creatures is mud
        // whatever its level. Six still reads as a sweep and you can still hear each one.
        public const float CryGap = 0.16f;

        /// <summary>
        /// How loud a cry is, by what the moment is worth.
        ///
        /// Five places play one, and the volumes were five numbers typed at five call sites -
        /// an ordering that existed in my head and nowhere a check could reach. Named, it is a
        /// rule: something arriving to fight you is the loudest, something becoming yours is
        /// next, one you sent in yourself is quieter because you knew it was coming, and
        /// browsing a list is quietest of all because it happens dozens of times a minute.
        /// </summary>
        public const float CryEncounter = 0.85f;   // a wild egg arrives
        public const float CryCaught    = 0.80f;   // it becomes yours
        public const float CrySentOut   = 0.75f;   // you chose this one
        public const float CryRecord    = 0.55f;   // reading the book
        public const float CryOwn       = 0.50f;   // running the cursor down your own nest
        float lastCryTime = -99f;

        public void PlayCry(SpeciesDef sp, float volumeScale = 1f)
        {
            if (Muted || sfx == null || sp == null) return;
            if (Time.unscaledTime - lastCryTime < CryGap) return;
            lastCryTime = Time.unscaledTime;
            sfx.PlayOneShot(ProcAudio.Cry(sp), SfxVolume * volumeScale);
        }

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
