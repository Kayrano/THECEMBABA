#if ENABLE_EXPERIMENTAL_INCREMENTAL_PIPELINE
using Bee.Core;
using Bee.Stevedore;
using Bee.Tools;
using Bee.TundraBackend;
using NiceIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Unity.Build.Common;
using Unity.BuildTools;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

namespace Unity.Build.Classic.Private.IncrementalClassicPipeline
{
    public abstract class ClassicIncrementalPipelineBase : ClassicPipelineBase
    {
        private string PlatformName => Platform.Name;

        public override Type[] UsedComponents => base.UsedComponents.Concat(new[] { typeof(OutputBuildDirectory), typeof(SceneList), typeof(ClassicScriptingSettings) }).ToArray();

        protected override CleanResult OnClean(CleanContext context)
        {
            var result = base.OnClean(context);
            if (result.Failed)
                return result;
            ResolveBeeProjectRoot(context).DeleteIfExists();
            return context.Success();
        }

        private void SetOuputBuildDirectoryAbsolute(BuildContext context)
        {
            // We need to provide absolute path, otherwise for incremental builds the output folder will depend on current directory which is not necessarily Unity's project directory
            var directory = context.GetOutputBuildDirectory();

            if (Path.IsPathRooted(directory))
            {
                context.SetComponent(new OutputBuildDirectory() { OutputDirectory = directory });
                return;
            }

            context.SetComponent(new OutputBuildDirectory() { OutputDirectory = Path.Combine(Directory.GetCurrentDirectory(), directory) });
        }

        protected override BuildResult OnBuild(BuildContext context)
        {
            SetOuputBuildDirectoryAbsolute(context);

            Configuration.AbsoluteRootArtifactsPath = ResolveBeeProjectRoot(context).Combine("artifacts").MakeAbsolute();

            var backups = new[]
            {
                new UnitySettingsState(UnitySettingsState.PlayerSettingsAsset)
            };
            PrepareContext(context);

            var scriptingSettings = context.GetComponentOrDefault<ClassicScriptingSettings>();
            var targetGroup = UnityEditor.BuildPipeline.GetBuildTargetGroup(context.GetValue<ClassicSharedData>().BuildTarget);
            // Needed by CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies) in GraphSetupCodeGenertion.cs
            // If we don't set the scripting backend, GetAssemblies might return assemblies compiled for il2cpp instead of mono, or vice versa
            PlayerSettings.SetScriptingBackend(targetGroup, scriptingSettings.ScriptingBackend);
            PlayerSettings.SetIl2CppCompilerConfiguration(targetGroup, scriptingSettings.Il2CppCompilerConfiguration);

            foreach (var b in backups)
                EditorUtility.ClearDirty(b.Target);

            try
            {
                if (!StevedoreGlobalSettings.HasInstance)
                    StevedoreGlobalSettings.Instance = new StevedoreGlobalSettings
                        {UnpackPathPrefix = "Library/IncPipeline/Stevedore"};

                context.BuildProgress?.Update($"Setting up typedb graph", "", 10);
                TundraTypeDBDag().DeleteIfExists();
                TundraTypeDBDagJson().DeleteIfExists();

                var firstTypeDBGraphGenResult = CreateTypeDBTundraGraph(0, context);
                if (firstTypeDBGraphGenResult.Failed)
                    return firstTypeDBGraphGenResult;

                context.BuildProgress?.Update($"Executing typedb graph", "", 10);

                JustTundra(incrementalPassNumber => CreateTypeDBTundraGraph(incrementalPassNumber, context), TundraTypeDBDag(), 0, context);

                context.BuildProgress?.Update($"Building datafiles", "", 10);
                using(new ProfilerMarker("DataBuild").Auto())
                    ExecuteDataBuild(context);

                context.BuildProgress?.Update($"Setting up full graph", "", 10);
                //for now, always regenerate the graph. we do not have enough knowledge yet to make a good decision on when it is, and when it is not safe to reuse the previous one.

                {
                    TundraDag().DeleteIfExists();
                    TundraDagJson().DeleteIfExists();
                    var result = CreateFullTundraGraph(0, context);
                    if (result.Failed)
                        return result;
                }

                context.BuildProgress?.Update($"Executing full graph", "", 10);
                JustTundra((passNumber) => CreateFullTundraGraph(passNumber, context), TundraDag(), 0,
                    context);
                return context.Success();
            }
            finally
            {
                foreach (var b in backups)
                    b.Restore();

                // Reset path, to avoid it leaking to other places
                Configuration.AbsoluteRootArtifactsPath = string.Empty;
            }
        }

