﻿using ForgeModGenerator.Converters;
using ForgeModGenerator.Models;
using ForgeModGenerator.ModGenerator.Models;
using ForgeModGenerator.SoundGenerator.Validations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections.Generic;
using System.IO;
using System.Windows.Controls;

namespace ForgeModGenerator.SoundGenerator.Models
{
    [JsonConverter(typeof(SoundConverter))]
    public class Sound : FileItem
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public enum SoundType { file, @event }

        private Sound() { }

        public Sound(string modid, string filePath)
        {
            if (modid == null)
            {
                throw new System.ArgumentNullException(nameof(modid));
            }

            if (File.Exists(filePath))
            {
                Name = FormatSoundPathFromFullPath(modid, filePath);
                SetInfo(filePath);
            }
            else
            {
                Name = filePath;
            }
            IsDirty = false;
        }

        public string ShortPath {
            get => GetRelativePathFromSoundPath(Name);
            set => Name = FormatSoundPath(Mod.GetModidFromPath(Name), value);
        }

        private string name;
        public string Name {
            get => name;
            set {
                DirtSet(ref name, value);
                RaisePropertyChanged(nameof(ShortPath));
            }
        }

        private float volume = 1.0f;
        public float Volume {
            get => volume;
            set => DirtSet(ref volume, Math.Clamp(value));
        }

        private float pitch = 1.0f;
        public float Pitch {
            get => pitch;
            set => DirtSet(ref pitch, value);
        }

        private int weight = 1;
        public int Weight {
            get => weight;
            set => DirtSet(ref weight, value);
        }

        private bool stream = false;
        public bool Stream {
            get => stream;
            set => DirtSet(ref stream, value);
        }

        private int attenuationDistance;
        public int AttenuationDistance {
            get => attenuationDistance;
            set => DirtSet(ref attenuationDistance, value);
        }

        private bool preload = false;
        public bool Preload {
            get => preload;
            set => DirtSet(ref preload, value);
        }

        private SoundType type = SoundType.file;
        public SoundType Type {
            get => type;
            set => DirtSet(ref type, value);
        }

        public override object DeepClone()
        {
            Sound sound = new Sound() {
                Name = Name,
                Volume = Volume,
                Pitch = Pitch,
                Weight = Weight,
                Stream = Stream,
                AttenuationDistance = AttenuationDistance,
                Preload = Preload,
                Type = Type
            };
            sound.SetInfo(Info.FullName);
            return sound;
        }

        public override bool CopyValues(object fromCopy)
        {
            if (fromCopy is Sound sound)
            {
                Name = sound.Name;
                Volume = sound.Volume;
                Pitch = sound.Pitch;
                Weight = sound.Weight;
                Stream = sound.Stream;
                AttenuationDistance = sound.AttenuationDistance;
                Preload = sound.Preload;
                Type = sound.Type;

                base.CopyValues(fromCopy);
                IsDirty = false;
                return true;
            }
            return false;
        }

        public void FormatName()
        {
            Name = FormatSoundPathFromFullPath(Mod.GetModidFromPath(Name), Info.FullName);
        }

        public ValidationResult IsValid(IEnumerable<Sound> sounds)
        {
            SoundRules rules = new SoundRules();
            SoundValidationDependencyWrapper parameters = new SoundValidationDependencyWrapper() {
                Sounds = sounds,
                SoundBeforeChange = this
            };
            ValidationResult result = rules.ValidateShortPath(ShortPath, parameters);
            return result;
        }

        public static string GetModidFromSoundPath(string path) => path.Substring(0, path.IndexOf(":"));
        public static string GetRelativePathFromSoundPath(string path) => path.Remove(0, path.IndexOf(":") + 1);

        // Get formatted sound from short path, "modid:shortPath"
        public static string FormatSoundPath(string modid, string shortPath) => $"{modid}:{shortPath}";

        // Get formatted sound from full path, "modid:shorten/path/toFile"
        public static string FormatSoundPathFromFullPath(string modid, string path)
        {
            string shortPath = FormatSoundShortPath(path);
            return $"{modid}:{shortPath}";
        }

        // Get formatted sound from full path, "shorten/path/toFile"
        public static string FormatSoundShortPath(string fullPath)
        {
            int startIndex = fullPath.IndexOf("sounds") + 7;
            if (startIndex == -1)
            {
                return null;
            }
            return Path.ChangeExtension(fullPath.Substring(startIndex, fullPath.Length - startIndex), null).Replace("\\", "/");
        }
    }
}