#if ENABLE_EXPERIMENTAL_INCREMENTAL_PIPELINE
using Bee.Core;
using NiceIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Unity.Build.Classic.Private.IncrementalClassicPipeline;
using UnityEditor;
using UnityEngine;

#if UNITY_ANDROID_SUPPORTS_SHADOWFILES
using Unity.BuildTools;
#endif

namespace Unity.Platforms.Android.Build
{
    struct ProguardContext
    {
        internal bool MinifyRelease;
        internal bool UseProguardRelease;
        internal bool MinifyDebug;
        internal bool UseProguardDebug;
    }

    struct AndroidProjectContext
    {
        internal string PackageName;
        internal int MinSDKVersion;
        internal int TargetSDKVersion;
        internal Version GoogleBuildTools;
        internal bool UseObb;
        internal ScriptingImplementation ScriptingBackend;
        internal bool SourceBuild;
        internal ProguardContext ProguardContext;
        internal NPath GradleTemplateDirectory;
        internal NPath ProGuardTemplateDirectory;
        internal bool BuildApkPerCpuArchitecture;
        internal bool InjectUnityBuildScripts;
        internal AndroidArchitecture Architectures;
    }

    partial class AndroidProjectExportGradle
    {
        //static readonly string k_ProguardUserFileName = "proguard-user.txt";
        static readonly string k_KotlinVersion = "1.3.11";
        static readonly string k_GoogleArtifactoryRepositoryURL = "https://bfartifactory.bf.unity3d.com/artifactory/maven-google";
        static readonly string k_JcenterArtifactoryRepositoryURL = "https://bfartifactory.bf.unity3d.com/artifactory/jcenter";
        private AndroidProjectContext m_ProjectContext;
        private AndroidBuildContext m_BuildContext;
        private IncrementalClassicSharedData m_ClassicContext;
        protected Dictionary<string, string> m_TemplateValues;

        protected virtual string BuildGradleFileName
        {
            get { return "build.gradle"; }
        }

        protected virtual string GradlePropertiesFileName
        {
            get { return "gradle.properties"; }
        }

        protected virtual string LocalPropertiesFileName
        {
            get { return "local.properties"; }
        }

        protected virtual string SettingsGradleFileName
        {
            get { return "settings.gradle"; }
        }

        protected static readonly string[] UnityPlayerActivities =
        {
            "UnityPlayerActivity",
        };

        protected static readonly string DefaultUnityPackage = "com.unity3d.player";

        internal AndroidProjectExportGradle(AndroidProjectContext projectContext, AndroidBuildContext buildContext, IncrementalClassicSharedData classicContext)
        {
            m_ProjectContext = projectContext;
            m_BuildContext = buildContext;
            m_ClassicContext = classicContext;

            var abiFilters = "";
            if (!m_ProjectContext.BuildApkPerCpuArchitecture)
            {
                abiFilters = string.Join(", ", m_BuildContext.DeviceTypes.Select(d => $"'{d.Value.ABI}'"));
            }
            var proguard = m_ProjectContext.ProguardContext;
            m_TemplateValues = new Dictionary<string, string>
            {
                { "BUILDTOOLS", m_ProjectContext.GoogleBuildTools.ToString() },
                { "APIVERSION", m_ProjectContext.TargetSDKVersion.ToString() },
                { "MINSDKVERSION", m_ProjectContext.MinSDKVersion.ToString() },
                { "TARGETSDKVERSION", m_ProjectContext.TargetSDKVersion.ToString() },
                { "APPLICATIONID", m_ProjectContext.PackageName },
                { "ABIFILTERS", abiFilters},
                { "VERSIONCODE", PlayerSettings.Android.bundleVersionCode.ToString() }, // TODO
                { "VERSIONNAME", PlayerSettings.bundleVersion }, // TODO
                { "MINIFY_RELEASE", proguard.MinifyRelease ? "true" : "false" },
                { "PROGUARD_RELEASE", proguard.UseProguardRelease ? "true" : "false" },
                { "MINIFY_DEBUG", proguard.MinifyDebug ? "true" : "false" },
                { "PROGUARD_DEBUG", proguard.UseProguardDebug ? "true" : "false" },
                { "USER_PROGUARD", "" },
                //{ "PATH_STAGINGAREA", m_StagingArea },
                { "PACKAGENAME", m_ProjectContext.PackageName },
                //{ "PATH_PLUGINS", m_AndroidPluginsPath },
                //{ "PATH_BUILDTOOLS", m_UnityAndroidBuildTools },
                { "APPLY_PLUGINS", "" },
                { "BUILD_SCRIPT_DEPS", "" },
                { "ARTIFACTORYREPOSITORY", GetArtifactoryRepository() },
                { "EXTERNAL_SOURCES", GetExternalSources() },
                { "DEPS", string.Empty }
            };
        }

