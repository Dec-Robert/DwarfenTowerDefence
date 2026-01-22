using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewEnemy", menuName = "Tower Defense/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Wygl¹d")]
    public GameObject prefab;

    [Header("Balans Fali")]
    public int threatCost = 1;

    [Header("Podstawowe")]
    public string enemyName;
    public float baseHp = 100f;
    public float moveSpeed = 3f;

    [Header("Defensywa")]
    [Range(0, 100)] public float armor = 0f;
    [Range(0, 100)] public float magicResist = 0f;
    [Range(0, 100)] public float dodgeChance = 0f;

    [Header("Ranga")]
    public bool canBeElite = true;
    public float eliteSpawnChance = 10f;
    public float eliteHpMultiplier = 2.0f;
    public float eliteSpeedMultiplier = 1.3f;

    [Header("Umiejêtnoœci")]
    public List<GameObject> skillPrefabs;
}