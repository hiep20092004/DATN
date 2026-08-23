using WaterFlow.Enums;
using UnityEngine;

namespace WaterFlow.Framework.Systems.AudioManagement
{
    public class LocalMusic : MonoBehaviour
    {
        [SerializeField] private AudioId music;
        [SerializeField] private string musicName;
        [Range(0f, 1f)] [SerializeField] private float volume = 1;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool backToLastMusic = false;
        [SerializeField] private Service<AudioService> audioService = new();
        private string lastMusic;

        private void OnEnable()
        {
            lastMusic = audioService.Instance.GetCurrentMusic();
            musicName = music == AudioId.None? musicName : music.ToString(); 
            audioService.Instance.PlayMusic(musicName, loop, volume);
        }

        private void OnDisable()
        {
            if (backToLastMusic && !string.IsNullOrEmpty(lastMusic)) audioService.Instance.PlayMusic(lastMusic);
        }
    }
}