        private string GetExternalSources()
        {
            var externalJavaSources = m_BuildContext.ExternalJavaSources;
            var externalKotlinSources = m_BuildContext.ExternalKotlinSources;

            var sourceTypes = new[] { externalJavaSources, externalKotlinSources };
            // Note: for kotlin we use tag 'java' instead of 'kotlin", the kotlin files will still get compiled
            // This is to workaround a bug.
            // If we have this this build.gradle:
            // sourceSets.main.kotlin.srcDirs += [
            //    'D:\\Projects\\personal\\Android\\Android\\KotlynTest.kt'
            // ]
            // it will throw an error
            //  * What went wrong:
            //  A problem occurred configuring root project 'MemoryViewer'.
            //  > Source directory 'D:\Projects\personal\Android\Android\KotlynTest.kt' is not a directory.
            // Interstingly enough directly referencing java files is okay.

            var tags = new[] { "java", "java" /*"kotlin"*/ };
            var externalSources = new StringBuilder();

            for (int i = 0; i < sourceTypes.Length; i++)
            {
                var sources = sourceTypes[i];
                if (sources == null || sources.Count == 0)
                    continue;

                externalSources.AppendLine("    sourceSets.main." + tags[i] + ".srcDirs += [");
                for (int s = 0; s < sources.Count; s++)
                {
                    externalSources.Append($"        '{sources[s]}'");
                    if (s != sources.Count - 1)
                        externalSources.Append(",");
                    externalSources.AppendLine();
                }
                externalSources.AppendLine("    ]");
            }
            return externalSources.ToString();
        }

        public virtual void Export()
        {
            PrepareForBuild();
            AddSigningInfo();
            PrepareUserProguardFile();
            WriteGradleProperties();
            WriteLocalPropertiesFile();

            CopyMainProjectContents();

            //AddAARDependencies(unityLibraryProjectLibsPath);
            CopyJavaSources("com.unity3d.player", UnityPlayerActivities);
            ProcessLibraries();
            ProcessSourcePlugins();
            SetupCompressionOptions();
            if (m_ProjectContext.SourceBuild)
            {
                AddSourceBuildInformation();
            }
            else
            {
                AddPackagingOptions();
            }

            if (m_ProjectContext.BuildApkPerCpuArchitecture)
                AddSplits();

            WriteGradleBuildFiles();
            WriteShadowFilesDeclaration();
            //CopyOBBFiles();
        }

        private void WriteShadowFilesDeclaration()
        {
#if UNITY_ANDROID_SUPPORTS_SHADOWFILES
            {
                var pramShadowAssetsFile = $"{m_BuildContext.GradleOuputDirectory}/pram-shadow-files";
                var shadowEnabledPaths = new []
                {
                    m_BuildContext.LibrarySrcMainAssetsDirectory,
                    m_BuildContext.LibrarySrcMainJniLibsDirectory,

                }.Select(p => p.RelativeTo(m_BuildContext.GradleOuputDirectory)).ToArray();

                Backend.Current.AddWriteTextAction(pramShadowAssetsFile, shadowEnabledPaths.Select(p => p.ToString()).SeparateWith("\n"));
                m_BuildContext.AddGradleProjectFile(pramShadowAssetsFile);
            }
#endif
        }

