﻿using avaness.PluginLoader.Compiler;
using avaness.PluginLoader.Config;
using avaness.PluginLoader.GUI;
using avaness.PluginLoader.Network;
using ProtoBuf;
using Sandbox.Graphics.GUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml.Serialization;

namespace avaness.PluginLoader.Data
{
    [ProtoContract]
    public partial class GitHubPlugin : PluginData
    {
        public override string Source => "GitHub";
        public override bool IsLocal => false;
        public override bool IsCompiled => true;

        [ProtoMember(1)]
        public string Commit { get; set; }

        [ProtoMember(2)]
        [XmlArray]
        [XmlArrayItem("Directory")]
        public string[] SourceDirectories { get; set; }

        [ProtoMember(3)]
        [XmlArray]
        [XmlArrayItem("Version")]
        public Branch[] AlternateVersions { get; set; }

        [ProtoMember(4)]
        public string AssetFolder { get; set; }

        [ProtoMember(5)]
        public NuGetPackageList NuGetReferences { get; set; }

        private string assemblyName;
        private GitHubPluginConfig config;
        private CacheManifest manifest;
        private NuGetClient nuget;
        private AssemblyResolver resolver;

        public GitHubPlugin()
        {
            Status = PluginStatus.None;
        }

        public override bool LoadData(ref PluginDataConfig config, bool enabled)
        {
            if (enabled)
            {
                if (config is GitHubPluginConfig githubConfig && IsValidConfig(githubConfig))
                {
                    this.config = githubConfig;
                    return false;
                }

                this.config = new GitHubPluginConfig()
                {
                    Id = Id,
                };
                config = this.config;
                return true;
            }

            this.config = null;

            if (config != null)
            {
                config = null;
                return true;
            }

            return false;
        }

        private bool IsValidConfig(GitHubPluginConfig githubConfig)
        {
            if (string.IsNullOrWhiteSpace(githubConfig.SelectedVersion))
                return true;
            if (AlternateVersions == null)
                return false;
            return AlternateVersions.Any(x => x.Name.Equals(githubConfig.SelectedVersion, StringComparison.OrdinalIgnoreCase));
        }

