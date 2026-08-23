using Cysharp.Threading.Tasks;
using WaterFlow.Enums;
using UnityEngine;

namespace WaterFlow.Framework.Systems.AudioManagement
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioUnit : MonoBehaviour
    {
        [Range(0f, 1f)] [SerializeField] private float volume = 1;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private Service<AudioService> audioService = new();

        private void Awake()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
        }
    }
}