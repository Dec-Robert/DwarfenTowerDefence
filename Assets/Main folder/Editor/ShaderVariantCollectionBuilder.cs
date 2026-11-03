using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Narzędzie edytora Unity do zbierania wariantów shaderów projektu i generowania
/// assetu ShaderVariantCollection dla ShaderPrewarmer oraz konfiguracji scen w Build Settings.
/// </summary>
public static class ShaderVariantCollectionBuilder
{
    private const string ResourceDir = "Assets/Resources";
    private const string CollectionAssetPath = "Assets/Resources/WarmupShaderVariants.shadervariants";

    [MenuItem("Tools/Dwarfen TD/Build Shader Variant Collection", priority = 100)]
    [InitializeOnLoadMethod]
    public static void BuildShaderVariantCollection()
    {
        if (!Directory.Exists(ResourceDir))
        {
            Directory.CreateDirectory(ResourceDir);
        }

        ShaderVariantCollection collection = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(CollectionAssetPath);
        if (collection == null)
        {
            collection = new ShaderVariantCollection();
            AssetDatabase.CreateAsset(collection, CollectionAssetPath);
        }
        else
        {
            collection.Clear();
        }

        int addedVariantsCount = 0;
        int materialsScanned = 0;

        // 1. Przeskanuj materiały w folderze Assets
        string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Main folder" });
        foreach (string guid in materialGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null || mat.shader == null) continue;

            materialsScanned++;
            string[] keywords = mat.shaderKeywords;

            PassType[] passesToWarmup = new[] { PassType.ForwardBase, PassType.ScriptableRenderPipeline, PassType.Normal };
            foreach (var pass in passesToWarmup)
            {
                try
                {
                    var variant = new ShaderVariantCollection.ShaderVariant(mat.shader, pass, keywords);
                    if (!collection.Contains(variant))
                    {
                        collection.Add(variant);
                        addedVariantsCount++;
                    }
                }
                catch
                {
                    // Niektóre kombinacje pass/keywords mogą nie istnieć w danym shaderze
                }
            }
        }

        EditorUtility.SetDirty(collection);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[ShaderVariantCollectionBuilder] Zbudowano kolekcję wariantów: {CollectionAssetPath}. " +
                  $"Przeskanowano {materialsScanned} materiałów, dodano {addedVariantsCount} wariantów shaderów. " +
                  $"Liczba shaderów w kolekcji: {collection.shaderCount}, variantCount: {collection.variantCount}.");
    }

    [MenuItem("Tools/Dwarfen TD/Configure Build Settings Scenes", priority = 101)]
    public static void ConfigureBuildSettingsScenes()
    {
        string mainMenuDemoScene = "Assets/Main folder/_Scenes/MainMenu Demo scene.unity";
        string gameScene = "Assets/Main folder/_Scenes/Game scene.unity";
        string mainMenuScene = "Assets/Main folder/_Scenes/MainMenu scene.unity";

        var list = new List<EditorBuildSettingsScene>();

        if (File.Exists(mainMenuDemoScene))
        {
            list.Add(new EditorBuildSettingsScene(mainMenuDemoScene, true));
        }
        if (File.Exists(gameScene))
        {
            list.Add(new EditorBuildSettingsScene(gameScene, true));
        }
        if (File.Exists(mainMenuScene))
        {
            list.Add(new EditorBuildSettingsScene(mainMenuScene, true));
        }

        EditorBuildSettings.scenes = list.ToArray();
        Debug.Log($"[ShaderVariantCollectionBuilder] Skonfigurowano sceny w Build Settings ({list.Count} scen aktywnych). Scena 0: MainMenu Demo scene, Scena 1: Game scene.");
    }
}
