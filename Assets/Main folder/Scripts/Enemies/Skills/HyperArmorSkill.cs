using UnityEngine;

public class HyperArmorSkill : EnemySkill
{
    [Header("Konfiguracja")]
    public int charges = 3; // Ile strza³ów neguje
    public GameObject shieldVFX; // Opcjonalnie efekt

    public override float OnBeforeDamageCalculation(float incomingDamage, DamageType type)
    {
        if (charges > 0)
        {
            charges--;
            Debug.Log($"{name} zablokowa³ obra¿enia Hiperpancerzem! Pozosta³o: {charges}");
            // Zwracamy 0 obra¿eñ
            return 0f;
        }
        return incomingDamage;
    }
}