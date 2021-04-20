#if ENABLE_EXPERIMENTAL_INCREMENTAL_PIPELINE
using Unity.Build.Classic.Private.IncrementalClassicPipeline;
#endif
using Unity.BuildSystem.NativeProgramSupport;

namespace Unity.Build.Classic.Private
{
    class BuildPipelineSelector : BuildPipelineSelectorBase
    {
        public override BuildPipelineBase SelectFor(Platform platform, bool incremental)
        {
            if (platform == null)
            {
                return null;
            }
#if ENABLE_EXPERIMENTAL_INCREMENTAL_PIPELINE
            if (incremental)
            {
                return TypeCacheHelper.ConstructTypeDerivedFrom<ClassicIncrementalPipelineBase>(p => p.Platform.GetType() == platform.GetType());
            }
            else
#endif
            {
                return TypeCacheHelper.ConstructTypeDerivedFrom<ClassicNonIncrementalPipelineBase>(p => p.Platform.GetType() == platform.GetType());
            }
        }
    }
}
