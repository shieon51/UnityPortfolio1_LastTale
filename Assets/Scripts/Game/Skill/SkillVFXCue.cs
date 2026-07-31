
// SkillVFXCue.cs (SkillBase 안에 포함될 데이터)
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SkillVFXCue
{
    public string cueId;  // Animation Event가 부를 이름 (예: "swing", "impact")
    [Tooltip("기본 이펙트 (레벨 오버라이드가 없을 때)")]
    public string vfxKey; // PoolManager가 찾을 실제 풀 이름
    [Tooltip("캐릭터 기준 상대 위치. 히트박스 오프셋과 같은 개념")]
    public Vector2 spawnOffset;
    public bool followCaster; // true: 시전자를 따라다님(Q 궤적), false: 스폰 위치 고정(W/NPC 이펙트)

    [Header("레벨별 오버라이드 (선택)")]
    public List<SkillVFXLevelOverride> levelOverrides = new();

    public string ResolveVFXKey(int level)
    {
        string best = vfxKey;
        int bestMinLevel = -1;
        foreach (var ov in levelOverrides)
            if (ov.minLevel <= level && ov.minLevel > bestMinLevel) { best = ov.vfxKey; bestMinLevel = ov.minLevel; }
        return best;
    }
}

[System.Serializable]
public class SkillVFXLevelOverride
{
    public int minLevel;
    public string vfxKey;
}