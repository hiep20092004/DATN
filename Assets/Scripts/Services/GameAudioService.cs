using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Sirenix.OdinInspector;
using WaterFlow.Enums;
using WaterFlow.Framework.Systems;
using WaterFlow.Framework.Systems.AudioManagement;
using WaterFlow.Framework.Systems.GameDataManagement;
using WaterFlow.Framework.Systems.LoadObject;
using UnityEngine;

[CreateAssetMenu(fileName = "GameAudioService", menuName = "Services/Audio/GameAudioService")]
public class GameAudioService : AudioService, IServiceInitializeAsync
{
    [BoxGroup("SERVICES", true)] [SerializeField]
    protected Service<DataService> dataService = new();

    [BoxGroup("SERVICES", true)] [SerializeField]
    protected Service<LoadObjectServiceAsync> loadServiceAsync = new();

    [BoxGroup("CONFIGS", true)] [Range(0, 1)] [SerializeField]
    private float volumeDefault = 1;

    [BoxGroup("PREWARM SETTINGS", true)] [SerializeField]
    private bool autoPrewarmOnInit = true;

    [BoxGroup("PREWARM SETTINGS", true)] [SerializeField] [Tooltip("Danh sách AudioId sẽ được load sẵn khi Initialize")]
    private List<AudioId> prewarmAudioList = new()
    {
        AudioId.ButtonClick,
        AudioId.Block_Pick,
        AudioId.Block_Put,
        AudioId.Block_Clear,
        AudioId.Block_Fill_water_Short,
        AudioId.Block_Fill_water_Medium,
        AudioId.Block_Fill_water_Long,
        AudioId.Block_Fill_water_SuperLong
    };
    
    [BoxGroup("DUCKING SETTINGS", true)] [SerializeField]
    private List<string> duckingTriggerSounds = new()
    {
        nameof(AudioId.Win),
        nameof(AudioId.Lose),
        nameof(AudioId.Obstacle_Bomb_Explosion)
    };
    [BoxGroup("DUCKING SETTINGS", true)] [Range(0, 1)] [SerializeField]
    private float duckingVolume = 0.3f; 

    [BoxGroup("DUCKING SETTINGS", true)] [SerializeField]
    private float duckingFadeDuration = 0.3f; 
    
    [BoxGroup("PREWARM SETTINGS", true)]
    [SerializeField]
    [Tooltip("Load theo batch để tránh lag, mỗi batch load bao nhiêu audio")]
    private int prewarmBatchSize = 3;

    [BoxGroup("PREWARM SETTINGS", true)] [SerializeField] [Tooltip("Delay giữa các batch (milliseconds)")]
    private int prewarmBatchDelay = 100;

    protected readonly Dictionary<string, AudioClip> audioClips = new(StringComparer.Ordinal);
    protected readonly Dictionary<AudioTracks, float> audioStates = new();
    protected string currentMusic;
    protected AudioSource musicAudioSource;
    protected AudioSource soundAudioSource;

    private bool _isPrewarming;
    private int _prewarmProgress;
    private int _prewarmTotal;
    private Tween _duckingTween;
    private float _originalMusicVolume = 1f;
    private bool _isDucking;
    private CancellationTokenSource _duckingCts;

    public async UniTaskVoid InitializeAsync()
    {
        var audioManager = new GameObject("AudioManager");
        musicAudioSource = audioManager.AddComponent<AudioSource>();
        soundAudioSource = audioManager.AddComponent<AudioSource>();
        DontDestroyOnLoad(audioManager.gameObject);
        Debug.Log("GameAudioService initialized");

        if (autoPrewarmOnInit)
        {
            await PrewarmAudios(prewarmAudioList);
        }
    }

    public async UniTask PrewarmAudios(List<AudioId> audioIds)
    {
        if (audioIds == null || audioIds.Count == 0)
        {
            Debug.LogWarning("Prewarm audio list is empty");
            return;
        }

        if (_isPrewarming)
        {
            Debug.LogWarning("Prewarm is already in progress");
            return;
        }

        _isPrewarming = true;
        _prewarmTotal = audioIds.Count;
        _prewarmProgress = 0;

        Debug.Log($"[AudioService] Starting prewarm {_prewarmTotal} audios...");

        for (int i = 0; i < audioIds.Count; i += prewarmBatchSize)
        {
            var batch = audioIds.Skip(i).Take(prewarmBatchSize).ToList();
            var tasks = batch.Select(PrewarmSingleAudio).ToList();

            await UniTask.WhenAll(tasks);

            _prewarmProgress += batch.Count;

            if (i + prewarmBatchSize < audioIds.Count)
            {
                await UniTask.Delay(prewarmBatchDelay);
            }
        }

        _isPrewarming = false;
        Debug.Log($"[AudioService] Prewarm completed! Loaded {_prewarmProgress}/{_prewarmTotal} audios");
    }

