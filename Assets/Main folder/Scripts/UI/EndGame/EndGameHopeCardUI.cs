using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Pojedyncza karta zdobytej Nadziei w panelu Scroll View na ekranie końcowym gry.
/// Wyświetla zgrupowany tytuł, zsumowaną liczbę punktów, klejnot rzadkości
/// oraz obsługuje animację pojawienia się i tooltip po najechaniu kursorem.
/// </summary>
public class EndGameHopeCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Referencje UI")]
    [SerializeField] private TextMeshProUGUI txtTitle;
    [SerializeField] private TextMeshProUGUI txtAmount;
    [SerializeField] private Image imgRarityGem;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Hover")]
    [SerializeField] private float hoverScale = 1.05f;

    private HopeSessionManager.GroupedHopeCardData currentData;
    private Action<HopeSessionManager.GroupedHopeCardData, RectTransform> onHoverEnter;
    private Action onHoverExit;
    private Coroutine revealCoroutine;
    private bool isHovered = false;

    private void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
    }

    /// <summary>
    /// Wypełnia kartę danymi zgrupowanego rekordu Nadziei.
    /// </summary>
    public void Setup(
        HopeSessionManager.GroupedHopeCardData data,
        Sprite raritySprite,
        Color rarityFallbackColor,
        Action<HopeSessionManager.GroupedHopeCardData, RectTransform> hoverEnterCallback = null,
        Action hoverExitCallback = null)
    {
        currentData = data;
        onHoverEnter = hoverEnterCallback;
        onHoverExit = hoverExitCallback;
        isHovered = false;
        transform.localScale = Vector3.one;

        gameObject.SetActive(true);

        if (txtTitle != null)
        {
            txtTitle.text = data.title;
        }

        if (txtAmount != null)
        {
            txtAmount.text = $"+{data.totalAmount}";
        }

        if (imgRarityGem != null)
        {
            if (raritySprite != null)
            {
                imgRarityGem.sprite = raritySprite;
                imgRarityGem.color = Color.white;
            }
            else
            {
                imgRarityGem.color = rarityFallbackColor;
            }
        }

        SnapToRevealed();
    }

    public void PrepareForReveal()
    {
        if (revealCoroutine != null)
        {
            StopCoroutine(revealCoroutine);
            revealCoroutine = null;
        }
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        transform.localScale = Vector3.one * 0.85f;
    }

    public void PlayReveal(float duration = 0.22f)
    {
        if (!gameObject.activeInHierarchy)
        {
            SnapToRevealed();
            return;
        }
        if (revealCoroutine != null) StopCoroutine(revealCoroutine);
        revealCoroutine = StartCoroutine(RevealRoutine(duration));
    }

    public void SnapToRevealed()
    {
        if (revealCoroutine != null)
        {
            StopCoroutine(revealCoroutine);
            revealCoroutine = null;
        }
        if (canvasGroup != null) canvasGroup.alpha = 1f;
        transform.localScale = isHovered ? Vector3.one * hoverScale : Vector3.one;
    }

    private IEnumerator RevealRoutine(float duration)
    {
        float d = Mathf.Max(0.01f, duration);
        float elapsed = 0f;
        while (elapsed < d)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / d);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            if (canvasGroup != null) canvasGroup.alpha = eased;
            float targetScale = isHovered ? hoverScale : 1f;
            transform.localScale = Vector3.Lerp(Vector3.one * 0.85f, Vector3.one * targetScale, eased);
            yield return null;
        }
        SnapToRevealed();
    }

    public void Hide()
    {
        if (revealCoroutine != null)
        {
            StopCoroutine(revealCoroutine);
            revealCoroutine = null;
        }
        isHovered = false;
        gameObject.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        if (revealCoroutine == null)
        {
            transform.localScale = Vector3.one * hoverScale;
        }
        onHoverEnter?.Invoke(currentData, transform as RectTransform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        if (revealCoroutine == null)
        {
            transform.localScale = Vector3.one;
        }
        onHoverExit?.Invoke();
    }
}
