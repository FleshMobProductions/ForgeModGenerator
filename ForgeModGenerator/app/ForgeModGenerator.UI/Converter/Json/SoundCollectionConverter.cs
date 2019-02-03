﻿using ForgeModGenerator.Miscellaneous;
using ForgeModGenerator.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;

namespace ForgeModGenerator.Converter
{
    public class SoundCollectionConverter : JsonConverter
    {
        public string ModName { get; set; }
        public string Modid { get; set; }

        protected static readonly StringBuilder builder = new StringBuilder(32);
        protected static readonly StringBuilder itemBuilder = new StringBuilder(32);

        public SoundCollectionConverter(string modname, string modid)
        {
            ModName = modname;
            Modid = modid;
        }

        public override bool CanConvert(Type objectType) => typeof(ObservableCollection<SoundEvent>).IsAssignableFrom(objectType);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            string soundsPath = ModPaths.SoundsFolder(ModName, Modid);
            ObservableCollection<SoundEvent> fileList = new ObservableCollection<SoundEvent>();
            JObject item = JObject.Load(reader);

            foreach (KeyValuePair<string, JToken> property in item)
            {
                SoundEvent soundEvent = item.GetValue(property.Key).ToObject<SoundEvent>();
                soundEvent.EventName = property.Key;
                soundEvent.SetInfo(soundsPath);

                foreach (Sound sound in soundEvent.Files)
                {
                    string soundName = sound.Name;
                    int modidLength = sound.Name.IndexOf(":") + 1;
                    if (modidLength != -1)
                    {
                        soundName = sound.Name.Remove(0, modidLength);
                    }
                    sound.SetInfo(Path.Combine(soundsPath, $"{soundName}.ogg"));
                    sound.IsDirty = false;
                }
                soundEvent.IsDirty = false;
                fileList.Add(soundEvent);
            }
            return fileList;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            builder.Clear();
            builder.Append("{\n");

            if (value is ObservableCollection<SoundEvent> fileList)
            {
                AppendFileListSoundEvent(fileList, true);
            }
            else
            {
                throw new JsonWriterException($"Object type was null, or not type of {typeof(ObservableCollection<SoundEvent>)}");
            }

            void AppendFileListSoundEvent(ObservableCollection<SoundEvent> val, bool removeCommaFromEnd)
            {
                for (int i = 0; i < val.Count; i++)
                {
                    SoundEvent item = val[i];
                    itemBuilder.Clear();
                    string json = JsonConvert.SerializeObject(item, Formatting.Indented);
                    itemBuilder.Append(json);

                    bool isLastElement = i < val.Count - 1;
                    if (removeCommaFromEnd && isLastElement)
                    {
                        itemBuilder.Append(',');
                    }
                    builder.Append(itemBuilder);
                }
            }

            builder.Append("\n}");
            string serializedJson = builder.ToString();
            writer.WriteRawValue(serializedJson.FormatJson(serializer.Formatting));
        }
    }
}
