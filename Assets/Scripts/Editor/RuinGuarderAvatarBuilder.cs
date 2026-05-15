using System.Linq;
using UnityEditor;
using UnityEngine;

/// Rebuilds the RuinGuarder humanoid Avatar from the prefab skeleton.
/// Run after reimporting or changing the rig on RuinGuarder.fbx.
public static class RuinGuarderAvatarBuilder
{
    private const string FbxPath        = "Assets/Data/Enemies/RuinGuarder/RuinGuarder.fbx";
    private const string PrefabPath     = "Assets/Data/Enemies/RuinGuarder/RuinGuarder_Prefab.prefab";
    private const string AvatarSavePath = "Assets/Data/Enemies/RuinGuarder/RuinGuarder_Avatar.asset";

    [MenuItem("Tools/Abyssfall/Enemies/RuinGuarder/Rebuild Avatar")]
    public static void RebuildAvatar()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { Debug.LogError("[RuinGuarder] Prefab not found at " + PrefabPath); return; }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

        try
        {
            foreach (Animator a in instance.GetComponentsInChildren<Animator>())
                a.enabled = false;

            HumanDescription desc = BuildDescription(instance.transform);
            Avatar avatar = AvatarBuilder.BuildHumanAvatar(instance, desc);

            if (!avatar.isValid)
            {
                Debug.LogError("[RuinGuarder] Avatar invalid — bone name mismatch. Check Console for details.");
                Object.DestroyImmediate(instance);
                return;
            }

            avatar.name = "RuinGuarder_Avatar";

            if (AssetDatabase.LoadAssetAtPath<Avatar>(AvatarSavePath) != null)
                AssetDatabase.DeleteAsset(AvatarSavePath);

            AssetDatabase.CreateAsset(avatar, AvatarSavePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            using var scope = new PrefabUtility.EditPrefabContentsScope(PrefabPath);
            GameObject root = scope.prefabContentsRoot;

            Animator anim = root.GetComponentInChildren<Animator>(true);
            if (anim != null)
            {
                anim.avatar          = AssetDatabase.LoadAssetAtPath<Avatar>(AvatarSavePath);
                anim.applyRootMotion = false;
                EditorUtility.SetDirty(anim);
            }

            RuinGuarderAI ai = root.GetComponent<RuinGuarderAI>();
            if (ai != null && anim != null)
            {
                var so = new SerializedObject(ai);
                so.FindProperty("animator").objectReferenceValue = anim;
                so.ApplyModifiedProperties();
            }

            Debug.Log("[RuinGuarder] Avatar rebuilt. IsHuman=" + avatar.isHuman + " IsValid=" + avatar.isValid);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    private static HumanDescription BuildDescription(Transform root)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);

        SkeletonBone[] skeleton = all.Select(t => new SkeletonBone
        {
            name     = t.name,
            position = t.localPosition,
            rotation = t.localRotation,
            scale    = t.localScale
        }).ToArray();

        HumanBone[] human = new HumanBone[]
        {
            Bone("Hips",          "Hips"),
            Bone("Spine",         "Spine"),
            Bone("Chest",         "Chest"),
            Bone("Spine02",       "UpperChest"),
            Bone("NeckTwist01",   "Neck"),
            Bone("Head",          "Head"),
            Bone("L_Clavicle",    "LeftShoulder"),
            Bone("LeftUpperArm",  "LeftUpperArm"),
            Bone("LeftLowerArm",  "LeftLowerArm"),
            Bone("LeftHand",      "LeftHand"),
            Bone("R_Clavicle",    "RightShoulder"),
            Bone("RightUpperArm", "RightUpperArm"),
            Bone("RightLowerArm", "RightLowerArm"),
            Bone("RightHand",     "RightHand"),
            Bone("LeftUpperLeg",  "LeftUpperLeg"),
            Bone("LeftLowerLeg",  "LeftLowerLeg"),
            Bone("LeftFoot",      "LeftFoot"),
            Bone("RightUpperLeg", "RightUpperLeg"),
            Bone("RightLowerLeg", "RightLowerLeg"),
            Bone("RightFoot",     "RightFoot"),
        };

        return new HumanDescription
        {
            human             = human,
            skeleton          = skeleton,
            upperArmTwist     = 0.5f,
            lowerArmTwist     = 0.5f,
            upperLegTwist     = 0.5f,
            lowerLegTwist     = 0.5f,
            armStretch        = 0.05f,
            legStretch        = 0.05f,
            feetSpacing       = 0f,
            hasTranslationDoF = false,
        };
    }

    private static HumanBone Bone(string boneName, string humanName) => new HumanBone
    {
        boneName  = boneName,
        humanName = humanName,
        limit     = new HumanLimit { useDefaultValues = true }
    };
}