    private async UniTask PrewarmSingleAudio(AudioId audioId)
    {
        try
        {
            string audioName = audioId.ToString();

            if (audioClips.ContainsKey(audioName))
            {
                Debug.Log($"[AudioService] Skip prewarm {audioName} - already loaded");
                return;
            }

            var clip = await LoadAudioAsync(audioName);
            if (clip)
            {
                Debug.Log($"[AudioService] Prewarmed: {audioName}");
            }
            else
            {
                Debug.LogWarning($"[AudioService] Failed to prewarm: {audioName}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[AudioService] Error prewarming {audioId}: {e.Message}");
        }
    }

    public async UniTask PrewarmAudiosByType(bool music = true, bool sound = true)
    {
        var audioIds = new List<AudioId>();

        foreach (AudioId audioId in Enum.GetValues(typeof(AudioId)))
        {
            if (audioId == AudioId.None) continue;

            string name = audioId.ToString();

            bool isMusic = name.StartsWith("BGM_");

            if ((isMusic && music) || (!isMusic && sound))
            {
                audioIds.Add(audioId);
            }
        }

        await PrewarmAudios(audioIds);
    }

    public float GetPrewarmProgress()
    {
        if (_prewarmTotal == 0) return 1f;
        return (float)_prewarmProgress / _prewarmTotal;
    }

    public bool IsPrewarming => _isPrewarming;

    public void UnloadAudio(AudioId audioId)
    {
        string audioName = audioId.ToString();
        if (audioClips.Remove(audioName))
        {
            Debug.Log($"[AudioService] Unloaded: {audioName}");
        }
    }

    public void UnloadAllAudios(bool keepCurrentMusic = true)
    {
        if (keepCurrentMusic && !string.IsNullOrEmpty(currentMusic))
        {
            var keysToRemove = audioClips.Keys.Where(k => k != currentMusic).ToList();
            foreach (var key in keysToRemove)
            {
                audioClips.Remove(key);
            }

            Debug.Log($"[AudioService] Unloaded all audios except current music: {currentMusic}");
        }
        else
        {
            audioClips.Clear();
            Debug.Log("[AudioService] Unloaded all audios");
        }
    }

    public int GetCachedAudioCount() => audioClips.Count;

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

            PlayMusic(fadeDuration, loop);
        };
    }

    public override async UniTaskVoid PlaySound(string soundName, float volume = 1, bool dunking = false)
    {
        if (IsMuted(AudioTracks.Sound)) return;
    
        var audio = await LoadAudioAsync(soundName);
        if (audio == null) return;
        soundAudioSource.PlayOneShot(audio, volume * GetVolume(AudioTracks.Sound));
        if (!IsMuted(AudioTracks.Music) && (dunking || duckingTriggerSounds.Contains(soundName)))
        {
            await DuckMusic(audio.length);
        }
    }

    private async UniTask DuckMusic(float soundDuration)
    {
        if (!musicAudioSource || !musicAudioSource.isPlaying) return;

        _duckingCts?.Cancel();
        _duckingCts?.Dispose();
        _duckingCts = new CancellationTokenSource();
        var token = _duckingCts.Token;

        _duckingTween?.Kill();

        // Only capture the original volume when not already ducking,
        // otherwise we'd save the already-reduced ducked volume.
        if (!_isDucking)
        {
            _originalMusicVolume = musicAudioSource.volume;
        }

        _isDucking = true;

        _duckingTween = musicAudioSource.DOFade(
            duckingVolume * GetVolume(AudioTracks.Music),
            duckingFadeDuration
        );

        var isCancelled = await UniTask.Delay(
            TimeSpan.FromSeconds(soundDuration + duckingFadeDuration),
            cancellationToken: token
        ).SuppressCancellationThrow();

        if (isCancelled) return;

        _isDucking = false;
        _duckingTween?.Kill();
        _duckingTween = musicAudioSource.DOFade(
            _originalMusicVolume,
            duckingFadeDuration
        );
    }

    public override void PlayAudio(string audioId, AudioClip audioClip, float volume = 1,
        AudioTracks audioTrack = AudioTracks.Sound)
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
        StopDucking();
    }

    private void StopDucking()
    {
        if (_duckingCts == null) return;

        _duckingCts.Cancel();
        _duckingCts.Dispose();
        _duckingCts = null;

        _isDucking = false;
        _duckingTween?.Kill();

        if (musicAudioSource && musicAudioSource.isPlaying)
        {
            _duckingTween = musicAudioSource.DOFade(
                _originalMusicVolume,
                duckingFadeDuration
            );
        }
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
