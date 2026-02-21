using UnityEngine;

public class DayNightCycle : MonoBehaviour
{
    [Header("Referencje")]
    public Light sunLight; // Twoje g��wne �wiat�o Directional Light

    [Header("Ustawienia Obrotu")]
    // O kt�rej godzinie s�o�ce wschodzi (k�t 0)? Domy�lnie 6:00
    public float sunriseHour = 6f;

    // Opcjonalnie: K�t padania cienia (o� Y)
    public float sunYRotation = -30f;

    [Header("Wygl�d �wiata")]
    public Gradient sunColor; // Kolor s�o�ca w zale�no�ci od pory dnia
    public Gradient skyColor; // Kolor nieba/ambientu (�eby cienie nie by�y czarne)
    public AnimationCurve sunIntensity; // Jasno�� s�o�ca (0 = noc, 1 = po�udnie)

    private void Start()
    {
        if (sunLight == null)
        {
            sunLight = GetComponent<Light>();
        }
    }
    //
    private void Update()
    {
        if (TimeCycleManager.Instance == null) return;

        // 1. Pobieramy czas (0.00 do 24.00)
        float currentTime = TimeCycleManager.Instance.currentTime;

        // 2. Obliczamy procent dnia (0.0 do 1.0)
        // 0.0 = P�noc (00:00), 0.5 = Po�udnie (12:00)
        float timePercent = currentTime / 24f;

        // 3. Obracanie S�o�ca
        // Chcemy, �eby o sunriseHour (np. 6:00) s�o�ce by�o na horyzoncie (0 stopni)
        // 24h = 360 stopni.
        // Wz�r: (Time - Sunrise) * 15 stopni/h
        float sunAngle = (currentTime - sunriseHour) * 15f;

        // Ustawiamy rotacj�. O� X to wysoko�� s�o�ca. O� Y to kierunek cienia.
        sunLight.transform.rotation = Quaternion.Euler(sunAngle, sunYRotation, 0);

        // 4. Zmiana Kolor�w i Jasno�ci
        // Evaluate bierze warto�� 0-1, wi�c u�ywamy timePercent

        sunLight.color = sunColor.Evaluate(timePercent);
        sunLight.intensity = sunIntensity.Evaluate(timePercent);

        // 5. Zmiana Ambientu (�wiat�a otoczenia)
        // To wa�ne, �eby w nocy nie by�o idealnie czarno w cieniach
        RenderSettings.ambientLight = skyColor.Evaluate(timePercent);
    }
}