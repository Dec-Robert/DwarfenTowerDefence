using UnityEngine;

public class HyperArmorSkill : EnemySkill
{
    [Header("Konfiguracja")]
    public int charges = 3; // Ile strza��w neguje
    public GameObject shieldVFX; // Opcjonalnie efekt

    public override float OnBeforeDamageCalculation(float incomingDamage, DamageType type)
    {
        if (charges > 0)
        {
            charges--;
            Debug.Log($"{name} zablokował obra�enia Hiperpancerzem! Pozosta�o: {charges}");
            // Zwracamy 0 obra�e�
            return 0f;
        }
        return incomingDamage;
    }
}