#if ENABLE_EXPERIMENTAL_INCREMENTAL_PIPELINE
#if UNITY_ANDROID_DONT
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using UnityEditor.Android.PostProcessor;
using UnityEditor.Android.PostProcessor.Tasks;
using UnityEditor.Utils;

namespace Unity.Platforms.Android.Build
{
    class AndroidProjectExportVisualStudioGradle : AndroidProjectExportGradle
    {
        class LibraryProject
        {
            private string m_Name;
            public string Name { get { return m_Name;  } }
            private string m_Guid;
            public string Guid { get { return m_Guid; } }

            public LibraryProject(string name, string guid)
            {
                m_Name = name;
                m_Guid = guid;
            }
        }

        readonly XNamespace m_XmlNamespace = "http://schemas.microsoft.com/developer/msbuild/2003";

        protected override string buildGradleFileName
        {
            get { return "build.gradle.template"; }
        }

        protected override string gradlePropertiesFileName
        {
            get { return "gradle.properties.template"; }
        }

        protected override string localPropertiesFileName
        {
            get { return "local.properties.template"; }
        }

        protected override string settingsGradleFileName
        {
            get { return "settings.gradle.template"; }
        }

        protected override bool sourceBuildReferenceStrippedLibraries
        {
            get
            {
                return true;
            }
        }
        protected override string GenerateProjectPath()
        {
            return Path.Combine(m_TargetPath, m_ProductNameForFileSystem, "app");
        }

        protected IEnumerable<string> GetConfigs()
        {
            if (!m_SourceBuild)
            {
                foreach (var config in SimpleConfigs)
                    yield return config;
            }
            else
            {
                foreach (var config in GetConfigsForSourceBuild())
                    yield return config;
            }
        }

        public override void ExportWithCurrentSettings()
        {
            // Is it Build & Run
            if (m_TargetPath == null)
            {
                m_TargetPath = Path.Combine("Temp", "visualStudioOut");// Utils.GetProjectExportTempPath(AndroidBuildSystem.VisualStudio);
                if (Directory.Exists(m_TargetPath))
                    Directory.Delete(m_TargetPath, true);
            }

            var solutionPath = m_TargetPath;
            var slnFilePath = Path.Combine(solutionPath, m_ProductNameForFileSystem + ".sln");
            var projectPath = Path.Combine(solutionPath, m_ProductNameForFileSystem);
            var androidProjFilePath = Path.Combine(projectPath, m_ProductNameForFileSystem + ".androidproj");
            var androidProjUserFilePath = Path.Combine(projectPath, m_ProductNameForFileSystem + ".androidproj.user");
            var unityLibraryProjectPath = Path.Combine(projectPath, "app", "unityLibrary");
            var unityLibraryProjectSrcMainPath = Path.Combine(unityLibraryProjectPath, "src", "main");
            var launcherProjectPath = Path.Combine(projectPath, "app", "launcher");
            var launcherProjectSrcMainPath = Path.Combine(launcherProjectPath, "src", "main");
            var symbolsProjectPath = Path.Combine(launcherProjectPath, "symbols");

            Directory.CreateDirectory(solutionPath);
            Directory.CreateDirectory(projectPath);
            Directory.CreateDirectory(launcherProjectPath);

            m_TemplateValues["BUILT_APK_LOCATION"] = GetApkLocation();
            var launcherManifestFilename = Path.Combine(launcherProjectSrcMainPath, "AndroidManifest.xml");
            var unityLibraryManifestFilename = Path.Combine(unityLibraryProjectSrcMainPath, "AndroidManifest.xml");

            // Delete previous version of AndroidManifest.xml if such exist otherwise it will be added to BuildReport twice (when adding directory & adding this specific file)
            // And exception will be thrown
            CleanupPreviousManifest(launcherManifestFilename);
            CleanupPreviousManifest(unityLibraryManifestFilename);

            base.ExportWithCurrentSettings();

            // Visual Studio expects AndroidManifest to have .template extension
            RenameWithTemplateExtension(launcherManifestFilename);
            RenameWithTemplateExtension(unityLibraryManifestFilename);

            CopyDir(Path.Combine(m_StagingArea, "symbols"), symbolsProjectPath);
            CopySymbols(symbolsProjectPath);

            var libraryProjects = ProcessLibraries(solutionPath);

            // Copy Gradle helper files, Visual Studio invokes it when building gradle target
            WriteGradleWrapperFiles(projectPath);

            var libraryJavaDirectory = Path.Combine(unityLibraryProjectSrcMainPath, "java");
            var libraryJavaFiles = Directory.GetFiles(libraryJavaDirectory, "*.java", SearchOption.AllDirectories);
            var javaFiles = libraryJavaFiles.Select(p => p.Substring(projectPath.Length + 1)).ToArray();

            var projectGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();
            WriteSolutionFile(slnFilePath, m_ProductNameForFileSystem, Guid.NewGuid().ToString("B").ToUpperInvariant(), projectGuid, libraryProjects);
            WriteAndroidProjectFile(androidProjFilePath, m_ProductNameForFileSystem, m_ProductNameForFileSystem, projectGuid, false, javaFiles, libraryProjects);
            WriteAndroidProjectUserFile(androidProjUserFilePath, false);

            var eclipseProjectPath = Path.Combine(solutionPath, ".workspace", m_ProductNameForFileSystem);
            WriteEclipseFiles(eclipseProjectPath);
        }

