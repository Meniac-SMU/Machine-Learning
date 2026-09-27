using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

public static class ExhibitionPackages
{
    static AddRequest request;
    static double deadline;
    public static void Install()
    {
        request = Client.Add("com.unity.localization");
        deadline = EditorApplication.timeSinceStartup + 600;
        EditorApplication.update += Poll;
    }
    static void Poll()
    {
        if (!request.IsCompleted && EditorApplication.timeSinceStartup < deadline) return;
        EditorApplication.update -= Poll;
        if (request.IsCompleted && request.Status == StatusCode.Success)
        { Debug.Log("EXHIBITION LOCALIZATION INSTALLED " + request.Result.version); EditorApplication.Exit(0); }
        else { Debug.LogError(request.Error?.message ?? "Localization installation timeout"); EditorApplication.Exit(1); }
    }
}
