#if ENABLE_EXPERIMENTAL_INCREMENTAL_PIPELINE
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditor.Utils;
using Bee.Core;
using System.Xml.Linq;

namespace Unity.Platforms.Android.Build
{
    partial class AndroidProjectExportGradle
    {
        const string unityLibraryProjectDir = "${project(':unityLibrary').projectDir}";

        protected string[] SimpleConfigs
        {
            get { return m_ProjectContext.SourceBuild ? new[] { "Debug", "Development", "Release" } : new[] { "Debug", "Release" }; }
        }

        protected string GetScriptingImplementationsForSourceBuild()
        {
            // It takes about 2 mins for Android Studio to import a source build gradle project when all scripting backends (il2cpp & mono) are included
            // When only one scripting backend is used, the import time goes down to 1 min
            // As far as I can tell Android Studio is doing "Gradle Sync", because it takes the same amount of time to do "Gradle Sync" after changing something in gradle
            var backend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android);
            switch (backend)
            {
                case ScriptingImplementation.IL2CPP:
                    return "IL2CPP";
                case ScriptingImplementation.Mono2x:
                    return "Mono";
                default:
                    throw new Exception("Unsupported scripting backend: " + backend);
            }
        }

        protected IEnumerable<string> GetConfigsForSourceBuild()
        {
            foreach (var config in SimpleConfigs)
                yield return config + '_' + GetScriptingImplementationsForSourceBuild();
        }

        private void AddSourceBuildInformation()
        {
            var referenceStrippedLibraries = false;
            var targetArchitectures = m_ProjectContext.Architectures;
            var sourceBuildSetup = new StringBuilder();
            sourceBuildSetup.Append("\n\nandroid {");
            var launcherBuildSetup = new StringBuilder();
            launcherBuildSetup.Append("\n\nandroid {");

            var buildVariants = SetupBuildVariants(targetArchitectures);
            sourceBuildSetup.Append(buildVariants);
            launcherBuildSetup.Append(buildVariants);
            // Tell gradle to link libunity.so and friends from build/AndroidPlayer or jniLibsUnstripped directory

            var scriptingImplementation = GetScriptingImplementationsForSourceBuild();

            var playerPackageUri = new Uri(m_ClassicContext.PlayerPackageDirectory + "/");
            var projectPathUri = new Uri(m_BuildContext.LibraryDirectory + "/");
            var androidPlayerPath = projectPathUri.MakeRelativeUri(playerPackageUri).ToString();

            var sourceSets = GenerateSourceSets(targetArchitectures, scriptingImplementation, referenceStrippedLibraries, androidPlayerPath, m_BuildContext.LibraryDirectory.ToString());
            sourceBuildSetup.Append(sourceSets);
            launcherBuildSetup.Append(sourceSets);

            // For Visual Studio, we have AndroidStupport project which is used both for building the library and navigating through sources
            // Thus we don't need this magic
            if (m_ProjectContext.InjectUnityBuildScripts)
            {
                sourceBuildSetup.Append(AddSourceBuildJamBuilding(targetArchitectures, scriptingImplementation, referenceStrippedLibraries));

                // Add Unity player files (they won't be compiled, but they will be included in Android Studio project)
                sourceBuildSetup.Append("\n\n    sourceSets {");
                // Note: 'test' config is picked on purpose here
                sourceBuildSetup.Append("\n        test {");
                var foldersToInclude = new[]
                {
                    "Configuration",
                    "Modules",
                    "PlatformDependent/AndroidPlayer",
                    "Platforms/Android",
                    "Runtime"
                };
                sourceBuildSetup.Append("\n            jni.srcDirs = [");
                for (int i = 0; i < foldersToInclude.Length; i++)
                {
                    sourceBuildSetup.Append("'../../../../../" + foldersToInclude[i] + "'");
                    if (i < foldersToInclude.Length - 1)
                        sourceBuildSetup.Append(", ");
                }
                sourceBuildSetup.Append("]");
                sourceBuildSetup.Append("\n        }");
                sourceBuildSetup.Append("\n    }");
                // externalNativeBuild has to be present, otherwise jni.srcDirs is ignored
                sourceBuildSetup.Append("\n\n    externalNativeBuild {");
                sourceBuildSetup.Append("\n        cmake {");
                sourceBuildSetup.Append("\n            path 'CMakeLists.txt'");
                sourceBuildSetup.Append("\n        }");
                sourceBuildSetup.Append("\n    }");
                Backend.Current.AddWriteTextAction(m_BuildContext.LibraryDirectory.Combine("CMakeLists.txt"), "");
            }

            sourceBuildSetup.Append("\n}");
            // Add configurations for linking classes.jar
            var tag = "Implementation";
            sourceBuildSetup.Append("\n\nconfigurations {");
            sourceBuildSetup.Append(GetSourceBuildConfigurations(tag));
            sourceBuildSetup.Append("\n}");
            sourceBuildSetup.Append("\n\ndependencies {");
            sourceBuildSetup.Append(GetSourceBuildDependencies(androidPlayerPath, tag));
            sourceBuildSetup.Append("\n}");

            launcherBuildSetup.Append("\n}");

            m_TemplateValues["SOURCE_BUILD_SETUP"] = sourceBuildSetup.ToString();
            m_TemplateValues["LAUNCHER_SOURCE_BUILD_SETUP"] = launcherBuildSetup.ToString();

            WriteAndroidStudioFiles();
        }

