using Bee.NativeProgramSupport.Building;
using Bee.Toolchain.Android;
using DotsBuildTargets;
using Unity.BuildSystem.NativeProgramSupport;

abstract class DotsAndroidTarget : DotsBuildSystemTarget
{
    protected override NativeProgramFormat GetExecutableFormatForConfig(DotsConfiguration config, bool enableManagedDebugger)
    {
        return new AndroidApkMainModuleFormat(ToolChain as AndroidApkToolchain);
    }

    public override bool CanUseBurst { get; } = true;
}

class DotsAndroidTargetArmv7 : DotsAndroidTarget
{
    public override string Identifier => "android_armv7";

    public override ToolChain ToolChain => AndroidApkToolchain.GetToolChain(true, new ARMv7Architecture());
}

class DotsAndroidTargetArm64 : DotsAndroidTarget
{
    public override string Identifier => "android_arm64";

    public override ToolChain ToolChain => AndroidApkToolchain.GetToolChain(true, new Arm64Architecture());
}
