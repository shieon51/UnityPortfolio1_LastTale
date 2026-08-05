using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// NPCSkillBase.cs  — Player의 SkillBase와 같은 사상, 선택 방식만 다름
public abstract class NPCSkillBase : NPCActionBase
{
    [System.Serializable]
    public struct NPCSkillContextVariant
    {
        public MovementContext context;
        public string animStateName;
        public bool overrideHitbox;
        public Vector2 hitboxOffsetOverride;
        public Vector2 hitboxSizeOverride;
    }

    [Header("Context Variants (지상/공중/비행별 모션·판정 차이)")]
    public List<NPCSkillContextVariant> contextVariants = new();

    [Header("Context Restriction")]
    [Tooltip("비워두면 모든 상황에서 사용 가능. 항목이 있으면 그 상황에서만 후보가 됨 (예: 비행 중에만 쓰는 스킬)")]
    public List<MovementContext> allowedContexts = new();

    //-----------------------------------------------------------------
    [Header("Data Table Link")]
    public string skillId;

    [Header("Basic Info")]
    public string skillName;
    public string animStateName;
    public int manaCost;

    [Header("Timing")]
    [Tooltip("스킬 결정 시점부터 실제 액티브(AE_ActiveStart)까지 걸리는 시간(초). 클립의 선딜 길이와 맞춰주세요.")]
    public float timeToActive = 0.5f;

    [Header("Telegraph")]
    public bool hasTelegraph = true;
    public float baseTelegraphDuration = 0.8f;

    [Header("Targeting")]
    public LayerMask targetableLayers;

    [Header("VFX Cues")]
    [Tooltip("Animation Event가 cueId로 호출하면, 여기 등록된 vfxKey로 이펙트가 재생됩니다.")]
    public List<SkillVFXCue> vfxCues = new List<SkillVFXCue>();

    public int GetManaCost(int level) => SkillDataManager.Instance?.GetLevelData(skillId, level)?.manaCost ?? manaCost;
    public float GetDamageMultiplier(int level) => SkillDataManager.Instance?.GetLevelData(skillId, level)?.damageMultiplier ?? 1f;

    public string ResolveAnimStateName(MovementContext context)
    {
        foreach (var v in contextVariants) if (v.context == context) return v.animStateName;
        return animStateName;
    }

    public (Vector2 offset, Vector2 size) ResolveContextHitbox(MovementContext context, Vector2 defaultOffset, Vector2 defaultSize)
    {
        foreach (var v in contextVariants)
            if (v.context == context && v.overrideHitbox) return (v.hitboxOffsetOverride, v.hitboxSizeOverride);
        return (defaultOffset, defaultSize);
    }

    protected bool IsContextAllowed(MovementContext context) => allowedContexts.Count == 0 || allowedContexts.Contains(context);

    public SkillVFXCue FindVFXCue(string cueId)
    {
        foreach (var cue in vfxCues)
            if (cue.cueId == cueId) return cue;
        return null;
    }

    protected void PlaySkillVFX(string cueId, NPC self)
    {
        var cue = FindVFXCue(cueId);
        if (cue == null) return;
        string vfxKey = cue.ResolveVFXKey(self.CurrentMovementContext, 1); // ★ 컨텍스트 반영
        if (string.IsNullOrEmpty(vfxKey)) return;

        float dir = self.SpriteRenderer.flipX ? -1f : 1f;
        Vector2 offset = cue.ResolveSpawnOffset(self.CurrentMovementContext);
        Vector2 worldOffset = new Vector2(offset.x * dir, offset.y);
        Transform followParent = cue.followCaster ? self.transform : null;
        VFXManager.Instance.Play(vfxKey, (Vector2)self.transform.position + worldOffset, dir, PoolType.Global, followParent);
    }
}