        void CleanupPreviousManifest(string filename)
        {
            FileUtil.DeleteFileOrDirectory(filename);
            FileUtil.DeleteFileOrDirectory(filename + ".template");
        }

        string RenameWithTemplateExtension(string path)
        {
            var newPath =  path + ".template";
            File.Move(path, newPath);
            return newPath;
        }

        void CopySymbols(string projectSymbolsDir)
        {
            // move il2cpp symbol files from jniLibs to symbols directory
            if (m_ScriptingBackend == ScriptingImplementation.IL2CPP)
            {
                foreach (var projectSymbolsABIDir in Directory.GetDirectories(projectSymbolsDir))
                {
                    RenameSymbolFile(projectSymbolsABIDir, "libil2cpp", ".sym.so", "dbg.so");
                }
            }

            if (m_SourceBuild)
                return;

            var stripEngineCode = m_Context.BuildSettings.GetComponent<ScriptingBackendConfiguration>().StripEngineCode;

            Directory.CreateDirectory(projectSymbolsDir);
            var symbolsDir = TasksCommon.GetSymbolsDirectory(m_Context);
            foreach (var abiDir in Directory.GetDirectories(symbolsDir))
            {
                var abi = Path.GetFileName(abiDir);
                var projectSymbolsABIDir = Path.Combine(projectSymbolsDir, abi);
                Directory.CreateDirectory(projectSymbolsABIDir);

                var symbols = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

                // .sym.so files contain partial debug information (things like function name only)
                foreach (var path in Directory.GetFiles(abiDir, "*.sym.so"))
                {
                    var name = Path.GetFileName(path);
                    name = name.Substring(0, name.LastIndexOf(".sym.so"));
                    symbols.Add(name, path);
                }

                // .dbg.so files contain full debug information (overwrite corresponding .sym.so file since it's "better")
                foreach (var path in Directory.GetFiles(abiDir, "*.dbg.so"))
                {
                    var name = Path.GetFileName(path);
                    name = name.Substring(0, name.LastIndexOf(".dbg.so"));
                    symbols[name] = path;
                }

                // visual studio can only read symbols from .so files
                foreach (var symbol in symbols)
                {
                    if (stripEngineCode && string.Equals(symbol.Key, "libunity", StringComparison.InvariantCultureIgnoreCase))
                        continue;
                    File.Copy(symbol.Value, Path.Combine(projectSymbolsABIDir, symbol.Key + ".so"), true);
                }

                if (stripEngineCode)
                {
                    RenameSymbolFile(projectSymbolsABIDir, "libunity", ".sym.so", ".dbg.so");
                }
            }
        }

        static void RenameSymbolFile(string directory, string name, string symExtension, string dbgExtension)
        {
            var symFile = Path.Combine(directory, name + symExtension);
            var dbgFile = Path.Combine(directory, name + dbgExtension);
            var source = File.Exists(symFile) ? symFile : null;
            if (File.Exists(dbgFile))
            {
                source = dbgFile;
                File.Delete(symFile);
            }
            if (source == null)
                return;
            var target = Path.Combine(directory, name + ".so");
            File.Delete(target);
            File.Move(source, target);
        }

