using UnityEngine;

public class DayNightCycle : MonoBehaviour
{
    [Header("Referencje")]
    public Light sunLight; //Main light

    [Header("Ustawienia Obrotu")]
    public float sunriseHour = 6f;
    public float sunYRotation = -30f;

    [Header("World look")]
    public Gradient sunColor; // color of the sun
    public Gradient skyColor; // Color of the sky
    public AnimationCurve sunIntensity; // Intensity of the sun

    private void Start()
    {
        if (sunLight == null)
        {
            sunLight = GetComponent<Light>();
        }
    }
    // Zmiana koloru nieba do ustalenia
    private void Update()
    {
        

        
    }
}