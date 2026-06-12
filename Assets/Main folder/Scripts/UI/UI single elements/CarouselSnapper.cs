using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

[RequireComponent(typeof(ScrollRect))]
public class CarouselSnapper : MonoBehaviour, IBeginDragHandler, IEndDragHandler
{
    [Header("Referencje")]
    public RectTransform viewport; 
    public RectTransform content;  

    [Header("Ustawienia Karuzeli")]
    public float snapSpeed = 0.3f;
    public Ease snapEase = Ease.OutQuad;

    private ScrollRect scrollRect;
    private bool isDragging = false;

    // Event wysyłający informację, który kafelek jest teraz na środku
    public event System.Action<GameObject> OnElementCentered;

    void Awake()
    {
        scrollRect = GetComponent<ScrollRect>();
    }

    void Update()
    {
        if (isDragging) return;

        // Jeśli lista wciąż "leci" siłą rozpędu, czekamy aż zwolni
        if (Mathf.Abs(scrollRect.velocity.x) > 50f) return;

        // Automatyczne dociąganie, gdy lista prawie się zatrzyma (po scrollowaniu)
        if (Mathf.Abs(scrollRect.velocity.x) > 0.1f && Mathf.Abs(scrollRect.velocity.x) <= 50f)
        {
            scrollRect.velocity = Vector2.zero; 
            SnapToClosest();
        }
    }

    // --- INTERFEJSY PRZECIĄGANIA ---

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = true;
        content.DOKill(); // Przerywa animację, jeśli gracz złapie w trakcie
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        isDragging = false;
    }

    // =========================================================================
    // NOWOŚĆ: AUTOMATYCZNE PODPINANIE KLIKNIĘĆ
    // =========================================================================
    
    /// <summary>
    /// Wywołaj tę funkcję ze skryptu menu, zaraz po wygenerowaniu kafelków.
    /// Karuzela sama znajdzie przyciski i podepnie pod nie kliknięcie!
    /// </summary>
    public void RegisterGeneratedButtons()
    {
        foreach (RectTransform child in content)
        {
            Button btn = child.GetComponent<Button>();
            if (btn != null)
            {
                // Usuwamy stare eventy, by się nie dublowały przy odświeżaniu listy
                btn.onClick.RemoveAllListeners();
                
                // Kiedy ktoś kliknie w ten przycisk -> wyśrodkuj go
                btn.onClick.AddListener(() => CenterOnElement(child));
            }
        }

        // Opcjonalnie: od razu dociągnij pierwszy element na środek po wygenerowaniu
        if (content.childCount > 0)
        {
            CenterOnElement((RectTransform)content.GetChild(0));
        }
    }

    // =========================================================================
    // LOGIKA WYŚRODKOWANIA
    // =========================================================================

    private void SnapToClosest()
    {
        if (content.childCount == 0) return;

        RectTransform closestChild = null;
        float minDistance = float.MaxValue;
        
        Vector3 viewportCenter = viewport.position;

        foreach (RectTransform child in content)
        {
            if (!child.gameObject.activeInHierarchy) continue;

            float distance = Mathf.Abs(child.position.x - viewportCenter.x);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestChild = child;
            }
        }

        if (closestChild != null)
        {
            CenterOnElement(closestChild);
        }
    }

    public void CenterOnElement(RectTransform targetChild)
    {
        scrollRect.velocity = Vector2.zero;
        content.DOKill();

        // Matematyka do wyliczenia idealnego środka:
        Vector3 viewportCenterLocal = viewport.InverseTransformPoint(viewport.position);
        Vector3 childLocal = viewport.InverseTransformPoint(targetChild.position);
        
        float differenceX = viewportCenterLocal.x - childLocal.x;
        float newTargetX = content.anchoredPosition.x + differenceX;

        // Odpalamy płynną animację
        content.DOAnchorPosX(newTargetX, snapSpeed)
               .SetEase(snapEase)
               .SetUpdate(true)
               .OnComplete(() => 
        {
            // Powiadamiamy inne skrypty, że ten konkretny kafelek zaparkował na środku
            OnElementCentered?.Invoke(targetChild.gameObject);
        });
    }
}