        void WriteSolutionFile(string solutionFilePath, string productName, string solutionGuid, string projectGuid, IEnumerable<LibraryProject> libraryProjects)
        {
            var contents = new StringBuilder();
            contents.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
            contents.AppendLine("# Visual Studio 15");
            contents.AppendLine("VisualStudioVersion = 15.0.27130.0");
            contents.AppendLine("MinimumVisualStudioVersion = 10.0.40219.1");
            var playerSolutionGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();
            const string playerProjectGuid = "{BBD84EDD-755B-8327-D120-258FDF04A1D0}";
            var ueSolutionGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();
            const string ueProjectGuid = "{F0499708-3EB6-4026-8362-97E6FFC4E7C8}";
            contents.AppendFormat("Project(\"{0}\") = \"{1}\", \"{1}\\{1}.androidproj\", \"{2}\"",
                solutionGuid,
                productName,
                projectGuid);
            contents.AppendLine();
            if (m_SourceBuild)
            {
                contents.AppendLine("\tProjectSection(ProjectDependencies) = postProject");
                contents.AppendFormat("\t\t{0} = {0}", playerProjectGuid);
                contents.AppendLine();
                contents.AppendLine("\tEndProjectSection");
            }
            contents.AppendLine("EndProject");
            if (m_SourceBuild)
            {
                var stripEngineCode = m_Context.BuildSettings.GetComponent<ScriptingBackendConfiguration>().StripEngineCode;
                var projectName = m_ScriptingBackend == ScriptingImplementation.IL2CPP && stripEngineCode == false ?
                    "AndroidPlayerIL2CPPNoStatic" : "AndroidPlayer";

                contents.AppendFormat("Project(\"{0}\") = \"AndroidPlayer\", \"..\\..\\..\\..\\Projects\\VisualStudio\\Projects\\" + projectName + ".vcxproj\", \"{1}\"", playerSolutionGuid, playerProjectGuid);
                contents.AppendLine();
                contents.AppendFormat("Project(\"{0}\") = \"UnityEngine\", \"..\\..\\..\\..\\Projects\\CSharp\\UnityEngine.csproj\", \"{1}\"", ueSolutionGuid, ueProjectGuid);
                contents.AppendLine();
                contents.AppendLine("EndProject");
            }

            foreach (var libraryProject in libraryProjects)
            {
                contents.AppendFormat("Project(\"{0}\") = \"{1}\", \"{1}\\{1}.androidproj\", \"{2}\"",
                    solutionGuid,
                    libraryProject.Name,
                    libraryProject.Guid);
                contents.AppendLine();
                contents.AppendLine("EndProject");
            }

            var platforms = new List<string>(m_DeviceTypes.Select(t => t.VisualStudioPlatform));

            contents.AppendLine("Global");
            contents.AppendLine("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution");
            foreach (var config in GetConfigs())
            {
                foreach (var platform in platforms)
                {
                    contents.AppendFormat("\t\t{0}|{1} = {0}|{1}", config, platform);
                    contents.AppendLine();
                }
            }

            contents.AppendLine("\tEndGlobalSection");
            contents.AppendLine("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution");
            if (m_SourceBuild)
            {
                var subTargets = new[] { "ActiveCfg", "Build.0" };
                foreach (var config in GetConfigs())
                {
                    // The sourcecode .vcxproj we are referencing has slightly differently named configurations.
                    // Map the ones in our .sln to the ones in .vcxproj properly.
                    string sourceConfig = config;
                    switch (config)
                    {
                        case "Debug_IL2CPP": sourceConfig = "DebugIl2cpp"; break;
                        case "Debug_Mono": sourceConfig = "DebugMono"; break;
                        case "Development_IL2CPP": sourceConfig = "ReleaseDevIl2cpp"; break;
                        case "Development_Mono": sourceConfig = "ReleaseDevMono"; break;
                        case "Release_IL2CPP": sourceConfig = "ReleaseNondevIl2cpp"; break;
                        case "Release_Mono": sourceConfig = "ReleaseNondevMono"; break;
                    }
                    foreach (var platform in platforms)
                    {
                        var activePlatform = (platform == "x86") ? "Win32" : platform;
                        foreach (var subTarget in subTargets)
                        {
                            contents.AppendFormat("\t\t{0}.{1}|{2}.{3} = {5}|{4}", playerProjectGuid, config, platform, subTarget, activePlatform, sourceConfig);
                            contents.AppendLine();
                        }
                    }
                }
            }
            var allProjects = new List<LibraryProject>();
            allProjects.Add(new LibraryProject(productName, projectGuid));
            allProjects.AddRange(libraryProjects);
            foreach (var project in allProjects)
            {
                foreach (var config in GetConfigs())
                {
                    foreach (var platform in platforms)
                    {
                        var subTargets = new List<string> { "ActiveCfg" };
                        string activePlatform;
                        if (platform != "x64")
                        {
                            subTargets.Add("Build.0");
                            if (project.Name == productName)
                                subTargets.Add("Deploy.0");
                            activePlatform = platform;
                        }
                        else
                        {
                            // At the moment x64 platform in AndroidPlayer.vcxproj represents FAT (it builds both ARM and x86 players).
                            // This project doesn't support FAT or x64 so select x86 and don't build or deploy it.
                            activePlatform = "x86";
                        }
                        foreach (var subtarget in subTargets)
                        {
                            contents.AppendFormat("\t\t{0}.{2}|{3}.{1} = {2}|{4}", project.Guid, subtarget, config, platform, activePlatform);
                            contents.AppendLine();
                        }
                    }
                }
            }
            contents.AppendLine("\tEndGlobalSection");
            contents.AppendLine("\tGlobalSection(SolutionProperties) = preSolution");
            contents.AppendLine("\t\tHideSolutionNode = FALSE");
            contents.AppendLine("\tEndGlobalSection");
            contents.AppendLine("EndGlobal");

            File.WriteAllText(solutionFilePath, contents.ToString());
        }

