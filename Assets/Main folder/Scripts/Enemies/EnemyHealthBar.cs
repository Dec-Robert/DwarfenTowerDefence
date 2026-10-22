using System;
using UnityEngine;

/// <summary>
/// Komponent paska zdrowia w przestrzeni świata (World-Space Billboard).
/// Kompensuje lokalną skalę rodzica, dzięki czemu ma zawsze stały rozmiar w świecie (96x24),
/// niezależnie od tego czy wróg jest normalny, elitarny czy podzielonym blobem.
/// Aktualizuje parametry wizualne przez MaterialPropertyBlock (zero alokacji GC).
/// </summary>
public class EnemyHealthBar : MonoBehaviour
{
    [Header("Referencje")]
    public EnemyStats stats;

    [Tooltip("Lokalne przesunięcie paska nad wrogiem")]
    public Vector3 offset = new Vector3(0, 1.5f, 0);

    // Zachowane dla kompatybilności wstecznej z prefabami
    [SerializeField, HideInInspector] private UnityEngine.Object styleSheet;

    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    private MaterialPropertyBlock propertyBlock;

    private static Mesh s_QuadMesh;
    private static Camera s_MainCamera;

    private static readonly int s_BarColorId = Shader.PropertyToID("_BarColor");
    private static readonly int s_BgColorId = Shader.PropertyToID("_BgColor");
    private static readonly int s_BorderColorId = Shader.PropertyToID("_BorderColor");
    private static readonly int s_DividerColorId = Shader.PropertyToID("_DividerColor");
    private static readonly int s_FillRatioId = Shader.PropertyToID("_FillRatio");
    private static readonly int s_SegmentCountId = Shader.PropertyToID("_SegmentCount");
    private static readonly int s_BorderThicknessId = Shader.PropertyToID("_BorderThickness");
    private static readonly int s_DividerThicknessId = Shader.PropertyToID("_DividerThickness");
    private static readonly int s_BarPixelSizeId = Shader.PropertyToID("_BarPixelSize");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_QuadMesh = null;
        s_MainCamera = null;
    }

    public void Awake()
    {
        InitializeComponents();
    }

    public void InitializeComponents()
    {
        if (meshFilter == null)
            meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
            meshFilter = gameObject.AddComponent<MeshFilter>();

        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            meshRenderer = gameObject.AddComponent<MeshRenderer>();

        if (meshFilter != null && (meshFilter.sharedMesh == null || meshFilter.sharedMesh.vertexCount == 0))
        {
            meshFilter.sharedMesh = GetOrCreateQuadMesh();
        }

        if (meshRenderer != null)
        {
            if (meshRenderer.sharedMaterial == null)
                meshRenderer.sharedMaterial = EnemyHealthBarManager.GetSharedBarMaterial();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        if (stats == null)
        {
            stats = GetComponentInParent<EnemyStats>();
        }

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();
    }

    public void OnEnable()
    {
        InitializeComponents();

        if (stats != null)
        {
            stats.OnHealthChanged -= HandleHealthChanged;
            stats.OnHealthChanged += HandleHealthChanged;
            UpdateBar(stats.GetCurrentHealth(), stats.GetMaxHealth());
        }

        EnemyHealthBarManager.OnVisibilityChanged -= HandleVisibilityChanged;
        EnemyHealthBarManager.OnVisibilityChanged += HandleVisibilityChanged;

        UpdateVisibility(EnemyHealthBarManager.GetGlobalVisibility());
    }

    public void OnDisable()
    {
        if (stats != null)
        {
            stats.OnHealthChanged -= HandleHealthChanged;
        }
        EnemyHealthBarManager.OnVisibilityChanged -= HandleVisibilityChanged;
    }

    private void LateUpdate()
    {
        UpdateTransform();
    }

    public void UpdateTransform()
    {
        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();

        if (meshRenderer != null && !meshRenderer.enabled) return;

        // 1. Pozycjonowanie z uwzględnieniem offsetu
        if (transform.parent != null)
        {
            transform.localPosition = offset;
        }

        // 2. Billboard - obrót w stronę kamery
        if (s_MainCamera == null || !s_MainCamera.isActiveAndEnabled)
        {
            s_MainCamera = Camera.main;
        }

        if (s_MainCamera != null)
        {
            transform.rotation = s_MainCamera.transform.rotation;
        }

        // 3. Kompensacja skali rodzica - stały rozmiar w świecie o proporcjach 96x24
        Vector2 pixelSize = EnemyHealthBarManager.GetBarPixelSize();
        float worldScale = EnemyHealthBarManager.GetWorldPixelScale();
        float targetWorldWidth = pixelSize.x * worldScale;
        float targetWorldHeight = pixelSize.y * worldScale;

        Vector3 parentLossy = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        float localX = targetWorldWidth / Mathf.Max(0.0001f, Mathf.Abs(parentLossy.x));
        float localY = targetWorldHeight / Mathf.Max(0.0001f, Mathf.Abs(parentLossy.y));
        float localZ = 1f / Mathf.Max(0.0001f, Mathf.Abs(parentLossy.z));

        transform.localScale = new Vector3(localX, localY, localZ);
    }

    public void UpdateBar(float currentHealth, float maxHealth)
    {
        InitializeComponents();
        if (meshRenderer == null) return;

        float fillRatio = maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;

        EnemyHealthBarManager.EvaluateTier(maxHealth, out Color barColor, out float segmentHpValue, out float visualSegmentCount);
        EnemyHealthBarManager.GetStylingColors(out Color bg, out Color border, out Color divider);

        propertyBlock.SetColor(s_BarColorId, barColor);
        propertyBlock.SetColor(s_BgColorId, bg);
        propertyBlock.SetColor(s_BorderColorId, border);
        propertyBlock.SetColor(s_DividerColorId, divider);
        propertyBlock.SetFloat(s_FillRatioId, fillRatio);
        propertyBlock.SetFloat(s_SegmentCountId, visualSegmentCount);
        propertyBlock.SetFloat(s_BorderThicknessId, EnemyHealthBarManager.GetBorderThickness());
        propertyBlock.SetFloat(s_DividerThicknessId, EnemyHealthBarManager.GetDividerThickness());
        propertyBlock.SetVector(s_BarPixelSizeId, EnemyHealthBarManager.GetBarPixelSize());

        meshRenderer.SetPropertyBlock(propertyBlock);
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        UpdateBar(currentHealth, maxHealth);
    }

    private void HandleVisibilityChanged(bool visible)
    {
        UpdateVisibility(visible);
    }

    public void UpdateVisibility(bool visible)
    {
        if (meshRenderer == null)
            meshRenderer = GetComponent<MeshRenderer>();

        if (meshRenderer != null)
        {
            meshRenderer.enabled = visible;
        }
    }

    private static Mesh GetOrCreateQuadMesh()
    {
        if (s_QuadMesh == null)
        {
            s_QuadMesh = new Mesh
            {
                name = "EnemyHealthBar_QuadMesh"
            };

            Vector3[] vertices = new Vector3[4]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3( 0.5f, -0.5f, 0f),
                new Vector3(-0.5f,  0.5f, 0f),
                new Vector3( 0.5f,  0.5f, 0f)
            };

            Vector2[] uvs = new Vector2[4]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            };

            int[] triangles = new int[6]
            {
                0, 2, 1,
                2, 3, 1
            };

            s_QuadMesh.vertices = vertices;
            s_QuadMesh.uv = uvs;
            s_QuadMesh.triangles = triangles;
            s_QuadMesh.RecalculateNormals();
            s_QuadMesh.RecalculateBounds();
        }

        return s_QuadMesh;
    }
}
