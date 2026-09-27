using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace MachineLearning.Soccer.Manager.Exhibition.Editor
{
    public static class ExhibitionPackages
    {
        static AddRequest request;
        static double deadline;
        public static void InstallCinemachine()
        {
            request=Client.Add("com.unity.cinemachine@3.1.7");
            deadline=EditorApplication.timeSinceStartup+600;
            EditorApplication.update+=Poll;
        }
        static void Poll()
        {
            if(!request.IsCompleted&&EditorApplication.timeSinceStartup<deadline)return;
            EditorApplication.update-=Poll;
            bool success=request.IsCompleted&&request.Status==StatusCode.Success;
            if(success)Debug.Log("EXHIBITION CINEMACHINE INSTALLED "+request.Result.packageId);
            else Debug.LogError(request.Error?.message??"Cinemachine installation timed out");
            EditorApplication.Exit(success?0:1);
        }
    }
}