        void WriteAndroidProjectFile(string androidProjectFilePath, string productName, string mainProjectProductName, string projectGuid, bool library, string[] javaFilePaths = null, IEnumerable<LibraryProject> libraryProjects = null)
        {
            var declaration = new XDeclaration("1.0", "utf-8", null);
            var document = new XDocument(declaration);
            var project = new XElement(m_XmlNamespace + "Project",
                new XAttribute("DefaultTargets", "Build"),
                new XAttribute("ToolsVersion", "14.0"));
            document.Add(project);

            var projectConfigs = new XElement(m_XmlNamespace + "ItemGroup",
                new XAttribute("Label", "ProjectConfigurations"));
            foreach (var deviceType in m_DeviceTypes)
            {
                foreach (var config in GetConfigs())
                {
                    var configName = m_SourceBuild ? deviceType.GradleProductFlavor + config : config;
                    var projectConfiguration = new XElement(m_XmlNamespace + "ProjectConfiguration", new XAttribute("Include", string.Format("{0}|{1}", configName, deviceType.VisualStudioPlatform)));
                    projectConfiguration.Add(new XElement(m_XmlNamespace + "Configuration", configName));
                    projectConfiguration.Add(new XElement(m_XmlNamespace + "Platform", deviceType.VisualStudioPlatform));
                    projectConfigs.Add(projectConfiguration);
                }
            }
            project.Add(projectConfigs);

            var globals = new XElement(m_XmlNamespace + "PropertyGroup", new XAttribute("Label", "Globals"));
            // This overrides SDK/NDK paths which are set via Tools->Options->Cross Platform->C++->Android
            globals.Add(new XElement(m_XmlNamespace + "VS_AndroidHome", m_AndroidBuildContext.SDKPath));
            globals.Add(new XElement(m_XmlNamespace + "VS_NdkRoot", m_AndroidBuildContext.NDKPath));

            globals.Add(new XElement(m_XmlNamespace + "AndroidBuildType", "Gradle"));
            globals.Add(new XElement(m_XmlNamespace + "RootNamespace", string.Join("_", productName.Split(new[] { ' ', '(', ')' }))));
            globals.Add(new XElement(m_XmlNamespace + "MinimumVisualStudioVersion", "14.0"));
            globals.Add(new XElement(m_XmlNamespace + "ProjectVersion", "1.0"));
            globals.Add(new XElement(m_XmlNamespace + "ProjectGuid", projectGuid));
            if (library)
                globals.Add(new XElement(m_XmlNamespace + "TargetExt", ".aar"));
            globals.Add(new XElement(m_XmlNamespace + "_PackagingProjectWithoutNativeComponent", "true"));
            var launchActivity = new XElement(m_XmlNamespace + "LaunchActivity", new XAttribute("Condition", "'$(LaunchActivity)' == ''"));
            if (library)
                launchActivity.SetValue(string.Format("com.{0}.{0}", productName));
            else
                launchActivity.SetValue("com.unity3d.player.UnityPlayerActivity");
            globals.Add(launchActivity);
            globals.Add(new XElement(m_XmlNamespace + "JavaSourceRoots", library ? "src" : "src\\main\\java"));
            project.Add(globals);

            project.Add(new XElement(m_XmlNamespace + "Import", new XAttribute("Project", "$(AndroidTargetsPath)\\Android.Default.props")));

            foreach (var deviceType in m_DeviceTypes)
            {
                foreach (var config in GetConfigs())
                {
                    var configName = m_SourceBuild ? deviceType.GradleProductFlavor + config : config;
                    var propGroup = new XElement(m_XmlNamespace + "PropertyGroup",
                        new XAttribute("Condition", string.Format("'$(Configuration)|$(Platform)'=='{0}|{1}'", configName, deviceType.VisualStudioPlatform)),
                        new XAttribute("Label", "Configuration"));
                    propGroup.Add(new XElement(m_XmlNamespace + "ConfigurationType", library ? "Library" : "Application"));
                    propGroup.Add(new XElement(m_XmlNamespace + "AndroidAPILevel", "android-" + m_TargetSDKVersion));
                    project.Add(propGroup);
                }
            }

            project.Add(new XElement(m_XmlNamespace + "Import", new XAttribute("Project", "$(AndroidTargetsPath)\\Android.props")));

            // getting installed Gradle version
            Version gradleVersion = new Version();
            var gradleDir = Path.Combine(BuildBridge.GetBuildToolsDirectory(BuildTarget.Android), "gradle");
            var libDir = Path.Combine(gradleDir, "lib");
            var launcherFiles = Directory.GetFiles(libDir, "gradle-launcher-*.jar", SearchOption.AllDirectories);
            if (launcherFiles.Length == 1)
            {
                var reg = new Regex(@"gradle-launcher-((\d+)\.(\d+)(\.(\d+))?)\.jar", RegexOptions.Compiled);
                var match = reg.Match(launcherFiles[0]);
                if (match.Success)
                    gradleVersion = BuildBridge.ParseVersion(match.Groups[1].Value);
            }

            var gradlePackage = new XElement(m_XmlNamespace + "GradlePackage",
                new XElement(m_XmlNamespace + "ProjectDirectory", "$(ProjectDir)app"),
                // Gradle plugin version is directly written in build.gradle, so this value is ignored, but
                // Visual Studio has a check inside against 'gradle-experimental' string, and performs building differently
                // That's why we set plugin version in format gradle:0.0.0
                new XElement(m_XmlNamespace + "GradlePlugin", "gradle" + ':' + new Version(0, 0, 0)),
                new XElement(m_XmlNamespace + "GradleVersion", gradleVersion));

            foreach (var config in GetConfigs())
            {
                foreach (var deviceType in m_DeviceTypes)
                {
                    var configName = m_SourceBuild ? deviceType.GradleProductFlavor + config : config;
                    var apkFileNameFlavor = (library || m_SourceBuild) ? $"-{deviceType.GradleProductFlavor}" : "";
                    gradlePackage.Add(new XElement(m_XmlNamespace + "ApkFileName",
                        new XAttribute("Condition", string.Format("'$(Configuration)'=='{0}' and '$(Platform)'=='{1}'", configName, deviceType.VisualStudioPlatform)),
                        string.Format("launcher{0}-{1}$(TargetExt)", apkFileNameFlavor, config)));
                }
            }

            project.Add(new XElement(m_XmlNamespace + "ItemDefinitionGroup", gradlePackage));

            project.Add(new XElement(m_XmlNamespace + "ImportGroup", new XAttribute("Label", "ExtensionSettings")));
            project.Add(new XElement(m_XmlNamespace + "PropertyGroup", new XAttribute("Label", "UserMacros")));

            project.Add(new XElement(m_XmlNamespace + "ItemGroup",
                new XElement(m_XmlNamespace + "GradleTemplate", new XAttribute("Include", "app\\build.gradle.template")),
                new XElement(m_XmlNamespace + "GradleTemplate", new XAttribute("Include", "app\\gradle.properties.template")),
                new XElement(m_XmlNamespace + "GradleTemplate", new XAttribute("Include", "app\\local.properties.template")),
                new XElement(m_XmlNamespace + "GradleTemplate", new XAttribute("Include", "app\\settings.gradle.template")),
                new XElement(m_XmlNamespace + "GradleTemplate", new XAttribute("Include", "app\\launcher\\build.gradle.template")),
                new XElement(m_XmlNamespace + "GradleTemplate", new XAttribute("Include", "app\\launcher\\src\\main\\AndroidManifest.xml.template")),
                new XElement(m_XmlNamespace + "GradleTemplate", new XAttribute("Include", "app\\unityLibrary\\build.gradle.template")),
                new XElement(m_XmlNamespace + "GradleTemplate", new XAttribute("Include", "app\\unityLibrary\\src\\main\\AndroidManifest.xml.template")),
                new XElement(m_XmlNamespace + "GradleTemplate", new XAttribute("Include", "gradle\\wrapper\\gradle-wrapper.properties.template"))));

            if (javaFilePaths != null && javaFilePaths.Length > 0)
            {
                var xJavaFiles = new List<object>();
                foreach (var javaFile in javaFilePaths)
                {
                    xJavaFiles.Add(new XElement(m_XmlNamespace + "JavaCompile", new XAttribute("Include", javaFile)));
                }
                project.Add(new XElement(m_XmlNamespace + "ItemGroup", xJavaFiles.ToArray()));
            }

            // Add symlinked sources
            var externalJavaSources = m_AndroidBuildContext.ExternalJavaSources;
            if (externalJavaSources != null && externalJavaSources.Count > 0)
            {
                var xJavaFiles = new List<object>();
                foreach (var javaFile in externalJavaSources)
                {
                    var file = javaFile.Replace("/", "\\");
                    xJavaFiles.Add(new XElement(m_XmlNamespace + "JavaCompile",
                        new XAttribute("Include", file),
                        new XElement(m_XmlNamespace + "Link", file.Substring(Path.GetPathRoot(file).Length))));
                }
                project.Add(new XElement(m_XmlNamespace + "ItemGroup", xJavaFiles.ToArray()));
            }

            project.Add(new XElement(m_XmlNamespace + "ItemGroup",
                new XElement(m_XmlNamespace + "None", new XAttribute("Include", library ? @"app\unityLibrary\src\**" : @"app\unityLibrary\src\main\assets\**")),
                new XElement(m_XmlNamespace + "None", new XAttribute("Include", library ? @"app\unityLibrary\jniLibs\**" : @"app\unityLibrary\src\main\jniLibs\**")),
                new XElement(m_XmlNamespace + "None", new XAttribute("Include", library ? @"app\unityLibrary\res\**" : @"app\unityLibrary\src\main\res\**"))));

            if (libraryProjects != null && libraryProjects.Any())
            {
                var projectReferences = new List<object>();
                foreach (var libraryProject in libraryProjects)
                {
                    projectReferences.Add(new XElement(m_XmlNamespace + "ProjectReference", new XAttribute("Include", string.Format("..\\{0}\\{0}.androidproj", libraryProject.Name))));
                }
                project.Add(new XElement(m_XmlNamespace + "ItemGroup", projectReferences.ToArray()));
            }

            project.Add(new XElement(m_XmlNamespace + "Import", new XAttribute("Project", "$(AndroidTargetsPath)\\Android.targets")));
            project.Add(new XElement(m_XmlNamespace + "ImportGroup", new XAttribute("Label", "ExtensionTargets")));

            if (library)
            {
                // copy aar file to main project's libs directory.
                // this is only needed because vs2015 update 3 doesn't handle library project references well.
                project.Add(new XElement(m_XmlNamespace + "PropertyGroup",
                    new XElement(m_XmlNamespace + "PostBuildEvent", string.Format("copy /Y \"$(TargetPath)\" \"$(SolutionDir){0}\\app\\unityLibrary\\libs\\$(TargetFileName)\"", mainProjectProductName))));
            }
            else
            {
                // Fix for a rare bug where Visual Studio doesn't even invoke build.gradle and says the package is up to date
                // Though libunity.so is newer than apk.
                // Try to avoid this situation by adding those libraries as external items.
                if (m_SourceBuild)
                {
                    var externalItems = new[] { "libunity.so", "libmain.so" };
                    var targetArchitectures = m_ProjectContext.Architectures;
                    var externalItemGroup = new XElement(m_XmlNamespace + "ItemGroup");

                    // Add libraries
                    foreach (var config in SimpleConfigs)
                    {
                        foreach (var deviceType in m_DeviceTypes)
                        {
                            foreach (var e in externalItems)
                            {
                                var path = Path.Combine(GetScriptingImplementationsForSourceBuild(),
                                    config,
                                    "Libs",
                                    deviceType.ABI,
                                    e);


                                var contentItemGroup = new XElement(m_XmlNamespace + "Content",
                                    new XAttribute("Include", Path.Combine(@"..\..\..\Variations", path)),
                                    new XElement(m_XmlNamespace + "Link", Path.Combine(@"External", path)));
                                externalItemGroup.Add(contentItemGroup);
                            }
                        }
                    }

                    // Add classes.jar
                    foreach (var config in SimpleConfigs)
                    {
                        var path = Path.Combine(GetScriptingImplementationsForSourceBuild(), config, "Classes", "classes.jar");
                        var contentItemGroup = new XElement(m_XmlNamespace + "Content",
                            new XAttribute("Include", Path.Combine(@"..\..\..\Variations", path)),
                            new XElement(m_XmlNamespace + "Link", Path.Combine(@"External", path)));
                        externalItemGroup.Add(contentItemGroup);
                    }

                    project.Add(externalItemGroup);
                }
            }

            document.Save(androidProjectFilePath);
        }