        string SetupBuildVariants(AndroidArchitecture targetArchitectures)
        {
            var output = new StringBuilder();
            output.Append("\n    buildTypes {");
            foreach (var config in GetConfigsForSourceBuild())
            {
                output.AppendFormat("\n        create('{0}') {{", config);

                string minifyEnabled;
                string useProguard;
                if (config.StartsWith("Release_"))
                {
                    minifyEnabled = m_TemplateValues["MINIFY_RELEASE"];
                    useProguard = m_TemplateValues["PROGUARD_RELEASE"];
                }
                else
                {
                    minifyEnabled = m_TemplateValues["MINIFY_DEBUG"];
                    useProguard = m_TemplateValues["PROGUARD_DEBUG"];
                }

                output.Append("\n            minifyEnabled " + minifyEnabled);
                output.Append("\n            useProguard " + useProguard);
                output.Append("\n            proguardFiles getDefaultProguardFile('proguard-android.txt'), 'proguard-unity.txt'" + m_TemplateValues["USER_PROGUARD"]);
                output.Append("\n            jniDebuggable = true");
                output.Append("\n            debuggable = true");
                output.Append("\n            signingConfig signingConfigs.debug");
                output.Append("\n        }");
            }
            output.Append("\n    }");

            // debug & release configurations don't have folders for .so libraries set, thus they won't compile
            // we could have point them to some default configs like mono/arm7, but it's much clearer to have named configs
            output.Append("\n\n    variantFilter { variant ->");
            output.Append("\n        def buildType = variant.buildType.name");
            output.Append("\n        if (buildType.equals('release') || buildType.equals('debug')) {");
            output.Append("\n            variant.setIgnore(true)");
            output.Append("\n        }");
            // Also Mono is not supported on ARM64
            output.Append("\n        else {");
            output.Append("\n            def flavor = variant.getFlavors().get(0).name");
            output.Append("\n            def scriptingBackend = buildType.tokenize('_').get(1)");
            output.Append("\n            if (flavor.equals('arm8') && scriptingBackend.equals('Mono')) {");
            output.Append("\n                variant.setIgnore(true)");
            output.Append("\n            }");
            output.Append("\n        }");
            output.Append("\n    }");

            // Add flavors for ARMv7, ARM64 and x86
            output.Append("\n\n    flavorDimensions 'abi'");
            output.Append("\n\n    productFlavors {");
            foreach (var d in m_BuildContext.DeviceTypes)
            {
                output.Append("\n        " + d.Value.GradleProductFlavor + " {");
                output.Append("\n            dimension 'abi'");
                output.AppendFormat("\n            ndk.abiFilter('{0}')", d.Value.ABI);
                output.Append("\n        }");
            }
            output.Append("\n    }");
            return output.ToString();
        }

