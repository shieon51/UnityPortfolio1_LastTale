using UnityEngine;

[CreateAssetMenu(menuName = "LastMarchan/Combat/Formulas/Parry Chance")]
public class ParryChanceFormula : ScriptableObject
{
    [Range(0f, 1f)] public float baseChance = 0.15f;
    public float agiScalingPerPoint = 0.01f;
    [Range(0f, 1f)] public float minChance = 0.05f;
    [Range(0f, 1f)] public float maxChance = 0.6f; // ★ 상한선 — 위 설계검토 참고

    public float CalculateChance(int defenderAgi, int attackerAgi)
    {
        int diff = defenderAgi - attackerAgi;
        float chance = baseChance + diff * agiScalingPerPoint;
        return Mathf.Clamp(chance, minChance, maxChance);
    }
}