        void WriteAndroidProjectUserFile(string androidProjectUserFilePath, bool library)
        {
            var declaration = new XDeclaration("1.0", "utf-8", null);
            var document = new XDocument(declaration);
            var project = new XElement(m_XmlNamespace + "Project",
                new XAttribute("DefaultTargets", "Build"),
                new XAttribute("ToolsVersion", "14.0"));
            document.Add(project);

            if (library)
            {
                project.Add(new XElement(m_XmlNamespace + "PropertyGroup"));
            }
            else
            {
                if (m_SourceBuild)
                {
                    foreach (var scriptingImplementation in ScriptingImplementations)
                    {
                        foreach (var config in SimpleConfigs)
                        {
                            foreach (var deviceType in m_DeviceTypes)
                            {
                                var propGroup = new XElement(m_XmlNamespace + "PropertyGroup", new XAttribute("Condition", string.Format("'$(Configuration)|$(Platform)'=='{0}_{1}|{2}'", deviceType.GradleProductFlavor + config, scriptingImplementation, deviceType.VisualStudioPlatform)));
                                var searchPath = string.Format(@"$(ProjectDir)..\..\..\Variations\{0}\{1}\Symbols\{2};$(ProjectDir)app\symbols\{2};$(AdditionalSymbolSearchPaths)", scriptingImplementation.ToLowerInvariant(), config, deviceType.ABI);
                                propGroup.Add(new XElement(m_XmlNamespace + "AdditionalSymbolSearchPaths", searchPath));
                                propGroup.Add(new XElement(m_XmlNamespace + "DebuggerFlavor", "AndroidDebugger"));
                                project.Add(propGroup);
                            }
                        }
                    }
                }
                else
                {
                    foreach (var config in GetConfigs())
                    {
                        foreach (var deviceType in m_DeviceTypes)
                        {
                            var propGroup = new XElement(m_XmlNamespace + "PropertyGroup", new XAttribute("Condition", string.Format("'$(Configuration)|$(Platform)'=='{0}|{1}'", config, deviceType.VisualStudioPlatform)));
                            var searchPath = string.Format(@"$(ProjectDir)app\symbols\{0};$(AdditionalSymbolSearchPaths)", deviceType.ABI);
                            propGroup.Add(new XElement(m_XmlNamespace + "AdditionalSymbolSearchPaths", searchPath));
                            propGroup.Add(new XElement(m_XmlNamespace + "DebuggerFlavor", "AndroidDebugger"));
                            project.Add(propGroup);
                        }
                    }
                }
            }

            document.Save(androidProjectUserFilePath);
        }

