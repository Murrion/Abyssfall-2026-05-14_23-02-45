using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Diagnostics;

public class PullAndPlay
{
    [MenuItem("Tools/Pull & Play")]
    public static void PullAndPlayGame()
    {
        UnityEngine.Debug.Log("Pobieranie zmian z GitHub...");
        RunGit("pull");
        AssetDatabase.Refresh();
        EditorApplication.isPlaying = true;
    }

    [MenuItem("Tools/Save & Push")]
    public static void SaveAndPush()
    {
        // Zapisz wszystkie sceny
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        UnityEngine.Debug.Log("Wysyłanie zmian na GitHub...");

        // Dodaj wszystkie zmiany
        RunGit("add -A");

        // Commit z datą i godziną
        string message = "Auto-save " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        RunGit($"commit -m \"{message}\"");

        // Push na GitHub
        string pushResult = RunGit("push");
        UnityEngine.Debug.Log("Push zakończony: " + pushResult);

        EditorUtility.DisplayDialog("Save & Push", "Zmiany zapisane na GitHub! ✅", "OK");
    }

    private static string RunGit(string arguments)
    {
        var process = new Process();
        process.StartInfo.FileName = "git";
        process.StartInfo.Arguments = arguments;
        process.StartInfo.WorkingDirectory = Application.dataPath + "/..";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;
        process.Start();
        process.WaitForExit();
        return process.StandardOutput.ReadToEnd();
    }
}