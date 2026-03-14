using UnityEngine;

public class BarrierSkill : EnemySkill
{
    [Header("Konfiguracja Bariery")]
    public float maxBarrierHp = 500f;
    public DamageType absorbType = DamageType.Magic; // Jakie obrażenia pochłania (Any = pochłania wszystkie)
    public bool absorbAllTypes = false;

    [Header("Stan (Podgląd)")]
    [SerializeField] private float currentBarrierHp;
    public GameObject barrierVFX; // Bańka tarczy (np. sphere)

    private bool isBarrierActive = true;

    public override void Initialize(EnemyStats _stats)
    {
        base.Initialize(_stats);
        currentBarrierHp = maxBarrierHp;
        if (barrierVFX != null) barrierVFX.SetActive(true);
    }

    public override float OnBeforeDamageCalculation(float incomingDamage, DamageType type, TowerEntity sourceTower = null, bool isAoE = false)
    {
        if (!isBarrierActive) return incomingDamage;

        // Czy bariera reaguje na ten typ obrażeń?
        if (absorbAllTypes || type == absorbType)
        {
            if (incomingDamage >= currentBarrierHp)
            {
                // Przebito tarczę - reszta obrażeń przechodzi na wroga
                float leftoverDamage = incomingDamage - currentBarrierHp;
                BreakBarrier();
                return leftoverDamage;
            }
            else
            {
                // Tarcza wchłania całe uderzenie
                currentBarrierHp -= incomingDamage;
                return 0f; // Boss nie dostaje nic!
            }
        }

        // Zły typ obrażeń (np. strzelasz Fizycznie w Magiczną Tarczę) -> bariera to ignoruje, wróg obrywa!
        return incomingDamage;
    }

    private void BreakBarrier()
    {
        currentBarrierHp = 0f;
        isBarrierActive = false;
        if (barrierVFX != null) barrierVFX.SetActive(false);
        Debug.Log($"<color=cyan>Bariera wroga {name} została ZNISZCZONA!</color>");
    }
}