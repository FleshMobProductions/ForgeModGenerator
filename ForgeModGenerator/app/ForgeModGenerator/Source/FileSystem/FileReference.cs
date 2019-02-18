﻿using ForgeModGenerator.Utility;
using GalaSoft.MvvmLight;
using System;
using System.Collections.Generic;
using System.IO;

namespace ForgeModGenerator
{
    // Base class that manages file references in application and synchronizes it with explorer
    public abstract class FileSystemInfoReference : ObservableObject
    {
        protected abstract class RefCounter
        {
            public FileSystemInfo File { get; }
            public int ReferenceCount;

            public RefCounter(string filePath)
            {
                File = CreateInstance(filePath);
                ReferenceCount = 1;
            }
            protected abstract FileSystemInfo CreateInstance(string path);
        }

        protected sealed class FileRefCounter : RefCounter
        {
            public FileRefCounter(string filePath) : base(filePath) { }
            protected override FileSystemInfo CreateInstance(string path) => new FileInfo(path);
        }

        protected sealed class DirectoryRefCounter : RefCounter
        {
            public DirectoryRefCounter(string filePath) : base(filePath) { }
            protected override FileSystemInfo CreateInstance(string path) => new DirectoryInfo(path);
        }

        public FileSystemInfoReference(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }
            FileSystemInfo = GetOrCreateInfo(path);
        }

        private static Dictionary<string, RefCounter> references = new Dictionary<string, RefCounter>();

        private FileSystemInfo fileSystemInfo;
        public FileSystemInfo FileSystemInfo {
            get => fileSystemInfo;
            protected set {
                if (Set(ref fileSystemInfo, value))
                {
                    changeName = FileSystemInfo is DirectoryInfo ? Name : Path.GetFileNameWithoutExtension(Name);
                    RaisePropertyChanged(nameof(Name));
                    RaisePropertyChanged(nameof(FullName));
                    RaisePropertyChanged(nameof(ChangeName));
                }
            }
        }

        private string changeName;
        public string ChangeName {
            get => changeName;
            set {
                string newPath = null;
                if (FileSystemInfo is DirectoryInfo)
                {
                    newPath = Path.Combine(new DirectoryInfo(FullName).Parent.FullName, value);
                    Directory.Move(FullName, newPath);
                }
                else if (FileSystemInfo is FileInfo)
                {
                    string valueWithExt = value + Path.GetExtension(Name);
                    newPath = Path.Combine(new FileInfo(FullName).Directory.FullName, valueWithExt);
                    File.Move(FullName, newPath);
                }
                SetInfo(newPath);
                Set(ref changeName, value);
            }
        }

        public string Name => FileSystemInfo.Name;
        public string FullName => FileSystemInfo.FullName;

        public int GetReferenceCount() => GetReferenceCount(FullName);

        public virtual void SetInfo(string newPath)
        {
            newPath = newPath.NormalizeFullPath();
            Remove();
            if (references.TryGetValue(newPath, out RefCounter newReference))
            {
                FileSystemInfo = newReference.File;
                newReference.ReferenceCount++;
            }
            else
            {
                AddReference(newPath);
                FileSystemInfo = references[newPath].File;
            }
        }

        /// <summary> Removes file reference </summary>
        public void Remove()
        {
            string path = FullName.NormalizeFullPath();
            RemoveReference(path);
        }

        protected virtual FileSystemInfo GetOrCreateInfo(string filePath)
        {
            filePath = filePath.NormalizeFullPath();
            AddReference(filePath);
            return references[filePath].File;
        }

        protected void AddReference(string filePath)
        {
            if (filePath == null)
            {
                return;
            }
            filePath = filePath.NormalizeFullPath();
            if (references.ContainsKey(filePath))
            {
                references[filePath].ReferenceCount++;
            }
            else
            {
                references[filePath] = CreateRefCounter(filePath);
            }
        }

        protected static bool RemoveReference(string filePath)
        {
            if (filePath == null)
            {
                return false;
            }
            filePath = filePath.NormalizeFullPath();
            if (references.ContainsKey(filePath))
            {
                references[filePath].ReferenceCount--;
                if (references[filePath].ReferenceCount <= 0)
                {
                    return references.Remove(filePath);
                }
                return true;
            }
            return false;
        }

        public static bool IsReferenced(string filePath) => GetReferenceCount(filePath.NormalizeFullPath()) > 0;
        public static int GetReferenceCount(string filePath) => references.TryGetValue(filePath.NormalizeFullPath(), out RefCounter refCounter) ? refCounter.ReferenceCount : 0;

        protected abstract RefCounter CreateRefCounter(string path);
    }

    // Wrapper for file references
    public sealed class FileInfoReference : FileSystemInfoReference
    {
        public FileInfoReference(string filePath) : base(filePath) { }
        protected override RefCounter CreateRefCounter(string path) => new FileRefCounter(path);
    }

    // Wrapper for directory references
    public sealed class DirectoryInfoReference : FileSystemInfoReference
    {
        public DirectoryInfoReference(string path) : base(IOHelper.GetDirectoryPath(path)) { }
        protected override RefCounter CreateRefCounter(string path) => new DirectoryRefCounter(IOHelper.GetDirectoryPath(path));
    }
}