        string GenerateSourceSets(AndroidArchitecture targetArchitectures, string scriptingImplementation, bool referenceStrippedLibraries, string androidPlayerPath, string projectPath)
        {
            var output = new StringBuilder();
            output.Append("\n\n    sourceSets {");
            foreach (var config in SimpleConfigs)
            {
                foreach (var deviceType in m_BuildContext.DeviceTypes)
                {
                    if (deviceType.Value.TargetArchitecture == AndroidArchitecture.ARM64 && scriptingImplementation == "Mono")
                        continue;
                    output.AppendFormat("\n        {0}{1}_{2} {{",
                        deviceType.Value.GradleProductFlavor, config, scriptingImplementation);
                    if (referenceStrippedLibraries)
                    {
                        output.AppendFormat("\n            jniLibs.srcDirs = ['" + androidPlayerPath + "Variations/{0}/{1}/Libs']",
                            scriptingImplementation.ToLowerInvariant(), config);
                    }
                    else
                    {
                        var subPath = Path.Combine("jniLibsUnstripped", scriptingImplementation.ToLowerInvariant(), config, deviceType.Value.ABI);
                        Directory.CreateDirectory(Path.Combine(projectPath, subPath));
                        output.AppendFormat("\n            jniLibs.srcDirs = [\"{0}/{1}\"]",
                            unityLibraryProjectDir,
                            Path.GetDirectoryName(subPath).Replace("\\", "/"));
                    }

                    output.Append("\n        }");
                }
            }
            output.Append("\n    }");
            return output.ToString();
        }

