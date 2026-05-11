using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class PlayModeSceneChangeSaver
{
    private const string EnabledPrefsKey = "Abyssfall.SavePlayModeChangesOnStop.Enabled";
    private const string MenuPath = "Tools/Abyssfall/Save Play Mode Changes On Stop";
    private const string SnapshotPath = "Library/AbyssfallPlayModeSnapshot.json";

    static PlayModeSceneChangeSaver()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static bool Enabled
    {
        get => EditorPrefs.GetBool(EnabledPrefsKey, true);
        set => EditorPrefs.SetBool(EnabledPrefsKey, value);
    }

    [MenuItem(MenuPath)]
    private static void ToggleEnabled()
    {
        Enabled = !Enabled;
        Debug.Log($"Save Play Mode Changes On Stop: {(Enabled ? "ON" : "OFF")}");
    }

    [MenuItem(MenuPath, true)]
    private static bool ToggleEnabledValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return true;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (!Enabled)
        {
            return;
        }

        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            SaveSnapshot();
            return;
        }

        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall += RestoreSnapshotAndSaveScenes;
        }
    }

    private static void SaveSnapshot()
    {
        SnapshotData snapshot = new SnapshotData();

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (!scene.isLoaded || string.IsNullOrEmpty(scene.path))
            {
                continue;
            }

            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                SnapshotGameObjectRecursive(rootObject, snapshot);
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(SnapshotPath));
        File.WriteAllText(SnapshotPath, JsonUtility.ToJson(snapshot));
    }

    private static void SnapshotGameObjectRecursive(GameObject gameObject, SnapshotData snapshot)
    {
        if (gameObject == null || HasDontSaveFlag(gameObject.hideFlags))
        {
            return;
        }

        string scenePath = gameObject.scene.path;
        string hierarchyPath = GetHierarchyPath(gameObject.transform);

        snapshot.GameObjects.Add(new GameObjectSnapshot
        {
            Id = GetGlobalId(gameObject),
            ScenePath = scenePath,
            HierarchyPath = hierarchyPath,
            Name = gameObject.name,
            Tag = gameObject.tag,
            Layer = gameObject.layer,
            IsStatic = gameObject.isStatic,
            ActiveSelf = gameObject.activeSelf,
            LocalPosition = gameObject.transform.localPosition,
            LocalRotation = gameObject.transform.localRotation,
            LocalScale = gameObject.transform.localScale
        });

        Component[] components = gameObject.GetComponents<Component>();

        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];

            if (component == null ||
                component is Transform ||
                HasDontSaveFlag(component.hideFlags))
            {
                continue;
            }

            ComponentSnapshot componentSnapshot = new ComponentSnapshot
            {
                Id = GetGlobalId(component),
                GameObjectId = GetGlobalId(gameObject),
                ScenePath = scenePath,
                GameObjectHierarchyPath = hierarchyPath,
                TypeName = component.GetType().AssemblyQualifiedName,
                SameTypeIndex = GetSameTypeIndex(components, i)
            };

            SnapshotSerializedFields(component, componentSnapshot.Fields);
            snapshot.Components.Add(componentSnapshot);
        }

        Transform transform = gameObject.transform;

        for (int i = 0; i < transform.childCount; i++)
        {
            SnapshotGameObjectRecursive(transform.GetChild(i).gameObject, snapshot);
        }
    }

    private static void SnapshotSerializedFields(Component component, List<FieldSnapshot> fields)
    {
        SerializedObject serializedObject = new SerializedObject(component);
        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;

        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (property.propertyPath == "m_Script" ||
                property.propertyType == SerializedPropertyType.ObjectReference ||
                property.isArray)
            {
                continue;
            }

            if (TryCreateFieldSnapshot(property, out FieldSnapshot fieldSnapshot))
            {
                fields.Add(fieldSnapshot);
            }
        }
    }

    private static bool TryCreateFieldSnapshot(SerializedProperty property, out FieldSnapshot fieldSnapshot)
    {
        fieldSnapshot = new FieldSnapshot
        {
            Path = property.propertyPath,
            Type = (int)property.propertyType
        };

        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.Enum:
            case SerializedPropertyType.LayerMask:
                fieldSnapshot.IntValue = property.intValue;
                return true;
            case SerializedPropertyType.Boolean:
                fieldSnapshot.BoolValue = property.boolValue;
                return true;
            case SerializedPropertyType.Float:
                fieldSnapshot.FloatValue = property.floatValue;
                return true;
            case SerializedPropertyType.String:
                fieldSnapshot.StringValue = property.stringValue;
                return true;
            case SerializedPropertyType.Color:
                fieldSnapshot.ColorValue = property.colorValue;
                return true;
            case SerializedPropertyType.Vector2:
                fieldSnapshot.Vector2Value = property.vector2Value;
                return true;
            case SerializedPropertyType.Vector3:
                fieldSnapshot.Vector3Value = property.vector3Value;
                return true;
            case SerializedPropertyType.Vector4:
                fieldSnapshot.Vector4Value = property.vector4Value;
                return true;
            case SerializedPropertyType.Rect:
                fieldSnapshot.RectValue = property.rectValue;
                return true;
            case SerializedPropertyType.Bounds:
                fieldSnapshot.BoundsValue = property.boundsValue;
                return true;
            case SerializedPropertyType.Quaternion:
                fieldSnapshot.QuaternionValue = property.quaternionValue;
                return true;
            default:
                return false;
        }
    }

    private static void RestoreSnapshotAndSaveScenes()
    {
        if (!File.Exists(SnapshotPath))
        {
            return;
        }

        string json = File.ReadAllText(SnapshotPath);
        File.Delete(SnapshotPath);

        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        SnapshotData snapshot = JsonUtility.FromJson<SnapshotData>(json);

        if (snapshot == null)
        {
            return;
        }

        RestoreGameObjects(snapshot.GameObjects);
        RestoreComponents(snapshot.Components);
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log("Saved Play Mode scene and safe component changes after Stop.");
    }

    private static void RestoreGameObjects(List<GameObjectSnapshot> snapshots)
    {
        foreach (GameObjectSnapshot snapshot in snapshots)
        {
            GameObject gameObject = ResolveObject<GameObject>(snapshot.Id) ??
                FindGameObjectByPath(snapshot.ScenePath, snapshot.HierarchyPath);

            if (gameObject == null)
            {
                continue;
            }

            gameObject.name = snapshot.Name;
            gameObject.layer = snapshot.Layer;
            gameObject.isStatic = snapshot.IsStatic;
            gameObject.SetActive(snapshot.ActiveSelf);

            try
            {
                gameObject.tag = snapshot.Tag;
            }
            catch (UnityException)
            {
                // Ignore tags that no longer exist in TagManager.
            }

            Transform transform = gameObject.transform;
            transform.localPosition = snapshot.LocalPosition;
            transform.localRotation = snapshot.LocalRotation;
            transform.localScale = snapshot.LocalScale;

            EditorUtility.SetDirty(gameObject);
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
    }

    private static void RestoreComponents(List<ComponentSnapshot> snapshots)
    {
        foreach (ComponentSnapshot snapshot in snapshots)
        {
            Component component = ResolveObject<Component>(snapshot.Id);

            if (component == null)
            {
                GameObject gameObject = ResolveObject<GameObject>(snapshot.GameObjectId) ??
                    FindGameObjectByPath(snapshot.ScenePath, snapshot.GameObjectHierarchyPath);
                component = FindComponent(gameObject, snapshot);
            }

            if (component == null)
            {
                component = AddMissingComponent(snapshot);
            }

            if (component == null)
            {
                continue;
            }

            RestoreSerializedFields(component, snapshot.Fields);
            EditorUtility.SetDirty(component);
            EditorSceneManager.MarkSceneDirty(component.gameObject.scene);
        }
    }

    private static void RestoreSerializedFields(Component component, List<FieldSnapshot> fields)
    {
        SerializedObject serializedObject = new SerializedObject(component);

        foreach (FieldSnapshot field in fields)
        {
            SerializedProperty property = serializedObject.FindProperty(field.Path);

            if (property == null ||
                property.propertyType != (SerializedPropertyType)field.Type ||
                property.propertyType == SerializedPropertyType.ObjectReference)
            {
                continue;
            }

            ApplyFieldSnapshot(property, field);
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ApplyFieldSnapshot(SerializedProperty property, FieldSnapshot field)
    {
        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
            case SerializedPropertyType.LayerMask:
                property.intValue = field.IntValue;
                break;
            case SerializedPropertyType.Enum:
                property.enumValueIndex = field.IntValue;
                break;
            case SerializedPropertyType.Boolean:
                property.boolValue = field.BoolValue;
                break;
            case SerializedPropertyType.Float:
                property.floatValue = field.FloatValue;
                break;
            case SerializedPropertyType.String:
                property.stringValue = field.StringValue;
                break;
            case SerializedPropertyType.Color:
                property.colorValue = field.ColorValue;
                break;
            case SerializedPropertyType.Vector2:
                property.vector2Value = field.Vector2Value;
                break;
            case SerializedPropertyType.Vector3:
                property.vector3Value = field.Vector3Value;
                break;
            case SerializedPropertyType.Vector4:
                property.vector4Value = field.Vector4Value;
                break;
            case SerializedPropertyType.Rect:
                property.rectValue = field.RectValue;
                break;
            case SerializedPropertyType.Bounds:
                property.boundsValue = field.BoundsValue;
                break;
            case SerializedPropertyType.Quaternion:
                property.quaternionValue = field.QuaternionValue;
                break;
        }
    }

    private static Component FindComponent(GameObject gameObject, ComponentSnapshot snapshot)
    {
        if (gameObject == null || string.IsNullOrEmpty(snapshot.TypeName))
        {
            return null;
        }

        Type componentType = Type.GetType(snapshot.TypeName);

        if (componentType == null)
        {
            return null;
        }

        Component[] components = gameObject.GetComponents(componentType);
        return snapshot.SameTypeIndex >= 0 && snapshot.SameTypeIndex < components.Length
            ? components[snapshot.SameTypeIndex]
            : null;
    }

    private static Component AddMissingComponent(ComponentSnapshot snapshot)
    {
        GameObject gameObject = ResolveObject<GameObject>(snapshot.GameObjectId) ??
            FindGameObjectByPath(snapshot.ScenePath, snapshot.GameObjectHierarchyPath);

        if (gameObject == null || string.IsNullOrEmpty(snapshot.TypeName))
        {
            return null;
        }

        Type componentType = Type.GetType(snapshot.TypeName);

        if (componentType == null ||
            componentType == typeof(Transform) ||
            !typeof(Component).IsAssignableFrom(componentType) ||
            componentType.IsAbstract)
        {
            return null;
        }

        try
        {
            return gameObject.AddComponent(componentType);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static GameObject FindGameObjectByPath(string scenePath, string hierarchyPath)
    {
        if (string.IsNullOrEmpty(scenePath) || string.IsNullOrEmpty(hierarchyPath))
        {
            return null;
        }

        Scene scene = default;

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene candidate = SceneManager.GetSceneAt(i);

            if (candidate.path == scenePath)
            {
                scene = candidate;
                break;
            }
        }

        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        string[] names = hierarchyPath.Split('/');

        if (names.Length == 0)
        {
            return null;
        }

        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            if (rootObject.name != names[0])
            {
                continue;
            }

            Transform current = rootObject.transform;

            for (int i = 1; i < names.Length; i++)
            {
                current = FindDirectChild(current, names[i]);

                if (current == null)
                {
                    break;
                }
            }

            if (current != null)
            {
                return current.gameObject;
            }
        }

        return null;
    }

    private static Transform FindDirectChild(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    private static T ResolveObject<T>(string id) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(id) ||
            !GlobalObjectId.TryParse(id, out GlobalObjectId globalObjectId))
        {
            return null;
        }

        return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalObjectId) as T;
    }

    private static string GetGlobalId(UnityEngine.Object unityObject)
    {
        return GlobalObjectId.GetGlobalObjectIdSlow(unityObject).ToString();
    }

    private static string GetHierarchyPath(Transform transform)
    {
        string path = transform.name;

        while (transform.parent != null)
        {
            transform = transform.parent;
            path = $"{transform.name}/{path}";
        }

        return path;
    }

    private static int GetSameTypeIndex(Component[] components, int componentIndex)
    {
        Type componentType = components[componentIndex].GetType();
        int sameTypeIndex = 0;

        for (int i = 0; i < componentIndex; i++)
        {
            if (components[i] != null && components[i].GetType() == componentType)
            {
                sameTypeIndex++;
            }
        }

        return sameTypeIndex;
    }

    private static bool HasDontSaveFlag(HideFlags hideFlags)
    {
        return (hideFlags & HideFlags.DontSave) != 0 ||
            (hideFlags & HideFlags.DontSaveInEditor) != 0 ||
            (hideFlags & HideFlags.DontSaveInBuild) != 0;
    }

    [Serializable]
    private class SnapshotData
    {
        public List<GameObjectSnapshot> GameObjects = new List<GameObjectSnapshot>();
        public List<ComponentSnapshot> Components = new List<ComponentSnapshot>();
    }

    [Serializable]
    private class GameObjectSnapshot
    {
        public string Id;
        public string ScenePath;
        public string HierarchyPath;
        public string Name;
        public string Tag;
        public int Layer;
        public bool IsStatic;
        public bool ActiveSelf;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        public Vector3 LocalScale;
    }

    [Serializable]
    private class ComponentSnapshot
    {
        public string Id;
        public string GameObjectId;
        public string ScenePath;
        public string GameObjectHierarchyPath;
        public string TypeName;
        public int SameTypeIndex;
        public List<FieldSnapshot> Fields = new List<FieldSnapshot>();
    }

    [Serializable]
    private class FieldSnapshot
    {
        public string Path;
        public int Type;
        public int IntValue;
        public bool BoolValue;
        public float FloatValue;
        public string StringValue;
        public Color ColorValue;
        public Vector2 Vector2Value;
        public Vector3 Vector3Value;
        public Vector4 Vector4Value;
        public Rect RectValue;
        public Bounds BoundsValue;
        public Quaternion QuaternionValue;
    }
}
