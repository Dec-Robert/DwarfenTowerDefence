using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System;

public class TelemetryManager : MonoBehaviour
{
    [Header("Konfiguracja Google Form")]
    // Link do Twojego formularza (z ko�c�wk� formResponse)
    [SerializeField] private string googleFormURL = "https://docs.google.com/forms/d/1l9Gj_MaGIw31kVcagDYCFExUexmhBpLiguwaE3KeEqY/formResponse";

    // ID pola tekstowego (entry.XXXXXX)
    [SerializeField] private string entryID = "entry.645495749";

    private const string PREF_KEY_HAS_SENT = "Telemetry_Sent_V1";
    private const string PREF_KEY_USER_ID = "Telemetry_UserID";
    //
    private void Start()
    {
        // Sprawdzamy, czy ju� wys�ali�my dane w przesz�o�ci
        if (PlayerPrefs.GetInt(PREF_KEY_HAS_SENT, 0) == 0)
        {
            StartCoroutine(SendFirstLaunchData());
        }
        else
        {
            Debug.Log("[Telemetry] U�ytkownik powracaj�cy. Dane ju� by�y wys�ane.");
        }
    }

    IEnumerator SendFirstLaunchData()
    {
        // 1. Generujemy lub pobieramy unikalne ID
        string userID = GetOrCreateUserID();
        Debug.Log($"[Telemetry] Nowy u�ytkownik! Wysy�am ID: {userID}");

        // 2. Przygotowujemy formularz
        WWWForm form = new WWWForm();
        // entryID to nazwa pola w Google Form
        form.AddField(entryID, userID);

        // 3. Wysy�amy ��danie
        using (UnityWebRequest www = UnityWebRequest.Post(googleFormURL, form))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[Telemetry] B��d wysy�ania: {www.error}");
                // Nie zapisujemy flagi, spr�bujemy znowu przy kolejnym uruchomieniu
            }
            else
            {
                Debug.Log("[Telemetry] Sukces! U�ytkownik zarejestrowany.");

                // 4. Zapisujemy flag�, �e wys�ano (�eby nie liczy� go drugi raz)
                PlayerPrefs.SetInt(PREF_KEY_HAS_SENT, 1);
                PlayerPrefs.Save();
            }
        }
    }

    string GetOrCreateUserID()
    {
        // Sprawdzamy czy mamy ju� ID (mo�e gracz skasowa� save, ale PlayerPrefs zosta�y)
        if (PlayerPrefs.HasKey(PREF_KEY_USER_ID))
        {
            return PlayerPrefs.GetString(PREF_KEY_USER_ID);
        }

        // Generujemy nowe UUID (Globalnie Unikalny Identyfikator)
        string newID = Guid.NewGuid().ToString(); // Wygl�da np. tak: "d83b2b4a-1c6d-4b5a-9e3f-2c8d1b4a5e6f"

        // Zapisujemy
        PlayerPrefs.SetString(PREF_KEY_USER_ID, newID);
        PlayerPrefs.Save();

        return newID;
    }

    // Opcjonalne: Metoda do resetowania test�w (przypisz np. pod przycisk w menu debugowym)
    [ContextMenu("Reset Telemetry (Debug)")]
    public void ResetTelemetry()
    {
        PlayerPrefs.DeleteKey(PREF_KEY_HAS_SENT);
        PlayerPrefs.DeleteKey(PREF_KEY_USER_ID);
        Debug.Log("[Telemetry] Zresetowano. Przy nast�pnym starcie gra uzna Ci� za nowego gracza.");
    }
}