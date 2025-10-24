#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
#endif
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

#if UNITY_EDITOR
[OpenXRFeature(UiName = "User Presence",
    BuildTargetGroups = new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android },
    Company = "Unity",
    Desc = "Enable user presence detection",
    DocumentationLink = "",
    OpenxrExtensionStrings = "XR_EXT_user_presence",
    Version = "1.0.0",
    FeatureId = featureId)]
#endif
public class UserPresenceFeature : OpenXRFeature
{
    public const string featureId = "com.unity.openxr.feature.userpresence";

    protected override bool OnInstanceCreate(ulong instance)
    {
        return base.OnInstanceCreate(instance);
    }
}