        private void ExecuteDataBuild(BuildContext context)
        {
            IncrementalPipelinePrepareAdditionallyProvidedFiles.Prepare(context);
            var classicSharedData = context.GetValue<ClassicSharedData>();
            var buildTarget = classicSharedData.BuildTarget;
            var scenes = context.GetValue<EmbeddedScenesValue>().Scenes;
            SlimBuildPipeline.BuildSlimGameManagers(buildTarget, scenes,
                context.GetComponentOrDefault<ClassicBuildProfile>().Configuration != BuildType.Release,
                context.GetValue<IncrementalClassicSharedData>().TypeDBOutputDirectory.ToString());

            UnityEditor.BuildPipeline.WriteBootConfig(SlimBuildPipeline.TempDataPath.Combine("boot.config").ToString(),
                buildTarget, DetermineBuildOptions(context));
        }

        protected static BuildOptions DetermineBuildOptions(ContextBase context)
        {
            var buildPlayerOptions = BuildOptions.None;
            foreach (var customizer in context.GetValue<ClassicSharedData>().Customizers)
                buildPlayerOptions |= customizer.ProvideBuildOptions();
            return buildPlayerOptions;
        }

        private BuildResult CreateTypeDBTundraGraph(int incrementalPassNumber, BuildContext context)
        {
            using (new ProfilerMarker("CreateTypeDBTundraGraph").Auto())
            {
                var typeDBackend = CreateTundraBackend(context, context.BuildConfigurationName + "TypeDB");
                new GraphSetupCodeGeneration().SetupCodegeneration(context, GraphSetupCodeGeneration.Mode.EnoughToProduceTypeDB);
                typeDBackend.Write(TundraTypeDBDagJson());
                BackupTundraFile(TundraTypeDBDagJson(), incrementalPassNumber);
                return context.Success();
            }
        }

        private NPath ResolveBeeProjectRoot(ContextBase context) => Path.GetFullPath(Application.dataPath + $"/../Library/IncPipeline/{context.BuildConfigurationName}");

        protected override void PrepareContext(BuildContext context)
        {
            base.PrepareContext(context);
            var classicData = context.GetValue<ClassicSharedData>();
            var classicContext = new IncrementalClassicSharedData()
            {
                PlatformName = PlatformName,
                PlayerPackageDirectory = new NPath(classicData.PlaybackEngineDirectory).MakeAbsolute(),
                BuildTarget = BuildTarget,
                TypeDBOutputDirectory = Configuration.RootArtifactsPath.Combine("TypeDB")
            };

            context.SetValue(classicContext);
        }

        private void BackupTundraFile(NPath file, int incrementalPassNumber)
        {
            if (!file.FileExists())
                return;
            file.Copy(file.Parent.Combine($"{file.FileNameWithoutExtension}{incrementalPassNumber}.{file.Extension}"));
        }

        BuildResult CreateFullTundraGraph(int incrementalPassNumber, BuildContext context)
        {
            using (new ProfilerMarker("CreateFullTundraGraph").Auto())
            {
                BuildResult result;
                var fullBackend = CreateTundraBackend(context, context.BuildConfigurationName);
                context.GetValue<IncrementalClassicSharedData>().Backend = fullBackend;
                using (new GraphBuildingNPathHooks())
                    //@TODO: should we do something about a failure here?
                {
                    result = BuildSteps.Run(context);
                }

                using (new ProfilerMarker("backendWrite").Auto())
                    fullBackend.Write(TundraDagJson());
                BackupTundraFile(TundraDagJson(), incrementalPassNumber);

                return result;
            }
        }

        TundraBackend CreateTundraBackend(BuildContext context, string graphTitle)
        {
            var classicContext = context.GetValue<IncrementalClassicSharedData>();

            var backend = new TundraBackend(Configuration.AbsoluteRootArtifactsPath, graphTitle, false);
            Backend.Current = backend;
            StevedoreBackendSettings.Get(Backend.Current).SevenZip = new SevenZipExecutable($"{EditorApplication.applicationContentsPath}/Tools/" + (HostPlatform.IsWindows ? "7z.exe" : "7za"));

            //super hack
            var field = backend.GetType()
                .GetProperty("ContentDigestExtensions", BindingFlags.Instance | BindingFlags.NonPublic);
            var extensions = (List<string>)field.GetValue(backend);
            extensions.Clear();
            backend.AddExtensionToBeScannedByHashInsteadOfTimeStamp("cs", "c", "cpp", "m", "mm", "rsp", "exe", "dll", "pdb", "txt", "json", "bundle", "entities", "header", "bin", "");

            return backend;
        }

        void JustTundra(Func<int, BuildResult> rerunFrontend, NPath tundraDag, int incrementalPassNumber, BuildContext context)
        {
            var processProgressInfo = RunTundraIncremental(tundraDag, incrementalPassNumber, context);

            using (new ProfilerMarker($"tundra.exe pass {incrementalPassNumber}").Auto())
            {
                //@TODO: make this non-blocking
                while (processProgressInfo.MoveNext())
                {
                    if (context.BuildProgress?.Update(processProgressInfo.Current.Info,
                        processProgressInfo.Current.Progress) ?? false)
                    {
                        processProgressInfo.Current.Process.Kill();
                    }
                }
            }

            if (processProgressInfo.Current.ExitCode == TundraInvoker.ExitCode_RerunFrontend)
            {
                rerunFrontend(incrementalPassNumber + 1);
                JustTundra(rerunFrontend, tundraDag, incrementalPassNumber + 1, context);
                return;
            }

            if (processProgressInfo.Current.ExitCode != 0)
            {
                var output = processProgressInfo.Current.Output.ToString().TrimStart("##### Command").TrimStart('\n', '\r');
                int keep = 1500;
                if (output.Length > keep * 2)
                    output = output.Substring(0, keep) + "\n\n>>SNIP<<\n\n" + output.Substring(output.Length - keep);
                throw new Exception(output);
            }
        }

