﻿using ForgeModGenerator.Models;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace ForgeModGenerator.SoundGenerator.Models
{
    public class SoundJsonUpdater : JsonUpdater<SoundEvent>
    {
        public SoundJsonUpdater(IEnumerable<SoundEvent> target, string jsonPath) : base(target, jsonPath) { }
        public SoundJsonUpdater(IEnumerable<SoundEvent> target, string jsonPath, JsonSerializerSettings settings) : base(target, jsonPath, settings) { }
        public SoundJsonUpdater(IEnumerable<SoundEvent> target, string jsonPath, JsonConverter converter) : base(target, jsonPath, converter) { }
        public SoundJsonUpdater(IEnumerable<SoundEvent> target, string jsonPath, Formatting formatting, JsonSerializerSettings settings) : base(target, jsonPath, formatting, settings) { }
        public SoundJsonUpdater(IEnumerable<SoundEvent> target, string jsonPath, Formatting formatting, JsonConverter converter) : base(target, jsonPath, formatting, converter) { }

        public override bool IsValidToSerialize()
        {
            foreach (SoundEvent soundEvent in Target)
            {
                System.Windows.Controls.ValidationResult result = soundEvent.IsValid(Target);
                if (!result.IsValid)
                {
                    Log.Warning($"Cannot serialize json. {soundEvent.EventName} is not valid. Reason: {result.ErrorContent}", true);
                    return false;
                }
            }
            return true;
        }

        public bool JsonContains(SoundEvent soundEvent, Sound sound)
        {
            string json = GetJsonFromFile();
            string itemJson = Serialize();
            if (json.Contains(itemJson))
            {
                itemJson = JsonConvert.SerializeObject(Target, Formatting == Formatting.Indented ? Formatting.None : Formatting.Indented);
            }
            return json.Contains(itemJson);
        }
    }
}