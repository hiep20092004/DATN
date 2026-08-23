using System;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using WaterFlow.Framework.Systems.LoadObject;
using UnityEngine;
#if using_addressable
using UnityEngine.AddressableAssets;
#endif

[CreateAssetMenu(menuName = "WaterFlow Services/Load Service/Load Addressable Json Async",
    fileName = "Load Addressable Json Async")]
public class LoadAddressableJsonAsync : LoadObjectServiceAsync
{
    private readonly TimeoutController timeoutController = new();
    [SerializeField] private float timeout = 3;
    [SerializeField] private LoadObjectServiceAsync fallback;

    public override async UniTask<T> LoadAsync<T>(string assetName) where T : class
    {
#if using_addressable
        try
        {
            string fullPath = $"{path}{assetName}.json";
            TextAsset textAsset = await Addressables.LoadAssetAsync<TextAsset>(fullPath)
                .WithCancellation(timeoutController.Timeout(TimeSpan.FromSeconds(timeout)));
            if (textAsset != null && !string.IsNullOrEmpty(textAsset.text))
            {
                return JsonConvert.DeserializeObject<T>(textAsset.text, Settings);
            }
        }
        catch (OperationCanceledException e)
        {
            if (fallback != null)
            {
                return await fallback.LoadAsync<T>(assetName);
            }
        }
#endif
        return null;
    }
}