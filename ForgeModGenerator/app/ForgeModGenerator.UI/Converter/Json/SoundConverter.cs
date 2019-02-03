﻿using ForgeModGenerator.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace ForgeModGenerator.Converter
{
    public class SoundConverter : JsonConverter<Sound>
    {
        public override Sound ReadJson(JsonReader reader, Type objectType, Sound existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            JObject item = JObject.Load(reader);
            string name = item.GetValue("name").ToObject<string>();
            string modid = name.Substring(0, name.IndexOf(":"));
            Sound sound = new Sound(modid, name);
            if (item.TryGetValue("volume", out JToken volume))
            {
                sound.Volume = volume.ToObject<float>();
            }
            if (item.TryGetValue("pitch", out JToken pitch))
            {
                sound.Pitch = pitch.ToObject<float>();
            }
            if (item.TryGetValue("weight", out JToken weight))
            {
                sound.Weight = weight.ToObject<int>();
            }
            if (item.TryGetValue("stream", out JToken stream))
            {
                sound.Stream = stream.ToObject<bool>();
            }
            if (item.TryGetValue("attenuation_distance", out JToken attenuation_distance))
            {
                sound.AttenuationDistance = attenuation_distance.ToObject<int>();
            }
            if (item.TryGetValue("preload", out JToken preload))
            {
                sound.Preload = preload.ToObject<bool>();
            }
            if (item.TryGetValue("type", out JToken type))
            {
                sound.Type = preload.ToObject<Sound.SoundType>();
            }
            sound.IsDirty = false;
            return sound;
        }

        public override void WriteJson(JsonWriter writer, Sound value, JsonSerializer serializer)
        {
            JObject jo = new JObject {
                { nameof(Sound.Name).ToLower(), value.Name },
                { nameof(Sound.Volume).ToLower(), value.Volume },
                { nameof(Sound.Pitch).ToLower(), value.Pitch },
                { nameof(Sound.Weight).ToLower(), value.Weight },
                { nameof(Sound.Stream).ToLower(), value.Stream },
                { nameof(Sound.AttenuationDistance).ToLower(), value.AttenuationDistance },
                { nameof(Sound.Preload).ToLower(), value.Preload },
                { nameof(Sound.Type).ToLower(), JToken.FromObject(value.Type, serializer) }
            };
            jo.WriteTo(writer);
        }
    }
}
