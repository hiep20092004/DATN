using WaterFlow.Framework.Systems;
using UnityEngine;

namespace WaterFlow.Framework.UIModule.SpriteService
{
    public abstract class SpriteAtlasService: ServiceSo
    {
        public abstract Sprite GetSprite(string spriteName);
    }
}