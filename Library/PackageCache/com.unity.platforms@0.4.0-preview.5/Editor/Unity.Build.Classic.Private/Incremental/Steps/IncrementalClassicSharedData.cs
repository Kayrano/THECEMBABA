#if ENABLE_EXPERIMENTAL_INCREMENTAL_PIPELINE
using Bee.NativeProgramSupport.Building;
using Bee.TundraBackend;
using NiceIO;
using System.Collections.Generic;
using Unity.BuildSystem.NativeProgramSupport;
using UnityEditor;

namespace Unity.Build.Classic.Private.IncrementalClassicPipeline
{
    public class ClassicBuildArchitectureData
    {
        public NPath DynamicLibraryDeployDirectory { set; get; }

        // TODO: this one is very specific, should it be here?
        public string BurstTarget { set; get; }

        public ToolChain ToolChain { get; set; }

        public NativeProgramFormat NativeProgramFormat { get; set; }
    }

    public class IncrementalClassicSharedData
    {
        public NPath TypeDBOutputDirectory { get; set; }
        public string PlatformName { set; get; }

        public TundraBackend Backend { set; get; }

        public NPath PlayerPackageDirectory { set; get; }

        public NPath BuildToolsDirectory { get => PlayerPackageDirectory.Combine("Tools"); }

        public NPath DataDeployDirectory { set; get; }

        public NPath VariationDirectory { set; get; }

        public NPath UnityEngineAssembliesDirectory { set; get; }

        public BuildTarget BuildTarget { set; get; }
        public Dictionary<Architecture, ClassicBuildArchitectureData> Architectures { set; get; }

        public NPath IL2CPPDataDirectory { get; set; }

        public NPath LibraryDeployDirectory { get; set; }

        public IncrementalClassicSharedData()
        {
            Architectures = new Dictionary<Architecture, ClassicBuildArchitectureData>();
        }
    }
}
#endif
