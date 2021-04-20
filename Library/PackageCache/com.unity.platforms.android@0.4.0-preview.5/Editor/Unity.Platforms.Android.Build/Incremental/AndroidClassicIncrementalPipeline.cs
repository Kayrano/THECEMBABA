#if ENABLE_EXPERIMENTAL_INCREMENTAL_PIPELINE
using Bee.Toolchain.Android;
using NiceIO;
using System;
using System.IO;
using Bee.Core;
using Unity.Build;
using Unity.Build.Classic;
using Unity.Build.Classic.Private;
using Unity.Build.Classic.Private.IncrementalClassicPipeline;
using Unity.Build.Common;
using Unity.BuildSystem.NativeProgramSupport;
using UnityEditor;

#if UNITY_ANDROID
using System.Linq;
#endif

namespace Unity.Platforms.Android.Build
{
    class AndroidClassicIncrementalPipeline : ClassicIncrementalPipelineBase
    {
        public override Platform Platform { get; } = new AndroidPlatform();

        protected override BuildTarget BuildTarget => BuildTarget.Android;

        public override BuildStepCollection BuildSteps { get; } = new[]
        {
            typeof(SetupCopiesFromSlimPlayerBuild),
            typeof(GraphCopyDefaultResources),
            typeof(GraphSetupCodeGenerationStep),
            typeof(GraphSetupIl2Cpp),
            //typeof(GraphSetupNativePlugins),
            typeof(GraphSetupPlayerFiles),
            typeof(GraphCopyGradleResources),
            typeof(GraphGenerateManifest),
            typeof(GraphAndroidProjectExport),
            // TODO: We need to get a list of files from SetupAdditionallyProvidedFiles for  GraphBuildGradleProject
            typeof(SetupAdditionallyProvidedFiles),
            typeof(GraphBuildGradleProject)
        };

        protected override void PrepareContext(BuildContext context)
        {
            base.PrepareContext(context);

#if UNITY_ANDROID
            var ndkPath = UnityEditor.Android.AndroidExternalToolsSettings.ndkRootPath;
#else
            var ndkPath = "";
#endif
            if (string.IsNullOrEmpty(ndkPath))
                throw new Exception("NDK path is empty. Is your active Editor platform set to Android?");

            var incrementalClassicData = context.GetValue<IncrementalClassicSharedData>();
            var classicData = context.GetValue<ClassicSharedData>();
            var exportSettings = context.GetComponentOrDefault<AndroidExportSettings>();
            var burstSettings = context.GetOrCreateValue<BurstSettings>();
            burstSettings.EnvironmentVariables["ANDROID_NDK_ROOT"] = ndkPath;

            if (Unsupported.IsSourceBuild())
            {
                // External\Android\NonRedistributable\ndk\builds\platforms doesn't contain android-16, which is used as default in Burst
                // This seems to be fixed in 2020.2
#if UNITY_2020_1
                burstSettings.EnvironmentVariables["BURST_ANDROID_MIN_API_LEVEL"] = "19";
#endif
            }

            NPath gradleProjectOutputDirectory;
            if (context.HasComponent<InstallInBuildFolder>())
            {
                gradleProjectOutputDirectory = incrementalClassicData.PlayerPackageDirectory.Combine("SourceBuild", context.BuildConfigurationName);
            }
            else
            {
                if (exportSettings.ExportProject)
                {
                    gradleProjectOutputDirectory = context.GetOutputBuildDirectory();
                }
                else
                {
                    gradleProjectOutputDirectory = Configuration.RootArtifactsPath.Combine("gradleOut");
                }
            }

            var scriptingSettings = context.GetComponentOrDefault<ClassicScriptingSettings>();
            string scriptingTag;
            switch (scriptingSettings.ScriptingBackend)
            {
                case ScriptingImplementation.IL2CPP:
                    scriptingTag = "il2cpp";
                    break;
                //throw new NotImplementedException("Il2CPP configuration doesn't work yet, we need some changes in GraphSetupCodeGeneration. Please switch to Mono");
                case ScriptingImplementation.Mono2x:
                    scriptingTag = "mono";
                    break;
                default:
                    throw new NotImplementedException("Unknown scripting backend " + scriptingSettings.ScriptingBackend);
            }

            var gradleLibrarySrcMain = gradleProjectOutputDirectory.Combine("unityLibrary/src/main").MakeAbsolute();


            var targetArchitectures = context.GetComponentOrDefault<AndroidArchitectures>().Architectures;
            foreach (var t in (AndroidArchitecture[])Enum.GetValues(typeof(AndroidArchitecture)))
            {
                if (t == AndroidArchitecture.None || t == AndroidArchitecture.All)
                    continue;
                if ((t & targetArchitectures) == 0)
                    continue;


                // There's no ARM64 on Mono
                if (scriptingSettings.ScriptingBackend == ScriptingImplementation.Mono2x && (t & AndroidArchitecture.ARM64) != 0)
                    continue;

                switch (t)
                {
                    // TODO: pick ABI names from AndroidTargetDevice
                    case AndroidArchitecture.ARMv7:
                        incrementalClassicData.Architectures.Add(Architecture.Armv7,
                            new ClassicBuildArchitectureData()
                            {
                                DynamicLibraryDeployDirectory = $"{gradleLibrarySrcMain}/jniLibs/armeabi-v7a",
                                BurstTarget = "ARMV7A_NEON32",
                                ToolChain = new AndroidNdkToolchain(AndroidNdk.LocatorArmv7.UseSpecific(ndkPath))
                            });

                        break;
                    case AndroidArchitecture.ARM64:
                        incrementalClassicData.Architectures.Add(Architecture.Arm64,
                            new ClassicBuildArchitectureData()
                            {
                                DynamicLibraryDeployDirectory = $"{gradleLibrarySrcMain}/jniLibs/arm64-v8a",
                                BurstTarget = "ARMV8A_AARCH64",
                                ToolChain = new AndroidNdkToolchain(AndroidNdk.LocatorArm64.UseSpecific(ndkPath))
                            });
                        break;

                    default:
                        throw new NotImplementedException($"Build pipeline target not implemeneted for {t}");
                }
            }

            foreach (var arch in incrementalClassicData.Architectures)
            {
                arch.Value.NativeProgramFormat = arch.Value.ToolChain.DynamicLibraryFormat;
            }

            incrementalClassicData.DataDeployDirectory = new NPath($"{gradleLibrarySrcMain}/assets/bin/Data");
            classicData.StreamingAssetsDirectory = $"{gradleLibrarySrcMain}/assets";
            incrementalClassicData.IL2CPPDataDirectory = incrementalClassicData.DataDeployDirectory.Combine("Managed");
            incrementalClassicData.LibraryDeployDirectory = context.GetOutputBuildDirectory();

            context.SetValue(classicData);

            string variation;
            switch (context.GetComponentOrDefault<ClassicBuildProfile>().Configuration)
            {
                case BuildType.Debug:
                    variation = Unsupported.IsSourceBuild() ? "Debug" : "Development";
                    break;
                case BuildType.Develop:
                    variation = "Development";
                    break;
                case BuildType.Release:
                    variation = "Release";
                    break;
                default:
                    throw new NotImplementedException();
            }
            incrementalClassicData.VariationDirectory = incrementalClassicData.PlayerPackageDirectory.Combine("Variations", scriptingTag, variation);

            incrementalClassicData.UnityEngineAssembliesDirectory = incrementalClassicData.PlayerPackageDirectory.Combine("Variations", scriptingTag, "Managed");
            var androidBuildContext = new AndroidBuildContext(context, gradleProjectOutputDirectory);
            context.SetValue(androidBuildContext);

            var gradleArtifact = context.GetOrCreateValue<AndroidProjectArtifact>();
            gradleArtifact.ProjectDirectory = new DirectoryInfo(androidBuildContext.GradleOuputDirectory.ToString());
        }

