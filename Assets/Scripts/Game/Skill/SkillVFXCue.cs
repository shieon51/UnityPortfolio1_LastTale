
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

    [Header("모션(컨텍스트)별 오버라이드 — 히트박스 오버라이드와 같은 개념")]
    public List<SkillVFXContextOverride> contextOverrides = new();

    [Header("레벨별 오버라이드 (선택)")]
    public List<SkillVFXLevelOverride> levelOverrides = new();

    public string ResolveVFXKey(int level = 1) => ResolveVFXKey(MovementContext.Grounded, level); // NPC 등 컨텍스트 개념이 없는 곳에서 사용


    // 1) 컨텍스트로 '이 상황의 기본형'을 먼저 정하고, 2) 그 위에 레벨 오버라이드를 얹음
    public string ResolveVFXKey(MovementContext context, int level = 1)
    {
        string resolved = vfxKey;
        foreach (var co in contextOverrides)
            if (co.context == context) { resolved = co.vfxKey; break; }

        string best = resolved;
        int bestMinLevel = -1;
        foreach (var lv in levelOverrides)
            if (lv.minLevel <= level && lv.minLevel > bestMinLevel) { best = lv.vfxKey; bestMinLevel = lv.minLevel; }

        return best;
    }

    public Vector2 ResolveSpawnOffset(MovementContext context)
    {
        foreach (var co in contextOverrides)
            if (co.context == context) return co.spawnOffset;
        return spawnOffset;
    }
}

[System.Serializable]
public class SkillVFXContextOverride
{
    public MovementContext context;
    public string vfxKey;
    public Vector2 spawnOffset;
}

[System.Serializable]
public class SkillVFXLevelOverride
{
    public int minLevel;
    public string vfxKey;
}