        void WriteGradleWrapperFiles(string projectPath)
        {
            var templatesDir = Path.Combine(BuildBridge.GetBuildToolsDirectory(BuildTarget.Android), "VisualStudioGradleTemplates");
            File.Copy(Path.Combine(templatesDir, "gradlew.bat"), Path.Combine(projectPath, "gradlew.bat"), true);
            var gradleWrapperPath = Path.Combine(projectPath, "gradle\\wrapper");
            Directory.CreateDirectory(gradleWrapperPath);
            File.Copy(Path.Combine(templatesDir, "gradle-wrapper.jar"), Path.Combine(gradleWrapperPath, "gradle-wrapper.jar"), true);
            File.Copy(Path.Combine(templatesDir, "gradle-wrapper.properties.template"), Path.Combine(gradleWrapperPath, "gradle-wrapper.properties.template"), true);
        }

        /// <summary>
        /// Visual Studio's Java Extensions use Eclipse project for java intellisense, but it doesn't work in some cases:
        /// - when java files are referenced outside the project, in VS it will say "Cannot determine the project containing the file", more info https://developercommunity.visualstudio.com/content/problem/395848/java-language-service-for-android-cannot-determine.html
        /// This function attempts to fix intellisense for those files
        /// </summary>
        void WriteEclipseFiles(string eclipseProjectPath)
        {
            Directory.CreateDirectory(eclipseProjectPath);
            var settingsDirectory = Path.Combine(eclipseProjectPath, ".settings");
            Directory.CreateDirectory(settingsDirectory);

            var classPathFile = Path.Combine(eclipseProjectPath, ".classpath");
            var projectFilePath = Path.Combine(eclipseProjectPath, ".project");
            XNamespace xmlNamespace = "";

            var declaration = new XDeclaration("1.0", "utf-8", null);
            var srcName = "src0";

            // ClassPath
            var classPathDocument = new XDocument(declaration);
            var classPath = new XElement(xmlNamespace + "classpath");
            classPath.Add(new XElement(xmlNamespace + "classpathentry",
                new XAttribute("kind", "src"),
                new XAttribute("path", srcName)));

            var targetSDKVersion = m_AndroidSettings.targetAPILevel;
            var platformRoot = Path.Combine(m_AndroidBuildContext.SDKPath, "platforms", $"android-{targetSDKVersion}", "android.jar").Replace("\\", "/");

            classPath.Add(new XElement(xmlNamespace + "classpathentry",
                new XAttribute("kind", "lib"),
                new XAttribute("path",  platformRoot)));

            classPath.Add(new XElement(xmlNamespace + "classpathentry",
                new XAttribute("kind", "lib"),
                new XAttribute("path", m_UnityJavaLibrary)));

            // Default eclipse project uses this file, but such file doesn't exist, keep commented
            //classPath.Add(new XElement(xmlNamespace + "classpathentry",
            //    new XAttribute("kind", "lib"),
            //    new XAttribute("path", "<androidSDK>/extras/android/support/v4/android-support-v4.jar")));

            classPath.Add(new XElement(xmlNamespace + "classpathentry",
                new XAttribute("kind", "output"),
                new XAttribute("path", "bin")));

            classPathDocument.Add(classPath);


            // Project
            var projectDocument = new XDocument(declaration);
            var projectDescription = new XElement(xmlNamespace + "projectDescription");
            projectDescription.Add(new XElement(xmlNamespace + "name", m_ProductNameForFileSystem));
            projectDescription.Add(new XElement(xmlNamespace + "comment"));
            projectDescription.Add(new XElement(xmlNamespace + "projects"));
            projectDescription.Add(new XElement(xmlNamespace + "buildSpec",
                new XElement("buildCommand",
                    new XElement(xmlNamespace + "name", "org.eclipse.jdt.core.javabuilder"),
                    new XElement(xmlNamespace + "arguments")
                )
            ));

            projectDescription.Add(new XElement(xmlNamespace + "natures",
                new XElement(xmlNamespace + "nature", "org.eclipse.jdt.core.javanature")));

            var linkedResources = new XElement(xmlNamespace + "linkedResources");

            var defaultLink = new XElement(xmlNamespace + "link",
                new XElement(xmlNamespace + "name", srcName),
                new XElement(xmlNamespace + "type", 2),
                new XElement(xmlNamespace + "location", Path.Combine(m_TargetPath, m_ProductNameForFileSystem, "app", "src", "main", "java").Replace("\\", "/")));
            linkedResources.Add(defaultLink);

            var externalJavaSources = m_AndroidBuildContext.ExternalJavaSources;
            if (externalJavaSources != null && externalJavaSources.Count > 0)
            {
                foreach (var javaFile in externalJavaSources)
                {
                    var file = javaFile.Replace("\\", "/");
                    var link = new XElement(xmlNamespace + "link",
                        new XElement(xmlNamespace + "name", "src/" + Path.GetFileName(file)),
                        new XElement(xmlNamespace + "type", 1),
                        new XElement(xmlNamespace + "location", file));

                    linkedResources.Add(link);
                }
            }
            projectDescription.Add(linkedResources);
            projectDocument.Add(projectDescription);

            classPathDocument.Save(classPathFile);
            projectDocument.Save(projectFilePath);

            // Settings
            // Fixes intelisense errors like "Syntax error, annotations are only available if source level is 1.5. or greater on @Override keywords)
            File.WriteAllText(Path.Combine(settingsDirectory, "org.eclipse.jdt.core.prefs"),
                string.Join(Environment.NewLine, new[]
                {
                    "eclipse.preferences.version=1",
                    "org.eclipse.jdt.core.compiler.codegen.inlineJsrBytecode=enabled",
                    "org.eclipse.jdt.core.compiler.codegen.methodParameters=do not generate",
                    "org.eclipse.jdt.core.compiler.codegen.targetPlatform = 1.8",
                    "org.eclipse.jdt.core.compiler.codegen.unusedLocal = preserve",
                    "org.eclipse.jdt.core.compiler.compliance = 1.8",
                    "org.eclipse.jdt.core.compiler.debug.lineNumber = generate",
                    "org.eclipse.jdt.core.compiler.debug.localVariable = generate",
                    "org.eclipse.jdt.core.compiler.debug.sourceFile = generate",
                    "org.eclipse.jdt.core.compiler.problem.assertIdentifier = error",
                    "org.eclipse.jdt.core.compiler.problem.enumIdentifier = error",
                    "org.eclipse.jdt.core.compiler.release = disabled",
                    "org.eclipse.jdt.core.compiler.source = 1.8"
                }));
        }