        protected override CleanResult OnClean(CleanContext context)
        {
            var buildType = context.GetComponentOrDefault<ClassicBuildProfile>().Configuration;
            bool isDevelopment = buildType == BuildType.Debug || buildType == BuildType.Develop;
            var playbackEngineDirectory = new NPath(UnityEditor.BuildPipeline.GetPlaybackEngineDirectory(BuildTarget, isDevelopment ? BuildOptions.Development : BuildOptions.None));

            if (context.HasComponent<InstallInBuildFolder>())
            {
                var sourceBuildDirectory = playbackEngineDirectory.Combine("SourceBuild", context.BuildConfigurationName);
                if (sourceBuildDirectory.DirectoryExists())
                    sourceBuildDirectory.Delete();
            }
            return base.OnClean(context);
        }

        protected override BoolResult OnCanRun(RunContext context)
        {
#if UNITY_ANDROID
            return BoolResult.True();
#else
            return BoolResult.False("Active Editor platform has to be set to Android.");
#endif
        }


        protected override RunResult OnRun(RunContext context)
        {
#if UNITY_ANDROID

            AndroidClassicPipelineShared.SetupPlayerConnection(context);

            try
            {
                var runTargets = context.RunTargets;

                // adhoc discovery
                // TODO will be removed with pram async discovery
                if (!runTargets.Any())
                {
                    runTargets = new Pram().Discover("android");

                    // if any devices were found, only pick first
                    if (runTargets.Any())
                        runTargets = new[] { runTargets.First() };
                }

                if (!runTargets.Any())
                    throw new Exception("No android devices available");

                var projectPath = context.GetLastBuildArtifact<AndroidProjectArtifact>().ProjectDirectory.ToString();
                var applicationId = context.GetComponentOrDefault<ApplicationIdentifier>().PackageName;

                foreach (var device in runTargets)
                {
                    EditorUtility.DisplayProgressBar("Installing Application", $"Installing {applicationId} to {device.DisplayName}", 0.2f);
                    device.Deploy(applicationId, projectPath);

                    EditorUtility.DisplayProgressBar("Starting Application", $"Starting {applicationId} on {device.DisplayName}", 0.8f);
                    device.ForceStop(applicationId);
                    device.Start(applicationId);
                }
            }
            catch (Exception ex)
            {
                return context.Failure(ex);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return context.Success(new AndroidRunInstance());
#else
            return context.Failure("Active Editor platform has to be set to Android.");
#endif
        }
    }
}
#endif
