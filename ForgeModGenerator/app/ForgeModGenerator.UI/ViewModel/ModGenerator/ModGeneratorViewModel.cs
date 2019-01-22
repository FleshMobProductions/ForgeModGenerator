﻿using ForgeModGenerator.Core;
using ForgeModGenerator.Miscellaneous;
using ForgeModGenerator.Model;
using ForgeModGenerator.Service;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ForgeModGenerator.ViewModel
{
    /// <summary> ModGenerator Business ViewModel </summary>
    public class ModGeneratorViewModel : ViewModelBase
    {
        public ISessionContextService SessionContext { get; }
        public ModValidationService Validator { get; }
        public IWorkspaceSetupService WorkspaceService { get; }

        public ModGeneratorViewModel(ISessionContextService sessionContext, ModValidationService validator, IWorkspaceSetupService workspaceService)
        {
            SessionContext = sessionContext;
            Validator = validator;
            WorkspaceService = workspaceService;

            if (IsInDesignMode || IsInDesignModeStatic)
            {
                SessionContextService context = sessionContext as SessionContextService;
                context.Mods = new ObservableCollection<Mod>() {
                    new Mod(new McModInfo(){ Name = "ExampleMod", Modid = "examplemod" }, "exampleorg", SessionContext.ForgeVersions[0]),
                    new Mod(new McModInfo(){ Name = "ExampleModTwo", Modid = "examplemodtwo" }, "exampleorg2", SessionContext.ForgeVersions[0]),
                    new Mod(new McModInfo(){ Name = "ExampleModThree", Modid = "examplemodthree" }, "exampleorg3", SessionContext.ForgeVersions[0])
                };
            }
            NewMod = new Mod(new McModInfo() {
                Name = "NewExampleMod",
                Modid = "newexamplemod",
                Credits = "For someone",
                Description = "Some description",
                McVersion = "12.2",
                Version = "1.0",
                AuthorList = new ObservableCollection<string>() { "Me", "And Him" },
                Dependencies = new ObservableCollection<string>() { "OtherMod", "EvenOtherMod" },
                Screenshots = new ObservableCollection<string>() { "url", "otherurl" }
            }, "newexampleorg", SessionContext.ForgeVersions[0]);
        }

        private readonly string[] assetsFolerToGenerate = new string[] {
            "blockstates", "lang", "recipes", "sounds", "models/item", "textures/blocks", "textures/entity", "textures/items", "textures/models/armor"
        };

        public IEnumerable<ModSide> Sides => Enum.GetValues(typeof(ModSide)).Cast<ModSide>();

        private Mod newMod;
        public Mod NewMod {
            get => newMod;
            set => Set(ref newMod, value);
        }

        private Mod selectedEditMod;
        public Mod SelectedEditMod {
            get => selectedEditMod;
            set => Set(ref selectedEditMod, value);
        }

        private ICommand addNewForgeVersion;
        public ICommand AddNewForgeVersion => addNewForgeVersion ?? (addNewForgeVersion = new RelayCommand(AddNewForge));

        private ICommand createMod;
        public ICommand CreateMod => createMod ?? (createMod = new RelayCommand(() => GenerateMod(NewMod)));

        private ICommand addNewItem;
        public ICommand AddNewItem => addNewItem ?? (addNewItem = new RelayCommand<ObservableCollection<string>>(AddNewItemTo));

        private ICommand removeItem;
        public ICommand RemoveItem => removeItem ?? (removeItem = new RelayCommand<Tuple<ObservableCollection<string>, string>>(RemoveItemFromList));

        private ICommand saveChanges;
        public ICommand SaveChanges => saveChanges ?? (saveChanges = new RelayCommand<Mod>(SaveModChanges));

        private void AddNewItemTo(ObservableCollection<string> collection) => collection.Add("NewItem");
        private void RemoveItemFromList(Tuple<ObservableCollection<string>, string> param) => param.Item1.Remove(param.Item2);

        private void AddNewForge()
        {
            Process.Start("https://files.minecraftforge.net/"); // to install version
            Process.Start(AppPaths.ForgeVersions); // paste zip there
        }

        private void GenerateMod(Mod mod)
        {
            if (!Validator.IsValid(mod))
            {
                MessageBox.Show($"Selected mod is not valid");
                return;
            }
            string newModPath = ModPaths.ModRoot(mod.ModInfo.Name);
            if (Directory.Exists(newModPath))
            {
                Log.Warning($"{newModPath} already exists", true);
                return;
            }
            mod.ForgeVersion.UnZip(newModPath);
            RemoveDumpExample(mod);

            string generatedPath = ModPaths.GeneratedSourceCode(mod.ModInfo.Name, mod.Organization);
            string assetsPath = ModPaths.Assets(mod.ModInfo.Name, mod.ModInfo.Modid);
            GenerateFolders(assetsPath, assetsFolerToGenerate);
            Directory.CreateDirectory(generatedPath);
            ExtractCore(generatedPath);
            ReplaceTemplateVariables(mod, generatedPath);
            mod.WorkspaceSetup.Setup(mod);
            Mod.Export(mod);

            SessionContext.Mods.Add(mod);
            Log.Info($"{mod.ModInfo.Name} was created successfully", true);
        }

        private void RemoveDumpExample(Mod mod)
        {
            string javaSource = ModPaths.JavaSource(mod.ModInfo.Name);
            string[] organizationPaths = Directory.GetDirectories(javaSource);
            foreach (string organization in organizationPaths)
            {
                Directory.Delete(organization, true);
            }
        }

        private void GenerateFolders(string rootPath, params string[] generatedFolders)
        {
            Directory.CreateDirectory(rootPath);
            foreach (string folder in generatedFolders)
            {
                Directory.CreateDirectory(Path.Combine(rootPath, folder));
            }
        }

        private void ExtractCore(string rootPath)
        {
            string tempZipPath = Path.Combine(rootPath, "temp.zip");
            File.WriteAllBytes(tempZipPath, Properties.Resources.SourceCodeArchive);
            ZipFile.ExtractToDirectory(tempZipPath, rootPath);
            File.Delete(tempZipPath);
        }

        private void ReplaceTemplateVariables(Mod mod, string rootPath)
        {
            foreach (string file in IOExtensions.EnumerateAllFiles(rootPath))
            {
                string content = File.ReadAllText(file);
                string newContent = content.Replace(CoreSourceCodeVariables.Modname, mod.ModInfo.Name)
                                            .Replace(CoreSourceCodeVariables.ModnameLower, mod.ModInfo.Name.ToLower())
                                            .Replace(CoreSourceCodeVariables.Organization, mod.Organization)
                                            .Replace(CoreSourceCodeVariables.ModVersion, mod.ModInfo.Version)
                                            .Replace(CoreSourceCodeVariables.McVersion, mod.ModInfo.McVersion)
                                            .Replace(CoreSourceCodeVariables.Modid, mod.ModInfo.Modid);
                File.WriteAllText(file, newContent);

                FileInfo fileInfo = new FileInfo(file);
                if (fileInfo.Name.Contains(CoreSourceCodeVariables.Modname))
                {
                    File.Move(file, file.Replace(CoreSourceCodeVariables.Modname, mod.ModInfo.Name));
                }
            }
        }

        private void SaveModChanges(Mod mod)
        {
            if (!Validator.IsValid(mod))
            {
                MessageBox.Show($"Selected mod is not valid");
                return;
            }

            Mod oldValues = Mod.Import(ModPaths.ModRoot(mod.ModInfo.Name));

            if (mod.Organization != oldValues.Organization)
            {
                ChangeOrganization(mod, oldValues);
            }

            if (mod.ModInfo.Name != oldValues.ModInfo.Name)
            {
                ChangeModName(mod, oldValues);
            }

            if (mod.ModInfo.Modid != oldValues.ModInfo.Modid)
            {
                ChangeModid(mod, oldValues);
            }

            if (mod.ModInfo.Version != oldValues.ModInfo.Version)
            {
                ChangeModVersion(mod, oldValues);
            }

            if (mod.ModInfo.McVersion != oldValues.ModInfo.McVersion)
            {
                ChangeMcVersion(mod, oldValues);
            }

            if (mod.ForgeVersion != oldValues.ForgeVersion)
            {
                ChangeForgeVersion(mod);
            }

            if (mod.WorkspaceSetup != oldValues.WorkspaceSetup)
            {
                mod.WorkspaceSetup.Setup(mod);
            }

            McModInfo.Export(mod.ModInfo);
            Mod.Export(mod);

            Log.Info($"Changes to {mod.ModInfo.Name} saved successfully", true);
        }

        private void ChangeForgeVersion(Mod mod)
        {
            string modRoot = ModPaths.ModRoot(mod.ModInfo.Name);

            foreach (string directory in Directory.EnumerateDirectories(modRoot))
            {
                DirectoryInfo info = new DirectoryInfo(directory);
                if (info.Name != "src")
                {
                    FileSystem.DeleteDirectory(directory, UIOption.AllDialogs, RecycleOption.SendToRecycleBin);
                }
            }

            foreach (string file in Directory.EnumerateFiles(modRoot))
            {
                FileInfo info = new FileInfo(file);
                if (info.Name != ModPaths.FmgInfoFileName)
                {
                    FileSystem.DeleteFile(file, UIOption.AllDialogs, RecycleOption.SendToRecycleBin);
                }
            }

            string tempDirPath = Path.Combine(modRoot, "temp");
            DirectoryInfo tempDir = Directory.CreateDirectory(tempDirPath);
            mod.ForgeVersion.UnZip(tempDirPath);
            Directory.Delete(Path.Combine(tempDirPath, "src"), true);
        }

        private void ChangeMcVersion(Mod mod, Mod oldValues)
        {
            string modHook = ModPaths.GeneratedModHookFile(mod.ModInfo.Name, mod.Organization);
            string content = File.ReadAllText(modHook);
            string newContent = content.Replace(oldValues.ModInfo.McVersion, mod.ModInfo.McVersion);
            File.WriteAllText(modHook, newContent);
        }

        private void ChangeModVersion(Mod mod, Mod oldValues)
        {
            string modHook = ModPaths.GeneratedModHookFile(mod.ModInfo.Name, mod.Organization);
            string content = File.ReadAllText(modHook);
            string newContent = content.Replace(oldValues.ModInfo.Version, mod.ModInfo.Version);
            File.WriteAllText(modHook, newContent);
        }

        private void ChangeModid(Mod mod, Mod oldValues)
        {
            string sourcePath = ModPaths.SourceCodeRoot(mod.ModInfo.Name, mod.Organization);
            foreach (string file in IOExtensions.EnumerateAllFiles(sourcePath))
            {
                string content = File.ReadAllText(file);
                string newContent = content.Replace(oldValues.ModInfo.Modid, mod.ModInfo.Modid);
                File.WriteAllText(file, newContent);
            }
            string assetsPath = ModPaths.Assets(mod.ModInfo.Name, oldValues.ModInfo.Modid);
            string newAssetsPath = ModPaths.Assets(mod.ModInfo.Name, mod.ModInfo.Modid);
            Directory.Move(assetsPath, newAssetsPath);

            foreach (string file in IOExtensions.EnumerateAllFiles(newAssetsPath))
            {
                string content = File.ReadAllText(file);
                string newContent = content.Replace(oldValues.ModInfo.Modid, mod.ModInfo.Modid);
                File.WriteAllText(file, newContent);
            }
        }

        private void ChangeModName(Mod mod, Mod oldValues)
        {
            string scriptsPath = ModPaths.SourceCodeRoot(mod.ModInfo.Name, oldValues.Organization);
            string sourceNamePath = Directory.GetDirectories(scriptsPath)[0];
            string newSourceNamePath = sourceNamePath.Replace(oldValues.ModInfo.Name.ToLower(), mod.ModInfo.Name.ToLower());
            Directory.Move(sourceNamePath, newSourceNamePath);
            foreach (string file in IOExtensions.EnumerateAllFiles(newSourceNamePath))
            {
                string content = File.ReadAllText(file);
                string newContent = content.Replace(oldValues.ModInfo.Name, mod.ModInfo.Name)
                                            .Replace(oldValues.ModInfo.Name.ToLower(), mod.ModInfo.Name.ToLower());
                File.WriteAllText(file, newContent);

                FileInfo fileInfo = new FileInfo(file);
                if (fileInfo.Name.Contains(oldValues.ModInfo.Name))
                {
                    File.Move(file, file.Replace(oldValues.ModInfo.Name, mod.ModInfo.Name));
                }
            }
        }

        private void ChangeOrganization(Mod mod, Mod oldValues)
        {
            string scriptsPath = ModPaths.SourceCodeRoot(oldValues.ModInfo.Name, oldValues.Organization);
            string newScriptsPath = ModPaths.SourceCodeRoot(mod.ModInfo.Name, mod.Organization);
            Directory.Move(scriptsPath, newScriptsPath);
            foreach (string file in IOExtensions.EnumerateAllFiles(newScriptsPath))
            {
                string content = File.ReadAllText(file);
                string newContent = content.Replace(oldValues.Organization, mod.Organization);
                File.WriteAllText(file, newContent);
            }
        }
    }
}
