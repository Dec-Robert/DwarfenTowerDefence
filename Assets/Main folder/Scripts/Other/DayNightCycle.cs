using UnityEngine;

public class DayNightCycle : MonoBehaviour
{
    [Header("Referencje")]
    public Light sunLight; // Twoje g³ówne œwiat³o Directional Light

    [Header("Ustawienia Obrotu")]
    // O której godzinie s³oñce wschodzi (k¹t 0)? Domyœlnie 6:00
    public float sunriseHour = 6f;

    // Opcjonalnie: K¹t padania cienia (oœ Y)
    public float sunYRotation = -30f;

    [Header("Wygl¹d Œwiata")]
    public Gradient sunColor; // Kolor s³oñca w zale¿noœci od pory dnia
    public Gradient skyColor; // Kolor nieba/ambientu (¿eby cienie nie by³y czarne)
    public AnimationCurve sunIntensity; // Jasnoœæ s³oñca (0 = noc, 1 = po³udnie)

    private void Start()
    {
        if (sunLight == null)
        {
            sunLight = GetComponent<Light>();
        }
    }

    private void Update()
    {
        if (TimeCycleManager.Instance == null) return;

        // 1. Pobieramy czas (0.00 do 24.00)
        float currentTime = TimeCycleManager.Instance.currentTime;

        // 2. Obliczamy procent dnia (0.0 do 1.0)
        // 0.0 = Pó³noc (00:00), 0.5 = Po³udnie (12:00)
        float timePercent = currentTime / 24f;

        // 3. Obracanie S³oñca
        // Chcemy, ¿eby o sunriseHour (np. 6:00) s³oñce by³o na horyzoncie (0 stopni)
        // 24h = 360 stopni.
        // Wzór: (Time - Sunrise) * 15 stopni/h
        float sunAngle = (currentTime - sunriseHour) * 15f;

        // Ustawiamy rotacjê. Oœ X to wysokoœæ s³oñca. Oœ Y to kierunek cienia.
        sunLight.transform.rotation = Quaternion.Euler(sunAngle, sunYRotation, 0);

        // 4. Zmiana Kolorów i Jasnoœci
        // Evaluate bierze wartoœæ 0-1, wiêc u¿ywamy timePercent

        sunLight.color = sunColor.Evaluate(timePercent);
        sunLight.intensity = sunIntensity.Evaluate(timePercent);

        // 5. Zmiana Ambientu (Œwiat³a otoczenia)
        // To wa¿ne, ¿eby w nocy nie by³o idealnie czarno w cieniach
        RenderSettings.ambientLight = skyColor.Evaluate(timePercent);
    }
}