        private void PrepareForBuild()
        {
            // m_TemplateValues["DIR_GRADLEPROJECT"] = m_ProjectContext.GradleOuputDirectory.ToString().Replace(Path.DirectorySeparatorChar, '/');
            // m_TemplateValues["DIR_UNITYPROJECT"]  = Path.GetFullPath(".").Replace(Path.DirectorySeparatorChar, '/');
        }

        // Clean out the directory from everything except temporary files, to facilitate
        // incremental build
        /*private void CleanDirectory(string directory, string[] ignoreFilter = null)
        {
            var ignoredFiles = new List<string> {"build", ".gradle"};
            if (ignoreFilter != null)
                ignoredFiles.AddRange(ignoreFilter);
            var gradleFiles = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
            foreach (var filePath in gradleFiles)
            {
                var fileName = Path.GetFileName(filePath);
                if (!ignoredFiles.Contains(fileName))
                {
                    try
                    {
                        FileUtil.DeleteFileOrDirectory(filePath);
                    }
                    catch (IOException)
                    {
                    }
                }
            }
        }*/

        private void CopyJavaSources(string packageName, string[] unityActivities)
        {
            var sourcePackageDir = m_BuildContext.JavaSourceDirectory.Combine(DefaultUnityPackage.Replace('.', '/'));
            var targetPackageDir = m_BuildContext.LibrarySrcMainJavaDirectory.Combine(packageName.Replace('.', '/'));

            foreach (var activityClassName in unityActivities)
            {
                var targetFile = targetPackageDir.Combine(activityClassName + ".java");
                var sourceFile = sourcePackageDir.Combine(activityClassName + ".java");
                CopyTool.Instance().Setup(targetFile, sourceFile);
                m_BuildContext.AddGradleProjectFile(targetFile);
            }
        }

        private void CopyOBBFiles()
        {
            // TODO
            /*
            if (m_UseObb)
            {
                var targetObbPath = Path.Combine(m_UnityLibraryProjectPath,
                    String.Format("{0}.main.obb", m_ProductNameForFileSystem));
                // Copy OBB
                CopyFile(Path.Combine(m_StagingArea, "main.obb"), targetObbPath);
               // TrackFileAdded(targetObbPath, "APK expansion (OBB)");
            }
            // Add any streaming resources to the /assets folder
            // bug: there is currently no generic way to enforce uncompressed assets
            var targetRawDir = Path.Combine(mainProjectPath, "assets");
            CopyDir(Path.Combine(m_StagingArea, "raw"), targetRawDir);
           // TrackDirectoryAdded(targetRawDir, "Streaming resource");
           */
        }

        private void PrepareUserProguardFile()
        {
            /*
             * TODO
            var customProguardPath = Path.Combine(m_AndroidPluginsPath, k_ProguardUserFileName);
            if (File.Exists(customProguardPath))
            {
                var targetProguardPath = Path.Combine(m_UnityLibraryProjectPath, k_ProguardUserFileName);
                CopyFile(customProguardPath, targetProguardPath);
                TrackFileAdded(targetProguardPath, "Custom Proguard config");
                m_TemplateValues["USER_PROGUARD"] = ", '" + k_ProguardUserFileName + "'";
            }
            */
        }

