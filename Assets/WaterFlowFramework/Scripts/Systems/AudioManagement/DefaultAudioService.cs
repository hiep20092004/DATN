using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Sirenix.OdinInspector;
using WaterFlow.Enums;
using WaterFlow.Framework.Systems.GameDataManagement;
using WaterFlow.Framework.Systems.LoadObject;
using UnityEngine;

namespace WaterFlow.Framework.Systems.AudioManagement
{
    [CreateAssetMenu(fileName = "DefaultAudioService", menuName = "WaterFlow Services/Audio Service")]
    public class DefaultAudioService : AudioService, IServiceInitialize
    {
        [BoxGroup("SERVICES", true)] [SerializeField]
        protected Service<DataService> dataService = new();

        [BoxGroup("SERVICES", true)] [SerializeField]
        protected Service<LoadObjectServiceAsync> loadServiceAsync = new();

        [BoxGroup("CONFIGS", true)] [Range(0, 1)] [SerializeField]
        private float volumeDefault = 1;

        protected readonly Dictionary<string, AudioClip> audioClips = new(StringComparer.Ordinal);
        protected readonly Dictionary<AudioTracks, float> audioStates = new();
        protected string currentMusic;
        protected AudioSource musicAudioSource;
        protected AudioSource soundAudioSource;


        public void Initialize()
        {
            var audioManager = new GameObject("AudioManager");
            musicAudioSource = audioManager.AddComponent<AudioSource>();
            soundAudioSource = audioManager.AddComponent<AudioSource>();
            DontDestroyOnLoad(audioManager.gameObject);
            Debug.Log("AudioManager initialized");
        }

        public override float GetVolume(AudioTracks audioTracks)
        {
            if (audioStates.TryGetValue(audioTracks, out var volume)) return volume;

            volume = dataService.Instance.GetFloat($"{audioTracks}_Volume", volumeDefault);
            audioStates.Add(audioTracks, volume);
            return volume;
        }

        public override void SetVolume(AudioTracks audioTrack, float volume)
        {
            dataService.Instance.SetFloat($"{audioTrack}_Volume", volume);
            if (!audioStates.TryAdd(audioTrack, volume)) audioStates[audioTrack] = volume;
        }

        public override bool IsMuted(AudioTracks audioTracks)
        {
            return GetVolume(audioTracks) == 0;
        }

        private void PlayMusic(float duration = 0.5f, bool loop = true)
        {
            if (!IsMuted(AudioTracks.Music))
            {
                musicAudioSource.loop = loop;
                musicAudioSource.Play();
                musicAudioSource.DOFade(1, duration);
            }
            else
            {
                musicAudioSource.Stop();
            }
        }

        private readonly HashSet<string> _currentLoadSound = new();

        public override async UniTask<AudioClip> LoadAudioAsync(string soundName)
        {
            if (audioClips.TryGetValue(soundName, out var audio)) return audio;
            if (_currentLoadSound.Contains(soundName))
            {
                await UniTask.WaitWhile(() => _currentLoadSound.Contains(soundName));
                if (audioClips.TryGetValue(soundName, out var newAudioCache)) return newAudioCache;
            }

            _currentLoadSound.Add(soundName);
            audio = await loadServiceAsync.Instance.LoadAsync<AudioClip>(soundName);
            _currentLoadSound.Remove(soundName);
            if (audio != null)
            {
                audioClips.TryAdd(soundName, audio);
                return audio;
            }

            return null;
        }

        public override void PlayMusic(AudioId music, bool loop = true, float volume = 1)
        {
            PlayMusic(music.ToString(), loop, volume).Forget();
        }

        public override void PlaySound(AudioId soundName, float volume = 1)
        {
            PlaySound(soundName.ToString(), volume).Forget();
        }

        public override void PlayAudio(AudioId audioId, AudioClip audioClip, float volume = 1,
            AudioTracks audioTrack = AudioTracks.Sound)
        {
            PlayAudio(audioId.ToString(), audioClip, volume, audioTrack);
        }

        public override async UniTaskVoid PlayMusic(string music, bool loop = true, float volume = 1)
        {
            float fadeDuration = 0;
            if (musicAudioSource.clip != null && musicAudioSource.isPlaying) fadeDuration = 0.5f;

            var audio = await LoadAudioAsync(music);
            musicAudioSource.DOFade(0, fadeDuration)
                .onComplete = () =>
            {
                if (audio != null)
                {
                    musicAudioSource.clip = audio;
                    currentMusic = music;
                }

                PlayMusic(fadeDuration);
            };
        }

        public override async UniTaskVoid PlaySound(string soundName, float volume = 1, bool dunking = false)
        {
            if (IsMuted(AudioTracks.Sound)) return;
            var audio = await LoadAudioAsync(soundName);
            if (audio == null) return;
            soundAudioSource.PlayOneShot(audio, volume * GetVolume(AudioTracks.Sound));
        }

        public override void PlayAudio(string audioId, AudioClip audioClip, float volume = 1, AudioTracks audioTrack = AudioTracks.Sound)
        {
            switch (audioTrack)
            {
                case AudioTracks.Sound when IsMuted(AudioTracks.Sound):
                    return;
                case AudioTracks.Sound:
                    soundAudioSource.PlayOneShot(audioClip, volume * GetVolume(AudioTracks.Sound));
                    break;
                case AudioTracks.Music when IsMuted(AudioTracks.Music):
                    return;
                case AudioTracks.Music:
                {
                    float fadeDuration = 0;
                    if (musicAudioSource.clip != null && musicAudioSource.isPlaying) fadeDuration = 1f;

                    musicAudioSource.DOFade(0, fadeDuration)
                        .onComplete = () =>
                    {
                        musicAudioSource.clip = audioClip;
                        currentMusic = audioId;
                        PlayMusic(volume);
                    };
                    break;
                }
            }
        }


        public void FadeVolume(float volume, float duration, float delay = 0f)
        {
            if (IsMuted(AudioTracks.Music)) return;

            if (musicAudioSource.clip != null)
                musicAudioSource.DOFade(volume, duration)
                    .SetDelay(delay);
        }

        public override string GetCurrentMusic()
        {
            return currentMusic;
        }

        public override void StopSound()
        {
            soundAudioSource.Stop();
        }

        public override void StopMusic()
        {
            musicAudioSource.Stop();
        }

        public override void ResumeMusic()
        {
            if (string.IsNullOrEmpty(currentMusic)) return;
            PlayMusic(currentMusic).Forget();
        }
    }
}