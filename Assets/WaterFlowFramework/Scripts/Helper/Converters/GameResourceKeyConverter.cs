using System;
using Newtonsoft.Json;
using WaterFlow.Enums;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;

namespace WaterFlow.Framework.Helper.Converters
{
    public class GameResourceKeyConverter : JsonConverter<GameResourceKey>
    {
        public override void WriteJson(JsonWriter writer, GameResourceKey value, JsonSerializer serializer)
        {
            writer.WriteValue($"{value.gameResource}_{value.id}");
        }

        public override GameResourceKey ReadJson(JsonReader reader, Type objectType, GameResourceKey existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            string s = (string)reader.Value;
            var parts = s.Split('_');

            return new GameResourceKey
            {
                gameResource = Enum.Parse<GameResource>(parts[0]),
                id = int.Parse(parts[1])
            };
        }
    }
}