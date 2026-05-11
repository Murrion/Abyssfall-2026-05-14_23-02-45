using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlayerAnimatorControllerBuilder
{
    private const string AnimationsFolder = "Assets/Animations";
    private const string ControllerPath = "Assets/Animations/PlayerAuto.controller";
    private const string PlayerModelPath = "Assets/Models/Player/Player.fbx";
    private const string BaseScenePath = "Assets/Scenes/BaseScene.unity";
    private const string InitialStateName = "standing idle";
    private const string AutoSetupSessionKey = "Abyssfall.PlayerAnimatorAutoSetupDone";

    [InitializeOnLoadMethod]
    private static void AutoSetupOpenScenesAfterScriptsReload()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                SessionState.GetBool(AutoSetupSessionKey, false))
            {
                return;
            }

            SessionState.SetBool(AutoSetupSessionKey, true);
            RebuildController();
            AssignControllerToPlayersInOpenScenes();
        };
    }

    [MenuItem("Tools/Abyssfall/Setup Player Animations In Base Scene")]
    public static void SetupBaseScene()
    {
        EditorSceneManager.OpenScene(BaseScenePath);
        RebuildController();
        AssignControllerToPlayersInOpenScenes();
        EditorSceneManager.SaveOpenScenes();
    }

    [MenuItem("Tools/Abyssfall/Rebuild Player Animator Controller")]
    public static void RebuildController()
    {
        ConfigureAnimationImports();

        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
        }

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        Vector3 statePosition = new Vector3(250f, 0f, 0f);
        AnimatorState fallbackDefaultState = null;

        foreach (string animationPath in GetAnimationPaths())
        {
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(animationPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(IsUsableClip);

            if (clip == null)
            {
                continue;
            }

            string stateName = SanitizeStateName(Path.GetFileNameWithoutExtension(animationPath));
            AnimatorState state = stateMachine.AddState(stateName, statePosition);
            state.motion = clip;
            state.writeDefaultValues = true;

            fallbackDefaultState ??= state;

            if (stateName == InitialStateName)
            {
                stateMachine.defaultState = state;
            }

            statePosition.y += 60f;
        }

        if (stateMachine.defaultState == null)
        {
            stateMachine.defaultState = fallbackDefaultState;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Rebuilt player animator controller at {ControllerPath}");
    }

    [MenuItem("Tools/Abyssfall/Assign Player Animator Controller To Selection")]
    public static void AssignControllerToSelection()
    {
        AnimatorController controller = GetOrCreateController();
        Animator animator = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponentInChildren<Animator>(true)
            : null;

        if (animator == null)
        {
            Debug.LogWarning("Select the player object or the character model that has an Animator.");
            return;
        }

        AssignController(animator, controller);
        Debug.Log($"Assigned {ControllerPath} to {animator.name}.");
    }

    [MenuItem("Tools/Abyssfall/Assign Player Animator Controller To All Players")]
    public static void AssignControllerToPlayersInOpenScenes()
    {
        AnimatorController controller = GetOrCreateController();
        PlayerMovement[] players = UnityEngine.Object.FindObjectsByType<PlayerMovement>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (PlayerMovement player in players)
        {
            Animator animator = EnsurePlayerAnimator(player);

            if (animator == null)
            {
                continue;
            }

            AssignController(animator, controller);
            player.SetAnimator(animator);
            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        }

        Debug.Log($"Assigned {ControllerPath} to {players.Length} player object(s).");
    }

    private static AnimatorController GetOrCreateController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

        if (controller == null)
        {
            RebuildController();
            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        }

        return controller;
    }

    private static Animator EnsurePlayerAnimator(PlayerMovement player)
    {
        Animator animator = player.GetComponentInChildren<Animator>(true) ?? player.GetComponentInParent<Animator>(true);
        CharacterController controller =
            player.GetComponent<CharacterController>() ?? player.GetComponentInParent<CharacterController>(true);
        Transform playerRoot = controller != null ? controller.transform : player.transform;

        if (animator != null)
        {
            HidePlaceholderRenderer(playerRoot);
            return animator;
        }

        GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModelPath);

        if (modelPrefab == null)
        {
            Debug.LogWarning($"Could not find player model at {PlayerModelPath}");
            return null;
        }

        GameObject modelInstance = PrefabUtility.InstantiatePrefab(modelPrefab, player.gameObject.scene) as GameObject;

        if (modelInstance == null)
        {
            modelInstance = UnityEngine.Object.Instantiate(modelPrefab);
        }

        modelInstance.name = "PlayerModel";
        modelInstance.transform.SetParent(playerRoot, false);
        modelInstance.transform.localPosition = new Vector3(0f, -1f, 0f);
        modelInstance.transform.localRotation = Quaternion.identity;
        modelInstance.transform.localScale = Vector3.one;

        animator = modelInstance.GetComponent<Animator>();

        if (animator == null)
        {
            animator = modelInstance.AddComponent<Animator>();
        }

        HidePlaceholderRenderer(playerRoot);
        EditorUtility.SetDirty(modelInstance);
        return animator;
    }

    private static void AssignController(Animator animator, AnimatorController controller)
    {
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        EditorUtility.SetDirty(animator);
    }

    private static void HidePlaceholderRenderer(Transform playerRoot)
    {
        foreach (Renderer renderer in playerRoot.GetComponents<Renderer>())
        {
            renderer.enabled = false;
            EditorUtility.SetDirty(renderer);
        }
    }

    private static void ConfigureAnimationImports()
    {
        Avatar playerAvatar = AssetDatabase.LoadAllAssetsAtPath(PlayerModelPath)
            .OfType<Avatar>()
            .FirstOrDefault(avatar => !avatar.name.Contains("Preview", StringComparison.OrdinalIgnoreCase));

        foreach (string animationPath in GetAnimationPaths())
        {
            ModelImporter importer = AssetImporter.GetAtPath(animationPath) as ModelImporter;

            if (importer == null)
            {
                continue;
            }

            bool changed = false;

            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                changed = true;
            }

            if (playerAvatar != null)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = playerAvatar;
                changed = true;
            }

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            string stateName = Path.GetFileNameWithoutExtension(animationPath);
            bool shouldLoop = ShouldLoop(stateName);

            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].loopTime = shouldLoop;
                clips[i].loopPose = shouldLoop;
            }

            importer.clipAnimations = clips;
            changed = true;

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }
    }

    private static string[] GetAnimationPaths()
    {
        return AssetDatabase.FindAssets("t:Model", new[] { AnimationsFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
            .Where(path => Path.GetFileNameWithoutExtension(path) != "Ch30_nonPBR")
            .OrderBy(path => path)
            .ToArray();
    }

    private static bool ShouldLoop(string stateName)
    {
        string name = stateName.ToLowerInvariant();

        if (name.Contains("attack") ||
            name.Contains("jump") ||
            name.Contains("react") ||
            name.Contains("equip") ||
            name.Contains("disarm") ||
            name.Contains("taunt") ||
            name.Contains("to standing"))
        {
            return false;
        }

        return name.Contains("idle") ||
            name.Contains("walk") ||
            name.Contains("run") ||
            name.Contains("block idle") ||
            name.Contains("crouch idle");
    }

    private static bool IsUsableClip(AnimationClip clip)
    {
        return clip != null &&
            !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase) &&
            !clip.name.StartsWith("preview", StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeStateName(string stateName)
    {
        return stateName.Replace('.', '_');
    }
}
