using System;
using System.Collections.Generic;
using WaterFlow.Enums;
using UnityEngine;

namespace WaterFlow.Framework.Systems.AudioManagement
{
    [CreateAssetMenu(menuName = "WaterFlow/Misc/Audio Group", fileName = "AudioGroup")]
    public class AudioGroup : ScriptableObject
    {
        public List<AudioConfig> audioConfigs;
        [SerializeField] private Service<AudioService> audioService = new ();

        public void PlayAudio(AudioId audioId, AudioTracks tracks = AudioTracks.Sound)
        {
            var audioConfig = audioConfigs.Find(x => x.audioId == audioId);
            if (audioConfig == null) return;
            audioService.Instance.PlayAudio(audioConfig.audioId, audioConfig.audioClip, audioConfig.volume);
        }
    }

    [Serializable]
    public class AudioConfig
    {
        public AudioId audioId;
        public AudioClip audioClip;
        public float volume;
    }
}