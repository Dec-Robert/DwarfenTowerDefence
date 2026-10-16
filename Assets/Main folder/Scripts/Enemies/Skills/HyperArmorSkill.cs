using UnityEngine;

public class HyperArmorSkill : EnemySkill
{
    [Header("Konfiguracja")]
    public int charges = 3; // Ile strzałów neguje
    public GameObject shieldVFX; // Opcjonalnie efekt

    public override float OnBeforeDamageCalculation(float incomingDamage, DamageType type, TowerEntity sourceTower = null, bool isAoE = false)
    {
        if (charges > 0)
        {
            charges--;
            Debug.Log($"{name} zablokowal obrazenia Hiperpancerzem! Pozostalo ladunkow: {charges}");
            return 0f;
        }
        return incomingDamage;
    }
}