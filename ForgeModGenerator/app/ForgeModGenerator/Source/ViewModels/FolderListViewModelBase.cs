﻿using ForgeModGenerator.Converters;
using ForgeModGenerator.Services;
using ForgeModGenerator.Utility;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Views;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Forms;
using System.Windows.Input;

namespace ForgeModGenerator.ViewModels
{
    /// <summary> Business ViewModel Base class for making file list </summary>
    public abstract class FolderListViewModelBase<TFolder, TFile> : ViewModelBase
        where TFolder : class, IFileFolder<TFile>
        where TFile : class, IFileItem
    {
        public FolderListViewModelBase(ISessionContextService sessionContext, IDialogService dialogService)
        {
            SessionContext = sessionContext;
            DialogService = dialogService;
            FolderFileConverter = new TupleValueConverter<TFolder, TFile>();
            if (IsInDesignMode)
            {
                return;
            }
            OpenFileDialog = new OpenFileDialog() {
                Multiselect = true,
                CheckFileExists = true,
                ValidateNames = true
            };
            OpenFolderDialog = new FolderBrowserDialog() { ShowNewFolderButton = true };
            AllowedFileExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { OpenFileDialog.DefaultExt };
            SessionContext.PropertyChanged += OnSessionContexPropertyChanged;
            FileSynchronizer = new FoldersSynchronizer<TFolder, TFile>(Folders, FoldersRootPath, AllowedFileExtensionsPatterns);
        }

        /// <summary> Path to folder root where are all files localized </summary>
        public abstract string FoldersRootPath { get; }

        /// <summary> Path to file that can be deserialized to folders </summary>
        public virtual string FoldersSerializeFilePath { get; }

        public ISessionContextService SessionContext { get; }

        public IMultiValueConverter FolderFileConverter { get; }

        public FileEditor<TFolder, TFile> FileEditor { get; protected set; }

        private ObservableCollection<TFolder> folders;
        public ObservableCollection<TFolder> Folders {
            get => folders;
            set {
                Set(ref folders, value);
                FileSynchronizer.Folders = folders;
            }
        }

        private TFolder selectedFolder;
        public TFolder SelectedFolder {
            get => selectedFolder;
            set => Set(ref selectedFolder, value);
        }

        private TFile selectedFile;
        public TFile SelectedFile {
            get => selectedFile;
            set => Set(ref selectedFile, value);
        }

        #region Commands
        private ICommand onLoadedCommand;
        public ICommand OnLoadedCommand => onLoadedCommand ?? (onLoadedCommand = new RelayCommand(OnLoaded));

        private ICommand editFileCommand;
        public ICommand EditFileCommand => editFileCommand ?? (editFileCommand = new RelayCommand<Tuple<TFolder, TFile>>(FileEditor.OpenFileEditor));

        private ICommand addFileCommand;
        public ICommand AddFileCommand => addFileCommand ?? (addFileCommand = new RelayCommand<TFolder>(ShowFileDialogAndCopyToFolder));

        private ICommand removeFileCommand;
        public ICommand RemoveFileCommand => removeFileCommand ?? (removeFileCommand = new RelayCommand<Tuple<TFolder, TFile>>(RemoveFileFromFolder));

        private ICommand removeFolderCommand;
        public ICommand RemoveFolderCommand => removeFolderCommand ?? (removeFolderCommand = new RelayCommand<TFolder>(RemoveFolder));

        private ICommand addFolderCommand;
        public ICommand AddFolderCommand => addFolderCommand ?? (addFolderCommand = new RelayCommand(ShowFolderDialogAndCopyToRoot));
        #endregion

        private bool isLoading;
        /// <summary> Determines when folders are loading - used to show loading circle </summary>
        public bool IsLoading {
            get => isLoading;
            set => Set(ref isLoading, value);
        }

        protected FoldersSynchronizer<TFolder, TFile> FileSynchronizer { get; set; }

        protected HashSet<string> AllowedFileExtensions { get; set; }

        protected string AllowedFileExtensionsPatterns => "*" + string.Join("|*", AllowedFileExtensions);

        protected OpenFileDialog OpenFileDialog { get; }

        protected FolderBrowserDialog OpenFolderDialog { get; }

        protected FrameworkElement FileEditForm { get; set; }

        protected IDialogService DialogService { get; }

        protected virtual async void OnLoaded() => await Refresh();

        protected virtual bool CanRefresh() => SessionContext.SelectedMod != null && (Directory.Exists(FoldersRootPath) || File.Exists(FoldersSerializeFilePath));
        protected virtual async Task<bool> Refresh()
        {
            if (CanRefresh())
            {
                FileSynchronizer.RootPath = FoldersRootPath;
                IsLoading = true;
                Folders = await FileSynchronizer.FindFolders(FoldersRootPath ?? FoldersSerializeFilePath, true);
                IsLoading = false;
                return true;
            }
            return false;
        }

        protected void ShowFolderDialogAndCopyToRoot()
        {
            DialogResult dialogResult = OpenFolderDialog.ShowDialog();
            if (dialogResult == DialogResult.OK)
            {
                CopyFolderToRoot(OpenFolderDialog.SelectedPath);
            }
        }

        /// <summary> Copies directory to root path, if directory with given name exists, add (n) number to its name </summary>
        protected void CopyFolderToRoot(string path)
        {
            path = IOHelper.GetDirectoryPath(path);
            string newFolderPath = IOHelper.GetUniqueName(Path.Combine(FoldersRootPath, new DirectoryInfo(path).Name), (name) => !Directory.Exists(name));
            IOHelper.DirectoryCopy(path, newFolderPath, AllowedFileExtensionsPatterns);
        }

        /// <summary> Removes folder and if it's empty, sends it to RecycleBin  </summary>
        protected void RemoveFolder(TFolder folder)
        {
            if (Folders.Remove(folder))
            {
                for (int i = folder.Files.Count - 1; i >= 0; i--)
                {
                    if (File.Exists(folder.Files[i].Info.FullName))
                    {
                        IOHelper.DeleteFileToBin(folder.Files[i].Info.FullName);
                    }
                    folder.Remove(folder.Files[i]);
                }
                if (Directory.Exists(folder.Info.FullName) && IOHelper.IsEmpty(folder.Info.FullName))
                {
                    IOHelper.DeleteDirectoryToBin(folder.Info.FullName);
                }
            }
        }

        protected virtual void RemoveFileFromFolder(Tuple<TFolder, TFile> param) => param.Item1.Remove(param.Item2);

        protected virtual void ShowFileDialogAndCopyToFolder(TFolder folder)
        {
            DialogResult dialogResult = OpenFileDialog.ShowDialog();
            if (dialogResult == DialogResult.OK)
            {
                CopyFilesToFolderAsync(folder, OpenFileDialog.FileNames);
            }
        }

        /// <summary> Copies files to folder path, if file with given name exists, prompt for overwriting </summary>
        protected async void CopyFilesToFolderAsync(TFolder folder, params string[] fileNames)
        {
            foreach (string filePath in fileNames)
            {
                string newPath = Path.Combine(folder.Info.FullName, Path.GetFileName(filePath));
                if (File.Exists(newPath))
                {
                    if (filePath != newPath)
                    {
                        bool overwrite = await DialogService.ShowMessage($"File {newPath} already exists.\nDo you want to overwrite it?", "Existing file conflict", "Yes", "No", null);
                        if (overwrite)
                        {
                            File.Copy(filePath, newPath, true);
                        }
                    }
                    else
                    {
                        folder.Add(newPath);
                    }
                }
                else
                {
                    File.Copy(filePath, newPath);
                }
            }
        }

        protected virtual async void OnSessionContexPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SessionContext.SelectedMod))
            {
                await Refresh();
            }
        }
    }
}
