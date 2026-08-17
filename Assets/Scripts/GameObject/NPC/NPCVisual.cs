using UnityEngine;

// NPCVisual.cs — Liel > Visual 오브젝트에 부착
public class NPCVisual : MonoBehaviour
{
    private Animator[] _partAnimators;
    private SpriteRenderer[] _allSpriteRenderers;
    private string _lastCommandedState;

    // 목표 단계에 따라 다른 클립 재생 (보스마다 다르게 그리시면 자동 반영)
    private IFormStageProvider _formProvider;

    // 좌우 방향별 모션
    private DirectionalPart[] _directionalParts;
    private DirectionalSprite[] _directionalSprites;

    // 눈 깜빡임
    private EyeBlinkController _eyeBlink;

    private void Awake()
    {
        _partAnimators = GetComponentsInChildren<Animator>(true);
        _allSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        _directionalParts = GetComponentsInChildren<DirectionalPart>(true);
        _directionalSprites = GetComponentsInChildren<DirectionalSprite>(true);
        _eyeBlink = GetComponentInChildren<EyeBlinkController>(true);
        _formProvider = GetComponentInParent<IFormStageProvider>(); // 페이즈 전환 쓰는 보스면 존재

        if (_formProvider != null) _formProvider.OnFormTransformStarted += HandleFormTransformStarted;
    }

    private void OnDestroy()
    {
        if (_formProvider != null) _formProvider.OnFormTransformStarted -= HandleFormTransformStarted;
    }

    private void HandleFormTransformStarted() => PlayImmediate($"TransformToPhase{_formProvider.TargetFormStage}");

    public void SetEyesVisible(bool visible) => _eyeBlink?.SetVisible(visible);

    // 같은 상태를 매 프레임 중복 명령해도 무시 — Walk 같은 루프 애니메이션이 매 프레임 0으로 리셋되는 걸 방지
    public void PlayIfChanged(string stateName)
    {
        if (_lastCommandedState == stateName) return;
        PlayImmediate(stateName);
    }

    public void PlayImmediate(string stateName)
    {
        _lastCommandedState = stateName;
        foreach (var anim in _partAnimators)
            if (anim != null && anim.gameObject.activeInHierarchy)
                anim.Play(stateName, -1, 0f);
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
        foreach (var sr in _allSpriteRenderers)
            if (sr != null) sr.flipX = flipX;

        bool facingRight = flipX; // 기존 컨벤션: flipX=true → 오른쪽
        foreach (var part in _directionalParts) part.ApplyFacing(facingRight);
        foreach (var s in _directionalSprites) s.ApplyFacing(facingRight);
    }
}