using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Editor-only setup. Does not modify a scene or add runtime scripts.
public static class FiveRobotAssetSetup
{
    static readonly string[] Names = { "RedRobot", "NavyRobot", "PinkGoalkeeper", "SkyGoalkeeper" };

    [MenuItem("Tools/Cube Robots/Create Materials and Prefabs")]
    public static void Build()
    {
        var pipeline = GraphicsSettings.currentRenderPipeline;
        bool urp = pipeline != null && pipeline.GetType().Name.Contains("Universal");
        if (pipeline != null && !urp)
        {
            EditorUtility.DisplayDialog("Cube Robots", "This setup supports Built-in and URP. For HDRP/custom pipelines, assign the supplied base color and metallic/smoothness maps manually.", "OK");
            return;
        }
        string root = "Assets/_Soccer/Manager5v5/Art/CubeRobots";
        Shader bodyShader = Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard");
        Shader eyeShader = Shader.Find(urp ? "Universal Render Pipeline/Unlit" : "Unlit/Color");
        if (bodyShader == null || eyeShader == null) throw new InvalidOperationException("Required shaders are unavailable.");
        EnsureFolder(root, "Materials"); EnsureFolder(root, "Prefabs");
        foreach (string name in Names)
        {
            string modelPath = root + "/Models/" + name + ".fbx";
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Missing FBX: " + modelPath);
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false; importer.importLights = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;
            importer.addCollider = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
            Texture2D color = ConfigureTexture(root + "/Textures/" + name + "_BaseColor.png", true);
            Texture2D mask = ConfigureTexture(root + "/Textures/" + name + "_MetallicSmoothness.png", false);
            Material body = GetMaterial(root + "/Materials/" + name + "_Body.mat", bodyShader);
            body.SetTexture(urp ? "_BaseMap" : "_MainTex", color);
            body.SetColor(urp ? "_BaseColor" : "_Color", Color.white);
            body.SetTexture("_MetallicGlossMap", mask);
            body.SetFloat("_Metallic", 1f);
            body.SetFloat(urp ? "_Smoothness" : "_GlossMapScale", 1f);
            body.SetFloat("_SmoothnessTextureChannel", 0f);
            body.EnableKeyword(urp ? "_METALLICSPECGLOSSMAP" : "_METALLICGLOSSMAP");
            body.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            body.DisableKeyword("_EMISSION");
            body.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            body.enableInstancing = true;
            Material eyes = GetMaterial(root + "/Materials/" + name + "_Eyes.mat", eyeShader);
            bool pink = name == "RedRobot" || name == "PinkGoalkeeper";
            Color hdr = pink ? new Color(2.5f, .15f, .3f, 1f) : new Color(.16200816f, .9406553f, 2.2827466f, 1f);
            eyes.SetColor(urp ? "_BaseColor" : "_Color", hdr);
            eyes.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            eyes.enableInstancing = true;
            EditorUtility.SetDirty(body); EditorUtility.SetDirty(eyes);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var modelInstance = PrefabUtility.InstantiatePrefab(model) as GameObject;
            if (modelInstance == null) throw new InvalidOperationException("Unable to instantiate " + name);
            var instance = new GameObject(name);
            modelInstance.transform.SetParent(instance.transform, false);
            // The imported FBX faces -Z. Gameplay uses +Z for the plate and ball anchor.
            modelInstance.transform.localRotation = Quaternion.Euler(0, 180, 0);
            try
            {
                instance.name = name;
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;
                var renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
                var filters = instance.GetComponentsInChildren<MeshFilter>(true);
                if (renderers.Length != 1 || filters.Length != 1 || filters[0].sharedMesh.subMeshCount != 2)
                    throw new InvalidOperationException(name + ": expected one mesh with two submeshes.");
                renderers[0].sharedMaterials = new[] { body, eyes };
                // The whole character moves, so do not mark the objects Static.
                foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, (StaticEditorFlags)0);
                var mesh = filters[0].sharedMesh;
                ulong indices = 0;
                for (int i = 0; i < mesh.subMeshCount; i++) indices += mesh.GetIndexCount(i);
                if (indices / 3 != 5768) throw new InvalidOperationException(name + ": triangle count changed.");
                if (Mathf.Abs(mesh.bounds.size.y - 1.04f) > .02f)
                    throw new InvalidOperationException(name + ": unexpected imported scale; check axis/unit settings.");
                PrefabUtility.SaveAsPrefabAsset(instance, root + "/Prefabs/" + name + ".prefab");
                Debug.Log("Cube Robots: " + name + " — 5768 triangles, 1 renderer, 2 materials. Bounds: " + mesh.bounds);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log("Cube robot materials and prefabs created.");
    }

    static void EnsureFolder(string root, string child)
    {
        if (!AssetDatabase.IsValidFolder(root + "/" + child)) AssetDatabase.CreateFolder(root, child);
    }
    static Material GetMaterial(string path, Shader shader)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        else material.shader = shader;
        return material;
    }
    static Texture2D ConfigureTexture(string path, bool srgb)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("Missing texture: " + path);
        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = srgb;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = false;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.isReadable = false;
        importer.maxTextureSize = 256;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}

