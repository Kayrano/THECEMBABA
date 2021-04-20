#if ENABLE_EXPERIMENTAL_INCREMENTAL_PIPELINE
using Bee.DotNet;
using NiceIO;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Build.Common;
using Unity.BuildSystem.NativeProgramSupport;
using UnityEditor;

namespace Unity.Build.Classic.Private.IncrementalClassicPipeline
{
    internal class Il2CppInputAssemblies
    {
        internal List<NPath> prebuiltAssemblies { set; get; }
        internal List<(DotNetAssembly dotNetAssembly, UnityEditor.Compilation.Assembly unityAssembly)> processedAssemblies { set; get; }
    }

    public class GraphSetupIl2Cpp : BuildStepBase
    {
        public override BuildResult Run(BuildContext context)
        {
            // TODO: Move to IsEnabled
            if (!context.UsesIL2CPP())
                return context.Success();

            var classicContext = context.GetValue<IncrementalClassicSharedData>();
            var input = context.GetValue<Il2CppInputAssemblies>();
            foreach (var a in classicContext.Architectures.Values)
            {
                if (!context.TryGetComponent(out ClassicScriptingSettings scriptingSettings))
                    throw new ArgumentException("IL2CPP Compiler Configuration was not set on BuildContext");

                var toolChain = a.ToolChain ?? throw new ArgumentException("ToolChain was not set on BuildContext");
                var npc = new NativeProgramConfiguration(
                    scriptingSettings.Il2CppCompilerConfiguration == Il2CppCompilerConfiguration.Debug ?
                        CodeGen.Debug :
                        scriptingSettings.Il2CppCompilerConfiguration == Il2CppCompilerConfiguration.Release ?
                            CodeGen.Release :
                            CodeGen.Master,
                    toolChain, false);

                var format = a.NativeProgramFormat;
                var nativeProgramForIl2CppOutput = IL2CPPBeeSupport.NativeProgramForIL2CPPOutputFor(
                    npc.ToolChain.Platform is AndroidPlatform ? "libil2cpp" : "GameAssembly", classicContext, input.prebuiltAssemblies,
                    input.processedAssemblies.Select(p => p.dotNetAssembly).ToList(),
                    classicContext.IL2CPPDataDirectory, context);

                nativeProgramForIl2CppOutput.RTTI.Set(toolChain.EnablingExceptionsRequiresRTTI);

                var builtNativeProgram = nativeProgramForIl2CppOutput.SetupSpecificConfiguration(npc, format);

                builtNativeProgram.DeployTo(a.DynamicLibraryDeployDirectory);
            }

            return context.Success();
        }
    }
}
#endif
