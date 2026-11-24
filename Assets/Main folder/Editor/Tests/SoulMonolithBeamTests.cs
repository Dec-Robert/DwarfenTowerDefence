using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

[TestFixture]
public class SoulMonolithBeamTests
{
    private readonly List<Object> createdObjects = new List<Object>();

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SoulMonolithBuilder.SoulMonolithPrefabPath);
        TowerController controller = prefab != null ? prefab.GetComponent<TowerController>() : null;
        TowerBeamWeapon beam = prefab != null ? prefab.GetComponent<TowerBeamWeapon>() : null;

        if (AssetDatabase.LoadAssetAtPath<GameObject>(SoulMonolithBuilder.OriginVfxPrefabPath) == null ||
            AssetDatabase.LoadAssetAtPath<GameObject>(SoulMonolithBuilder.ImpactVfxPrefabPath) == null ||
            controller == null || beam == null || controller.BeamWeapon == null || !controller.TillDeath)
        {
            SoulMonolithBuilder.BuildAll(saveAssets: true);
        }
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                Object.DestroyImmediate(createdObjects[i]);
            }
        }
        createdObjects.Clear();
    }

    [Test]
    public void SoulMonolithAssets_ExistAndAreConfigured()
    {
        Shader beamShader = Shader.Find(SoulMonolithBuilder.BeamShaderName);
        Assert.IsNotNull(beamShader, $"Nie znaleziono shadera {SoulMonolithBuilder.BeamShaderName}.");

        Material beamMat = AssetDatabase.LoadAssetAtPath<Material>(SoulMonolithBuilder.BeamMaterialPath);
        Material particleMat = AssetDatabase.LoadAssetAtPath<Material>(SoulMonolithBuilder.ParticlesMaterialPath);
        Assert.IsNotNull(beamMat);
        Assert.IsNotNull(particleMat);
        Assert.AreEqual(beamShader, beamMat.shader);
        Assert.AreEqual(0f, particleMat.GetFloat("_LobeAmplitude"), 0.0001f, "Cząsteczki powinny być gładkimi okręgami bez gwiazdek.");

        TowerData data = AssetDatabase.LoadAssetAtPath<TowerData>(SoulMonolithBuilder.SoulMonolithDataPath);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SoulMonolithBuilder.SoulMonolithPrefabPath);
        Assert.IsNotNull(data);
        Assert.IsNotNull(prefab);
        Assert.AreEqual(prefab, data.prefab, "7. Soul Monolith.asset powinien wskazywać na Soul Monolith Pref.prefab.");
        Assert.IsNull(data.bulletPrefab, "Wieża promieniowa nie powinna używać bulletPrefab.");
        Assert.IsTrue(data.tillDeath, "Monolit Dusz powinien mieć tillDeath = true.");
        Assert.AreEqual(DamageType.Magic, data.damageType);
        Assert.AreEqual(50f, data.baseDamage, 0.001f);
        Assert.AreEqual(10f, data.fireRate, 0.001f);
        Assert.AreEqual(3f, data.baseRange, 0.001f);

        TowerController controller = prefab.GetComponent<TowerController>();
        TowerBeamWeapon beam = prefab.GetComponent<TowerBeamWeapon>();
        Assert.IsNotNull(controller);
        Assert.IsNotNull(beam);
        Assert.AreEqual(beam, controller.BeamWeapon);
        Assert.IsTrue(controller.TillDeath);
        Assert.IsNotNull(controller.firePoint);
        Assert.IsNotNull(beam.BeamLine);
        Assert.AreEqual(beamMat, beam.BeamLine.sharedMaterial);
        Assert.IsNotNull(beam.OriginVfx);
        Assert.IsNotNull(beam.ImpactVfx);
        Assert.LessOrEqual(beam.OriginVfx.main.startSize.constantMax, 0.17f);
        Assert.LessOrEqual(beam.ImpactVfx.main.startSize.constantMax, 0.17f);
    }

    [Test]
    public void BeamWidth_Scales10To100PercentWithDamage_AndSmoothlyTo150PercentOnOvercharge()
    {
        GameObject go = new GameObject("BeamWidthTest");
        createdObjects.Add(go);
        TowerBeamWeapon beam = go.AddComponent<TowerBeamWeapon>();

        Assert.AreEqual(0.22f * 0.10f, beam.GetBeamWidthForMultiplier(0.10f), 0.0001f, "10% obrażeń powinno dać 10% szerokości bazowej.");
        Assert.AreEqual(0.22f * 0.50f, beam.GetBeamWidthForMultiplier(0.50f), 0.0001f, "50% obrażeń powinno dać 50% szerokości bazowej.");
        Assert.AreEqual(0.22f * 1.00f, beam.GetBeamWidthForMultiplier(1.00f), 0.0001f, "100% obrażeń powinno dać 100% szerokości bazowej.");
        Assert.AreEqual(0.22f * 1.25f, beam.GetBeamWidthForMultiplier(1.75f), 0.0001f, "175% obrażeń powinno dać połowę wzrostu przeciążenia (125%).");
        Assert.AreEqual(0.22f * 1.50f, beam.GetBeamWidthForMultiplier(2.50f), 0.0001f, "250% obrażeń powinno dać 150% szerokości bazowej.");
    }

    [Test]
    public void BeamRampFormula_MatchesSpecAndCapsAt250Percent()
    {
        GameObject go = new GameObject("BeamFormulaTest");
        createdObjects.Add(go);
        TowerBeamWeapon beam = go.AddComponent<TowerBeamWeapon>();

        Assert.AreEqual(0.10f, beam.GetMultiplierForTick(1), 0.0001f, "Tick 1 powinien zaczynać od 10%.");
        Assert.AreEqual(0.50f, beam.GetMultiplierForTick(5), 0.0001f, "Tick 5 powinien wynosić 50%.");
        Assert.AreEqual(1.00f, beam.GetMultiplierForTick(10), 0.0001f, "Tick 10 (po 1s) powinien osiągnąć 100%.");
        Assert.AreEqual(1.02f, beam.GetMultiplierForTick(11), 0.0001f, "Tick 11 powinien dodać +2% (102%).");
        Assert.AreEqual(1.20f, beam.GetMultiplierForTick(20), 0.0001f, "Dodatkowe 10 ticków powinno dać +20% (120%).");
        Assert.AreEqual(1.40f, beam.GetMultiplierForTick(30), 0.0001f, "Tick 30 powinien dać 140%.");
        Assert.AreEqual(2.50f, beam.GetMultiplierForTick(85), 0.0001f, "Tick 85 powinien osiągnąć limit 250%.");
        Assert.AreEqual(2.50f, beam.GetMultiplierForTick(150), 0.0001f, "Powyżej 85 ticków obrażenia nie mogą przekroczyć 250%.");
    }

    [Test]
    public void BeamWeapon_DealsScaledMagicDamageAndSlowsTarget_ResetsOnTargetChange()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SoulMonolithBuilder.SoulMonolithPrefabPath);
        GameObject towerGo = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
        createdObjects.Add(towerGo);

        TowerController controller = towerGo.GetComponent<TowerController>();
        TowerBeamWeapon beam = controller.BeamWeapon;
        Material sharedBeamMat = beam.BeamLine.sharedMaterial;

        TowerStats stats = new TowerStats
        {
            damage = 50f,
            fireRate = 10f,
            range = 3f,
            damageType = DamageType.Magic,
            criticalChancel = 0f,
            criticalDamageMultiplier = 2f
        };

        EnemyStats enemyA = CreateTestEnemy("EnemyA", new Vector3(2f, 0f, 0f), 10000f, 4f, progress: 1);
        EnemyStats enemyB = CreateTestEnemy("EnemyB", new Vector3(2.5f, 0f, 0f), 10000f, 4f, progress: 2);

        // 10 ticków po 0.1s (1.0s): 10% + 20% + ... + 100% = 550% z 5 dmg/tick = 27.5 dmg
        for (int i = 0; i < 10; i++)
        {
            beam.TickBeam(enemyA.transform, stats, null, controller.firePoint, 0.1f);
        }

        Assert.AreEqual(10, beam.CurrentTickCount);
        Assert.AreEqual(1.00f, beam.CurrentDamageMultiplier, 0.0001f);
        Assert.AreEqual(10000f - 27.5f, enemyA.GetCurrentHealth(), 0.05f, "Po 10 tickach suma obrażeń powinna wynosić 27.5.");
        Assert.AreEqual(4f * 0.65f, enemyA.Walker.EffectiveSpeed, 0.001f, "Promień powinien spowolnić cel o 35%.");
        Assert.IsTrue(beam.BeamLine.enabled);
        Assert.AreSame(sharedBeamMat, beam.BeamLine.sharedMaterial, "Aktualizacja _Charge01 przez MPB nie może tworzyć kopii materiału.");

        // Kolejne 10 ticków (11..20): 102%..120% -> średnio 111% -> 55.5 dmg (łącznie 83.0 dmg)
        for (int i = 0; i < 10; i++)
        {
            beam.TickBeam(enemyA.transform, stats, null, controller.firePoint, 0.1f);
        }

        Assert.AreEqual(20, beam.CurrentTickCount);
        Assert.AreEqual(1.20f, beam.CurrentDamageMultiplier, 0.0001f);
        Assert.AreEqual(10000f - 83.0f, enemyA.GetCurrentHealth(), 0.05f, "Kolejne 10 ticków powinno zadać 55.5 dmg.");

        // Zmiana celu resetuje licznik ticków i obrażenia wracają na 10%
        beam.TickBeam(enemyB.transform, stats, null, controller.firePoint, 0.1f);
        Assert.AreEqual(1, beam.CurrentTickCount);
        Assert.AreEqual(0.10f, beam.CurrentDamageMultiplier, 0.0001f);
        Assert.AreEqual(10000f - 0.5f, enemyB.GetCurrentHealth(), 0.05f, "Pierwszy tick na nowym celu powinien zadać 10% (0.5 dmg).");
    }

    [Test]
    public void TowerController_TillDeath_KeepsCurrentTargetUntilOutOfRangeOrDead()
    {
        TowerData data = AssetDatabase.LoadAssetAtPath<TowerData>(SoulMonolithBuilder.SoulMonolithDataPath);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SoulMonolithBuilder.SoulMonolithPrefabPath);
        GameObject towerGo = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
        createdObjects.Add(towerGo);

        TowerController controller = towerGo.GetComponent<TowerController>();
        controller.towerData = data;
        InvokePrivate(controller, "Awake");
        InvokePrivate(controller, "Start");

        EnemyStats enemy1 = CreateTestEnemy("SlowFront", new Vector3(1.5f, 0f, 0f), 1000f, 3f, progress: 5);
        EnemyStats enemy2 = CreateTestEnemy("Overtaker", new Vector3(2.0f, 0f, 0f), 1000f, 3f, progress: 3);

        // Pierwsze namierzenie -> wybiera enemy1 (wyższy progress)
        InvokePrivate(controller, "UpdateTarget");
        Assert.AreEqual(enemy1.transform, controller.CurrentTarget);

        // enemy2 wyprzedza enemy1 na ścieżce, ale enemy1 żyje i jest w zasięgu -> tillDeath utrzymuje enemy1
        SetWalkerProgress(enemy2.Walker, 10);
        InvokePrivate(controller, "UpdateTarget");
        Assert.AreEqual(enemy1.transform, controller.CurrentTarget, "Przy tillDeath=true wieża nie powinna przeskoczyć na wyprzedzającego wroga.");

        // Kiedy wyłączymy tillDeath -> wieża przełącza się na nowy priorytet
        controller.SetTillDeath(false);
        InvokePrivate(controller, "UpdateTarget");
        Assert.AreEqual(enemy2.transform, controller.CurrentTarget, "Przy tillDeath=false wieża powinna przełączyć się na nowy priorytet.");

        // Włączamy ponownie tillDeath i wyprowadzamy enemy2 poza zasięg (> 6.0f) -> przełącza na enemy1
        controller.SetTillDeath(true);
        enemy2.transform.position = new Vector3(20f, 0f, 0f);
        InvokePrivate(controller, "UpdateTarget");
        Assert.AreEqual(enemy1.transform, controller.CurrentTarget, "Po wyjściu celu z zasięgu wieża powinna przełączyć się na cel w zasięgu.");
    }

    [Test]
    public void EnemyWalker_TowerSlow_DoesNotStackOrOverwriteSpellSlow_AndRespectsFloor()
    {
        EnemyStats enemy = CreateTestEnemy("WalkerTest", Vector3.zero, 500f, 10f, progress: 1);
        EnemyWalker walker = enemy.Walker;

        // Dwa Monolity aplikują 35% slow -> nie sumują się do 70%
        walker.ApplyTowerSlow(0.35f, 0.25f);
        walker.ApplyTowerSlow(0.35f, 0.25f);
        Assert.AreEqual(6.5f, walker.EffectiveSpeed, 0.001f);

        // Zaklęcie Druida 60% (0.40x) + slow z wieży 35% (0.65x) -> 0.40 * 0.65 = 0.26x, ale podłoga wynosi 0.30x
        walker.ApplySpellSlow(0.60f, 1.0f);
        Assert.AreEqual(3.0f, walker.EffectiveSpeed, 0.001f, "Łączne spowolnienie bez zamrożenia nie powinno spaść poniżej 30% prędkości bazowej.");
    }

    private EnemyStats CreateTestEnemy(string name, Vector3 pos, float hp, float speed, int progress)
    {
        GameObject go = new GameObject(name);
        go.transform.position = pos;
        createdObjects.Add(go);

        EnemyStats stats = go.AddComponent<EnemyStats>();
        stats.SetHealthManually(hp);

        EnemyWalker walker = go.AddComponent<EnemyWalker>();
        walker.speed = speed;
        SetWalkerProgress(walker, progress);

        InvokePrivate(stats, "Awake");
        InvokePrivate(stats, "OnEnable");
        return stats;
    }

    private static void SetWalkerProgress(EnemyWalker walker, int progress)
    {
        FieldInfo field = typeof(EnemyWalker).GetField("targetIndex", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field);
        field.SetValue(walker, progress);
    }

    private static void InvokePrivate(object instance, string methodName)
    {
        MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, $"Nie znaleziono metody {methodName}.");
        method.Invoke(instance, null);
    }
}
