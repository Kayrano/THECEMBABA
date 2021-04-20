#if ENABLE_EXPERIMENTAL_INCREMENTAL_PIPELINE
using Bee.Core;
using NiceIO;
using System;
using Unity.Build;
using Unity.Build.Classic;
using Unity.Build.Classic.Private.IncrementalClassicPipeline;
using UnityEditor;

namespace Unity.Platforms.Android.Build
{
    class GraphSetupPlayerFiles : BuildStepBase
    {
        public override Type[] UsedComponents { get; } = { typeof(ClassicScriptingSettings) };

        public override BuildResult Run(BuildContext context)
        {
            var incrementalClassicData = context.GetValue<IncrementalClassicSharedData>();
            var androidContext = context.GetValue<AndroidBuildContext>();
            var libsDirectory = incrementalClassicData.VariationDirectory.Combine("Libs");
            var monoLibsDirectory = incrementalClassicData.VariationDirectory.Combine("MonoLibs");
            var sourceBuild = context.HasComponent<InstallInBuildFolder>();
            var isMono = context.GetComponentOrDefault<ClassicScriptingSettings>().ScriptingBackend == ScriptingImplementation.Mono2x;
            foreach (var a in incrementalClassicData.Architectures)
            {
                var abi = androidContext.DeviceTypes[a.Key].ABI;
                if (!sourceBuild)
                    PrepareNativeUnityLibs(androidContext, libsDirectory.Combine(abi), a.Value.DynamicLibraryDeployDirectory);

                if (isMono)
                {
                    CopyMonoLibraries(androidContext, monoLibsDirectory.Combine(abi), a.Value.DynamicLibraryDeployDirectory);
                }
            }
            return context.Success();
        }

        private void PrepareNativeUnityLibs(AndroidBuildContext androidBuildContext, NPath srcLibsDirectory, NPath dstLibsDirectory)
        {
            var stripEngineCode = false; //TODO: scriptingConfig.StripEngineCode;
            foreach (var file in srcLibsDirectory.Files(false))
            {
                if (stripEngineCode && file.FileName.Equals("libunity.so", StringComparison.InvariantCultureIgnoreCase))
                    continue;
                var target = dstLibsDirectory.Combine(file.FileName);
                CopyTool.Instance().Setup(target, file);

                androidBuildContext.AddGradleProjectFile(target);
            }
        }

        private void CopyMonoLibraries(AndroidBuildContext androidBuildContext, NPath srcLibsDirectory, NPath dstLibsDirectory)
        {
            var targetMonoLibName = dstLibsDirectory.Combine(AndroidBuildContext.MonoLibName);
            var targetMonoPosixHelperName = dstLibsDirectory.Combine(AndroidBuildContext.MonoPosixHelperName);
            CopyTool.Instance().Setup(
                targetMonoLibName,
                srcLibsDirectory.Combine(AndroidBuildContext.MonoLibName));
            CopyTool.Instance().Setup(
                dstLibsDirectory.Combine(AndroidBuildContext.MonoPosixHelperName),
                srcLibsDirectory.Combine(AndroidBuildContext.MonoPosixHelperName));

            androidBuildContext.AddGradleProjectFile(targetMonoLibName);
            androidBuildContext.AddGradleProjectFile(targetMonoPosixHelperName);
        }
    }
}
#endif
