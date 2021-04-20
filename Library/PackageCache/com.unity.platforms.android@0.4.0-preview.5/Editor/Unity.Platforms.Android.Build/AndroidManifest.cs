using NiceIO;
using System;
using System.Collections.Generic;
using System.Xml;

namespace Unity.Platforms.Android.Build
{
    internal class AndroidManifest : AndroidXmlDocument
    {
        public static readonly string AndroidConfigChanges = string.Join("|", new[]
        {
            "mcc",
            "mnc",
            "locale",
            "touchscreen",
            "keyboard",
            "keyboardHidden",
            "navigation",
            "orientation",
            "screenLayout",
            "uiMode",
            "screenSize",
            "smallestScreenSize",
            "fontScale",
            "layoutDirection",
            // "density",   // this is added dynamically if target SDK level is higher than 23.
        });

        private readonly XmlElement ApplicationElement;

        public AndroidManifest(NPath path) : base(path)
        {
            ApplicationElement = SelectSingleNode("/manifest/application") as XmlElement;
        }

        public string packageName
        {
            get { return DocumentElement.GetAttribute("package"); }
            set { DocumentElement.SetAttribute("package", value); }
        }

        public int versionCode
        {
            get { return int.Parse(DocumentElement.GetAttribute("versionCode", AndroidXmlNamespace)); }
            set { DocumentElement.SetAttribute("versionCode", AndroidXmlNamespace, value.ToString()); }
        }

        public void SetInstallLocation(string location)
        {
            DocumentElement.Attributes.Append(CreateAndroidAttribute("installLocation", location));
        }

        public void SetApplicationFlag(string name, bool value)
        {
            ApplicationElement.Attributes.Append(CreateAndroidAttribute(name, value ? "true" : "false"));
        }

        public void RemoveApplicationFlag(string name)
        {
            ApplicationElement.Attributes.RemoveNamedItem("android:" + name);
        }

        public void SetApplicationBanner(string name)
        {
            ApplicationElement.Attributes.Append(CreateAndroidAttribute("banner", name));
        }

        public void AddGLESVersion(string glEsVersion)
        {
            var element = AppendElement(DocumentElement, "uses-feature", "android:glEsVersion");
            if (element != null)
                element.Attributes.Append(CreateAndroidAttribute("glEsVersion", glEsVersion));
        }

        public bool SetOrientation(string activity, string orientation)
        {
            return SetActivityAndroidAttribute(activity, "screenOrientation", orientation);
        }

        public bool SetLaunchMode(string activity, string launchMode)
        {
            return SetActivityAndroidAttribute(activity, "launchMode", launchMode);
        }

        public bool SetMaxAspectRatio(string activity, string maxAspectRatio)
        {
            return SetActivityAndroidAttribute(activity, "maxAspectRatio", maxAspectRatio);
        }

        public bool SetConfigChanges(string activity, string configChanges)
        {
            return SetActivityAndroidAttribute(activity, "configChanges", configChanges);
        }

        // Daydream-specific attributes.
        public bool EnableVrMode(string activity)
        {
            return SetActivityAndroidAttribute(activity, "enableVrMode", "@string/gvr_vr_mode_component");
        }

        public bool SetResizableActivity(string activity, bool value)
        {
            return SetActivityAndroidAttribute(activity, "resizeableActivity", value ? "true" : "false");
        }

        public bool SetAndroidNativeUIHWAcceleration(string activity, bool value)
        {
            return SetActivityAndroidAttribute(activity, "hardwareAccelerated", value ? "true" : "false");
        }

        public bool RenameActivity(string src, string dst)
        {
            return SetActivityAndroidAttribute(src, "name", dst);
        }

        private bool SetActivityAndroidAttribute(string activity, string name, string val)
        {
            var activityElement = SelectSingleNode(String.Format("/manifest/application/activity[@android:name='{0}']", activity), nsMgr) as XmlElement;
            if (activityElement == null)
                return false;
            activityElement.Attributes.Append(CreateAndroidAttribute(name, val));
            return true;
        }

        public string GetActivityWithLaunchIntent()
        {
            XmlNode activityNode = SelectSingleNode(
                "/manifest/application/activity[intent-filter/action/@android:name='android.intent.action.MAIN' and " +
                "intent-filter/category/@android:name='android.intent.category.LAUNCHER']", nsMgr);
            return (activityNode != null) ? activityNode.Attributes["android:name"].Value : "";
        }

        public HashSet<string> GetActivitiesByMetadata(string name, string value)
        {
            HashSet<string> activities = new HashSet<string>();
            var activityNodes = SelectNodes(string.Format("//activity/meta-data[@android:name='{0}' and @android:value='{1}']/..", name, value), nsMgr);
            foreach (var activityNode in activityNodes)
            {
                activities.Add((activityNode as XmlElement).GetAttribute("android:name"));
            }
            return activities;
        }

        public bool HasLeanbackLauncherActivity()
        {
            return SelectSingleNode("/manifest/application/activity/intent-filter/category[@android:name='android.intent.category.LEANBACK_LAUNCHER']", nsMgr) != null;
        }

        public bool AddLeanbackLauncherActivity()
        {
            return AddIntentFilterCategory("android.intent.category.LEANBACK_LAUNCHER");
        }

        public bool AddIntentFilterCategory(string category)
        {
            XmlNode launchActivity = SelectSingleNode("/manifest/application/activity/intent-filter[category/@android:name='android.intent.category.LAUNCHER']", nsMgr);
            if (launchActivity == null)
            {
                return false;
            }

            XmlNode categoryElement = launchActivity.AppendChild(CreateElement("category"));
            categoryElement.Attributes.Append(CreateAndroidAttribute("name", category));
            return true;
        }