        private void CopyMainProjectContents()
        {
            var targetProguardPath = m_BuildContext.LibraryDirectory.Combine("proguard-unity.txt");
            CopyTool.Instance().Setup(targetProguardPath, m_ProjectContext.ProGuardTemplateDirectory.Combine("UnityProGuardTemplate.txt"));
            m_BuildContext.AddGradleProjectFile(targetProguardPath);
            // WTF is this?
            /*
            try
            {
                Directory.Delete(Path.Combine(srcMainPath, "assets", "bin", "Data"), true);
            }
            catch (IOException) {}
            */
            // TODO

            //CopyDir(Path.Combine(m_StagingArea, "plugins"), libsPath);

            //CopyDir(Path.Combine(m_StagingArea, "java"), Path.Combine(srcMainPath, "java"));


            // For Source builds we directly reference unity-classes.jar file build\AndroidPlayer\Variations\<scripting>\<config>\Classes
            if (!m_ProjectContext.SourceBuild)
            {
                var targetClassesJar = m_BuildContext.LibraryLibsDirectory.Combine("unity-classes.jar");
                CopyTool.Instance().Setup(
                    targetClassesJar,
                    m_BuildContext.ClassesJarPath);
                m_BuildContext.AddGradleProjectFile(targetClassesJar);
            }
            // TODO

            // var targetJniLibsDir = Path.Combine(srcMainPath, "jniLibs");
            //CopyDir(Path.Combine(m_StagingArea, "libs"), targetJniLibsDir);
            //var targetAssetsDir = Path.Combine(srcMainPath, "assets");
            //CopyDir(Path.Combine(m_StagingArea, "assets"), targetAssetsDir);
        }

        private void ProcessLibraries()
        {
            var libTemplatePath = m_ProjectContext.GradleTemplateDirectory.Combine("libTemplate.gradle");
            var libTemplateContents = libTemplatePath.ReadAllText();
            var libDeps = new StringBuilder();
            var gradleSettings = new StringBuilder();
            /*
            foreach (string libraryPath in m_AndroidLibraries)
            {
                m_TemplateValues["LIBSDKTARGET"] = GetAndroidLibraryTargetSdk(libraryPath);
                var libName = Path.GetFileName(libraryPath);
                var targetLibDir = Path.Combine(m_UnityLibraryProjectPath, libName);
                var libGradleFilePath = Path.Combine(targetLibDir, "build.gradle");
                libDeps.AppendFormat("    implementation project('{0}')\n", libName);
                gradleSettings.AppendFormat("include 'unityLibrary:{0}'\n", libName);
                CopyDir(libraryPath, targetLibDir);
                TrackDirectoryAdded(targetLibDir, "Android library: " + libName);

                // Use existing build.gradle and hope users know what they're doing.
                if (!File.Exists(libGradleFilePath))
                {
                    var libContents = TemplateReplace(libTemplateContents, m_TemplateValues);
                    File.WriteAllText(libGradleFilePath, libContents);
                }
            }
            */
            var oldDeps = m_TemplateValues["DEPS"];
            m_TemplateValues["DEPS"] = oldDeps + libDeps;

            var settingsTemplate = m_ProjectContext.GradleTemplateDirectory.Combine("settingsTemplate.gradle").ReadAllText();

            var settingsValues = new Dictionary<string, string> { { "INCLUDES", gradleSettings.ToString() } };
            var settingsContents = TemplateReplace(settingsTemplate, settingsValues);
            settingsContents = settingsContents.Trim();
            if (settingsContents.Length != 0)
            {
                var targetSettingsGradleFile = m_BuildContext.GradleOuputDirectory.Combine(SettingsGradleFileName);
                Backend.Current.AddWriteTextAction(targetSettingsGradleFile, settingsContents);
                m_BuildContext.AddGradleProjectFile(targetSettingsGradleFile);
            }
        }

        private void ProcessSourcePlugins()
        {
            if (m_BuildContext.ExternalKotlinSources.Count == 0)
                return;
            var buildDeps = m_TemplateValues["BUILD_SCRIPT_DEPS"];
            buildDeps += string.Format("\t\tclasspath 'org.jetbrains.kotlin:kotlin-gradle-plugin:{0}'", k_KotlinVersion);
            m_TemplateValues["BUILD_SCRIPT_DEPS"] = buildDeps;
            var deps = m_TemplateValues["DEPS"];
            deps += string.Format("\timplementation 'org.jetbrains.kotlin:kotlin-stdlib-jdk7:{0}'\n", k_KotlinVersion);
            m_TemplateValues["DEPS"] = deps;
            var applyPlugins = m_TemplateValues["APPLY_PLUGINS"];
            applyPlugins += "apply plugin: 'kotlin-android'\n";
            m_TemplateValues["APPLY_PLUGINS"] = applyPlugins;
            m_TemplateValues["REPOSITORIES"] = "\nrepositories {\n\tmavenCentral()\n}";
        }

