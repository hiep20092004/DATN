namespace WaterFlow.Framework.Systems.SettingsManagement
{
    // public class SettingsServiceDefault: SettingsService, IServiceInitializeAsync
    // {
    // 	public Action<AudioTracks, float> OnVolumChange { get; set; }
    // 	private readonly Service<IConfigServiceAsync> configsService = new();
    // 	private readonly Service<DataService> dataService = new();
    // 	private SettingsConfig settingsConfig;
    // 	
    // 	public async UniTaskVoid InitializeAsync()
    // 	{
    // 		settingsConfig = await configsService.Instance.GetConfig<SettingsConfig>();
    // 	}
    // 	
    // 	public void SetAudioVolume(AudioTracks audioTracks, float volume)
    // 	{
    // 		dataService.Instance.SetFloat($"{audioTracks}_Volume", volume);
    // 		OnVolumChange?.Invoke(audioTracks, volume);
    // 	}
    //
    // 	public float GetAudioVolume(AudioTracks audioTracks)
    // 	{
    // 		switch (audioTracks)
    // 		{
    // 			case AudioTracks.Music:
    // 				return dataService.Instance.GetFloat($"{audioTracks}_Volume", settingsConfig.musicVolume);
    // 			case AudioTracks.Sound:
    // 				return dataService.Instance.GetFloat($"{audioTracks}_Volume", settingsConfig.soundVolume);
    // 		}
    // 		return 0f;
    // 	}
    // }
}