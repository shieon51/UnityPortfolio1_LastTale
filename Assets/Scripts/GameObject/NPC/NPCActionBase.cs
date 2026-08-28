using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// NPCActionBase.cs — 스킬이든 순수 이동이든 모든 NPC 행동의 공통 조상
public abstract class NPCActionBase : ScriptableObject
{
    [System.Serializable]
    public struct NPCSkillContextVariant
    {
        public MovementContext context;
        public string animStateName;

        [Header("히트박스 오버라이드 (스킬 전용 — 이동/가드 액션은 이 필드들을 그냥 안 씀)")]
        public bool overrideHitbox;
        public Vector2 hitboxOffsetOverride;
        public Vector2 hitboxSizeOverride;
    }

    [Header("Context Variants (지상/공중/비행별 모션·판정 차이)")]
    public List<NPCSkillContextVariant> contextVariants = new();

    [Header("Context Restriction")]
    [Tooltip("비워두면 모든 상황에서 사용 가능. 항목이 있으면 그 상황에서만 후보가 됨 (예: 비행 중에만 쓰는 스킬)")]
    public List<MovementContext> allowedContexts = new();

    // ---------------------------------------------------------------------
    public string actionName;
    public float actionCooldown = 0f;
    [Tooltip("이 행동이 끝난 뒤 다음 판단까지의 빈틈(회복 시간)")]
    public float recoveryDuration = 0.5f;

    [Header("Safety")]
    [Tooltip("AE_ActiveStart/End 이벤트가 이 시간 안에 안 오면 강제로 진행 (이벤트 누락 시 무한 대기 방지)")]
    public float eventTimeoutSeconds = 1f;

    //private bool _activeStarted, _activeEnded;
    private bool _dashStarted, _hitboxStarted, _slideStarted, _actionEnded;

    public void OnDashStart() 
    { 
        _dashStarted = true; 
        Debug.Log($"[DBG Action] {name} OnDashStart at {Time.time:F3}"); // *
    }

    public void OnHitboxStart() 
    { 
        _hitboxStarted = true; 
        Debug.Log($"[DBG Action] {name} OnHitboxStart at {Time.time:F3}"); // *
    }

    public void OnSlideStart() => _slideStarted = true;
    public void OnActionEndEvent() => _actionEnded = true;

    protected IEnumerator WaitForDashStart() => WaitForFlag(() => _dashStarted, v => _dashStarted = v, "AE_DashStart");
    protected IEnumerator WaitForHitboxStart() => WaitForFlag(() => _hitboxStarted, v => _hitboxStarted = v, "AE_HitboxStart");
    protected IEnumerator WaitForSlideStart() => WaitForFlag(() => _slideStarted, v => _slideStarted = v, "AE_SlideStart");
    protected IEnumerator WaitForActionEndEvent() => WaitForFlag(() => _actionEnded, v => _actionEnded = v, "AE_ActionEnd");

    private IEnumerator WaitForFlag(System.Func<bool> isSet, System.Action<bool> setFlag, string eventName)
    {
        setFlag(false);
        float t = 0f;
        while (!isSet() && t < eventTimeoutSeconds) { t += Time.deltaTime; yield return null; }
        if (!isSet()) Debug.LogWarning($"[{name}] {eventName} 이벤트가 {eventTimeoutSeconds}초 안에 안 와서 강제로 진행합니다.");
    }

    public string ResolveAnimStateName(MovementContext context, string defaultState)
    {
        foreach (var v in contextVariants) if (v.context == context) return v.animStateName;
        return defaultState;
    }

    protected bool IsContextAllowed(MovementContext context) => allowedContexts.Count == 0 || allowedContexts.Contains(context);

    // 점수가 높을수록 이번 턴에 선택될 확률이 높음
    public abstract float EvaluateScore(NPCDecisionContext ctx);
    public abstract IEnumerator Execute(NPC self, NPCVisual visual, Transform target);

#if UNITY_EDITOR
    public virtual void DrawEditorGizmos(Vector3 basePos, float facingDir) { }
#endif
}