        private void WriteGradleBuildFiles()
        {
            var unityLibraryTemplateFilePath = m_ProjectContext.GradleTemplateDirectory.Combine("mainTemplate.gradle");
            var launcherTemplateFilePath = m_ProjectContext.GradleTemplateDirectory.Combine("launcherTemplate.gradle");
            var baseProjectTemplateFilePath = m_ProjectContext.GradleTemplateDirectory.Combine("baseProjectTemplate.gradle");

            // Write out our gradle files to disk
            WriteGradleTemplate(baseProjectTemplateFilePath, m_BuildContext.GradleOuputDirectory.Combine(BuildGradleFileName));
            WriteGradleTemplate(launcherTemplateFilePath, m_BuildContext.LauncherDirectory.Combine(BuildGradleFileName));
            WriteGradleTemplate(unityLibraryTemplateFilePath, m_BuildContext.LibraryDirectory.Combine(BuildGradleFileName));
        }

        private void WriteGradleTemplate(NPath templatePath, NPath targetPath)
        {
            var gradleTemplateContents = templatePath.ReadAllText();
            var gradleContents = TemplateReplace(gradleTemplateContents, m_TemplateValues);
            Backend.Current.AddWriteTextAction(targetPath, gradleContents);
            m_BuildContext.AddGradleProjectFile(targetPath);
        }

        private void AddSigningInfo()
        {
            // if (!PlayerSettings.Android.useCustomKeystore)
            {
                m_TemplateValues["SIGNCONFIG"] = "\n            signingConfig signingConfigs.debug";
                return;
            }

            /* TODO
            var keystore = PlayerSettings.Android.GetAndroidKeystoreFullPath();
            // Use forward slashes in the paths in build.gradle
            keystore = keystore.Replace('\\', '/');
            var keyData = string.Format(
                "            storeFile file('{0}')\n            storePassword '{1}'\n            keyAlias '{2}'\n            keyPassword '{3}'",
                EscapeString(keystore), EscapeString(PlayerSettings.Android.keystorePass), EscapeString(PlayerSettings.Android.keyaliasName), EscapeString(PlayerSettings.Android.keyaliasPass));
            var signerVersionConfig = "";
            if (PlayerSettings.virtualRealitySupported)
            {
                PostProcessor.Tasks.VrSupportChecker vrSupport = new PostProcessor.Tasks.VrSupportChecker();
                if (vrSupport.isOculusEnabled && !PlayerSettings.VROculus.v2Signing)
                {
                    signerVersionConfig = "\n\t\tv2SigningEnabled false"; // Oculus store modifies signed apks and won't accept the ones signed with V2.
                }
            }
            m_TemplateValues["SIGN"] = "\n\n    signingConfigs {\n        release {\n" + keyData + signerVersionConfig + "\n        }\n    }";
            m_TemplateValues["SIGNCONFIG"] = "\n            signingConfig signingConfigs.release";
            */
        }

        private string EscapeString(string pass)
        {
            // Escape any illegal gradle characters
            return pass.Replace("\\", "\\\\").Replace("'", "\\'");
        }

        private void AddPackagingOptions()
        {
            var po = new StringBuilder();

            // Prevent gradle stripping of unity libraries, that way runtime stacktrace resolving will work correctly
            po.Append("\n\n    packagingOptions {\n");
            foreach (var d in m_BuildContext.DeviceTypes)
                po.Append($"        doNotStrip '*/{d.Value.ABI}/*.so'\n");
            po.Append("    }");

            m_TemplateValues["PACKAGING_OPTIONS"] = po.ToString();
        }