        string AddSourceBuildJamBuilding(AndroidArchitecture targetArchitectures, string scriptingImplementation, bool referenceStrippedLibraries)
        {
            var sourceBuildSetup = new StringBuilder();
            var dependencies = new StringBuilder();

            foreach (var config in SimpleConfigs)
            {
                foreach (var deviceType in m_BuildContext.DeviceTypes)
                {
                    if (deviceType.Value.TargetArchitecture == AndroidArchitecture.ARM64 && scriptingImplementation == "Mono")
                        continue;
                    var prefix = string.Format("{0}{1}_{2}", deviceType.Value.GradleProductFlavor, config, scriptingImplementation);
                    prefix = char.ToUpper(prefix[0]) + prefix.Substring(1);
                    var taskName = string.Format("Build{0}", prefix);
                    sourceBuildSetup.Append("\n\n    task " + taskName + " {");
                    sourceBuildSetup.Append("\n        doLast {");

                    var configName = config.ToLower() == "debug" ? "debug" : "release";
                    var devPlayer = config.ToLower() == "release" ? "0" : "1";
                    sourceBuildSetup.Append("\n            exec {");
                    var targetName = "AndroidPlayer";

                    switch (deviceType.Value.TargetArchitecture)
                    {
                        case AndroidArchitecture.ARM64: targetName += "64"; break;
                            // Nothing to add if AndroidArchitecture.ARMv7, because it's a default target
                    }

                    if (!scriptingImplementation.Equals("mono", StringComparison.InvariantCultureIgnoreCase))
                        targetName += scriptingImplementation.ToUpper();

                    if (string.Compare(config.ToLower(), "release", true) == 0)
                        targetName += "NoDevelopment";

                    sourceBuildSetup.AppendFormat(
                        "\n                commandLine 'perl', '../../../../../jam.pl', '{0}', '-g', '-e', '-sCONFIG={1}'",
                        targetName,
                        configName);
                    sourceBuildSetup.Append("\n            }");

                    // Create symbolic links towards unstripped libraries
                    if (!referenceStrippedLibraries)
                    {
                        var targetNames = new[] { "AndroidPlayer", "Main" };
                        var targetLibraries = new[] { "libunity", "libmain" };
                        var srcPaths = new[] { "artifacts/Android/Variations", "artifacts/libmain" };

                        var abiFolder = "";
                        switch (deviceType.Value.ABI)
                        {
                            case "armeabi-v7a": abiFolder = "arm32"; break;
                            case "arm64-v8a": abiFolder = "arm64"; break;
                            default: abiFolder = "x86"; break;
                        }

                        var subFolder = string.Format("Android_{0}_{1}_{2}{3}{4}",
                            abiFolder,
                            devPlayer == "1" ? "dev" : "nondev",
                            scriptingImplementation.ToLower() == "mono" ? "m" : "i",
                            devPlayer == "1" ? "_ut" : "",
                            configName == "debug" ? "_d" : "_r");

                        for (int i = 0; i < targetNames.Length; i++)
                        {
                            var srcPath = string.Format("{0}/{1}/{2}.so",
                                srcPaths[i],
                                subFolder,
                                targetLibraries[i]);
                            var dstPath = string.Format("jniLibsUnstripped/{0}/{1}/{2}/{3}.so",
                                scriptingImplementation.ToLowerInvariant(),
                                config,
                                deviceType.Value.ABI,
                                targetLibraries[i]);
                            sourceBuildSetup.Append("\n            // " + targetLibraries[i]);

                            if (Application.platform == RuntimePlatform.WindowsEditor)
                            {
                                srcPath = "../../../../../" + srcPath;
                                // Note: Can't use ant.symlink, because it uses 'ln' (which is a unix command) under the hood
                                srcPath = srcPath.Replace("/", "\\\\");
                                dstPath = dstPath.Replace("/", "\\\\");

                                // Delete old symbolic link if any
                                sourceBuildSetup.Append("\n            exec {");
                                sourceBuildSetup.AppendFormat("\n                commandLine 'cmd', '/c', 'if exist {0} del {0}'",
                                    dstPath);
                                sourceBuildSetup.Append("\n            }");

                                // Create the actualy symbolic link towards unstripped library
                                sourceBuildSetup.Append("\n            exec {");
                                sourceBuildSetup.AppendFormat("\n                commandLine 'cmd', '/c', 'mklink /H {0} {1}'",
                                    dstPath, srcPath);
                                sourceBuildSetup.Append("\n            }");
                            }
                            else
                            {
                                // For some reason, working directory for ant.symlink is different
                                srcPath = "../../../../../../../../../" + srcPath;
                                dstPath = string.Format("{0}/{1}", unityLibraryProjectDir, dstPath);
                                // Create symbolic link towards unstripped library
                                // Note: We're create symbolic link every time, this task invoked
                                //       Because the modification time of a file is copied at the time symbolic link created
                                //       In other words, if we create a symbolic link only once, Android Studio won't pick up changes from .so files even if there are any
                                //       Because it would think the file was not modified
                                //       On other hand, this command fails when Android Studio debugger is running (because it's locking the file), so before rebuilding the debugger must be stopped
                                sourceBuildSetup.AppendFormat("\n            ant.symlink(link: \"{0}\", resource: '{1}', overwrite: 'true')",
                                    dstPath, srcPath);
                            }
                        }
                    }

                    sourceBuildSetup.Append("\n        }");
                    sourceBuildSetup.Append("\n    }");
                    // Due to parallel execution we must make the launcher project depend on the unity library project
                    var targetLauncherTask = "merge" + prefix + "JniLibFolders";
                    var targetLibraryTask = "compile" + prefix + "JavaWithJavac";
                    // For some reason, on OSX tasks which are not of active configuration don't exist
                    // Thus we cannot apply dependencies, this doesn't happen on Windows though
                    // For ex., if Arm7Debug_Mono is selected, X86Debug_Mono task won't exist
                    dependencies.Append("\n        if (project.tasks.findByName('" + targetLibraryTask + "') && depProject.tasks.findByName('" + targetLauncherTask + "')) {");
                    dependencies.Append("\n            depProject." + targetLauncherTask + ".dependsOn " + taskName);
                    dependencies.Append("\n            " + targetLibraryTask + ".dependsOn " + taskName);
                    dependencies.Append("\n        }");
                }
            }


            sourceBuildSetup.Append("\n\n    afterEvaluate {");
            sourceBuildSetup.Append("\n\t\tdef depProject = project(':launcher')");
            sourceBuildSetup.Append(dependencies);
            sourceBuildSetup.Append("\n    }");

            return sourceBuildSetup.ToString();
        }

