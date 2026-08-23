using System;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using WaterFlow.Enums;
using WaterFlow.Framework.Helper.Converters;
using WaterFlow.Framework.Systems.TimeManagement;
using UnityEngine;

namespace WaterFlow.Framework.Systems.InventoryManagement.GameResources
{
    [Serializable]
    public class ResourceData
    {
        [FoldoutGroup("@gameResource", expanded: false)] [HorizontalGroup("@gameResource/Row1", width: 0.7f)] [LabelText("Resource")] [LabelWidth(70)]
        public GameResource gameResource;

        [FoldoutGroup("@gameResource")] [HorizontalGroup("@gameResource/Row1", width: 0.3f)] [LabelText("ID")] [LabelWidth(30)]
        public int id;

        [FoldoutGroup("@gameResource")] public int quantity;
        [FoldoutGroup("@gameResource")] public long seconds;
        [HideInInspector] public long timestamp = 0;
        [JsonIgnore] public Action onUpdate;

        [JsonIgnore] public GameResourceKey Key => new() { gameResource = this.gameResource, id = this.id };

        public ResourceData()
        {
        }

        public ResourceData(GameResource gameResource, int quantity)
        {
            this.gameResource = gameResource;
            this.quantity = quantity;
        }

        public ResourceData(GameResource gameResource, int id, int quantity)
        {
            this.gameResource = gameResource;
            this.id = id;
            this.quantity = quantity;
        }

        public ResourceData(GameResource gameResource, int id, long seconds)
        {
            this.gameResource = gameResource;
            this.id = id;
            this.seconds = seconds;
        }

        public void Add(ResourceData resourceData)
        {
            if (resourceData.gameResource == this.gameResource && resourceData.id == this.id)
            {
                this.quantity += resourceData.quantity;
                AddSeconds(resourceData.seconds);
            }
        }

        public void Multiply(float multiplier)
        {
            this.quantity = (int)(multiplier * this.quantity);
            this.seconds = (long)(multiplier * this.seconds);
        }

        public void AddSeconds(long seconds)
        {
            long now = GameSystem.GetService<TimeService>().GetUnixTimeSeconds();
            if (seconds > 0 && timestamp + this.seconds < now)
            {
                this.seconds = 0;
                timestamp = now;
            }

            this.seconds += seconds;
            onUpdate?.Invoke();
        }

        [JsonIgnore] public int EarnTrackingValue => seconds > 0 ? (int)(seconds / 60) : quantity;

        public bool CanReduce(int amount = 1)
        {
            return !IsExpired() && this.quantity >= amount;
        }

        public void Reduce(int amount)
        {
            this.quantity -= amount;
            onUpdate?.Invoke();
        }

        public bool IsExpired(bool force = false)
        {
            if (timestamp > 0)
            {
                long now = GameSystem.GetService<TimeService>().GetUnixTimeSeconds();
                return timestamp + seconds < now;
            }

            return force;
        }

        public long GetRemainingTime()
        {
            if (timestamp > 0)
            {
                long now = GameSystem.GetService<TimeService>().GetUnixTimeSeconds();
                return timestamp + seconds - now;
            }

            return seconds;
        }

        public ResourceData Clone()
        {
            return new ResourceData()
            {
                gameResource = this.gameResource,
                id = this.id,
                quantity = this.quantity,
                seconds = this.seconds,
                timestamp = this.timestamp
            };
        }
    }

    [JsonConverter(typeof(GameResourceKeyConverter))]
    public struct GameResourceKey
    {
        public GameResource gameResource;
        public int id;
        private int hashCode;

        public static bool operator ==(GameResourceKey resourceData1, GameResourceKey resourceData2)
        {
            return resourceData1.gameResource == resourceData2.gameResource && resourceData1.id == resourceData2.id;
        }

        public static bool operator !=(GameResourceKey resourceData1, GameResourceKey resourceData2)
        {
            return !(resourceData1 == resourceData2);
        }

        public bool Equals(GameResourceKey other)
        {
            return gameResource == other.gameResource && id == other.id;
        }

        public override bool Equals(object obj)
        {
            return obj is GameResourceKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                if (hashCode != 0) return hashCode;
                int hash = 17;
                hash = hash * 31 + gameResource.GetHashCode();
                hash = hash * 31 + id.GetHashCode();
                hashCode = hash;
                return hash;
            }
        }

        public override string ToString()
        {
            return string.Concat(gameResource, '_', id);
            //$"{gameResource}_{id}";
        }
    }

    [Serializable]
    public struct CurrencyData
    {
        public GameResource currency;
        public int value;
    }
}