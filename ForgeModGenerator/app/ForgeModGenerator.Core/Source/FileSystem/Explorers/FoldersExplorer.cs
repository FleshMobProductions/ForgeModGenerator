﻿using ForgeModGenerator.Services;
using ForgeModGenerator.Utility;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ForgeModGenerator
{
    public class FoldersExplorer<TFolder, TFile> : BindableBase, IFoldersExplorer<TFolder, TFile>, IDisposable
        where TFolder : class, IFolderObject<TFile>
        where TFile : class, IFileObject
    {
        public FoldersExplorer(IDialogService dialogService, IFileSystem fileSystem, IFolderSynchronizer<TFolder, TFile> fileSynchronizer)
        {
            DialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            FileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            AllowedFileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            OpenFileDialog = fileSystem.CreateFileBrowser();
            OpenFolderDialog = fileSystem.CreateFolderBrowser();
            FileSynchronizer = fileSynchronizer;
            Folders = fileSynchronizer.Factory.CreateFolders();
        }

        protected IDialogService DialogService { get; }

        protected IFileSystem FileSystem { get; }

        public IFileBrowser OpenFileDialog { get; }

        public IFolderBrowser OpenFolderDialog { get; }

        public HashSet<string> AllowedFileExtensions { get; protected set; }

        public bool HasEmptyFolders => Folders.Files.Any(x => x.Count == 0);

        public string AllowedFileExtensionsPatterns => AllowedFileExtensions != null ? "*" + string.Join("|*", AllowedFileExtensions) : "";

        private IFolderObject<TFolder> folders;
        public IFolderObject<TFolder> Folders {
            get => folders;
            protected set {
                if (folders != null)
                {
                    folders.Clear();
                }
                SetProperty(ref folders, value);
                if (folders != null && FileSynchronizer != null)
                {
                    FileSynchronizer.SyncedFolders = folders;
                }
            }
        }

        private IFolderSynchronizer<TFolder, TFile> fileSynchronizer;
        public IFolderSynchronizer<TFolder, TFile> FileSynchronizer {
            get => fileSynchronizer;
            set {
                if (fileSynchronizer != value)
                {
                    if (fileSynchronizer != null)
                    {
                        fileSynchronizer.Dispose();
                    }
                    fileSynchronizer = value;
                }
            }
        }

        public void RemoveEmptyFolders()
        {
            for (int i = Folders.Files.Count - 1; i >= 0; i--)
            {
                if (Folders.Files[i].Files.Count == 0)
                {
                    Folders.RemoveAt(i);
                }
            }
        }

        public DialogResult ShowFolderDialog(out IFolderBrowser browser)
        {
            browser = OpenFolderDialog;
            return OpenFolderDialog.ShowDialog();
        }

        public DialogResult ShowFileDialog(out IFileBrowser browser)
        {
            browser = OpenFileDialog;
            return OpenFileDialog.ShowDialog();
        }

        /// <summary> Copies directory to root path, if directory with given name exists, add (n) number to its name </summary>
        public async Task CopyFolderToRoot(string rootPath, string path)
        {
            path = IOHelper.GetDirectoryPath(path);
            string newFolderPath = IOHelper.GetUniqueName(Path.Combine(rootPath, new DirectoryInfo(path).Name), (name) => !Directory.Exists(name));
            await IOHelper.DirectoryCopyAsync(path, newFolderPath, AllowedFileExtensionsPatterns);
        }

        /// <summary> Copies files to folder path, if file with given name exists, prompt for overwriting </summary>
        public async Task CopyFilesToFolderAsync(TFolder folder, params string[] fileNames)
        {
            foreach (string filePath in fileNames)
            {
                string newPath = Path.Combine(folder.Info.FullName, Path.GetFileName(filePath));
                if (File.Exists(newPath))
                {
                    if (filePath != newPath)
                    {
                        bool overwrite = await DialogService.ShowMessage($"File {newPath} already exists.{Environment.NewLine}Do you want to overwrite it?", "Existing file conflict", "Yes", "No", null);
                        if (overwrite)
                        {
                            if (!FileSystem.CopyFile(filePath, newPath, true))
                            {
                                await DialogService.ShowMessage(StaticMessage.GetOperationFailedMessage(filePath), "Copy failed");
                            }
                        }
                    }
                    else
                    {
                        folder.Add(newPath);
                    }
                }
                else
                {
                    if (!FileSystem.CopyFile(filePath, newPath))
                    {
                        await DialogService.ShowMessage(StaticMessage.GetOperationFailedMessage(filePath), "Copy failed");
                    }
                }
            }
        }

        /// <summary> Removes folder and if it's empty, sends it to RecycleBin </summary>
        public void RemoveFolder(TFolder folder)
        {
            string folderPath = folder.Info.FullName;
            if (Folders.Remove(folder))
            {
                for (int i = folder.Files.Count - 1; i >= 0; i--)
                {
                    string filePath = folder.Files[i].Info.FullName;
                    if (FileSystemInfoReference.GetReferenceCount(filePath) <= 1 && File.Exists(filePath))
                    {
                        FileSystem.DeleteFile(filePath, true);
                    }
                }
                folder.Clear();
                if (Directory.Exists(folderPath) && IOHelper.IsEmpty(folderPath))
                {
                    FileSystem.DeleteDirectory(folderPath, true);
                }
            }
        }

        public void RemoveFileFromFolder(TFolder folder, TFile file)
        {
            string filePath = file.Info.FullName;
            if (folder.Remove(file))
            {
                if (!FileSystemInfoReference.IsReferenced(filePath))
                {
                    if (!FileSystem.DeleteFile(filePath, true))
                    {
                        DialogService.ShowMessage(StaticMessage.GetOperationFailedMessage(file.Info.FullName), "Deletion failed");
                        folder.Add(file);
                    }
                }
            }
        }

        public TFolder CreateFolder(string path) => FileSynchronizer.Factory.Create(path, null);

        public void Dispose() => fileSynchronizer.Dispose();
    }
}