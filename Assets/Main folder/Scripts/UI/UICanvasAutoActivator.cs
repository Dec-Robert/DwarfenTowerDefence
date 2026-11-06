using System;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Automatycznie gwarantuje aktywację głównych Canvasów i zakładek UI na początku sceny.
/// Zapobiega sytuacjom, w których developer zapomni włączyć wyłączony podczas edycji Canvas
/// (np. Canvas_MainMenu, Canvas_HUD, Canvas_ContextWindows, Canvas_Tooltips, Canvas_ScreenBlockers).
/// Działa automatycznie przy uruchomieniu gry (Play Mode / Build) oraz przy każdym załadowaniu sceny.
/// </summary>
public static class UICanvasAutoActivator
{
    private static bool isRegistered = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitializeOnLoad()
    {
        if (!isRegistered)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            isRegistered = true;
        }

        // Aktywuj dla bieżącej aktywnej sceny
        EnsureMainCanvasesActive(SceneManager.GetActiveScene());
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureMainCanvasesActive(scene);
    }

    /// <summary>
    /// Sprawdza obiekty w korzeniu podanej sceny i aktywuje wszystkie główne Canvasy/zakładki UI.
    /// </summary>
    public static int EnsureMainCanvasesActive(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return 0;
        }

        int activatedCount = 0;
        GameObject[] rootObjects = scene.GetRootGameObjects();

        foreach (GameObject root in rootObjects)
        {
            if (root == null) continue;

            if (IsMainUICanvas(root))
            {
                if (!root.activeSelf)
                {
                    root.SetActive(true);
                    activatedCount++;
                    Debug.Log($"<color=#38BDF8>[UICanvasAutoActivator]</color> Automatycznie aktywowano wyłączony główny Canvas: <b>{root.name}</b> na scenie <i>{scene.name}</i>.");
                }
            }
        }

        return activatedCount;
    }

    /// <summary>
    /// Sprawdza czy dany obiekt korzenia reprezentuje główny Canvas / nadrzędną zakładkę UI.
    /// </summary>
    private static bool IsMainUICanvas(GameObject go)
    {
        // 1. Sprawdzenie po komponencie Canvas
        if (go.GetComponent<Canvas>() != null)
        {
            return true;
        }

        // 2. Sprawdzenie po konwencji nazewnictwa głównych Canvasów projektu
        string name = go.name;
        if (name.StartsWith("Canvas", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("UI_", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("UI", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    private static void RegisterEditorPlayModeCheck()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            // Przed wejściem do Play Mode upewnij się, że sceny otwarte w edytorze mają aktywne canvasy
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                {
                    EnsureMainCanvasesActive(scene);
                }
            }
        }
    }

    [MenuItem("Tools/UI/Aktywuj Wszystkie Główne Canvasy")]
    public static void ActivateAllCanvasesMenuItem()
    {
        int totalActivated = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded)
            {
                totalActivated += EnsureMainCanvasesActive(scene);
            }
        }

        if (totalActivated > 0)
        {
            Debug.Log($"<color=#4ADE80>[UICanvasAutoActivator]</color> Aktywowano łącznie {totalActivated} głównych Canvasów.");
        }
        else
        {
            Debug.Log("<color=#4ADE80>[UICanvasAutoActivator]</color> Wszystkie główne Canvasy są już aktywne.");
        }
    }
#endif
}
