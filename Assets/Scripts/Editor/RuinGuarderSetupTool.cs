using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// Configures the RuinGuarder prefab (layers, collider, NavMeshAgent)
/// and bakes the NavMesh for the open scene.
public static class RuinGuarderSetupTool
{
    private const string PrefabPath = "Assets/Data/Enemies/RuinGuarder/RuinGuarder_Prefab.prefab";

    [MenuItem("Tools/Abyssfall/Enemies/RuinGuarder/Setup (Prefab + NavMesh)")]
    public static void SetupAll()
    {
        SetupPrefab();
        SetupNavMesh();
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[RuinGuarder] Setup done. Assign RuinGuarderData SO in the Inspector.");
    }

    private static void SetupPrefab()
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer  = LayerMask.NameToLayer("Enemy");

        if (playerLayer < 0 || enemyLayer < 0)
        {
            Debug.LogError("[RuinGuarder] Player or Enemy layer not found — add them in Project Settings > Tags and Layers.");
            return;
        }

        using var scope = new PrefabUtility.EditPrefabContentsScope(PrefabPath);
        GameObject root = scope.prefabContentsRoot;

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = enemyLayer;

        var col = root.GetComponent<CapsuleCollider>();
        if (col != null)
        {
            col.center = new Vector3(0f, 1f, 0f);
            col.radius = 0.25f;
            col.height = 1.8f;
        }

        var agent = root.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.speed            = 3.5f;
            agent.angularSpeed     = 360f;
            agent.stoppingDistance = 1.5f;
            agent.baseOffset       = 0f;
        }

        var ai       = root.GetComponent<RuinGuarderAI>();
        var animator = root.GetComponentInChildren<Animator>(true);

        if (ai != null && animator != null)
        {
            var so = new SerializedObject(ai);
            so.FindProperty("animator").objectReferenceValue = animator;
            so.ApplyModifiedProperties();
        }

        Debug.Log("[RuinGuarder] Prefab configured. Assign RuinGuarderData SO manually.");
    }

    private static void SetupNavMesh()
    {
        int marked = 0;
#pragma warning disable CS0618
        foreach (MeshRenderer r in Object.FindObjectsByType<MeshRenderer>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var flags = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
            if ((flags & StaticEditorFlags.NavigationStatic) == 0)
            {
                GameObjectUtility.SetStaticEditorFlags(r.gameObject,
                    flags | StaticEditorFlags.NavigationStatic);
                EditorUtility.SetDirty(r.gameObject);
                marked++;
            }
        }
        UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
#pragma warning restore CS0618

        Debug.Log($"[RuinGuarder] NavMesh baked. Marked {marked} objects as Navigation Static.");
    }
}