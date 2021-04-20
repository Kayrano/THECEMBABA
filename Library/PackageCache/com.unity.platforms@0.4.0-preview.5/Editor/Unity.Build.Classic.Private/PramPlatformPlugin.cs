#if ENABLE_EXPERIMENTAL_INCREMENTAL_PIPELINE
using NiceIO;
using System.Collections.Generic;

namespace Unity.Build.Classic.Private
{
    public abstract class PramPlatformPlugin
    {
        public abstract string[] Providers { get; }
        public abstract NPath PlatformAssemblyLoadPath { get; }
        public abstract IReadOnlyDictionary<string, string> Environment { get; }
    }
}
#endif
