using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class RuinGuarderFullSetup
{
    private const string FbxPath        = "Assets/Models/Enemies/RuinGuarder.fbx";
    private const string MatPath        = "Assets/Models/Enemies/tripo_material_ca54182f-46a4-43b2-9b21-131967386d23.mat";
    private const string ControllerPath = "Assets/Animations/RuinGuarder/RuinGuarder.controller";
    private const string PrefabPath     = "Assets/Models/Enemies/RuinGuarder_Prefab.prefab";

    [MenuItem("Tools/Abyssfall/Setup RuinGuarder — Full Reset")]
    public static void Run()
    {
        // Reimport FBX — forces avatar auto-generation
        AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();

        // Check avatar
        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
            .OfType<Avatar>()
            .FirstOrDefault();

        if (avatar == null)
            Debug.LogError("[RuinGuarder] FBX has no Avatar asset. Open: Models/Enemies/RuinGuarder.fbx → Inspector → Rig → Animation Type: Humanoid → Apply.");
        else if (!avatar.isValid)
            Debug.LogError("[RuinGuarder] Avatar invalid — bone mapping failed. Open FBX Inspector → Rig → Configure Avatar → fix bone assignments manually.");
        else if (!avatar.isHuman)
            Debug.LogWarning("[RuinGuarder] Avatar is not Humanoid — animations may not play correctly.");
        else
            Debug.Log("[RuinGuarder] Avatar OK: isHuman=true, isValid=true");

        BuildPrefab(avatar);
        PlaceInScene();
        BakeNavMesh();

        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[RuinGuarder] Setup complete. Check Console for avatar status.");
    }

    private static void BuildPrefab(Avatar avatar)
    {
        var fbxRoot = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (fbxRoot == null) { Debug.LogError("[RuinGuarder] FBX not found"); return; }

        var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (ctrl == null) { Debug.LogError("[RuinGuarder] Controller not found: " + ControllerPath); return; }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);

        GameObject go = Object.Instantiate(fbxRoot);
        go.name = "RuinGuarder";

        // Warstwy
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer < 0) enemyLayer = 0;
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = enemyLayer;

        // Materiał
        if (mat != null)
        {
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                smr.sharedMaterials = Enumerable.Repeat(mat, smr.sharedMaterials.Length).ToArray();
            foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
                mr.sharedMaterials = Enumerable.Repeat(mat, mr.sharedMaterials.Length).ToArray();
        }

        // Animator
        var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion            = false;
        if (avatar != null) anim.avatar = avatar;

        // CapsuleCollider
        var col = go.GetComponent<CapsuleCollider>() ?? go.AddComponent<CapsuleCollider>();
        col.center = new Vector3(0f, 0.9f, 0f);
        col.radius = 0.3f;
        col.height = 1.8f;
        col.direction = 1;

        // NavMeshAgent
        var agent = go.GetComponent<NavMeshAgent>() ?? go.AddComponent<NavMeshAgent>();
        agent.speed            = 3.5f;
        agent.angularSpeed     = 360f;
        agent.stoppingDistance = 1.5f;
        agent.baseOffset       = 0f;
        agent.radius           = 0.3f;
        agent.height           = 1.8f;

        // EnemyStats
        var stats = go.GetComponent<EnemyStats>() ?? go.AddComponent<EnemyStats>();
        stats.maxHP       = 200f;
        stats.defence     = 10f;
        stats.meleeDamage = 20f;

        // RuinGuarder1AI
        var ai = go.GetComponent<RuinGuarder1AI>() ?? go.AddComponent<RuinGuarder1AI>();
        var so = new SerializedObject(ai);
        so.FindProperty("animator").objectReferenceValue = anim;
        int playerLayerIdx = LayerMask.NameToLayer("Player");
        if (playerLayerIdx >= 0)
            so.FindProperty("playerLayer").intValue = 1 << playerLayerIdx;
        so.ApplyModifiedProperties();

        // Zapis prefabu
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            AssetDatabase.DeleteAsset(PrefabPath);

        PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        Object.DestroyImmediate(go);
        Debug.Log("[RuinGuarder] Prefab saved: " + PrefabPath);
    }

    private static void PlaceInScene()
    {
        foreach (var old in Object.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (old.name.StartsWith("RuinGuarder"))
                Object.DestroyImmediate(old);
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) { Debug.LogError("[RuinGuarder] Prefab missing."); return; }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.position = new Vector3(3f, 0f, 3f);
        instance.transform.rotation = Quaternion.identity;

        var parent = GameObject.Find("Enemies");
        if (parent != null) instance.transform.SetParent(parent.transform);

        EditorSceneManager.MarkSceneDirty(instance.scene);
        Debug.Log("[RuinGuarder] Placed in scene at (3,0,3).");
    }

    private static void BakeNavMesh()
    {
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
            if ((flags & StaticEditorFlags.NavigationStatic) == 0)
                GameObjectUtility.SetStaticEditorFlags(r.gameObject,
                    flags | StaticEditorFlags.NavigationStatic);
        }
#pragma warning disable CS0618
        UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
#pragma warning restore CS0618
        Debug.Log("[RuinGuarder] NavMesh baked.");
    }
}
