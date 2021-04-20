#if ENABLE_EXPERIMENTAL_INCREMENTAL_PIPELINE
using Bee.Core;
using Bee.DotNet;
using NiceIO;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.BuildSystem.CSharpSupport;
using Unity.BuildTools;
using Unity.Profiling;
using UnityEditor;

namespace Unity.Build.Classic.Private.IncrementalClassicPipeline
{
    class PostProcessor
    {
        public NPath OutputDirectory { get; }
        private DotNetAssembly _processorRunner;
        private DotNetAssembly[] _builtProcessorsDependenciesAndSelf;
        private NPath[] _inputFilesFromPostProcessor;
        private DotNetAssembly[] _builtProcessors;
        private DotNetRunnableProgram _processorRunnableProgram;

        Dictionary<DotNetAssembly, DotNetAssembly> _processedAssemblies = new Dictionary<DotNetAssembly, DotNetAssembly>();
        private readonly NPath[] _bclFiles;

        public PostProcessor(DotNetAssembly[] processors, NPath outputDirectory)
        {
            OutputDirectory = outputDirectory;

            NPath srcDir = Path.GetFullPath(Path.Combine(Package.PackagePath, "Editor/Unity.Build.Classic.Private/Incremental/ILPostProcessing~"));
            var processorRunnerProgram = new CSharpProgram()
            {
                FileName = "ILPostProcessorRunner.exe",
                Sources =
                {
                    srcDir,
                },
                References =
                {
                    EditorApplication.applicationContentsPath + "/Managed/Unity.CompilationPipeline.Common.dll"
                },
                GenerateXmlDoc = XmlDocMode.Disabled,
                ProjectFilePath = $"{srcDir}/ILProcessor.gen.csproj",
                CopyReferencesNextToTarget = true,
                Framework = { Framework.Framework471 }
            };
            _processorRunner = processorRunnerProgram.SetupDefault();
            _processorRunnableProgram = new DotNetRunnableProgram(new DotNetAssembly(_processorRunner.Path, Framework.NetStandard20));
            _builtProcessors = processors.Select(a => a.DeployTo(Configuration.RootArtifactsPath.Combine("postprocessors"))).ToArray();
            _builtProcessorsDependenciesAndSelf = _builtProcessors.SelectMany(bp => bp.RecursiveRuntimeDependenciesIncludingSelf).Distinct().ToArray();

            _inputFilesFromPostProcessor = _processorRunner.RecursiveRuntimeDependenciesIncludingSelf.Concat(_builtProcessorsDependenciesAndSelf).Select(p => p.Path).ToArray();

            var stevedoreReferenceAssemblyProvider = new StevedoreReferenceAssemblyProvider();

            //we are in a funky situation today where user code is compiled against .net standard 2,  but unityengine assemblies are compiled against .net47.  Postprocessors
            //need to be able to deep-resolve any typereference, and it's going to encounter references to both netstandard.dll as well as mscorlib.dll and system.dll. We're going
            //to allow all these typereferences to resolve by resolving against the reference assemblies for both these profiles.
            stevedoreReferenceAssemblyProvider.TryFor(Framework.Framework471, false, out var net47files, out _, out _);
            stevedoreReferenceAssemblyProvider.TryFor(Framework.NetStandard20, false, out var net20files, out _, out _);
            _bclFiles = net47files.Concat(net20files).ToArray();
        }

        public DotNetAssembly SetupPostProcessorInvocation(DotNetAssembly inputAsm)
        {
            List<DotNetAssembly> inputAssemblyAndReferences;
            using (new ProfilerMarker(nameof(inputAsm.RecursiveRuntimeDependenciesIncludingSelf)).Auto())
                inputAssemblyAndReferences = inputAsm.RecursiveRuntimeDependenciesIncludingSelf.ToList();

            var referenceAsmPaths = inputAssemblyAndReferences
                .Where(a => !a.Path.IsChildOf("post_ilprocessing"))
                .Select(a => a.Path)
                .Concat(_bclFiles)
                .Select(p => p.InQuotes(SlashMode.Native));

            var referencesArg = referenceAsmPaths.Select(r => $"-r={r}");

            var inputFiles = _inputFilesFromPostProcessor.Concat(inputAssemblyAndReferences.Select(p => p.Path)).ToArray();

            var result = new DotNetAssembly(OutputDirectory.Combine(inputAsm.Path.FileName), inputAsm.Framework, debugSymbolPath: OutputDirectory.Combine(inputAsm.DebugSymbolPath.FileName))
                    .WithRuntimeDependencies(inputAsm.RuntimeDependencies.Select(r => _processedAssemblies.TryGetValue(r, out var processed) ? processed : r).ToArray());

            var processorPathsArg = _builtProcessors.Select(p => $"-p={p.Path.InQuotes(SlashMode.Native)}");

            var args = new List<string>
            {
                "-a",
                inputAsm.Path.InQuotes(SlashMode.Native),
                $"--outputDir={OutputDirectory.InQuotes(SlashMode.Native)}",
                processorPathsArg,
                referencesArg,
                "-f=.",
            }.ToArray();

            Backend.Current.AddAction($"ILPostProcessorRunner",
                result.Paths,
                inputFiles,
                executableStringFor: _processorRunnableProgram.InvocationString,
                args,
                supportResponseFile: true
            );
            _processedAssemblies.Add(inputAsm, result);

            return result;
        }
    }
}
#endif
