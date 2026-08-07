using UnityEngine;

// NPCVisual.cs — Liel > Visual 오브젝트에 부착
public class NPCVisual : MonoBehaviour
{
    private Animator[] _partAnimators;
    private SpriteRenderer[] _allSpriteRenderers;
    private string _lastCommandedState;

    // 목표 단계에 따라 다른 클립 재생 (보스마다 다르게 그리시면 자동 반영)
    private IFormStageProvider _formProvider;

    private void Awake()
    {
        _partAnimators = GetComponentsInChildren<Animator>(true);
        _allSpriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        _formProvider = GetComponentInParent<IFormStageProvider>(); // 페이즈 전환 쓰는 보스면 존재
        if (_formProvider != null) _formProvider.OnFormTransformStarted += HandleFormTransformStarted;
    }

    private void OnDestroy()
    {
        if (_formProvider != null) _formProvider.OnFormTransformStarted -= HandleFormTransformStarted;
    }

    private void HandleFormTransformStarted() => PlayImmediate($"TransformToPhase{_formProvider.TargetFormStage}");


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

    public void SetFacingDirection(bool flipX)
    {
        foreach (var sr in _allSpriteRenderers)
            if (sr != null) sr.flipX = flipX;
    }
}