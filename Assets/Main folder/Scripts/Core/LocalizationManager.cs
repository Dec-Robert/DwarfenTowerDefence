using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>
/// Zarządca lokalizacji gry (PL / EN oraz przyszłe języki).
/// Przechowuje i ładuje słowniki z plików JSON w folderze Resources/Localization/Languages/.
/// Zapewnia natychmiastowe przełączanie języków w locie i powiadamia zarejestrowane komponenty UI.
/// </summary>
public class LocalizationManager : MonoBehaviour
{
    public const string PREFS_KEY = "Game_SelectedLanguage";
    public const string DEFAULT_LANGUAGE = "pl";
    public const string ENGLISH_LANGUAGE = "en";

    public static LocalizationManager Instance { get; private set; }

    public static event Action OnLanguageChanged;

    private static string currentLanguage = DEFAULT_LANGUAGE;
    private static readonly Dictionary<string, string> localizedStrings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> fallbackStrings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static bool isInitialized = false;

    public static string CurrentLanguage => currentLanguage;

    public static bool IsInitialized => isInitialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitializeOnLoad()
    {
        if (!isInitialized)
        {
            Initialize();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (!isInitialized)
        {
            Initialize();
        }
    }

    /// <summary>
    /// Inicjalizuje system lokalizacji, wczytując zapisany lub systemowy język.
    /// </summary>
    public static void Initialize()
    {
        string savedLang = PlayerPrefs.GetString(PREFS_KEY, string.Empty);

        if (string.IsNullOrEmpty(savedLang))
        {
            savedLang = Application.systemLanguage == SystemLanguage.Polish ? DEFAULT_LANGUAGE : ENGLISH_LANGUAGE;
        }

        LoadLanguage(savedLang, false);
        isInitialized = true;
    }

    /// <summary>
    /// Zmienia bieżący język gry i wywołuje OnLanguageChanged.
    /// </summary>
    public static void SetLanguage(string langCode)
    {
        if (string.IsNullOrEmpty(langCode)) langCode = DEFAULT_LANGUAGE;
        langCode = langCode.ToLowerInvariant();

        if (currentLanguage == langCode && isInitialized) return;

        LoadLanguage(langCode, true);
        PlayerPrefs.SetString(PREFS_KEY, currentLanguage);
        PlayerPrefs.Save();

        OnLanguageChanged?.Invoke();
    }

    /// <summary>
    /// Pobiera przetłumaczony ciąg dla danego klucza.
    /// Jeśli klucz nie istnieje w bieżącym języku, sprawdza fallback (PL), a na końcu zwraca fallbackText lub sam klucz.
    /// </summary>
    public static string Get(string key, string fallbackText = null)
    {
        if (string.IsNullOrEmpty(key)) return fallbackText ?? string.Empty;

        if (!isInitialized)
        {
            Initialize();
        }

        if (localizedStrings.TryGetValue(key, out string val))
        {
            return val;
        }

        if (fallbackStrings.TryGetValue(key, out string fallbackVal))
        {
            return fallbackVal;
        }

        return fallbackText ?? key;
    }

    /// <summary>
    /// Pobiera przetłumaczony ciąg i formatuje go przekazanymi argumentami string.Format.
    /// </summary>
    public static string GetFormat(string key, params object[] args)
    {
        string template = Get(key);
        if (args == null || args.Length == 0) return template;

        try
        {
            return string.Format(template, args);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LocalizationManager] Błąd formatowania klucza '{key}': {ex.Message}");
            return template;
        }
    }

    /// <summary>
    /// Sprawdza czy dany klucz istnieje w bieżącym słowniku.
    /// </summary>
    public static bool HasKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        if (!isInitialized) Initialize();
        return localizedStrings.ContainsKey(key) || fallbackStrings.ContainsKey(key);
    }

    private static void LoadLanguage(string langCode, bool notify)
    {
        currentLanguage = langCode;
        localizedStrings.Clear();
        fallbackStrings.Clear();

        // 1. Zawsze załaduj język domyślny jako bazę fallback
        string fallbackJson = LoadJsonFromResources(DEFAULT_LANGUAGE);
        if (!string.IsNullOrEmpty(fallbackJson))
        {
            ParseJsonIntoDictionary(fallbackJson, fallbackStrings);
        }

        // 2. Załaduj język docelowy
        if (langCode.Equals(DEFAULT_LANGUAGE, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var kvp in fallbackStrings)
            {
                localizedStrings[kvp.Key] = kvp.Value;
            }
        }
        else
        {
            string targetJson = LoadJsonFromResources(langCode);
            if (!string.IsNullOrEmpty(targetJson))
            {
                ParseJsonIntoDictionary(targetJson, localizedStrings);
            }
            else
            {
                Debug.LogWarning($"[LocalizationManager] Nie znaleziono pliku tłumaczeń dla języka '{langCode}'. Użyto domyślnego.");
                foreach (var kvp in fallbackStrings)
                {
                    localizedStrings[kvp.Key] = kvp.Value;
                }
            }
        }
    }

    private static string LoadJsonFromResources(string langCode)
    {
        TextAsset textAsset = Resources.Load<TextAsset>($"Localization/Languages/{langCode}");
        if (textAsset != null)
        {
            return textAsset.text;
        }
        return null;
    }

    /// <summary>
    /// Lekki, niezależny od zewnętrznych bibliotek parser standardowego JSON key-value.
    /// Obsługuje znaki ucieczki: \", \\, \n, \r, \t, unicode \uXXXX.
    /// </summary>
    public static void ParseJsonIntoDictionary(string json, Dictionary<string, string> dict)
    {
        if (string.IsNullOrWhiteSpace(json) || dict == null) return;

        int i = 0;
        int len = json.Length;

        void SkipWhitespace()
        {
            while (i < len && char.IsWhiteSpace(json[i])) i++;
        }

        string ReadString()
        {
            if (i >= len || json[i] != '"') return null;
            i++; // pomiń otwierający cudzysłów
            StringBuilder sb = new StringBuilder();

            while (i < len)
            {
                char c = json[i++];
                if (c == '"') return sb.ToString();
                if (c == '\\' && i < len)
                {
                    char esc = json[i++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 <= len)
                            {
                                string hex = json.Substring(i, 4);
                                if (int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                                {
                                    sb.Append((char)code);
                                    i += 4;
                                }
                            }
                            break;
                        default:
                            sb.Append(esc);
                            break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        SkipWhitespace();
        if (i < len && json[i] == '{') i++;

        while (i < len)
        {
            SkipWhitespace();
            if (i >= len || json[i] == '}') break;
            if (json[i] == ',') { i++; continue; }

            string key = ReadString();
            if (key == null) { i++; continue; }

            SkipWhitespace();
            if (i < len && json[i] == ':') i++;
            SkipWhitespace();

            string val = ReadString();
            if (val != null)
            {
                dict[key] = val;
            }
        }
    }
}
