using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RuinGuarder1AvatarBuilder
{
    private const string PrefabPath     = "Assets/Models/Enemies/RuinGuarder1/RuinGuarder1.prefab";
    private const string AvatarSavePath = "Assets/Models/Enemies/RuinGuarder1/RuinGuarder1_Avatar.asset";

    [MenuItem("Tools/Abyssfall/Rebuild RuinGuarder1 Avatar")]
    public static void RebuildAvatar()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { Debug.LogError("[AvatarBuilder] Prefab not found at " + PrefabPath); return; }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

        try
        {
            // Disable animator so bones stay in bind/T-pose
            foreach (Animator a in instance.GetComponentsInChildren<Animator>())
                a.enabled = false;

            HumanDescription desc = BuildDescription(instance.transform);
            Avatar avatar = AvatarBuilder.BuildHumanAvatar(instance, desc);

            if (!avatar.isValid)
            {
                Debug.LogError("[AvatarBuilder] Avatar invalid — bone name mismatch. Check Console for details.");
                Object.DestroyImmediate(instance);
                return;
            }

            avatar.name = "RuinGuarder1_Avatar";

            // Replace asset
            if (AssetDatabase.LoadAssetAtPath<Avatar>(AvatarSavePath) != null)
                AssetDatabase.DeleteAsset(AvatarSavePath);

            AssetDatabase.CreateAsset(avatar, AvatarSavePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Assign rebuilt avatar + ensure animator reference is set in prefab
            using var scope = new PrefabUtility.EditPrefabContentsScope(PrefabPath);
            GameObject root = scope.prefabContentsRoot;

            Animator anim = root.GetComponentInChildren<Animator>(true);
            if (anim != null)
            {
                anim.avatar          = AssetDatabase.LoadAssetAtPath<Avatar>(AvatarSavePath);
                anim.applyRootMotion = false;
                EditorUtility.SetDirty(anim);
            }

            // Also fix AI animator reference while we're here
            RuinGuarder1AI ai = root.GetComponent<RuinGuarder1AI>();
            if (ai != null && anim != null)
            {
                var so = new SerializedObject(ai);
                so.FindProperty("animator").objectReferenceValue = anim;
                so.ApplyModifiedProperties();
            }

            Debug.Log("[AvatarBuilder] Avatar rebuilt and assigned. IsHuman=" + avatar.isHuman + " IsValid=" + avatar.isValid);
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

        // Full mapping including shoulders that were missing in the original avatar
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
            human              = human,
            skeleton           = skeleton,
            upperArmTwist      = 0.5f,
            lowerArmTwist      = 0.5f,
            upperLegTwist      = 0.5f,
            lowerLegTwist      = 0.5f,
            armStretch         = 0.05f,
            legStretch         = 0.05f,
            feetSpacing        = 0f,
            hasTranslationDoF  = false,
        };
    }

    private static HumanBone Bone(string boneName, string humanName) => new HumanBone
    {
        boneName  = boneName,
        humanName = humanName,
        limit     = new HumanLimit { useDefaultValues = true }
    };
}