        protected string GetSourceBuildConfigurations(string configurationPostfix)
        {
            var sb = new StringBuilder();
            var scriptingImplementation = GetScriptingImplementationsForSourceBuild();
            foreach (var config in SimpleConfigs)
            {
                sb.AppendFormat("\n    {0}_{1}{2}", config, scriptingImplementation, configurationPostfix);
            }
            return sb.ToString();
        }

        protected string GetSourceBuildDependencies(string pathPrefix, string dependencyPostfix)
        {
            var sb = new StringBuilder();
            var scriptingImplementation = GetScriptingImplementationsForSourceBuild();

            foreach (var config in SimpleConfigs)
            {
                sb.AppendFormat("\n    '{0}_{1}{3}' fileTree(dir: '{4}Variations/{2}/{0}/Classes', include: ['*.jar'])",
                    config, scriptingImplementation, scriptingImplementation.ToLowerInvariant(), dependencyPostfix, pathPrefix);
            }


            return sb.ToString();
        }

        /// <summary>
        /// Hints Android Studio how to populate settings for Native debugger:
        /// - LLDB Post Attach Commands
        /// - Symbol Directories
        /// </summary>
        protected virtual void WriteAndroidStudioFiles()
        {
            var workspacePath = m_BuildContext.GradleOuputDirectory.Combine(".idea", "workspace.xml");

            var declaration = new XDeclaration("1.0", "utf-8", null);
            var document = new XDocument(declaration);
            var project = new XElement("project", new XAttribute("version", "4"));
            document.Add(project);

            var component = new XElement("component", new XAttribute("name", "RunManager"));
            project.Add(component);

            var configuration = new XElement("configuration",
                new XAttribute("name", "launcher"),
                new XAttribute("type", "AndroidRunConfigurationType"),
                new XAttribute("factoryName", "Android App"));
            component.Add(configuration);

            var module = new XElement("module", new XAttribute("name", "launcher"));
            configuration.Add(module);

            configuration.Add(new XElement("option", new XAttribute("name", "DEBUGGER_TYPE"), new XAttribute("value", "Native")));

            var native = new XElement("Native");
            configuration.Add(native);

            var rootFolder = m_ClassicContext.PlayerPackageDirectory.Combine("Variations/", GetScriptingImplementationsForSourceBuild().ToLowerInvariant());

            // Not entirely good, as we can select a different config via Android Studio menu, but will do for now
            var symbolDirectories = new[]
            {
                rootFolder.Combine(EditorUserBuildSettings.androidBuildType.ToString(), "Symbols")
            };

            foreach (var s in symbolDirectories)
            {
                native.Add(new XElement("symbol_dirs", new XAttribute("symbol_path", s.ToString())));
            }

            var postAttachCommands = new[]
            {
                "process handle SIGXCPU -n true -p true -s false",
                "process handle SIGPWR -n true -p true -s false"
            };

            foreach (var p in postAttachCommands)
            {
                native.Add(new XElement("post_attach_commands", new XAttribute("post_attach_commands_attr", p)));
            }

            Backend.Current.AddWriteTextAction(workspacePath, document.ToString());
        }
    }
} // namespace
#endif
