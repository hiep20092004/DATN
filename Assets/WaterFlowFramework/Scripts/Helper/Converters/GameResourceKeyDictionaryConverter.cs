using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using WaterFlow.Enums;
using WaterFlow.Framework.Systems.InventoryManagement.GameResources;

namespace WaterFlow.Framework.Helper.Converters
{
    public class GameResourceKeyDictionaryConverter<T> : JsonConverter<Dictionary<GameResourceKey, T>>
    {
        public override void WriteJson(JsonWriter writer,
            Dictionary<GameResourceKey, T> value,
            JsonSerializer serializer)
        {
            writer.WriteStartObject();

            foreach (var kv in value)
            {
                var keyString = $"{kv.Key.gameResource}_{kv.Key.id}";
                writer.WritePropertyName(keyString);

                serializer.Serialize(writer, kv.Value);
            }

            writer.WriteEndObject();
        }

        public override Dictionary<GameResourceKey, T> ReadJson(JsonReader reader,
            Type objectType,
            Dictionary<GameResourceKey, T> existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            var dict = new Dictionary<GameResourceKey, T>();

            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject)
                    break;

                string keyString = (string)reader.Value;
                var parts = keyString.Split('_');

                var key = new GameResourceKey
                {
                    gameResource = Enum.Parse<GameResource>(parts[0]),
                    id = int.Parse(parts[1])
                };

                reader.Read(); // move to value
                T value = serializer.Deserialize<T>(reader);

                dict[key] = value;
            }

            return dict;
        }
    }
}