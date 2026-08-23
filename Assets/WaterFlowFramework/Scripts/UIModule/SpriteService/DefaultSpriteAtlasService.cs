using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using WaterFlow.Framework.UIModule.UIElements;
using WaterFlow.Framework.Systems;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.U2D;
#endif

namespace WaterFlow.Framework.UIModule.SpriteService
{
    [CreateAssetMenu(fileName = "DefaultSpriteAtlasService", menuName = "WaterFlow Services/Atlas Service")]
    public class DefaultSpriteAtlasService : SpriteAtlasService
    {
        [Required][SerializeField] private SpriteAtlasData[] spriteAtlasDatas;

        public override Sprite GetSprite(string spriteName)
        {
            return GetSprite(spriteName, SpriteAtlasIndex.GameResource);
        }

        public Sprite GetSprite(string spriteName, SpriteAtlasIndex spriteAtlasIndex)
        {
            var spriteAtlasData = spriteAtlasDatas.FirstOrDefault(x => x.index == spriteAtlasIndex);
            if (spriteAtlasData == null) return null;
            return spriteAtlasData.spriteAtlas.GetSprite(spriteName);
        }

#if UNITY_EDITOR
        private static DefaultSpriteAtlasService editorService;

        public static Sprite GetSpriteInEditor(string spriteName, SpriteAtlasIndex spriteAtlasIndex)
        {
            if (editorService == null)
            {
                var guids = AssetDatabase.FindAssets($"t:{nameof(DefaultSpriteAtlasService)}");
                if (guids.Length == 0) return null;
                editorService =
                    AssetDatabase.LoadAssetAtPath<DefaultSpriteAtlasService>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            return editorService == null ? null : editorService.FindPackedSprite(spriteName, spriteAtlasIndex);
        }

        private Sprite FindPackedSprite(string spriteName, SpriteAtlasIndex spriteAtlasIndex)
        {
            var spriteAtlasData = spriteAtlasDatas?.FirstOrDefault(x => x.index == spriteAtlasIndex);
            if (spriteAtlasData?.spriteAtlas == null) return null;

            foreach (var packable in spriteAtlasData.spriteAtlas.GetPackables())
            {
                var packablePath = AssetDatabase.GetAssetPath(packable);
                if (string.IsNullOrEmpty(packablePath)) continue;

                if (!AssetDatabase.IsValidFolder(packablePath))
                {
                    var directSprite = LoadSpriteAtPath(packablePath, spriteName);
                    if (directSprite != null) return directSprite;
                    continue;
                }

                foreach (var guid in AssetDatabase.FindAssets($"{spriteName} t:Sprite", new[] { packablePath }))
                {
                    var sprite = LoadSpriteAtPath(AssetDatabase.GUIDToAssetPath(guid), spriteName);
                    if (sprite != null) return sprite;
                }
            }

            return null;
        }

        private static Sprite LoadSpriteAtPath(string assetPath, string spriteName)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (asset is Sprite sprite && sprite.name == spriteName) return sprite;
            }

            return null;
        }
#endif
    }
    [Serializable]
    public class SpriteAtlasData
    {
        public SpriteAtlasIndex index;
        public SpriteAtlas spriteAtlas;
    }

    [Serializable]
    public enum SpriteAtlasIndex
    {
        GameResource = 0,
        SubGameResource = 1,
        Coin = 2,
    }
    public static class CoinIconProvider
    {
        public static Sprite GetCoinSprite(int quantity)
        {
            return GameSystem.GetService<DefaultSpriteAtlasService>()
                .GetSprite(GetCoinSpriteName(quantity), SpriteAtlasIndex.Coin);
        }

        public static string GetCoinSpriteName(int quantity)
        {
            string spriteName;
            if (quantity >= 5000)
                spriteName = "ico_coin_6";
            else if (quantity >= 2500)
                spriteName = "ico_coin_5";
            else if (quantity >= 1000)
                spriteName = "ico_coin_4";
            else if (quantity >= 500)
                spriteName = "ico_coin_3";
            else if (quantity >= 50)
                spriteName = "ico_coin_2";
            else
                spriteName = "ico_coin_1";

            return spriteName;
        }
    }

    public static class SpriteAtlasServiceExtension
    {
        public static void SetIcon(this Image image, string spriteName)
        {
            if (image == null) return;
            Sprite sprite = GameSystem.GetService<SpriteAtlasService>().GetSprite(spriteName);
            if (sprite == null)
            {
                Debug.LogError($"Sprite {spriteName} not found");
            }

            image.sprite = sprite;
        }

        public static void SetIcon(this FixedImageRatio image, string spriteName)
        {
            if (image == null) return;
            Sprite sprite = GameSystem.GetService<SpriteAtlasService>().GetSprite(spriteName);
            if (sprite == null)
            {
                Debug.LogError($"Sprite {spriteName} not found");
            }

            image.SetSprite(sprite);
        }
    }
}