        void AddSplits()
        {
            var sb = new StringBuilder();

            sb.Append("\n\n    splits {");
            sb.Append("\n        abi {");
            sb.Append("\n            enable true");
            sb.Append("\n            reset()");

            sb.Append("\n            include");
            bool abiAdded = false;
            foreach (var d in m_BuildContext.DeviceTypes)
            {
                var abi = d.Value.ABI;
                if (abiAdded)
                    sb.Append(',');
                sb.Append($" '{abi}'");
                abiAdded = true;
            }

            sb.Append("\n            universalApk false");
            sb.Append("\n        }");
            sb.Append("\n    }");

            m_TemplateValues["SPLITS"] = sb.ToString();

            AddSplitsVersionCode();
        }

        void AddSplitsVersionCode()
        {
            const int versionCodeIncrement = 100000;
            if (PlayerSettings.Android.bundleVersionCode >= versionCodeIncrement)
            {
                Debug.LogWarning($"Android bundle version code should be less than {versionCodeIncrement}. Split APKs might have invalid version codes.");
            }

            var sb = new StringBuilder();

            sb.Append("\n\next.abiCodes = [");
            bool abiAdded = false;
            foreach (var d in m_BuildContext.DeviceTypes)
            {
                var deviceType = d.Value;
                if (abiAdded)
                    sb.Append(", ");
                sb.Append($"'{deviceType.ABI}': {deviceType.VersionCodeBase}");
                abiAdded = true;
            }
            sb.Append(']');

            sb.Append("\n\nimport com.android.build.OutputFile");

            sb.Append("\n\nandroid.applicationVariants.all { variant ->");
            sb.Append("\n    variant.outputs.each { output ->");
            sb.Append("\n        def baseAbiVersionCode = project.ext.abiCodes.get(output.getFilter(OutputFile.ABI))");
            sb.Append("\n        if (baseAbiVersionCode != null) {");
            sb.Append($"\n            output.versionCodeOverride = baseAbiVersionCode * {versionCodeIncrement} + variant.versionCode");
            sb.Append("\n        }");
            sb.Append("\n    }");
            sb.Append("\n}");

            m_TemplateValues["SPLITS_VERSION_CODE"] = sb.ToString();
        }

        private void AddAARDependencies(string libsProjectPath)
        {
            /*TODO
            var searchDir = Path.Combine(m_StagingArea, "aar");
            var aarFiles = AndroidFileLocator.Find(Path.Combine(searchDir, "*.aar"));
            var deps = "";
            foreach (var aarFilePath in aarFiles)
            {
                var targetAARFilePath = Path.Combine(libsProjectPath, Path.GetFileName(aarFilePath));
                CopyFile(aarFilePath, targetAARFilePath);
                var aarBaseName = Path.GetFileNameWithoutExtension(aarFilePath);
                //TrackFileAdded(targetAARFilePath, "AAR library " + aarBaseName);
                deps = deps + string.Format("    implementation(name: '{0}', ext:'aar')\n", aarBaseName);
            }
            m_TemplateValues["DEPS"] = deps;
            */
        }

        void WriteGradleProperties()
        {
            var gradleProperties = m_BuildContext.GradleOuputDirectory.Combine(GradlePropertiesFileName);
            var sb = new StringBuilder();
            sb.AppendLine(string.Format("org.gradle.jvmargs=-Xmx{0}M", 4096));// TODO: AndroidJavaTools.PreferredHeapSizeForJVM));
            sb.AppendLine("org.gradle.parallel=true");
            Backend.Current.AddWriteTextAction(gradleProperties, sb.ToString());
            m_BuildContext.AddGradleProjectFile(gradleProperties);
        }

