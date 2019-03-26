﻿using FluentValidation;
using FluentValidation.Results;
using ForgeModGenerator.CodeGeneration;
using ForgeModGenerator.Models;
using ForgeModGenerator.ModGenerator.Controls;
using ForgeModGenerator.ModGenerator.SourceCodeGeneration;
using ForgeModGenerator.ModGenerator.Validations;
using ForgeModGenerator.Services;
using ForgeModGenerator.Utility;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ForgeModGenerator.ModGenerator.ViewModels
{
    /// <summary> ModGenerator Business ViewModel </summary>
    public class ModGeneratorViewModel : BindableBase
    {
        public ModGeneratorViewModel(ISessionContextService sessionContext, IWorkspaceSetupService workspaceService, IDialogService dialogService)
        {
            SessionContext = sessionContext;
            WorkspaceService = workspaceService;
            DialogService = dialogService;
            ResetNewMod();
            Form = new ModForm() {
                AddForgeVersionCommand = AddNewForgeVersionCommand,
                Setups = WorkspaceService.Setups,
                ForgeVersions = SessionContext.ForgeVersions,
                Sides = Sides
            };
            EditorForm = new EditorForm<Mod>(Cache.Default, DialogService, Form, ModValidator);
            EditorForm.ItemEdited += Editor_OnItemEdited;
        }

        private readonly string[] assetsFolerToGenerate = new string[] {
            "blockstates", "lang", "recipes", "sounds", "models/item", "textures/blocks", "textures/entity", "textures/items", "textures/models/armor"
        };

        protected ModValidator ModValidator { get; set; } = new ModValidator();

        protected FrameworkElement Form { get; set; }
        protected EditorForm<Mod> EditorForm { get; set; }

        public ISessionContextService SessionContext { get; }
        public IWorkspaceSetupService WorkspaceService { get; }
        public IDialogService DialogService { get; }

        public IEnumerable<ModSide> Sides => Enum.GetValues(typeof(ModSide)).Cast<ModSide>();

        private Mod newMod;
        public Mod NewMod {
            get => newMod;
            set => SetProperty(ref newMod, value);
        }

        private Mod selectedEditMod;
        public Mod SelectedEditMod {
            get => selectedEditMod;
            set => SetProperty(ref selectedEditMod, value);
        }

        private ICommand addNewForgeVersionCommand;
        public ICommand AddNewForgeVersionCommand => addNewForgeVersionCommand ?? (addNewForgeVersionCommand = new DelegateCommand(AddNewForge));

        private ICommand createModCommand;
        public ICommand CreateModCommand => createModCommand ?? (createModCommand = new DelegateCommand(() => CreateNewMod(NewMod)));

        private ICommand removeModCommand;
        public ICommand RemoveModCommand => removeModCommand ?? (removeModCommand = new DelegateCommand<Mod>(RemoveMod));

        private ICommand editModCommand;
        public ICommand EditModCommand => editModCommand ?? (editModCommand = new DelegateCommand<Mod>(EditMod));

        private ICommand showModContainerCommand;
        public ICommand ShowModContainerCommand => showModContainerCommand ?? (showModContainerCommand = new DelegateCommand<Mod>(ShowContainer));

        private void ShowContainer(Mod mod) => Process.Start(ModPaths.ModRootFolder(mod.ModInfo.Name));

        private string OnModValidate(Mod sender, string propertyName) => ModValidator.Validate(sender, propertyName).ToString();

        private async void RemoveMod(Mod mod)
        {
            bool shouldRemove = await DialogService.ShowMessage("Are you sure you want to delete this mod? Folder will be moved to trash bin", "Confirm deletion", "Yes", "No", null);
            if (shouldRemove)
            {
                if (SessionContext.Mods.Remove(mod))
                {
                    try
                    {
                        IOHelperWin.DeleteDirectoryRecycle(ModPaths.ModRootFolder(mod.ModInfo.Name));
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Couldn't remove mod. Make sure folder or its file is not used in any other process", true);
                        SessionContext.Mods.Add(mod); // remove failed, so rollback
                    }
                }
            }
        }

        private void AddNewForge()
        {
            Process.Start("https://files.minecraftforge.net/"); // to install version
            Process.Start(AppPaths.ForgeVersions); // paste zip there
        }

        private void ResetNewMod() => NewMod = new Mod(new McModInfo() {
            Name = "NewExampleMod",
            Modid = "newexamplemod",
            Credits = "For contributors of ForgeModGenerator",
            Description = "This is example mod",
            McVersion = "12.2",
            Version = "1.0",
            AuthorList = new ObservableCollection<string>() { "Me" },
            Dependencies = new ObservableCollection<string>(),
            Screenshots = new ObservableCollection<string>()
        }, "newexampleorg", SessionContext.ForgeVersions[0]);

        private void EditMod(Mod mod)
        {
            SelectedEditMod = mod;
            mod.ValidateProperty += OnModValidate;
            EditorForm.OpenItemEditor(mod);
        }

        private void CreateNewMod(Mod mod)
        {
            EditorForm<Mod> tempEditor = new EditorForm<Mod>(Cache.Default, DialogService, Form, ModValidator);
            tempEditor.ItemEdited += (sender, e) => {
                if (e.Result)
                {
                    GenerateMod(e.ActualItem);
                }
                mod.ValidateProperty -= OnModValidate;
            };
            mod.ValidateProperty += OnModValidate;
            tempEditor.OpenItemEditor(mod);
        }

        private void Editor_OnItemEdited(object sender, ItemEditedEventArgs<Mod> e)
        {
            if (e.Result)
            {
                if (e.ActualItem.Validate().IsValid)
                {
                    SaveModChanges(e.ActualItem);
                }
            }
            else
            {
                e.ActualItem.CopyValues(e.CachedItem);
            }
            e.ActualItem.ValidateProperty -= OnModValidate;
        }

        private void GenerateMod(Mod mod)
        {
            ValidationResult validation = ModValidator.Validate(mod);
            if (!validation.IsValid)
            {
                Log.Warning($"Selected mod is not valid. Reason: {validation}", true);
                return;
            }
            string newModPath = ModPaths.ModRootFolder(mod.ModInfo.Name);
            if (Directory.Exists(newModPath))
            {
                Log.Warning($"{newModPath} already exists", true);
                return;
            }
            mod.ForgeVersion.UnZip(newModPath);
            RemoveDumpExample(mod);

            string assetsPath = ModPaths.AssetsFolder(mod.ModInfo.Name, mod.ModInfo.Modid);
            IOHelper.GenerateFolders(assetsPath, assetsFolerToGenerate);
            RegenerateSourceCode(mod);

            mod.WorkspaceSetup.Setup(mod);
            ModHelper.ExportMod(mod);
            ModHelper.ExportMcInfo(mod.ModInfo);

            SessionContext.Mods.Add(mod);
            Log.Info($"{mod.ModInfo.Name} was created successfully", true);
        }

        private void RegenerateSourceCode(Mod mod)
        {
            foreach (ScriptCodeGenerator generator in ReflectionHelper.EnumerateSubclasses<ScriptCodeGenerator>(mod))
            {
                generator.RegenerateScript();
            }
        }

        private void RemoveDumpExample(Mod mod)
        {
            string javaSource = ModPaths.JavaSourceFolder(mod.ModInfo.Name);
            foreach (string organization in Directory.EnumerateDirectories(javaSource))
            {
                IOHelperWin.DeleteDirectoryPerm(organization);
            }
        }

        private void SaveModChanges(Mod mod)
        {
            ValidationResult validation = ModValidator.Validate(mod);
            if (!validation.IsValid)
            {
                Log.Warning($"Selected mod is not valid. Reason: {validation}", true);
                return;
            }
            Mod oldValues = ModHelper.ImportMod(ModPaths.ModRootFolder(mod.CachedName));
            try
            {
                bool organizationChanged = mod.Organization != oldValues.Organization;
                bool nameChanged = mod.ModInfo.Name != oldValues.ModInfo.Name;
                bool modidChanged = mod.ModInfo.Modid != oldValues.ModInfo.Modid;

                if (organizationChanged)
                {
                    string oldOrganizationPath = ModPaths.OrganizationRootFolder(oldValues.ModInfo.Name, oldValues.Organization);
                    //string newOrganizationPath = ModPaths.OrganizationRootFolder(oldValues.ModInfo.Name, mod.Organization);
                    if (!IOSafeWin.RenameDirectory(oldOrganizationPath, mod.Organization))
                    {
                        DialogService.ShowMessage(IOSafeWin.GetOperationFailedMessage(oldOrganizationPath), "Rename failed");
                        mod.Organization = oldValues.Organization;
                    }
                }
                if (modidChanged)
                {
                    string oldAssetPath = ModPaths.AssetsFolder(oldValues.ModInfo.Name, oldValues.ModInfo.Modid);
                    //string newAssetPath = ModPaths.AssetsFolder(oldValues.ModInfo.Name, mod.ModInfo.Modid);
                    if (!IOSafeWin.RenameDirectory(oldAssetPath, mod.ModInfo.Modid))
                    {
                        DialogService.ShowMessage(IOSafeWin.GetOperationFailedMessage(oldAssetPath), "Rename failed");
                        mod.ModInfo.Modid = oldValues.ModInfo.Modid;
                    }
                }
                if (nameChanged)
                {
                    bool canChangeName = true;
                    string oldSourceCodePath = ModPaths.SourceCodeRootFolder(oldValues.ModInfo.Name, mod.Organization);
                    //IOHelper.MoveDirectory(oldSourceCodePath, newSourceCodePath);
                    if (!IOSafeWin.RenameDirectory(oldSourceCodePath, mod.ModInfo.Name.ToLower()))
                    {
                        DialogService.ShowMessage(IOSafeWin.GetOperationFailedMessage(oldSourceCodePath), "Rename failed");
                        mod.Name = oldValues.Name;
                    }

                    string oldNamePath = ModPaths.ModRootFolder(oldValues.ModInfo.Name);
                    //string newNamePath = ModPaths.ModRootFolder(mod.ModInfo.Name);
                    if (canChangeName && !IOSafeWin.RenameDirectory(oldNamePath, mod.ModInfo.Name))
                    {
                        DialogService.ShowMessage(IOSafeWin.GetOperationFailedMessage(oldNamePath), "Rename failed");
                        string sourceCodeParentPath = new DirectoryInfo(oldSourceCodePath).Parent.FullName;
                        string newSourceCodePath = Path.Combine(sourceCodeParentPath, mod.ModInfo.Name.ToLower());
                        IOHelperWin.RenameDirectory(newSourceCodePath, oldValues.Name.ToLower());
                        mod.Name = oldValues.Name;
                    }
                }

                bool forgeVersionChanged = mod.ForgeVersion.Name != oldValues.ForgeVersion.Name;
                if (forgeVersionChanged)
                {
                    ChangeForgeVersion(mod);
                }

                bool workspaceChanged = mod.WorkspaceSetup.Name != oldValues.WorkspaceSetup.Name;
                if (workspaceChanged)
                {
                    mod.WorkspaceSetup.Setup(mod);
                }

                if (organizationChanged || nameChanged || modidChanged)
                {
                    RegenerateSourceCode(mod);
                }
                else
                {
                    bool versionChanged = mod.ModInfo.Version != oldValues.ModInfo.Version;
                    bool mcVersionChanged = mod.ModInfo.McVersion != oldValues.ModInfo.McVersion;
                    if (versionChanged || mcVersionChanged)
                    {
                        new ModHookCodeGenerator(mod).RegenerateScript();
                    }
                }

                ModHelper.ExportMcInfo(mod.ModInfo);
                ModHelper.ExportMod(mod);
            }
            catch (Exception ex)
            {
                Log.Error(ex, Log.UnexpectedErrorMessage, true);
                return;
            }
            Log.Info($"Changes to {mod.ModInfo.Name} saved successfully", true);
        }

        private void ChangeForgeVersion(Mod mod)
        {
            string modRoot = ModPaths.ModRootFolder(mod.ModInfo.Name);

            // remove every folder except 'src'
            foreach (string directory in Directory.EnumerateDirectories(modRoot))
            {
                DirectoryInfo info = new DirectoryInfo(directory);
                if (info.Name != "src")
                {
                    IOHelperWin.DeleteDirectoryRecycle(directory);
                }
            }

            // remove every file except FmgModInfo
            foreach (string file in Directory.EnumerateFiles(modRoot))
            {
                FileInfo info = new FileInfo(file);
                if (info.Name != ModPaths.FmgInfoFileName)
                {
                    IOHelperWin.DeleteFileRecycle(file);
                }
            }

            // unzip forgeversion to temp path
            string tempDirPath = Path.Combine(modRoot, "temp");
            Directory.CreateDirectory(tempDirPath);
            mod.ForgeVersion.UnZip(tempDirPath);

            IOHelperWin.DeleteDirectoryPerm(Path.Combine(tempDirPath, "src"));
            IOHelperWin.MoveDirectoriesAndFiles(tempDirPath, modRoot);
            IOHelperWin.DeleteDirectoryPerm(tempDirPath);
            Log.Info($"Changed forge version for {mod.CachedName} to {mod.ForgeVersion.Name}");
        }
    }
}