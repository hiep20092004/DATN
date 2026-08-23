using Haptic = WaterFlow.Core.Haptic;
using WaterFlow.Enums;
using UnityEngine;
using UnityEngine.EventSystems;

namespace WaterFlow.Framework.Systems.AudioManagement
{
    public class ButtonSoundPlayer : AudioPlayer, IPointerClickHandler
    {
        [SerializeField] private bool playHaptic = true;

        private void Awake()
        {
            audioId = AudioId.ButtonClick;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            PlayAudio();
            if (audioId == AudioId.ButtonClick && playHaptic) Haptic.Play(Haptic.PATTERN_BUTTON_CLICK);
        }
    }
}
