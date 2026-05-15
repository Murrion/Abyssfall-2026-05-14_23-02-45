using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class RuinGuarderFullSetup
{
    // ── Paths ─────────────────────────────────────────────────────────
    private const string FbxPath        = "Assets/Data/Enemies/RuinGuarder/RuinGuarder.fbx";
    private const string MatPath        = "Assets/Data/Enemies/RuinGuarder/tripo_material_ca54182f-46a4-43b2-9b21-131967386d23.mat";
    private const string ControllerPath = "Assets/Data/Enemies/RuinGuarder/RuinGuarder.controller";
    private const string PrefabPath     = "Assets/Data/Enemies/RuinGuarder/RuinGuarder_Prefab.prefab";
    private const string DataPath       = "Assets/Data/Enemies/RuinGuarder/RuinGuarderData.asset";
    private const string RootName       = "RuinGuarder";

    [MenuItem("Tools/Abyssfall/Enemies/RuinGuarder/Setup — Full Reset")]
    public static void Run()
    {
        // Clear Inspector selection before any destruction to prevent MissingReferenceException.
        Selection.objects = System.Array.Empty<Object>();

        // Read avatar from FBX as-is — user configures Rig settings manually and clicks Apply.
        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
            .OfType<Avatar>()
            .FirstOrDefault();

        if (avatar == null)
            Debug.LogWarning("[RuinGuarder] No Avatar found — set FBX Rig to Generic and click Apply first.");
        else
            Debug.Log($"[RuinGuarder] Avatar OK — isHuman={avatar.isHuman}, isValid={avatar.isValid}");

        var data = EnsureDataAsset();
        BuildPrefab(avatar, data);
        AssetDatabase.Refresh();   // ensure prefab is indexed before PlaceInScene reads it
        PlaceInScene();
        BakeNavMesh();

        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[RuinGuarder] Setup complete.");
    }

    // ─────────────────────────────────────────────────────────────────
    // Prefab building
    // Architecture:
    //   RuinGuarder  (empty root — all gameplay components here)
    //   └── Visual    (FBX model — Animator + mesh + skeleton)
    //
    // Adding components to a plain new GameObject avoids all FBX
    // prefab-connection restrictions that caused MissingComponentException.
    // ─────────────────────────────────────────────────────────────────
    private static void BuildPrefab(Avatar avatar, RuinGuarderData data)
    {
        var fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (fbxAsset == null)
        {
            Debug.LogError("[RuinGuarder] FBX not found at: " + FbxPath);
            return;
        }

        var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        if (ctrl == null)
            Debug.LogWarning("[RuinGuarder] Animator Controller not found: " + ControllerPath +
                             " — assign manually after setup.");

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
            Debug.LogWarning("[RuinGuarder] Material not found: " + MatPath);

        // ── 1. Empty root (all gameplay components go here) ───────────
        var root = new GameObject(RootName);

        // ── 2. FBX visual as child ────────────────────────────────────
        var visual = (GameObject)Object.Instantiate(fbxAsset);
        visual.name = "Visual";
        visual.transform.SetParent(root.transform, false);
        RemoveBlenderArtifacts(visual);

        // ── 3. Layers ─────────────────────────────────────────────────
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer < 0) enemyLayer = 0;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = enemyLayer;

        // ── 4. Animator (lives on the visual child) ───────────────────
        var anim = visual.GetComponentInChildren<Animator>(true);
        if (anim == null)
        {
            anim = visual.AddComponent<Animator>();
            Debug.Log("[RuinGuarder] Added Animator to Visual child.");
        }
        if (ctrl  != null) anim.runtimeAnimatorController = ctrl;
        if (avatar != null) anim.avatar = avatar;
        anim.applyRootMotion = false;

        // ── 5. Material ───────────────────────────────────────────────
        if (mat != null)
        {
            foreach (var smr in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                smr.sharedMaterials = Enumerable.Repeat(mat, smr.sharedMaterials.Length).ToArray();
            foreach (var mr in visual.GetComponentsInChildren<MeshRenderer>(true))
                mr.sharedMaterials = Enumerable.Repeat(mat, mr.sharedMaterials.Length).ToArray();
        }

        // ── 6. CapsuleCollider on root ────────────────────────────────
        var col = root.AddComponent<CapsuleCollider>();
        col.center    = new Vector3(0f, 0.9f, 0f);
        col.radius    = 0.3f;
        col.height    = 1.8f;
        col.direction = 1;

        // ── 7. NavMeshAgent on root ───────────────────────────────────
        var agent = root.AddComponent<NavMeshAgent>();
        agent.speed            = 3.5f;
        agent.angularSpeed     = 360f;
        agent.stoppingDistance = 1.5f;
        agent.baseOffset       = 0f;
        agent.radius           = 0.3f;
        agent.height           = 1.8f;

        // ── 8. EnemyStats on root ─────────────────────────────────────
        root.AddComponent<EnemyStats>();

        // ── 9. RuinGuarderAI on root — wire animator + data ──────────
        var ai   = root.AddComponent<RuinGuarderAI>();
        var aiSo = new SerializedObject(ai);
        aiSo.FindProperty("animator").objectReferenceValue = anim;
        aiSo.FindProperty("data").objectReferenceValue     = data;
        aiSo.ApplyModifiedProperties();

        // ── 10. Save prefab ───────────────────────────────────────────
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            AssetDatabase.DeleteAsset(PrefabPath);

        Selection.activeGameObject = null;   // deselect before destroy to avoid Inspector errors
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        Debug.Log("[RuinGuarder] Prefab saved: " + PrefabPath);
    }

    private static void PlaceInScene()
    {
        // Remove any existing RuinGuarder instances.
        Selection.activeGameObject = null;   // deselect before destroy
        foreach (var old in Object.FindObjectsByType<GameObject>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (old.name == RootName && old.transform.parent == null)
                Object.DestroyImmediate(old);
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[RuinGuarder] Prefab not found at: " + PrefabPath +
                           "\nCheck that BuildPrefab completed without errors.");
            return;
        }

        Debug.Log("[RuinGuarder] Prefab loaded OK — instantiating in scene.");
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.position = new Vector3(3f, 0f, 3f);
        instance.transform.rotation = Quaternion.identity;

        var parent = GameObject.Find("Enemies");
        if (parent != null) instance.transform.SetParent(parent.transform);

        EditorSceneManager.MarkSceneDirty(instance.scene);
        Debug.Log("[RuinGuarder] Placed in scene at (3, 0, 3).");
    }

    private static RuinGuarderData EnsureDataAsset()
    {
        var existing = AssetDatabase.LoadAssetAtPath<RuinGuarderData>(DataPath);
        if (existing != null) return existing;

        Directory.CreateDirectory(
            Path.GetDirectoryName(Path.Combine(Application.dataPath, "..", DataPath)));

        var asset = ScriptableObject.CreateInstance<RuinGuarderData>();
        AssetDatabase.CreateAsset(asset, DataPath);
        AssetDatabase.SaveAssets();
        Debug.Log("[RuinGuarder] Created RuinGuarderData at " + DataPath);
        return asset;
    }

    private static void RemoveBlenderArtifacts(GameObject root)
    {
        var toDelete = new System.Collections.Generic.List<GameObject>();

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == root.transform) continue;

            // Only remove genuine Blender scene objects — never touch meshes.
            if (t.GetComponent<Camera>() != null || t.GetComponent<Light>() != null)
                toDelete.Add(t.gameObject);
        }

        foreach (var obj in toDelete)
        {
            Debug.Log($"[RuinGuarder] Removed Blender artifact: {obj.name}");
            Object.DestroyImmediate(obj);
        }
    }

    private static void BakeNavMesh()
    {
        // Try modern NavMeshSurface (AI Navigation package) first.
        var surfaces = Object.FindObjectsByType<Unity.AI.Navigation.NavMeshSurface>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (surfaces.Length > 0)
        {
            foreach (var s in surfaces)
                s.BuildNavMesh();
            Debug.Log($"[RuinGuarder] NavMesh baked via {surfaces.Length} NavMeshSurface(s).");
        }
        else
        {
            Debug.LogWarning("[RuinGuarder] No NavMeshSurface found in scene. " +
                             "Add a NavMeshSurface component and bake manually, " +
                             "or add one so this tool can bake automatically.");
        }
    }
}
