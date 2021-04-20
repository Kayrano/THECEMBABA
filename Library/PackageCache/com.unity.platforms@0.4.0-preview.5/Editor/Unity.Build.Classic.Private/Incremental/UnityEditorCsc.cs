using Bee.Core;
using System;
using Unity.BuildSystem.CSharpSupport;
using Unity.BuildTools;
using UnityEditor;

namespace Unity.Build.Classic.Private
{
    internal class UnityEditorCsc : Csc
    {
        public UnityEditorCsc()
        {
            CompilerProgram = new NativeRunnableProgram($"{EditorApplication.applicationContentsPath}/Tools/Roslyn/" + (HostPlatform.IsWindows ? "csc.exe" : "csc"));
        }

        protected override RunnableProgram CompilerProgram { get; }

        public override string ActionName { get; } = "UnityCsc";
        public override int PreferredUseScore => 3;
        public override bool CanBuild() => true;
        public override Func<CSharpCompiler> StaticFunctionToCreateMe { get; } = () => new UnityEditorCsc();

        //disabling /shared on linux, because we observe that for some reason a unity build leaves behind 30+ compilation server processes at non0 cpu%.
        //This behaviour seems to only happen on linux
        public override bool SupportsShared => true;
    }
}