        public void InitPaths()
        {
            string[] nameArgs = Id.Split(new char[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (nameArgs.Length < 2)
                throw new Exception("Invalid GitHub name: " + Id);

            CleanPaths(SourceDirectories);

            if(!string.IsNullOrWhiteSpace(AssetFolder))
            {
                AssetFolder = AssetFolder.Replace('\\', '/').TrimStart('/');
                if (AssetFolder.Length > 0 && AssetFolder[AssetFolder.Length - 1] != '/')
                    AssetFolder += '/';
            }

            assemblyName = MakeSafeString(nameArgs[1]);
            manifest = CacheManifest.Load(nameArgs[0], nameArgs[1]);
        }

        private void CleanPaths(string[] paths)
        {
            if (paths != null)
            {
                for (int i = paths.Length - 1; i >= 0; i--)
                {
                    string path = paths[i].Replace('\\', '/').TrimStart('/');

                    if (path.Length == 0)
                        continue;

                    if (path[path.Length - 1] != '/')
                        path += '/';

                    paths[i] = path;
                }
            }
        }

        private string MakeSafeString(string s)
        {
            StringBuilder sb = new StringBuilder();
            foreach (char ch in s)
            {
                if (char.IsLetterOrDigit(ch))
                    sb.Append(ch);
                else
                    sb.Append('_');
            }
            return sb.ToString();
        }

        public override Assembly GetAssembly()
        {
            InitPaths();

            Assembly a;

            resolver = new AssemblyResolver();

            int gameVersion = Main.Instance.Config.GameVersion;
            string selectedCommit = GetSelectedVersion()?.Commit ?? Commit;
            if (!manifest.IsCacheValid(selectedCommit, gameVersion, !string.IsNullOrWhiteSpace(AssetFolder), NuGetReferences != null && NuGetReferences.HasPackages))
            {
                var lbl = Main.Instance.Splash;
                lbl.SetText($"Downloading '{FriendlyName}'");

                manifest.GameVersion = gameVersion;
                manifest.Commit = selectedCommit;
                manifest.ClearAssets();
                string name = assemblyName + '_' + Path.GetRandomFileName();
                byte[] data = CompileFromSource(selectedCommit, name, x => lbl.SetBarValue(x));
                File.WriteAllBytes(manifest.DllFile, data);
                manifest.DeleteUnknownFiles();
                manifest.Save();

                Status = PluginStatus.Updated;
                lbl.SetText($"Compiled '{FriendlyName}'");
                resolver.AddSourceFolder(manifest.LibDir);
                resolver.AddAllowedAssemblyFile(manifest.DllFile);
                resolver.AddAllowedAssemblyName(name);
                a = Assembly.Load(data);
            }
            else
            {
                manifest.DeleteUnknownFiles();
                resolver.AddSourceFolder(manifest.LibDir);
                resolver.AddAllowedAssemblyFile(manifest.DllFile);
                a = Assembly.LoadFile(manifest.DllFile);
            }

            Version = a.GetName().Version;
            return a;
        }

        private Branch GetSelectedVersion()
        {
            if (config == null || string.IsNullOrWhiteSpace(config.SelectedVersion))
                return null;
            return AlternateVersions?.FirstOrDefault(x => x.Name.Equals(config.SelectedVersion, StringComparison.OrdinalIgnoreCase));
        }

        public byte[] CompileFromSource(string commit, string assemblyName, Action<float> callback = null)
        {
            RoslynCompiler compiler = new RoslynCompiler();
            using (Stream s = GitHub.DownloadRepo(Id, commit))
            using (ZipArchive zip = new ZipArchive(s))
            {
                callback?.Invoke(0);
                for (int i = 0; i < zip.Entries.Count; i++)
                {
                    ZipArchiveEntry entry = zip.Entries[i];
                    CompileFromSource(compiler, entry);
                    callback?.Invoke(i / (float)zip.Entries.Count);
                }
            }
            if(NuGetReferences?.PackageIds != null)
            {
                if (nuget == null)
                    nuget = new NuGetClient();
                InstallPackages(nuget.DownloadPackages(NuGetReferences.PackageIds), compiler);
            }
            callback?.Invoke(1);
            return compiler.Compile(assemblyName, out _);
        }

        private void CompileFromSource(RoslynCompiler compiler, ZipArchiveEntry entry)
        {
            string path = RemoveRoot(entry.FullName);
            if (NuGetReferences != null && path == NuGetReferences.PackagesConfigNormalized)
            {
                nuget = new NuGetClient();
                NuGetPackage[] packages;
                using (Stream entryStream = entry.Open())
                {
                    packages = nuget.DownloadFromConfig(entryStream);
                }
                InstallPackages(packages, compiler);
            }
            if (AllowedZipPath(path))
            {
                using (Stream entryStream = entry.Open())
                {
                    compiler.Load(entryStream, entry.FullName);
                }
            }
            if (IsAssetZipPath(path, out string assetFilePath))
            {
                AssetFile newFile = manifest.CreateAsset(assetFilePath);
                if(!manifest.IsAssetValid(newFile))
                {
                    using (Stream entryStream = entry.Open())
                    {
                        manifest.SaveAsset(newFile, entryStream);
                    }
                }
            }
        }

        private void InstallPackages(IEnumerable<NuGetPackage> packages, RoslynCompiler compiler)
        {
            foreach (NuGetPackage package in packages)
                InstallPackage(package, compiler);
        }

        private void InstallPackage(NuGetPackage package, RoslynCompiler compiler)
        {
            foreach(NuGetPackage.Item file in package.LibFiles)
            {
                AssetFile newFile = manifest.CreateAsset(file.FilePath, AssetFile.AssetType.Lib);
                if (!manifest.IsAssetValid(newFile))
                {
                    using (Stream entryStream = File.OpenRead(file.FullPath))
                    {
                        manifest.SaveAsset(newFile, entryStream);
                    }
                }

                if(Path.GetDirectoryName(newFile.FullPath) == newFile.BaseDir)
                    compiler.TryAddDependency(newFile.FullPath);
            }

            foreach(NuGetPackage.Item file in package.ContentFiles)
            {
                AssetFile newFile = manifest.CreateAsset(file.FilePath, AssetFile.AssetType.LibContent);
                if (!manifest.IsAssetValid(newFile))
                {
                    using (Stream entryStream = File.OpenRead(file.FullPath))
                    {
                        manifest.SaveAsset(newFile, entryStream);
                    }
                }
            }
        }

        private bool IsAssetZipPath(string path, out string assetFilePath)
        {
            assetFilePath = null;

            if (path.EndsWith("/") || string.IsNullOrEmpty(AssetFolder))
                return false;

            if (path.StartsWith(AssetFolder, StringComparison.Ordinal) && path.Length > (AssetFolder.Length + 1))
            {
                assetFilePath = path.Substring(AssetFolder.Length).TrimStart('/');
                return true;
            }
            return false;
        }

        private bool AllowedZipPath(string path)
        {
            if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                return false;

            if (SourceDirectories == null || SourceDirectories.Length == 0)
                return true;

            foreach(string dir in SourceDirectories)
            {
                if (path.StartsWith(dir, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private string RemoveRoot(string path)
        {
            path = path.Replace('\\', '/').TrimStart('/');
            int index = path.IndexOf('/');
            if(index >= 0 && (index + 1) < path.Length)
                return path.Substring(index + 1);
            return path;
        }

        public override void Show()
        {
            MyGuiSandbox.OpenUrl("https://github.com/" + Id, UrlOpenMode.SteamOrExternalWithConfirm);
        }

        public override void InvalidateCache()
        {
            try
            {
                manifest.Invalidate();
                LogFile.WriteLine($"Cache for GitHub plugin {Id} was invalidated, it will need to be compiled again at next game start");
            }
            catch (Exception e)
            {
                LogFile.Error("Failed to invalidate github cache: " + e);
            }
        }

        public override void AddDetailControls(PluginDetailMenu screen, MyGuiControlBase bottomControl, out MyGuiControlBase topControl)
        {
            if(AlternateVersions == null || AlternateVersions.Length == 0)
            {
                topControl = null;
                return;
            }

            string selectedCommit = GetSelectedVersion()?.Commit ?? Commit;
            MyGuiControlCombobox versionDropdown = new MyGuiControlCombobox();
            versionDropdown.AddItem(-1, "Default");
            int selectedKey = -1;
            for (int i = 0; i < AlternateVersions.Length; i++)
            {
                Branch version = AlternateVersions[i];
                versionDropdown.AddItem(i, version.Name);
                if (version.Commit == selectedCommit)
                    selectedKey = i;
            }
            versionDropdown.SelectItemByKey(selectedKey);
            versionDropdown.ItemSelected += () =>
            {
                PluginConfig mainConfig = Main.Instance.Config;

                int selectedKey = (int)versionDropdown.GetSelectedKey();
                string newVersion = selectedKey >= 0 ? AlternateVersions[selectedKey].Name : null;
                string currentVersion = GetSelectedVersion()?.Name;
                if (currentVersion == newVersion)
                    return;

                if (config == null)
                {
                    config = new GitHubPluginConfig()
                    {
                        Id = Id,
                    };

                    mainConfig.SavePluginData(config);
                }

                config.SelectedVersion = newVersion;
                mainConfig.Save();
                if(mainConfig.IsEnabled(Id))
                    screen.InvokeOnRestartRequired();
            };

            screen.PositionAbove(bottomControl, versionDropdown, MyAlignH.Left);
            screen.Controls.Add(versionDropdown);

            MyGuiControlLabel lblVersion = new MyGuiControlLabel(text: "Installed Version");
            screen.PositionAbove(versionDropdown, lblVersion, align: MyAlignH.Left, spacing: 0);
            screen.Controls.Add(lblVersion);
            topControl = lblVersion;
        }

        public override string GetAssetPath()
        {
            if (string.IsNullOrEmpty(AssetFolder))
                return null;
            return Path.GetFullPath(manifest.AssetFolder);
        }

        [ProtoContract]
        public class Branch
        {
            [ProtoMember(1)]
            public string Name { get; set; }

            [ProtoMember(2)]
            public string Commit { get; set; }

            public Branch()
            {

            }
        }
    }
}