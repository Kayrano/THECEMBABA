#if ENABLE_EXPERIMENTAL_INCREMENTAL_PIPELINE
using NiceIO;
using System.Collections.Generic;
using System.IO;
using Unity.Build;
using Unity.Build.Classic.Private.IncrementalClassicPipeline;
using Unity.BuildSystem.NativeProgramSupport;
using UnityEditor;

#if UNITY_ANDROID
using UnityEditor.Android;
#endif

namespace Unity.Platforms.Android.Build
{
#if !UNITY_ANDROID
    // TODO: A placeholder, should probably be moved into this package, instead of being used from UnityEditor.Android.Extensions.dll
    public abstract class AndroidTargetDeviceType
    {
        public abstract string Architecture { get; }
        public abstract string ABI { get; }
        public abstract string GradleProductFlavor { get; }
        public abstract string VisualStudioPlatform { get; }
        public abstract string NDKToolchain { get; }
        public abstract string NDKArchitecture { get; }
        public abstract string NDKBinPrefix { get; }
        public abstract AndroidArchitecture TargetArchitecture { get; }
        public abstract int APILevel { get; }
        public abstract int VersionCodeBase { get; }
    }
#endif

    class AndroidBuildContext
    {
        private NPath m_ResourcesPath;
        private NPath m_SDKDirectory;
        private NPath m_NDKDirectory;
        private NPath m_JDKDirectory;
        private NPath m_GradleOutputDirectory;
        private NPath m_ClassesJarPath;
        private NPath m_JavaSourceDirectory;
        private Dictionary<Architecture, AndroidTargetDeviceType> m_AndroidTargetDevices;
        private List<NPath> m_GradleProjectFiles;

        internal string ActivityWithLaunchIntent { set; get; }

        internal AndroidBuildContext(BuildContext context, NPath gradleProjectOutputDirectory)
        {
#if UNITY_ANDROID
            m_SDKDirectory = AndroidExternalToolsSettings.sdkRootPath;
            m_NDKDirectory = AndroidExternalToolsSettings.ndkRootPath;
            m_JDKDirectory = AndroidExternalToolsSettings.jdkRootPath;
#else
            m_SDKDirectory = "";
            m_NDKDirectory = "";
            m_JDKDirectory = "";
#endif
            var packagePath = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.platforms.android").resolvedPath;
            m_ResourcesPath = new NPath(packagePath).Combine("Editor/Unity.Platforms.Android.Build/Resources").MakeAbsolute();

            var classicContext = context.GetValue<IncrementalClassicSharedData>();
            m_GradleOutputDirectory = gradleProjectOutputDirectory;

            var packageName = context.GetComponentOrDefault<ApplicationIdentifier>().PackageName;
            SafeProductName = string.Join("_", packageName.Split(Path.GetInvalidFileNameChars()));

            m_AndroidTargetDevices = new Dictionary<Architecture, AndroidTargetDeviceType>();
#if UNITY_ANDROID
            foreach (var a in classicContext.Architectures.Keys)
            {
                if (a == Architecture.Armv7)
                    m_AndroidTargetDevices[a] = new AndroidTargetDeviceARMv7();
                if (a == Architecture.Arm64)
                    m_AndroidTargetDevices[a] = new AndroidTargetDeviceARM64();
            }
#endif

            m_ClassesJarPath = classicContext.VariationDirectory.Combine("Classes", "classes.jar");
            m_JavaSourceDirectory = classicContext.PlayerPackageDirectory.Combine("Source");

            ExternalJavaSources = new List<NPath>();
            ExternalKotlinSources = new List<NPath>();
            m_GradleProjectFiles = new List<NPath>();
        }

        internal NPath ResourcesPath
        {
            get => m_ResourcesPath;
        }

        internal NPath SDKDirectory
        {
            get => m_SDKDirectory;
        }

        internal NPath NDKDirectory
        {
            get => m_NDKDirectory;
        }

        internal NPath JDKDirectory
        {
            get => m_JDKDirectory;
        }

        internal Dictionary<Architecture, AndroidTargetDeviceType> DeviceTypes
        {
            get => m_AndroidTargetDevices;
        }

        internal NPath ClassesJarPath
        {
            get => m_ClassesJarPath;
        }

        internal NPath JavaSourceDirectory
        {
            get => m_JavaSourceDirectory;
        }

        internal NPath LauncherManifestPath
        {
            get => m_ResourcesPath.Combine(kLauncherManifestFileName);
        }

        internal NPath LibraryManifestPath
        {
            get => m_ResourcesPath.Combine(kUnityLibraryManifestFileName);
        }

        internal void AddGradleProjectFile(NPath file)
        {
            m_GradleProjectFiles.Add(file);
        }

        internal NPath[] GetAllGradleProjectFiles()
        {
            return m_GradleProjectFiles.ToArray();
        }

        internal NPath GradleOuputDirectory => m_GradleOutputDirectory;

        internal const string MonoLibName =
#if ENABLE_MONO_SGEN
                "libmonosgen-2.0.so";
#elif ENABLE_MONO_BDWGC
                "libmonobdwgc-2.0.so";
#else
                "libmonoboehm-2.0.so";
#endif

        internal const string MonoPosixHelperName = "libMonoPosixHelper.so";

        internal NPath LibraryDirectory => GradleOuputDirectory.Combine("unityLibrary");
        internal NPath LibrarySrcMainDirectory => LibraryDirectory.Combine("src", "main");
        internal NPath LibrarySrcMainJavaDirectory => LibrarySrcMainDirectory.Combine("java");
        internal NPath LibraryLibsDirectory => LibraryDirectory.Combine("libs");
        internal NPath LibrarySrcMainAssetsDirectory => LibrarySrcMainDirectory.Combine("assets");
        internal NPath LibrarySrcMainJniLibsDirectory => LibrarySrcMainDirectory.Combine("jniLibs");

        internal NPath LauncherDirectory => GradleOuputDirectory.Combine("launcher");
        internal NPath LauncherSrcMainDirectory => LauncherDirectory.Combine("src", "main");

        internal List<NPath> ExternalJavaSources { private set; get; }
        internal List<NPath> ExternalKotlinSources { private set; get; }
        internal string SafeProductName { private set; get; }

        internal const int kMinificationNone = 0;
        internal const int kMinificationProguard = 1;
        internal const int kMinificationGradle = 2;

        internal const string kLauncherManifestFileName = "LauncherManifest.xml";

        internal const string kUnityLibraryManifestFileName = "UnityManifest.xml";
    }
}
#endif
