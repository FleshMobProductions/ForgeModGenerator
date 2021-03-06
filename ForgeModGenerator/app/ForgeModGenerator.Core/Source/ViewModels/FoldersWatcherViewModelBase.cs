﻿using ForgeModGenerator.Services;
using ForgeModGenerator.Utility;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ForgeModGenerator.ViewModels
{
    /// <summary> Base ViewModel class to explore folders </summary>
    public abstract class FoldersWatcherViewModelBase<TFolder, TFile> : BindableBase
        where TFolder : class, IFolderObject<TFile>
        where TFile : class, IFileObject
    {
        public FoldersWatcherViewModelBase(ISessionContextService sessionContext, IFoldersExplorerFactory<TFolder, TFile> explorerFactory)
        {
            Explorer = explorerFactory.Create();
            SessionContext = sessionContext;
            SessionContext.PropertyChanged += OnSessionContexPropertyChanged;
        }

        public IFoldersExplorer<TFolder, TFile> Explorer { get; }

        public abstract string FoldersRootPath { get; }

        protected ISessionContextService SessionContext { get; }

        private bool isLoading;
        /// <summary> Determines when folders are loading - used to show loading circle </summary>
        public bool IsLoading {
            get => isLoading;
            set => SetProperty(ref isLoading, value);
        }

        public bool HasEmptyFolders => Explorer.HasEmptyFolders;

        private ICommand onLoadedCommand;
        public ICommand OnLoadedCommand => onLoadedCommand ?? (onLoadedCommand = new DelegateCommand(OnLoaded));

        private ICommand addFileCommand;
        public ICommand AddFileCommand => addFileCommand ?? (addFileCommand = new DelegateCommand<TFolder>(ShowFileDialogAndCopyToFolder));

        private ICommand removeFileCommand;
        public ICommand RemoveFileCommand => removeFileCommand ?? (removeFileCommand = new DelegateCommand<Tuple<TFolder, TFile>>(RemoveFileFromFolder));

        private ICommand removeFolderCommand;
        public ICommand RemoveFolderCommand => removeFolderCommand ?? (removeFolderCommand = new DelegateCommand<TFolder>(Explorer.RemoveFolder));

        private ICommand addFolderCommand;
        public ICommand AddFolderCommand => addFolderCommand ?? (addFolderCommand = new DelegateCommand(ShowFolderDialogAndCopyToRoot));

        private ICommand addFileAsFolderCommand;
        public ICommand AddFileAsFolderCommand => addFileAsFolderCommand ?? (addFileAsFolderCommand = new DelegateCommand(ShowFileDialogAndCreateFolder));

        private ICommand removeEmptyFoldersCommand;
        public ICommand RemoveEmptyFoldersCommand => removeEmptyFoldersCommand ?? (removeEmptyFoldersCommand = new DelegateCommand(Explorer.RemoveEmptyFolders));

        protected virtual async void OnLoaded() => await Refresh();

        protected virtual bool CanRefresh() => SessionContext.SelectedMod != null && Directory.Exists(FoldersRootPath);

        public abstract Task<bool> Refresh();

        protected void RemoveFileFromFolder(Tuple<TFolder, TFile> param) => Explorer.RemoveFileFromFolder(param.Item1, param.Item2);

        protected async void ShowFolderDialogAndCopyToRoot()
        {
            DialogResult dialogResult = Explorer.ShowFolderDialog(out IFolderBrowser browser);
            if (dialogResult == DialogResult.OK)
            {
                await Explorer.CopyFolderToRootAsync(FoldersRootPath, browser.SelectedPath);
            }
        }

        protected void ShowFileDialogAndCreateFolder()
        {
            DialogResult dialogResult = Explorer.ShowFileDialog(out IFileBrowser browser);
            if (dialogResult == DialogResult.OK)
            {
                string newFolderPath = null;
                string newFolderName = IOHelper.GetUniqueName(Path.GetFileNameWithoutExtension(browser.FileName),
                                                                name => !Directory.Exists((newFolderPath = Path.Combine(FoldersRootPath, name))));
                TFolder folder = Explorer.CreateFolder(newFolderPath);
                Explorer.CopyFilesToFolder(folder, browser.FileNames);
            }
        }

        protected void ShowFileDialogAndCopyToFolder(TFolder folder)
        {
            DialogResult dialogResult = Explorer.ShowFileDialog(out IFileBrowser browser);
            if (dialogResult == DialogResult.OK)
            {
                Explorer.CopyFilesToFolder(folder, browser.FileNames);
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
