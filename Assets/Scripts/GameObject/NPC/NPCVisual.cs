using UnityEngine;

// NPCVisual.cs — Liel > Visual 오브젝트에 부착
public class NPCVisual : MonoBehaviour
{
    private Animator[] _partAnimators;
    private SpriteRenderer[] _allSpriteRenderers;
    private string _lastCommandedState;
    private Rigidbody2D _rb;

    // 목표 단계에 따라 다른 클립 재생 (보스마다 다르게 그리시면 자동 반영)
    private IFormStageProvider _formProvider;

    // 좌우 방향별 모션
    private DirectionalPart[] _directionalParts;
    private DirectionalSprite[] _directionalSprites;

    // 눈 깜빡임
    private EyeBlinkController _eyeBlink;

    // 그로기 등
    private CharacterStats _stats; // ★ 추가

    [Header("리액션 포즈 유지시간")]
    public float hitReactionDuration = 0.2f;
    public float parryReactionDuration = 0.3f;

    private float _reactionPoseEndTime = -10f;
    public bool IsShowingReactionPose => Time.time < _reactionPoseEndTime;

    // PlayImmediate에 이어재생용 오버로드 추가, 현재 상태 조회용 프로퍼티도 추가
    public string CurrentState => _lastCommandedState;

    private void Awake()
    {
        _rb = GetComponentInParent<Rigidbody2D>();
        _partAnimators = GetComponentsInChildren<Animator>(true);
        _allSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        _directionalParts = GetComponentsInChildren<DirectionalPart>(true);
        _directionalSprites = GetComponentsInChildren<DirectionalSprite>(true);
        _eyeBlink = GetComponentInChildren<EyeBlinkController>(true);
        _formProvider = GetComponentInParent<IFormStageProvider>(); // 페이즈 전환 쓰는 보스면 존재
        _stats = GetComponentInParent<CharacterStats>(); // ★ 추가

        if (_formProvider != null) _formProvider.OnFormTransformStarted += HandleFormTransformStarted;

        if (_stats != null) // ★ 추가 — PlayerVisual과 완전히 같은 패턴
        {
            _stats.OnKnockbackApplied += HandleHitVisual;
            _stats.OnParrySuccess += HandleParrySuccessVisual;
            _stats.OnFacingChangedByHit += HandleFacingChangedByHit;
        }
    }

    private void OnDestroy()
    {
        if (_formProvider != null) _formProvider.OnFormTransformStarted -= HandleFormTransformStarted;
        if (_stats != null) // ★ 추가
        {
            _stats.OnKnockbackApplied -= HandleHitVisual;
            _stats.OnParrySuccess -= HandleParrySuccessVisual;
            _stats.OnFacingChangedByHit -= HandleFacingChangedByHit;
        }
    }

    private void HandleFormTransformStarted() => PlayImmediate($"TransformToPhase{_formProvider.TargetFormStage}");

    private void HandleHitVisual()
    {
        _reactionPoseEndTime = Time.time + hitReactionDuration;
        if (_rb != null) _rb.linearVelocity = Vector2.zero;
        PlayImmediate(NPCAnimStateNames.Hit);
    }

    private void HandleParrySuccessVisual(CharacterStats attacker)
    {
        _reactionPoseEndTime = Time.time + parryReactionDuration;
        PlayImmediate(NPCAnimStateNames.Parrying);
    }

    private void HandleFacingChangedByHit(bool faceRight) => SetFacingDirection(faceRight);

    public void SetEyesVisible(bool visible) => _eyeBlink?.SetVisible(visible);

    // 같은 상태를 매 프레임 중복 명령해도 무시 — Walk 같은 루프 애니메이션이 매 프레임 0으로 리셋되는 걸 방지
    public void PlayIfChanged(string stateName)
    {
        if (_lastCommandedState == stateName) return;
        PlayImmediate(stateName);
    }

    public void PlayImmediate(string stateName, float normalizedTime = 0f) // ★ 매개변수 추가 (기존 호출부는 그대로 동작)
    {
        _lastCommandedState = stateName;
        foreach (var anim in _partAnimators)
            if (anim != null && anim.gameObject.activeInHierarchy)
                anim.Play(stateName, -1, normalizedTime);
    }

    public void CrossFadeAll(string stateName, float duration)
    {
        _lastCommandedState = stateName;
        foreach (var anim in _partAnimators)
            if (anim != null && anim.gameObject.activeInHierarchy)
                anim.CrossFade(stateName, duration);
    }

    public void SetAnimatorSpeed(float speed)
    {
        foreach (var anim in _partAnimators)
            if (anim != null) anim.speed = speed;
    }

    public void SetFacingDirection(bool flipX)
    {
        //Debug.LogWarning($"[DBG Flip2] SetFacingDirection({flipX}) 호출됨, 호출자={new System.Diagnostics.StackTrace().GetFrame(1)?.GetMethod()?.Name}"); // ★ 이 줄 추가

        foreach (var sr in _allSpriteRenderers)
            if (sr != null) sr.flipX = flipX;

        bool facingRight = flipX; // 기존 컨벤션: flipX=true → 오른쪽
        foreach (var part in _directionalParts) part.ApplyFacing(facingRight);
        foreach (var s in _directionalSprites) s.ApplyFacing(facingRight);
    }

#if UNITY_EDITOR
    [ContextMenu("애니메이션 클립 길이 전체 출력")]
    private void PrintClipLengths()
    {
        var animator = GetComponentInChildren<Animator>();
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (var clip in animator.runtimeAnimatorController.animationClips)
            Debug.Log($"{clip.name}: {clip.length:F3}초");
    }
#endif

}