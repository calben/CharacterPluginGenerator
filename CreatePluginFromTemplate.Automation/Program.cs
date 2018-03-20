﻿using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Newtonsoft.Json;
using CommandLine;
using System.Threading;

namespace CreatePluginFromTemplate.Automation
{
    class Package
    {
        public int FileVersion = 3;
        public int Version = 1;
        public string VersionName = "1.0";
        public string FriendlyName;
        public string Description = "Character package autogenerated for WellVr";
        public string Category = "CharacterPackage";
        public string CreatedBy = "AutoGenerated";
        public string CreatedByUrl = "";
        public string DocsUrl = "";
        public string MarketplaceUrl = "";
        public string SupportUrl = "";
        public bool CanContainContent = true;
        public bool IsBetaVersion = false;
        public bool Installed = false;
    }

    class UFileImportGroupSettings
    {
        public bool bUpdateSkeletonReferencePose = false;
        public bool bUseT0AsRefPose = false;
        public bool bPreserveSmoothingGroups = true;
        public bool bImportMeshesInBoneHierarchy = true;
        public bool bImportMorphTargets = true;
        public bool bKeepOverlappingVertices = true;
        public string Skeleton;
    }

    class UFileImportGroup
    {
        public string GroupName;
        public string[] Filenames;
        public string Destinationpath;
        public bool bReplaceExisting = true;
        public bool bSkipReadOnly = true;
        public string FactoryName = "FbxFactory";
        public UFileImportGroupSettings ImportGroupSettings = new UFileImportGroupSettings();
    }

    class UFileImportSettings
    {
        public UFileImportGroup[] ImportGroups;

    }

    class Options
    {
        [Option('p', "projectdirectory", Required = true, HelpText = "Base directory for uproject.")]
        public string ProjectDirectory { get; set; }

        [Option('r', "uprojectname", Required = true, HelpText = "Name of Unreal project.")]
        public string UprojectName { get; set; }

        [Option('u', "unrealdirectory", Required = true, HelpText = "Unreal Root Directory.  Should end with something like 4_19/")]
        public string UnrealDirectory { get; set; }

        [Option('n', "pluginname", Required = false, HelpText = "Plugin name.  Generated if not available.")]
        public string PluginName { get; set; }

        [Option('c', "charactername", Required = true, HelpText = "Character name for the plugin.")]
        public string CharacterName { get; set; }

        [Option('d', "charactersdirectory", Required = true, HelpText = "Directory containing character source folders.")]
        public string CharactersDirectory { get; set; }

        [Option('s', "skeletonasset", Required = false, HelpText = "Skeleton asset to use for imported asset.  If none, then skeleton from asset will be imported.")]
        public string SkeletonAsset { get; set; }

        [Option('e', "characterdescription", Required = false, HelpText = "Description for the character.")]
        public string CharacterDescription { get; set; }

        [Option('o', "outputdestination", Required = false, HelpText = "Destination to which to copy the package file.")]
        public string OutputDestination { get; set; }

        [Option('b', "build package", Default = false, Required = false, HelpText = "If included, builds package.")]
        public bool BuildPackage { get; set; }

        public string GetUprojectPath()
        {
            return ProjectDirectory + "\\" + UprojectName;
        }

        public string GetPluginDirectory()
        {
            return ProjectDirectory + "\\Plugins\\" + PluginName + "\\";
        }

        public string GetCharacterImportSettingsPath()
        {
            return GetPluginDirectory() + "ImportSettings\\" + CharacterName + ".json";
        }
    }

    class Program
    {

