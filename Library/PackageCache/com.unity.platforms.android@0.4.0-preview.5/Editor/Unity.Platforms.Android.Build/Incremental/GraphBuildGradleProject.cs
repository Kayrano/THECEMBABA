#if ENABLE_EXPERIMENTAL_INCREMENTAL_PIPELINE
using Bee.Core;
using NiceIO;
using System.IO;
using Unity.Build;
using Unity.Build.Classic;
using Unity.Build.Classic.Private;
using Unity.Build.Common;
using UnityEngine;

namespace Unity.Platforms.Android.Build
{
    class GraphBuildGradleProject : BuildStepBase
    {
        //public override IEnumerable<Type> UsedComponents { get; } = new[] { typeof(GeneralSettings) };

        public override BuildResult Run(BuildContext context)
        {
            var classicData = context.GetValue<ClassicSharedData>();
            var exportSettings = context.GetComponentOrDefault<AndroidExportSettings>();
            if (exportSettings.ExportProject || context.HasComponent<InstallInBuildFolder>())
                return context.Success();

            var isAppBundle = exportSettings.TargetType == AndroidTargetType.AndroidAppBundle;
            var androidContext = context.GetValue<AndroidBuildContext>();
            var gradleTask = GradleTools.GetGradleTask(classicData.DevelopmentPlayer, isAppBundle);
            var gradleFile = androidContext.GradleOuputDirectory.Combine("build.gradle");
            var gradleLauncherJar = GradleTools.GetGradleLauncherJar(classicData.BuildToolsDirectory);
            var fileName = androidContext.JDKDirectory.Combine("bin", "java" + (Application.platform == RuntimePlatform.WindowsEditor ? ".exe" : ""));
            var outputPath = androidContext.GradleOuputDirectory.Combine(GradleTools.GetGradleOutputPath(classicData.DevelopmentPlayer, isAppBundle));


            var extraFiles = androidContext.LibrarySrcMainDirectory.FilesIfExists("*", true);
            Backend.Current.AddAction("Build Gradle",
                new[] { outputPath },
                //extraFiles,
                androidContext.GetAllGradleProjectFiles(),
                $"\"{fileName.ToString()}\"",
                new[]
                {
                    "-classpath",
                    $"\"{gradleLauncherJar}\"",
                    "org.gradle.launcher.GradleMain",
                    "-b",
                     $"\"{gradleFile}\"",
                    $"\"{gradleTask}\""
                },
                // Gradle has its own dependency tracking, and for some reason might decide not to rebuild APK.
                // This confuses Bee, as it expects file date to become newer, which never happens. Thus Bee throws an erro
                // Ensure file doesn't exist before invoking gradle
                deleteOutputsBeforeRun: true);

            var finalPath = new NPath(context.GetOutputBuildDirectory()).Combine(context.GetComponentOrDefault<GeneralSettings>().ProductName + "." + outputPath.Extension);
            CopyTool.Instance().Setup(finalPath, outputPath);

            var artifact = context.GetOrCreateValue<AndroidArtifact>();
            artifact.OutputTargetFile = new FileInfo(finalPath.ToString());

            return context.Success();
        }
        /*

        static NPath[] GetOutputAPKs(bool developmentPlayer, AndroidTargetDeviceType[] deviceTypes, bool buildApkPerCpuArchitecture)
        {
            var config = developmentPlayer ? "debug" : "release";

            if (!buildApkPerCpuArchitecture)
                return new[] { new KeyValuePair<string, string>($"{k_ProjectName}-{config}.apk", "Package.apk") };

            var apks = new KeyValuePair<string, string>[deviceTypes.Length];
            for (var i = 0; i < deviceTypes.Length; ++i)
            {
                var abi = deviceTypes[i].ABI;
                apks[i] = new KeyValuePair<string, string>($"{k_ProjectName}-{abi}-{config}.apk", $"Package.{abi}.apk");
            }
            return apks;
        }
        */
    }
}
#endif
