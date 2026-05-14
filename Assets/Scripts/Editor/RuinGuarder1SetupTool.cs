using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class RuinGuarder1SetupTool
{
    private const string PrefabPath = "Assets/Models/Enemies/RuinGuarder1/RuinGuarder1.prefab";

    [MenuItem("Tools/Abyssfall/Setup RuinGuarder1 (Full)")]
    public static void SetupAll()
    {
        SetupPrefab();
        SetupNavMesh();
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[RuinGuarder1Setup] Done.");
    }

    private static void SetupPrefab()
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer  = LayerMask.NameToLayer("Enemy");

        if (playerLayer < 0 || enemyLayer < 0)
        {
            Debug.LogError("[RuinGuarder1Setup] Player or Enemy layer not found. Check Project Settings > Tags and Layers.");
            return;
        }

        using var scope = new PrefabUtility.EditPrefabContentsScope(PrefabPath);
        GameObject root = scope.prefabContentsRoot;

        // Root + all children -> Enemy layer
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = enemyLayer;

        // CapsuleCollider center Y = 1
        var col = root.GetComponent<CapsuleCollider>();
        if (col != null)
            col.center = new Vector3(0f, 1f, 0f);
            col.radius = 0.25f;
            col.height = 1.8f;

        // NavMeshAgent defaults
        var agent = root.GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            agent.speed           = 3.5f;
            agent.angularSpeed    = 360f;
            agent.stoppingDistance = 1.5f;
            agent.baseOffset      = 0f;
        }

        // RuinGuarder1AI: assign Animator + Player layer mask
        var ai       = root.GetComponent<RuinGuarder1AI>();
        var animator = root.GetComponentInChildren<Animator>(true);

        if (ai != null)
        {
            var so = new SerializedObject(ai);

            if (animator != null)
                so.FindProperty("animator").objectReferenceValue = animator;

            so.FindProperty("playerLayer").intValue = 1 << playerLayer;
            so.ApplyModifiedProperties();
        }

        Debug.Log("[RuinGuarder1Setup] Prefab configured.");
    }

    private static void SetupNavMesh()
    {
        // Mark all MeshRenderers in open scenes as Navigation Static
        int marked = 0;
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

        // Bake NavMesh
#pragma warning disable CS0618
        UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
#pragma warning restore CS0618

        Debug.Log($"[RuinGuarder1Setup] NavMesh baked. Marked {marked} objects as Navigation Static.");
    }
}

