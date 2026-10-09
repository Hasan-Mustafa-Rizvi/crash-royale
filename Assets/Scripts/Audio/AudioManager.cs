using System.Collections;
using System.Collections.Generic;
using CrashRoyale.Meta;
using UnityEngine;

namespace CrashRoyale.Audio
{
    /// <summary>
    /// Music and sound effects. Created once before the first scene loads, so there is exactly one instance
    /// no matter which scene you start from or how often you go between the menu and a battle.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        private const int SfxVoices = 10;
        private const float CrossfadeTime = 0.8f;

        private readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        private SoundLibrary library;
        private AudioSource[] voices;
        private int nextVoice;
        private AudioSource musicA;
        private AudioSource musicB;
        private MusicTrack currentTrack = MusicTrack.None;
        private Coroutine fade;

        public static AudioManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;

            var go = new GameObject("AudioManager");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<AudioManager>();
            Instance.Setup(Resources.Load<SoundLibrary>(SoundLibrary.ResourcePath));
        }

        private void Setup(SoundLibrary soundLibrary)
        {
            library = soundLibrary;

            voices = new AudioSource[SfxVoices];
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i] = gameObject.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
            }

            musicA = CreateMusicSource();
            musicB = CreateMusicSource();
        }

        private AudioSource CreateMusicSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.volume = 0f;
            source.ignoreListenerPause = true;
            return source;
        }

        public static void Play(string id) => Instance?.PlayInternal(id);

        public static void PlayMusic(MusicTrack track) => Instance?.SetMusic(track);

        public static void StopMusic() => Instance?.SetMusic(MusicTrack.None);

        /// <summary>Call after changing the volume settings.</summary>
        public static void RefreshVolumes()
        {
            if (Instance == null) return;
            var playing = Instance.musicA.isPlaying ? Instance.musicA : Instance.musicB;
            if (Instance.fade == null) playing.volume = Instance.MusicVolume;
        }

        private float MusicVolume => PlayerProfile.MusicVolume * (library != null ? library.musicGain : 1f);

        private void PlayInternal(string id)
        {
            if (library == null || string.IsNullOrEmpty(id)) return;

            var entry = library.Find(id);
            if (entry == null || entry.clips == null || entry.clips.Length == 0) return;

            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(id, out float last) && now - last < entry.minInterval) return;
            lastPlayed[id] = now;

            var clip = entry.clips[Random.Range(0, entry.clips.Length)];
            if (clip == null) return;

            var voice = voices[nextVoice];
            nextVoice = (nextVoice + 1) % voices.Length;
            voice.pitch = Random.Range(entry.pitchMin, entry.pitchMax);
            voice.PlayOneShot(clip, entry.volume * PlayerProfile.SfxVolume);
        }

        private void SetMusic(MusicTrack track)
        {
            if (track == currentTrack) return;
            currentTrack = track;

            AudioClip clip = null;
            if (library != null)
            {
                if (track == MusicTrack.Menu) clip = library.menuMusic;
                else if (track == MusicTrack.Battle) clip = library.battleMusic;
            }

            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(Crossfade(clip));
        }

        private IEnumerator Crossfade(AudioClip clip)
        {
            var from = musicA.isPlaying ? musicA : musicB;
            var to = from == musicA ? musicB : musicA;
            float fromStart = from.volume;

            if (clip != null)
            {
                to.clip = clip;
                to.volume = 0f;
                to.Play();
            }

            for (float t = 0f; t < CrossfadeTime; t += Time.unscaledDeltaTime)
            {
                float k = t / CrossfadeTime;
                from.volume = Mathf.Lerp(fromStart, 0f, k);
                if (clip != null) to.volume = Mathf.Lerp(0f, MusicVolume, k);
                yield return null;
            }

            from.Stop();
            from.volume = 0f;
            if (clip != null) to.volume = MusicVolume;
            fade = null;
        }
    }
}
