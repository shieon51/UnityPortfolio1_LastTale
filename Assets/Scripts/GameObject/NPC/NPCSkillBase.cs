using System.Collections.Generic;
using UnityEngine;

// NPCSkillBase.cs  — Player의 SkillBase와 같은 사상, 선택 방식만 다름
public abstract class NPCSkillBase : NPCActionBase
{
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

    [Header("Camera Cues")]
    public List<CameraCue> cameraCues = new();
    public CameraCue FindCameraCue(string cueId) => cameraCues.Find(c => c.cueId == cueId);

    public int GetManaCost(int level) => SkillDataManager.Instance?.GetLevelData(skillId, level)?.manaCost ?? manaCost;
    public float GetDamageMultiplier(int level) => SkillDataManager.Instance?.GetLevelData(skillId, level)?.damageMultiplier ?? 1f;

    public (Vector2 offset, Vector2 size) ResolveContextHitbox(MovementContext context, Vector2 defaultOffset, Vector2 defaultSize)
    {
        foreach (var v in contextVariants)
            if (v.context == context && v.overrideHitbox) return (v.hitboxOffsetOverride, v.hitboxSizeOverride);
        return (defaultOffset, defaultSize);
    }

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

        // 이펙트 사운드
        if (!string.IsNullOrEmpty(cue.sfxKey)) 
            SoundManager.Instance?.PlaySFX(cue.sfxKey);

        float dir = self.SpriteRenderer.flipX ? -1f : 1f;
        Vector2 offset = cue.ResolveSpawnOffset(self.CurrentMovementContext);
        Vector2 worldOffset = new Vector2(offset.x * dir, offset.y);
        Transform followParent = cue.followCaster ? self.transform : null;
        VFXManager.Instance.Play(vfxKey, (Vector2)self.transform.position + worldOffset, dir, PoolType.Global, followParent);
    }
}