        IEnumerator<ShellProcessProgressInfo> RunTundraIncremental(NPath tundraDag, int incrementalPassNumber, BuildContext context)
        {
            //Paths.ProjectRoot = projectRoot;
            //Paths.ProjectRoot.EnsureDirectoryExists();

            var fullTundraPath = Path.GetFullPath(Path.Combine(Package.PackagePath, "Editor/Unity.Build.Classic.Private/bee_as_library/tundra~", HostPlatform.IsWindows ? "Win_x64/tundra2.exe" : "Mac_x64/tundra2"));

            if (!new NPath(fullTundraPath).FileExists())
                throw new Exception("Cannot find tundra2.exe: " + fullTundraPath);

            var tundraArguments = new[]
            {
                $"-R {tundraDag.QuoteForProcessStart()}",
                $"--profile={tundraDag.Parent.Combine($"profile_{tundraDag.FileNameWithoutExtension}{incrementalPassNumber}.json")}",
                $"--working-dir={NPath.CurrentDirectory.MakeAbsolute().InQuotes(SlashMode.Native)}"
            };

            tundraDag.Parent.Combine("run_" + tundraDag.FileNameWithoutExtension + (HostPlatform.IsWindows ? ".bat" : ".sh")).WriteAllText($"{fullTundraPath} {tundraArguments.SeparateWithSpace()}");

            BackupTundraFile(TundraLogJson(), incrementalPassNumber);

            var progressInfo = new ShellProcessProgressInfo() { Output = new StringBuilder() };
            var process = ShellProcess.Start(new ShellProcessArguments
            {
                Executable = fullTundraPath,
                Arguments = tundraArguments ,
                OutputDataReceived = (sender, args) =>
                {
                    if (string.IsNullOrWhiteSpace(args.Data))
                    {
                        return;
                    }

                    var match = BeeProgressRegex.Match(args.Data);
                    if (match.Success)
                    {
                        var num = match.Groups["nominator"].Value;
                        var den = match.Groups["denominator"].Value;
                        if (int.TryParse(num, out var numInt) && int.TryParse(den, out var denInt))
                        {
                            progressInfo.Progress = (float)numInt / denInt;
                        }

                        var info = match.Groups["annotation"].Value;
                        progressInfo.Info = ShortenAnnotation(info);
                    }

                    var busyMatch = BeeBusyRegex.Match(args.Data);
                    if (busyMatch.Success)
                    {
                        var info = busyMatch.Groups["annotation"].Value;
                        progressInfo.Info = ShortenAnnotation(info);
                    }

                    progressInfo.Output.AppendLine(args.Data);
                }
            });

            progressInfo.Process = process.Process;
            yield return progressInfo;

            var maxIdleTimeMs = 30 * 1000;// max 30 seconds idle
            var status = process.WaitForProcess(maxIdleTimeMs);
            while (status.MoveNext())
            {
                yield return progressInfo;
            }

            progressInfo.ExitCode = process.Process.ExitCode;
            yield return progressInfo;
        }

        private static string ShortenAnnotation(string info)
        {
            if (info.Length > 40)
            {
                var split = info.Split(' ');
                for (int i = 0; i != split.Length; i++)
                {
                    int lastSlash = split[i].LastIndexOf('/');
                    if (lastSlash != -1)
                        split[i] = split[i].Substring(lastSlash + 1);
                }

                info = string.Join(" ", split);
            }

            return info;
        }

        static readonly Regex BeeProgressRegex = new Regex(@"^\[\s*(?'nominator'\d+)\/(?'denominator'\d+)\ .*] (?'annotation'.*)$", RegexOptions.Compiled);
        static readonly Regex BeeBusyRegex = new Regex(@"^\[\s*BUSY.*\] (?'annotation'.*)$", RegexOptions.Compiled);

        NPath BeeProjectRootFor() => Configuration.AbsoluteRootArtifactsPath.Combine("..");
        NPath TundraDag() => BeeProjectRootFor().Combine("tundra.dag");
        NPath TundraDagJson() => $"{TundraDag()}.json";
        NPath TundraTypeDBDag() => BeeProjectRootFor().Combine("tundra_typedb.dag");
        NPath TundraTypeDBDagJson() => $"{TundraTypeDBDag()}.json";
        NPath TundraLogJson() => BeeProjectRootFor().Combine("tundra.log.json");
    }
}
#endif