        /// <summary>
        /// Gradle builds apk to directory <ProjectName>\app\launcher\build\outputs\apk\<Config>\launcher-<Config>.apk, (<ProjectName>\app\launcher\build\outputs\apk\<Arch>\<Config>\launcher-<Arch>-<Config>.apk for source build)
        /// but Visual Studio expects it to be in <ProjectName>\app\build\outputs\apk\launcher-<Config>.apk
        /// So adjust output accordingly
        /// </summary>
        /// <returns></returns>
        private string GetApkLocation()
        {
            var pathTag = m_SourceBuild ? "../../" : "../";
            return string.Join(Environment.NewLine, new[]
            {
                "\tbuildDir = \"${rootProject.projectDir}/build/\"",
                "",
                "\tapplicationVariants.all { variant ->",
                "\t\tvariant.outputs.all {",
                "\t\t\toutputFileName = \"" + pathTag + "\" + outputFileName",
                "\t\t}",
                "\t}"
            });
        }

        IEnumerable<LibraryProject> ProcessLibraries(string solutionPath)
        {
            var libraryProjects = new List<LibraryProject>();
            /*TODO
            foreach (var libraryPath in m_AndroidLibraries)
            {
                var libraryName = Path.GetFileNameWithoutExtension(libraryPath);
                var projectPath = Path.Combine(solutionPath, libraryName);
                var androidProjFilePath = Path.Combine(projectPath, libraryName + ".androidproj");
                var androidProjUserFilePath = Path.Combine(projectPath, libraryName + ".androidproj.user");

                var projectGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();
                WriteAndroidProjectFile(androidProjFilePath, libraryName, m_ProductNameForFileSystem, projectGuid, true);
                WriteAndroidProjectUserFile(androidProjUserFilePath, true);

                libraryProjects.Add(new LibraryProject(libraryName, projectGuid));
            }*/
            return libraryProjects;
        }
    }
}
#endif
#endif