        private void WriteLocalPropertiesFile()
        {
            var localProperties = m_BuildContext.GradleOuputDirectory.Combine(LocalPropertiesFileName);

            // escape \ and : symbols with \ in local.properties
            var sdkRoot = m_BuildContext.SDKDirectory.ToString().Replace("\\", "\\\\").Replace(":", "\\:");
            var sb = new StringBuilder();
            sb.AppendLine($"sdk.dir={sdkRoot}");

            // NDK is required by il2cpp to compile auto generated C++ files
            // NDK is required by source build, because we use externalNativeBuild task inside build.gradle to force Android Studio to show Unity native files
            if (m_ProjectContext.ScriptingBackend == ScriptingImplementation.IL2CPP || m_ProjectContext.SourceBuild)
            {
                var ndkRoot = m_BuildContext.NDKDirectory.ToString().Replace("\\", "\\\\").Replace(":", "\\:");
                sb.AppendLine($"ndk.dir={ndkRoot}");
            }
            Backend.Current.AddWriteTextAction(localProperties, sb.ToString());
            m_BuildContext.AddGradleProjectFile(localProperties);

        }

        private void SetupCompressionOptions()
        {
            /*
            string[] uncompressedFileExtensions = new string[] { ".unity3d", ".ress", ".resource", ".obb" };
            var basePath = Path.Combine(m_StagingArea, "raw");
            if (Directory.Exists(basePath))
            {
                var allResources = BuildBridge.GetAllFilesRecursive(basePath);
                var resourcesString = new StringBuilder();
                foreach (var resource in allResources)
                {
                    var extension = Path.GetExtension(resource).ToLower();
                    if (uncompressedFileExtensions.Contains(extension))
                        continue; // Don't add files that we've already added via extensions
                    // Call ToLower for path, since gradle doesn't find paths with capital letters
                    resourcesString.AppendFormat(", '{0}'", resource.Substring(basePath.Length + 1).Replace("\\", "/").ToLower());
                }
                m_TemplateValues["STREAMING_ASSETS"] = resourcesString.ToString();
            }
            */
        }

        private static string GetArtifactoryRepository()
        {
            if (Environment.GetEnvironmentVariable("UNITY_THISISABUILDMACHINE") == "1")
            {
                StringBuilder sb = new StringBuilder();
                sb.Append($"\n        maven {{\n            url '{k_GoogleArtifactoryRepositoryURL}'\n        }}");
                sb.Append($"\n        maven {{\n            url '{k_JcenterArtifactoryRepositoryURL}'\n        }}");
                return sb.ToString();
            }
            return string.Empty;
        }

        private static string GetAndroidLibraryTargetSdk(string libraryPath)
        {
            // Gradle adds unwanted permissions if targetVersion is < 4. Set it to current minimum if nothing specified
            int targetSdk = 16;
            var propFile = Path.Combine(libraryPath, "project.properties");
            if (File.Exists(propFile))
            {
                // Read target version from old project.properties
                var contents = File.ReadAllText(propFile);
                var regex = new Regex(@"^\s*target\s*=\s*android-(\d+)\s*$", RegexOptions.Multiline);
                var targets = regex.Matches(contents);
                if (targets.Count > 0)
                {
                    string target = targets[0].Groups[1].Value;
                    Int32.TryParse(target, out targetSdk);
                }
            }
            return targetSdk.ToString();
        }

        // Replace all instances of **VARIABLE** with values from dictionary
        // **VARIABLE** must be capitalized and can contain an underscore
        protected static string TemplateReplace(string template, Dictionary<string, string> values)
        {
            if (template.IndexOf("\r\n") != -1)
                throw new Exception("Template contains windows line endings:\n" + template);


            var output = new StringBuilder(template.Length);
            var matches = Regex.Matches(template, "\\*\\*([A-Z_]+)\\*\\*");
            int lastIndex = 0;
            foreach (Match match in matches)
            {
                output.Append(template, lastIndex, match.Index - lastIndex);
                var variableName = match.Groups[1].Value;
                if (values.ContainsKey(variableName))
                    output.Append(values[variableName]);
                lastIndex = match.Index + match.Length;
            }
            output.Append(template, lastIndex, template.Length - lastIndex);
            return output.ToString();
        }
    }
}
#endif
