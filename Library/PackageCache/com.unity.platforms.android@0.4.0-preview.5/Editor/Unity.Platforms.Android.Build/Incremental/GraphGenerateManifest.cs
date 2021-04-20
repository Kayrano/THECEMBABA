#if ENABLE_EXPERIMENTAL_INCREMENTAL_PIPELINE
using Bee.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Build;
using Unity.Build.Classic.Private;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.Platforms.Android.Build
{
    class GraphGenerateManifest : BuildStepBase
    {
        public override Type[] UsedComponents { get; } =
        {
            typeof(ApplicationIdentifier),
            typeof(AndroidAPILevels),
            typeof(AndroidAspectRatio)
        };

        AndroidManifest m_LauncherManifest;
        AndroidManifest m_LibraryManifest;

        BuildContext m_Context;

        public override BuildResult Run(BuildContext context)
        {
            m_Context = context;
            var classicData = context.GetValue<ClassicSharedData>();
            var androidContext = context.GetValue<AndroidBuildContext>();
            m_LauncherManifest = new AndroidManifest(androidContext.LauncherManifestPath);
            m_LibraryManifest = new AndroidManifest(androidContext.LibraryManifestPath);

            PatchLauncherManifest(m_LauncherManifest, context.GetComponentOrDefault<ApplicationIdentifier>().PackageName);

            var apiLevels = context.GetComponentOrDefault<AndroidAPILevels>();
            PatchLibraryManifest(m_LibraryManifest, (int)apiLevels.ResolvedTargetAPILevel, classicData.DevelopmentPlayer);

            string mainActivity = m_LibraryManifest.GetActivityWithLaunchIntent();
            androidContext.ActivityWithLaunchIntent = mainActivity;

            if (mainActivity.Length == 0)
            {
                Debug.LogWarning("No activity found in the manifest with action MAIN and category LAUNCHER.\n" +
                    "Your application may not start correctly.");
            }

            // In old pipeline, the build id always changes when you click build, we cannot do the same here, since build would constantly be rebuilding
            // Use BuildConfiguration GUID instead
            m_LibraryManifest.SetBuildId(new Guid(context.BuildConfigurationAssetGUID).ToString());

            var targetLauncherManifest = androidContext.LauncherSrcMainDirectory.Combine("AndroidManifest.xml");
            var targetLibraryManifest = androidContext.LibrarySrcMainDirectory.Combine("AndroidManifest.xml");
            Backend.Current.AddWriteTextAction(targetLauncherManifest, m_LauncherManifest.GetContents());
            Backend.Current.AddWriteTextAction(targetLibraryManifest, m_LibraryManifest.GetContents());

            androidContext.AddGradleProjectFile(targetLauncherManifest);
            androidContext.AddGradleProjectFile(targetLibraryManifest);
            return context.Success();
        }

        /* TODO
        private void AddVRRelatedLibraryManifestEntries(AndroidManifest manifest, HashSet<string> activities)
        {
            VrSupportChecker vrSupport = new VrSupportChecker();

            if (vrSupport.isCardboardEnabled || vrSupport.isDaydreamEnabled)
            {
                manifest.OverrideTheme("@style/VrActivityTheme");
                manifest.AddApplicationMetaDataAttribute("unityplayer.SkipPermissionsDialog", "true");
            }

            if (vrSupport.isDaydreamEnabled)
            {
                SupportedHeadTracking minHeadTracking = PlayerSettings.VRDaydream.minimumSupportedHeadTracking;
                SupportedHeadTracking maxHeadTracking = PlayerSettings.VRDaydream.maximumSupportedHeadTracking;

                if (maxHeadTracking < minHeadTracking)
                {
                    CancelPostProcess.AbortBuild("VR Manifest Generation Error",
                        "Maximum Head Tracking must be greater than or equal to Minimum Head Tracking!");
                    return;
                }

                bool needsHeadTrackingEntry = minHeadTracking != SupportedHeadTracking.ThreeDoF ||
                    maxHeadTracking != SupportedHeadTracking.ThreeDoF;
                bool requiresFullHeadTracking = minHeadTracking != SupportedHeadTracking.ThreeDoF &&
                    maxHeadTracking != SupportedHeadTracking.ThreeDoF;

                if (needsHeadTrackingEntry)
                {
                    const int HEAD_TRACKING_FEATURE_VERSION = 1;
                    manifest.AddAndroidVrHeadTrackingFeature(HEAD_TRACKING_FEATURE_VERSION,
                        requiresFullHeadTracking);
                }

                manifest.AddUsesFeature("android.software.vr.mode", !vrSupport.isCardboardEnabled);
                manifest.AddUsesFeature("android.hardware.vr.high_performance", vrSupport.isDaydreamOnly);
                foreach (string activity in activities)
                {
                    if (vrSupport.isDaydreamPrimary)
                    {
                        manifest.EnableVrMode(activity);
                    }

                    manifest.SetResizableActivity(activity, false);
                }

                bool result = true;
                result &= manifest.AddIntentFilterCategory("com.google.intent.category.DAYDREAM");
                result &= manifest.AddResourceToLaunchActivity("com.google.android.vr.icon", "@drawable/vr_icon_front");
                result &= manifest.AddResourceToLaunchActivity("com.google.android.vr.icon_background", "@drawable/vr_icon_back");
                if (!result)
                    throw new Exception("Failed to add DAYDREAM category to manifest");
            }

            if (vrSupport.isCardboardEnabled)
            {
                // Cardboard has no VrCore and as such has to store scanned QR codes and
                // viewer properties on device and need this permission to do it.
                manifest.AddUsesPermission("android.permission.READ_EXTERNAL_STORAGE");

                if (!manifest.AddIntentFilterCategory("com.google.intent.category.CARDBOARD"))
                    throw new Exception("Failed to add CARDBOARD category to manifest");
            }
        }
        */

        private void PatchLauncherManifest(AndroidManifest manifest, string packageName)
        {
            manifest.packageName = packageName;
            var loc = PreferredInstallLocationAsString();
            manifest.SetInstallLocation(loc);
            /*
             * TODO
            if (PlayerSettings.Android.androidTVCompatibility)
            {
                if (PlayerSettings.Android.androidBannerEnabled)
                {
                    manifest.SetApplicationBanner("@drawable/app_banner");
                }
            }
            */

            /* TODO
            // Add round icons if they are set and current SDK supports them
            AndroidPlatformIconProvider iconProvider = (AndroidPlatformIconProvider)PlayerSettings.GetPlatformIconProvider(BuildTargetGroup.Android);
            bool roundIconsSupported = iconProvider.targetSDKSupportsRoundIcons;
            bool roundIconAvailable = PlayerSettings.GetNonEmptyPlatformIconCount(PlayerSettings.GetPlatformIcons(BuildTargetGroup.Android, UnityEditor.Android.AndroidPlatformIconKind.Round)) != 0;

            if (roundIconAvailable)
            {
                if (roundIconsSupported)
                {
                    manifest.AddRoundIconAttribute("@mipmap/app_icon_round");
                }
                else
                {
                    var targetSDK = AndroidPlatformIconProvider.GetCurrentSetAndroidSDKVersion();
                    Debug.LogWarning(string.Format("Round icons are set but will not be included in the APK because they are not supported by the currently set Target API Level ({0}).\n Round icons require API Level 25.", targetSDK));
                }
            }
            */

        }

        private void PatchLibraryManifest(AndroidManifest manifest, int targetSdkVersion, bool developmentPlayer)
        {
            manifest.packageName = "com.unity3d.player";

            UpdateGraphicsAPIEntries(manifest);

            // patch unity activities
            HashSet<string> activities = manifest.GetActivitiesByMetadata("unityplayer.UnityActivity", "true");
            activities.Add("com.unity3d.player.UnityPlayerActivity");
            string maxAspectRatioValue = GetMaxAspectRatio(targetSdkVersion);
            bool specifyMaxAspectRatio = !String.IsNullOrEmpty(maxAspectRatioValue);
            // before API level 26 max aspect ratio was specified with a meta-data element, since API level 26 activity attribute is used instead.
            bool maxAspectRatioAsActivityAttribute = targetSdkVersion >= 26;
            // Android docs say "density" value was added in API level 17, but it doesn't compile with target SDK level lower than 24.
            string configChanges = (targetSdkVersion > 23) ? AndroidManifest.AndroidConfigChanges + "|density" : AndroidManifest.AndroidConfigChanges;
            string orientationAttribute = GetOrientationAttr();
            bool activityPatched = false;
            foreach (string activity in activities)
            {
                activityPatched = manifest.SetOrientation(activity, orientationAttribute) || activityPatched;
                activityPatched = manifest.SetLaunchMode(activity, "singleTask") || activityPatched;
                if (maxAspectRatioAsActivityAttribute && specifyMaxAspectRatio)
                    activityPatched = manifest.SetMaxAspectRatio(activity, maxAspectRatioValue) || activityPatched;
                activityPatched = manifest.SetConfigChanges(activity, configChanges) || activityPatched;

                // Enabling hardware acceleration of native UI reduces performance of draw calls on certain
                // devices (e.g. >70% slow down on ShieldTV). If this setting is enabled Android runtime
                // will create a separate render thread which does some GL init and waits for render commands.
                // Because the more than one thread is using GL the driver will use fallback code path which
                // is slower.
                activityPatched = manifest.SetAndroidNativeUIHWAcceleration(activity, false) || activityPatched;
            }
            if (!activityPatched)
                Debug.LogWarning(string.Format("Unable to find unity activity in manifest. You need to make sure orientation attribute is set to {0} manually.", orientationAttribute));

            /*
             * TODO
            if (PlayerSettings.Android.androidTVCompatibility)
            {
                manifest.SetApplicationFlag("isGame", PlayerSettings.Android.androidIsGame);
                if (!manifest.HasLeanbackLauncherActivity() && !manifest.AddLeanbackLauncherActivity())
                {
                    Debug.LogWarning("No activity with LEANBACK_LAUNCHER or LAUNCHER categories found.\n" +
                        "The build may not be compatible with Android TV. Specify an activity with LEANBACK_LAUNCHER or LAUNCHER category in the manifest, " +
                        "or disable Android TV compatibility in Player Settings.");
                }
            }
            */

            /*TODO
         if (EditorUserBuildSettings.androidBuildSubtarget != MobileTextureSubtarget.Generic)
             CreateSupportsTextureElem(manifest, EditorUserBuildSettings.androidBuildSubtarget);
             */
            /* TODO
        switch (PlayerSettings.Android.androidGamepadSupportLevel)
        {
            case AndroidGamepadSupportLevel.SupportsDPad:
                // No manifest changes needed
                break;
            case AndroidGamepadSupportLevel.SupportsGamepad:
                manifest.AddUsesFeature("android.hardware.gamepad", false);
                break;
            case AndroidGamepadSupportLevel.RequiresGamepad:
                manifest.AddUsesFeature("android.hardware.gamepad", true);
                break;
            default:
                break;
        }

        if (PlayerSettings.preserveFramebufferAlpha)
        {
            manifest.OverrideTheme("@style/UnityThemeSelector.Translucent");
        }
        if (PlayerSettings.virtualRealitySupported)
        {
            AddVRRelatedLibraryManifestEntries(manifest, activities);
        }
        if (PlayerSettings.Android.ARCoreEnabled)
        {
            manifest.AddUsesPermission("android.permission.CAMERA");
            manifest.AddApplicationMetaDataAttribute("unity.tango-enable", true.ToString());
            manifest.AddApplicationMetaDataAttribute("unityplayer.SkipPermissionsDialog", "true");
        }
        */
            // TODO
            manifest.AddApplicationMetaDataAttribute("unity.splash-mode", ((Int32)PlayerSettings.Android.splashScreenScale).ToString());
#pragma warning disable 0618
            manifest.AddApplicationMetaDataAttribute("unity.splash-enable", (!PlayerSettings.virtualRealitySupported).ToString());
#pragma warning restore 0618
            if (!maxAspectRatioAsActivityAttribute && specifyMaxAspectRatio)
                manifest.AddApplicationMetaDataAttribute("android.max_aspect", maxAspectRatioValue);

            // Patch old icon path to prevent collisions with the launcher manifest
            string iconAttrValue = manifest.GetIconAttributeValue();
            if (iconAttrValue == "@drawable/app_icon")
            {
                manifest.AddIconAttribute("@mipmap/app_icon");
            }

            // notch support on Android <9
            manifest.SetNotchSupport(PlayerSettings.Android.renderOutsideSafeArea);
            // TODO: fix me properly
            manifest.AddUsesPermission("android.permission.INTERNET");

            // TODO: this seems to be needed on Android 10 or higher, otherwise BuildOptions.ConnectToHost doesn't work
            manifest.AddUsesPermission("android.permission.ACCESS_NETWORK_STATE");
            //var checker = context.Get<AndroidBuildContext>().ReferenceChecker;
            // SetPermissionAttributes(context, manifest, checker, developmentPlayer);

        }

        private static void UpdateGraphicsAPIEntries(AndroidManifest manifest)
        {
            // TODO
            var devices = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            var glesVersion = GetRequiredGLESVersion(devices);
            manifest.AddGLESVersion(glesVersion);

            // Add "require AEP" flag if needed (only if min ES version is already set to 3.1)
            if (glesVersion == "0x00030001" && PlayerSettings.openGLRequireES31AEP)
                manifest.AddUsesFeature("android.hardware.opengles.aep", true);

            if (devices.Contains(GraphicsDeviceType.Vulkan))
            {
                // Require Vulkan only if it's the only selected API
                manifest.AddUsesFeature("android.hardware.vulkan", devices.Length == 1);
            }
        }

        private static string GetRequiredGLESVersion(GraphicsDeviceType[] devices)
        {
            var glesVersion = "0x00020000";

            if (devices.Contains(GraphicsDeviceType.OpenGLES3))
                glesVersion = "0x00030000";
            if (devices.Contains(GraphicsDeviceType.OpenGLES2))
                glesVersion = "0x00020000";

            // Check ES3.1/AEP flags
            if (glesVersion == "0x00030000") // only up requirement to 3.1+ when min is 3.0
            {
                if (PlayerSettings.openGLRequireES32)
                    glesVersion = "0x00030002";
                else if (PlayerSettings.openGLRequireES31 || PlayerSettings.openGLRequireES31AEP)
                    glesVersion = "0x00030001";
            }
            return glesVersion;
        }

        private void CreateSupportsTextureElem(AndroidManifest manifest, MobileTextureSubtarget subTarget)
        {
            switch (subTarget)
            {
                case MobileTextureSubtarget.Generic:
                case MobileTextureSubtarget.ETC:
                    manifest.AddSupportsGLTexture("GL_OES_compressed_ETC1_RGB8_texture");
                    break;
                case MobileTextureSubtarget.DXT:
                    manifest.AddSupportsGLTexture("GL_EXT_texture_compression_dxt1");
                    manifest.AddSupportsGLTexture("GL_EXT_texture_compression_dxt5");
                    manifest.AddSupportsGLTexture("GL_EXT_texture_compression_s3tc");
                    break;
                case MobileTextureSubtarget.PVRTC:
                    manifest.AddSupportsGLTexture("GL_IMG_texture_compression_pvrtc");
                    break;
                case MobileTextureSubtarget.ASTC:
                    manifest.AddSupportsGLTexture("GL_KHR_texture_compression_astc_ldr");
                    break;
                case MobileTextureSubtarget.ETC2:
                    // Nothing to do here, Khronos does not specify an extension for ETC2, GLES3.0 mandates support.
                    break;

                default:
                    Debug.LogWarning("SubTarget not recognized : " + subTarget);
                    break;
            }
        }

        private string PreferredInstallLocationAsString()
        {
            switch (PlayerSettings.Android.preferredInstallLocation)
            {
                case AndroidPreferredInstallLocation.Auto: return "auto";
                case AndroidPreferredInstallLocation.PreferExternal: return "preferExternal";
                case AndroidPreferredInstallLocation.ForceInternal: return "internalOnly";
            }
            return "preferExternal";
        }

        private string GetMaxAspectRatio(int targetSdkVersion)
        {
            var aspectRatio = m_Context.GetComponentOrDefault<AndroidAspectRatio>();

            if (aspectRatio.AspectRatioMode == AspectRatioMode.Custom)
                return aspectRatio.CustomAspectRatio.ToString();

            if (targetSdkVersion < 26)
            {
                // in API level 25 and lower the default maximum aspect ratio is 1.86
                // return 2.1 if super wide screen aspect ratio should be supported
                if (aspectRatio.AspectRatioMode == AspectRatioMode.SuperWideScreen)
                    return "2.1";
            }
            else
            {
                // since API level 26 the default maximum aspect ratio is the native aspect ratio of the device
                // return 1.86 if only regular wide screen aspect ratio should be supported
                if (aspectRatio.AspectRatioMode == AspectRatioMode.LegacyWideScreen)
                    return "1.86";
            }

            // don't specify max aspect ratio, use default value for the API level
            return "";
        }

        private string GetOrientationAttr()
        {
            /*TODO
            var orientation = PlayerSettings.defaultInterfaceOrientation;
            var autoPortrait = PlayerSettings.allowedAutorotateToPortrait || PlayerSettings.allowedAutorotateToPortraitUpsideDown;
            var autoLandscape = PlayerSettings.allowedAutorotateToLandscapeLeft || PlayerSettings.allowedAutorotateToLandscapeRight;
            */
            var orientation = UIOrientation.AutoRotation;
            var autoPortrait = true;
            var autoLandscape = true;
            string orientationAttr = null;

            if (orientation == UIOrientation.Portrait)
                orientationAttr = "portrait";
            else if (orientation == UIOrientation.PortraitUpsideDown)
                orientationAttr = "reversePortrait";
            else if (orientation == UIOrientation.LandscapeRight)
                orientationAttr = "reverseLandscape";
            else if (orientation == UIOrientation.LandscapeLeft)
                orientationAttr = "landscape";
            else if (autoPortrait && autoLandscape)
                orientationAttr = "fullSensor";
            else if (autoPortrait)
                orientationAttr = "sensorPortrait";
            else if (autoLandscape)
                orientationAttr = "sensorLandscape";
            else
                orientationAttr = "unspecified";

            return orientationAttr;
        }
        /*
        private void SetPermissionAttributes(AndroidBuildContext context, AndroidManifest manifest, BuildBridge.BuildAssemblyReferenceChecker checker, bool developmentPlayer)
        {
            // Add internet permission if it's necessary
            if (developmentPlayer || PlayerSettings.Android.forceInternetPermission || DoesReferenceNetworkClasses(checker)
            // TODO
            //#if ENABLE_CLOUD_SERVICES_CRASH_REPORTING
            //                || CrashReporting.CrashReportingSettings.enabled
            //#endif
            )
                manifest.AddUsesPermission("android.permission.INTERNET");

            // Used when the user wants to receive broadcast packets
            // By default android ignores them unless a multicast lock is acquired
            if (checker.HasReferenceToMethod("UnityEngine.Networking.NetworkTransport::SetMulticastLock")
                || checker.HasReferenceToType("UnityEngine.Networking.NetworkDiscovery"))
            {
                manifest.AddUsesPermission("android.permission.CHANGE_WIFI_MULTICAST_STATE");
            }

            if (checker.HasReferenceToMethod("UnityEngine.Handheld::Vibrate"))
                manifest.AddUsesPermission("android.permission.VIBRATE");

            if (checker.HasReferenceToMethod("UnityEngine.Application::get_internetReachability"))
                manifest.AddUsesPermission("android.permission.ACCESS_NETWORK_STATE");

            if (checker.HasReferenceToMethod("UnityEngine.Input::get_location"))
            {
                //TODO
                //if (PlayerSettings.Android.useLowAccuracyLocation)
                //   manifest.AddUsesPermission("android.permission.ACCESS_COARSE_LOCATION");
                //else
                manifest.AddUsesPermission("android.permission.ACCESS_FINE_LOCATION");
                manifest.AddUsesFeature("android.hardware.location.gps", false /); //encourage gps, but don't require it
                                                                                   // This is an implied feature, make it not required to support Android TV
                manifest.AddUsesFeature("android.hardware.location", false);
            }

            if (checker.HasReferenceToType("UnityEngine.WebCamTexture"))
            {
                manifest.AddUsesPermission("android.permission.CAMERA");
                // By default we don't require any camera since a WebCamTexture may not be a crucial part of the app.
                // We need to explicitly say so, since CAMERA otherwise implicitly marks camera and autofocus as required.
                manifest.AddUsesFeature("android.hardware.camera", false);
                manifest.AddUsesFeature("android.hardware.camera.autofocus", false);
                manifest.AddUsesFeature("android.hardware.camera.front", false);
            }

            // Do not strictly require this feature because it breaks compatibility with Android TV
            if (checker.HasReferenceToType("UnityEngine.Microphone"))
            {
                manifest.AddUsesPermission("android.permission.RECORD_AUDIO");
                manifest.AddUsesPermission("android.permission.MODIFY_AUDIO_SETTINGS");
                manifest.AddUsesPermission("android.permission.BLUETOOTH"); // required to connect to paired bluetooth devices
                manifest.AddUsesFeature("android.hardware.microphone", false);
            }

            if (context.Get<AndroidBuildContext>().UsingObbFiles)
            {
                manifest.AddUsesPermission("android.permission.READ_EXTERNAL_STORAGE");
            }


            if (PlayerSettings.Android.forceSDCardPermission)
            {
                manifest.AddUsesPermission("android.permission.WRITE_EXTERNAL_STORAGE");
            }


            // Do not strictly require this feature because it breaks compatibility with Android TV
            if (checker.HasReferenceToMethod("UnityEngine.Input::get_acceleration")
                || checker.HasReferenceToMethod("UnityEngine.Input::GetAccelerationEvent")
                || checker.HasReferenceToMethod("UnityEngine.Input::get_accelerationEvents")
                || checker.HasReferenceToMethod("UnityEngine.Input::get_accelerationEventCount"))
                manifest.AddUsesFeature("android.hardware.sensor.accelerometer", false);

            // Add touch screen as non-required feature regardless of references
            // Needed for Android TV
            // Needs a proper fix - check all the input channels and detect whether touch input is the only one used,
            // in this case strictly require touch screen
            manifest.AddUsesFeature("android.hardware.touchscreen", false);

            if (checker.HasReferenceToMethod("UnityEngine.Input::get_touches")
                || checker.HasReferenceToMethod("UnityEngine.Input::GetTouch")
                || checker.HasReferenceToMethod("UnityEngine.Input::get_touchCount")
                || checker.HasReferenceToMethod("UnityEngine.Input::get_multiTouchEnabled")
                || checker.HasReferenceToMethod("UnityEngine.Input::set_multiTouchEnabled"))
            {
                manifest.AddUsesFeature("android.hardware.touchscreen.multitouch", false);
                manifest.AddUsesFeature("android.hardware.touchscreen.multitouch.distinct", false);
            }
        }
        */

        /*
        private bool DoesReferenceNetworkClasses(BuildBridge.BuildAssemblyReferenceChecker checker)
        {
            return checker.HasReferenceToType("UnityEngine.Networking") // UNET
                || checker.HasReferenceToType("System.Net.Sockets")
                || checker.HasReferenceToType("UnityEngine.Network")
                || checker.HasReferenceToType("UnityEngine.RPC")
                || checker.HasReferenceToType("UnityEngine.WWW")
                || checker.HasReferenceToType("UnityEngine.Ping")
                || checker.HasReferenceToType(typeof(UnityWebRequest).FullName)
                || UnityEditor.Analytics.AnalyticsSettings.enabled
                || UnityEditor.Analytics.PerformanceReportingSettings.enabled
                || UnityEditor.CrashReporting.CrashReportingSettings.enabled;
        }
        */

    }
}
#endif
