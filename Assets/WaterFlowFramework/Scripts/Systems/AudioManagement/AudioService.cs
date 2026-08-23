using System;
using Cysharp.Threading.Tasks;
using WaterFlow.Enums;
using UnityEngine;

namespace WaterFlow.Framework.Systems.AudioManagement
{
    public abstract class AudioService : ServiceSo
    {
        public Action<AudioTracks> onAudioUpdate;

        public abstract float GetVolume(AudioTracks audioTrack);

        public abstract void SetVolume(AudioTracks audioTrack, float volume);

        public abstract bool IsMuted(AudioTracks audioTracks);

        public abstract UniTask<AudioClip> LoadAudioAsync(string soundName);

        public abstract void PlayMusic(AudioId music, bool loop = true, float volume = 1);

        public abstract void PlaySound(AudioId soundName, float volume = 1);

        public abstract void PlayAudio(AudioId audioId, AudioClip audioClip, float volume = 1,
            AudioTracks audioTrack = AudioTracks.Sound);

        public abstract UniTaskVoid PlayMusic(string music, bool loop = true, float volume = 1);

        public abstract UniTaskVoid PlaySound(string soundName, float volume = 1, bool dunking = false);

        public abstract void PlayAudio(string audioId, AudioClip audioClip, float volume = 1,
            AudioTracks audioTrack = AudioTracks.Sound);

        public abstract string GetCurrentMusic();
        public abstract void StopSound();
        public abstract void StopMusic();
        public abstract void ResumeMusic();
    }

    public enum AudioTracks : byte
    {
        Music = 0,
        Sound = 1
    }
}