        public bool AddMetaDataToLaunchActivity(string name, string value)
        {
            XmlNode launchActivity = SelectSingleNode("/manifest/application/activity[intent-filter/category/@android:name='android.intent.category.LAUNCHER']", nsMgr);
            if (launchActivity == null)
            {
                return false;
            }

            XmlNode metaDataElement = launchActivity.AppendChild(CreateElement("meta-data"));
            metaDataElement.Attributes.Append(CreateAndroidAttribute("name", name));
            metaDataElement.Attributes.Append(CreateAndroidAttribute("value", value));
            return true;
        }

        public bool AddResourceToLaunchActivity(string name, string resource)
        {
            XmlNode launchActivity = SelectSingleNode("/manifest/application/activity[intent-filter/category/@android:name='android.intent.category.LAUNCHER']", nsMgr);
            if (launchActivity == null)
            {
                return false;
            }

            XmlNode metaDataElement = launchActivity.AppendChild(CreateElement("meta-data"));
            metaDataElement.Attributes.Append(CreateAndroidAttribute("name", name));
            metaDataElement.Attributes.Append(CreateAndroidAttribute("resource", resource));
            return true;
        }

        // http://developer.android.com/guide/topics/manifest/uses-feature-element.html
        public XmlElement AddUsesFeature(string feature, bool required)
        {
            var usesfeatElem = AppendTopAndroidNameTag("uses-feature", feature);
            // The default value for android:required if not declared is "true".
            if (usesfeatElem != null && !required)
                usesfeatElem.Attributes.Append(CreateAndroidAttribute("required", "false"));

            return usesfeatElem;
        }

        public void AddAndroidVrHeadTrackingFeature(int version, bool required)
        {
            var usesfeatElem = AddUsesFeature("android.hardware.vr.headtracking", required);

            if (usesfeatElem != null)
                usesfeatElem.Attributes.Append(CreateAndroidAttribute("version", version.ToString()));
        }

        public void AddUsesPermission(string permission)
        {
            AppendTopAndroidNameTag("uses-permission", permission);
        }

        public void AddUsesPermission(string permission, int maxSdkVersion)
        {
            AppendTopAndroidNameTag("uses-permission", permission,
                new List<XmlAttribute>() { CreateAndroidAttribute("maxSdkVersion", maxSdkVersion.ToString()) });
        }

        public void AddSupportsGLTexture(string format)
        {
            AppendTopAndroidNameTag("supports-gl-texture", format);
        }

        public void AddApplicationMetaDataAttribute(string name, string value)
        {
            AppendApplicationAndroidNameTag("meta-data", name, new List<XmlAttribute>() { CreateAndroidAttribute("value", value) });
        }

        public void AddIconAttribute(string name)
        {
            ApplicationElement.Attributes.Append(CreateAndroidAttribute("icon", name));
        }

        public void AddRoundIconAttribute(string name)
        {
            ApplicationElement.Attributes.Append(CreateAndroidAttribute("roundIcon", name));
        }

        public void OverrideTheme(string theme)
        {
            ApplicationElement.Attributes.Append(CreateAndroidAttribute("theme", theme));
        }

        public string GetIconAttributeValue()
        {
            var attr = ApplicationElement.Attributes["android:icon"];

            if (attr != null)
                return attr.Value;
            return null;
        }

        public void SetBuildId(string buildId)
        {
            var sdkNode = SelectSingleNode("/manifest/application/meta-data[@android:name='unity.build-id']", nsMgr) ??
                ApplicationElement.AppendChild(CreateElement("meta-data"));

            (sdkNode as XmlElement).Attributes.Append(CreateAndroidAttribute("name", "unity.build-id"));
            (sdkNode as XmlElement).Attributes.Append(CreateAndroidAttribute("value", buildId));
        }

        public void SetNotchSupport(bool enabled)
        {
            if (enabled)
            {
                // Huawei can be applied to activity or application
                AddMetaDataToLaunchActivity("android.notch_support", "true");
                // Xiaomi can be applied only for application
                AddApplicationMetaDataAttribute("notch.config", "portrait|landscape");
            }
        }

        private XmlAttribute CreateAndroidAttribute(string key, string value)
        {
            XmlAttribute attr = CreateAttribute("android", key, AndroidXmlNamespace);
            attr.Value = value;
            return attr;
        }

        private XmlElement AppendTopAndroidNameTag(string tag, string value)
        {
            return AppendTopAndroidNameTag(tag, value, null);
        }

        // Only applies attributes if element doesn't already exist
        private XmlElement AppendApplicationAndroidNameTag(string tag, string value, List<XmlAttribute> attributes)
        {
            return AppendAndroidNameTag(ApplicationElement, tag, value, attributes);
        }

        private XmlElement AppendTopAndroidNameTag(string tag, string value, List<XmlAttribute> attributes)
        {
            return AppendAndroidNameTag(DocumentElement, tag, value, attributes);
        }

        private XmlElement AppendAndroidNameTag(XmlElement element, string tag, string value, List<XmlAttribute> attributes)
        {
            XmlElement elem = AppendElement(element, tag, "android:name", value);
            if (elem != null)
            {
                elem.Attributes.Append(CreateAndroidAttribute("name", value));
                if (attributes != null)
                {
                    foreach (XmlAttribute attribute in attributes)
                        elem.Attributes.Append(attribute);
                }
            }
            return elem;
        }
    }
}
