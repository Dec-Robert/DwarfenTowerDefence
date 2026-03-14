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
            Debug.Log($"{name} zablokował obra�enia Hiperpancerzem! Pozosta�o: {charges}");
            return 0f;
        }
        return incomingDamage;
    }
}