        private static void GeneratePlugin(Options opts)
        {
            if (opts.ProjectDirectory != null)
                opts.ProjectDirectory = opts.ProjectDirectory.Replace("\"", "");
            if (opts.UprojectName != null)
                opts.UprojectName = opts.UprojectName.Replace("\"", "");
            if (opts.UnrealDirectory != null)
                opts.UnrealDirectory = opts.UnrealDirectory.Replace("\"", "");
            if (opts.PluginName != null)
                opts.PluginName = opts.PluginName.Replace("\"", "");

            double seconds = DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            string packageName = "C" + seconds.ToString().Replace(".", "");
            Package p = new Package();
            p.FriendlyName = packageName;
            opts.PluginName = packageName;
            if (opts.CharacterDescription != null)
            {
                p.Description = opts.CharacterDescription;
            }
            string packageUpluginFileText = JsonConvert.SerializeObject(p, Formatting.Indented);

            Directory.CreateDirectory(opts.GetPluginDirectory());
            Directory.CreateDirectory(opts.GetPluginDirectory() + "Config/");
            Directory.CreateDirectory(opts.GetPluginDirectory() + "Content/");
            Directory.CreateDirectory(opts.GetPluginDirectory() + "Resources/");

            string upluginFile = opts.GetPluginDirectory() + p.FriendlyName + ".uplugin";
            using (TextWriter tw = File.CreateText(upluginFile))
            {
                tw.Write(packageUpluginFileText);
                tw.Close();
            }

            Console.Write("Generated plugin files for " + opts.PluginName + "\n");

            GenerateCharacterImportSettingsFile(null, null, opts);
            RunGenerateProjectFilesProcess(null, null, opts);
            RunCharacterImportProcess(null, null, opts);
            if (opts.BuildPackage)
            {
                RunBuildDlcPackageProcess(null, null, opts);
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        private static void GenerateCharacterImportSettingsFile(object sender, EventArgs e, Options opts)
        {
            UFileImportSettings uFileImportSettings = new UFileImportSettings();

            UFileImportGroup SkeletalMesh = new UFileImportGroup();
            SkeletalMesh.GroupName = "SkeletalMesh";
            SkeletalMesh.Filenames = new string[] { opts.CharactersDirectory + "\\" + opts.CharacterName + "\\" + opts.CharacterName + ".fbx" };
            SkeletalMesh.Destinationpath = "/" + opts.PluginName + "/SkeletalMeshes/";
            if (opts.SkeletonAsset != null)
            {
                SkeletalMesh.ImportGroupSettings.Skeleton = opts.SkeletonAsset;
            }

            UFileImportGroup Textures = new UFileImportGroup();
            Textures.GroupName = "Textures";
            Textures.FactoryName = "TextureFactory";
            Textures.Filenames = new string[] { opts.CharactersDirectory + "\\" + opts.CharacterName + "\\" + opts.CharacterName + "_body_derm.png", opts.CharactersDirectory + "\\" + opts.CharacterName + "\\" + opts.CharacterName + "_body_displ.png" };
            Textures.Destinationpath = "/" + opts.PluginName + "/Textures/";

            uFileImportSettings.ImportGroups = new UFileImportGroup[] { SkeletalMesh, Textures };

            string characterImportSettingsFileText = JsonConvert.SerializeObject(uFileImportSettings, Formatting.Indented);

            Directory.CreateDirectory(opts.GetPluginDirectory() + "ImportSettings\\");

            string importSettingsFile = opts.GetCharacterImportSettingsPath();
            using (TextWriter tw = File.CreateText(importSettingsFile))
            {
                tw.Write(characterImportSettingsFileText);
                tw.Close();
            }

            Console.Write("Generated character import settings for " + opts.PluginName + "\n");
        }

        private static void RunGenerateProjectFilesProcess(object sender, EventArgs e, Options opts)
        {
            string buildToolPath = opts.UnrealDirectory + "\\Engine\\Binaries\\DotNET\\UnrealBuildTool.exe";

            Process unrealBuildProcess = new Process();
            ProcessStartInfo unrealBuildProcessStartInfo = new ProcessStartInfo();
            unrealBuildProcessStartInfo.CreateNoWindow = true;
            unrealBuildProcessStartInfo.UseShellExecute = false;
            unrealBuildProcessStartInfo.FileName = buildToolPath;
            unrealBuildProcessStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            unrealBuildProcessStartInfo.RedirectStandardOutput = true;
            unrealBuildProcessStartInfo.Arguments = "-projectfiles -project=\"" + opts.GetUprojectPath() + "\"";
            unrealBuildProcess.StartInfo = unrealBuildProcessStartInfo;
            //unrealBuildProcess.Exited += (sender, e) => OnProjectFilesGenerated(sender, e, opts);

            Console.WriteLine("Starting " + unrealBuildProcessStartInfo.FileName + " " + unrealBuildProcessStartInfo.Arguments);
            unrealBuildProcess.Start();

            do
            {
                Thread.Sleep(100);
                Console.Out.Write(unrealBuildProcess.StandardOutput.ReadToEnd());
            }
            while (!unrealBuildProcess.HasExited);
            Console.Out.Write(unrealBuildProcess.StandardOutput.ReadToEnd());
        }

        private static void RunCharacterImportProcess(object sender, EventArgs e, Options opts)
        {
            Process characterImportProcess = new Process();

            ProcessStartInfo characterImportProcessStartInfo = new ProcessStartInfo();
            characterImportProcessStartInfo.CreateNoWindow = true;
            characterImportProcessStartInfo.UseShellExecute = false;
            characterImportProcessStartInfo.FileName = opts.UnrealDirectory + "/Engine/Binaries/Win64/UE4Editor-Cmd.exe";
            characterImportProcessStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            characterImportProcessStartInfo.RedirectStandardOutput = true;
            characterImportProcessStartInfo.Arguments = "\"" + opts.GetUprojectPath() + "\"" + " -run=ImportAssets " +
                "-importsettings=\"" + opts.GetCharacterImportSettingsPath() + "\"" +
                " -nosourcecontrol";
            characterImportProcess.StartInfo = characterImportProcessStartInfo;
            //unrealBuildProcess.Exited += (sender, e) => OnProjectFilesGenerated(sender, e, opts);

            Console.WriteLine("Starting " + characterImportProcessStartInfo.FileName + " " + characterImportProcessStartInfo.Arguments);
            characterImportProcess.Start();

            do
            {
                Thread.Sleep(100);
                Console.Out.Write(characterImportProcess.StandardOutput.ReadToEnd());
            }
            while (!characterImportProcess.HasExited);
            Console.Out.Write(characterImportProcess.StandardOutput.ReadToEnd());
        }

        private static void RunBuildDlcPackageProcess(object sender, EventArgs e, Options opts)
        {
            Process dlcBuildProcess = new Process();

            ProcessStartInfo dlcBuildProcessStartInfo = new ProcessStartInfo();
            dlcBuildProcessStartInfo.CreateNoWindow = true;
            dlcBuildProcessStartInfo.UseShellExecute = false;
            dlcBuildProcessStartInfo.FileName = opts.UnrealDirectory + "\\Engine\\Binaries\\DotNET\\AutomationTool.exe";
            dlcBuildProcessStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            dlcBuildProcessStartInfo.RedirectStandardOutput = true;
            dlcBuildProcessStartInfo.Arguments =
                "-ScriptsForProject=\"" + opts.GetUprojectPath() + "\" " +
                "BuildCookRun " +
                "-project=\"" + opts.GetUprojectPath() + "\" " +
                "-dlcname=" + opts.PluginName +
                " -noP4 -clientconfig=Development -serverconfig=Development -nocompile -nocompileeditor -installed -ue4exe=UE4Editor-Cmd.exe -utf8output " +
                "-platform=Win64 -targetplatform=Win64 -build -cook -unversionedcookedcontent -pak -DLCIncludeEngineContent -basedonreleaseversion=1.0 -compressed -stage -package";
            dlcBuildProcess.StartInfo = dlcBuildProcessStartInfo;

            //unrealBuildProcess.Exited += (sender, e) => OnProjectFilesGenerated(sender, e, opts);

            Console.WriteLine("Starting " + dlcBuildProcessStartInfo.FileName + " " + dlcBuildProcessStartInfo.Arguments);

            dlcBuildProcess.Start();

            do
            {
                Thread.Sleep(100);
                Console.Out.Write(dlcBuildProcess.StandardOutput.ReadToEnd());
            }
            while (!dlcBuildProcess.HasExited);
            Console.Out.Write(dlcBuildProcess.StandardOutput.ReadToEnd());
        }

        private static void HandleParseError(IEnumerable<Error> errs)
        {
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        static void Main(string[] args)
        {
            CommandLine.Parser.Default.ParseArguments<Options>(args)
              .WithParsed<Options>(opts => GeneratePlugin(opts))
              .WithNotParsed<Options>((errs) => HandleParseError(errs));
        }
    }
}