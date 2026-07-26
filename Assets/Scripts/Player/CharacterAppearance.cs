using System.Collections.Generic;
using UnityEngine;

// Visual 오브젝트에 부착
public class CharacterAppearance : MonoBehaviour
{
    [SerializeField] private CharacterVisualProfile _profile;

    private BodyPartSlotTag[] _parts;
    private Dictionary<Animator, BodyPartSlot> _slotByAnimator;
    private IFormStageProvider _formProvider;

    private void Awake()
    {
        _parts = GetComponentsInChildren<BodyPartSlotTag>(true);
        _slotByAnimator = new Dictionary<Animator, BodyPartSlot>();
        foreach (var p in _parts)
        {
            if (p.Animator == null) continue; // 혹시 모를 누락 파츠는 조용히 건너뜀
            _slotByAnimator[p.Animator] = p.slot;
        }

        _formProvider = GetComponentInParent<IFormStageProvider>();
        ApplyFormStage(_formProvider?.FormStage ?? 0);
    }

    private void OnEnable()
    {
        if (_formProvider != null)
        {
            _formProvider.OnFormStageChanged += ApplyFormStage;
            _formProvider.OnFormTransformStarted += HandleTransformStarted;
        }
    }

    private void OnDisable()
    {
        if (_formProvider != null)
        {
            _formProvider.OnFormStageChanged -= ApplyFormStage;
            _formProvider.OnFormTransformStarted -= HandleTransformStarted;
        }
    }

    // 어느 CharacterVisualProfile를 쓸지 런타임에서 바꿀 수 있도록 함
    public void SetProfile(CharacterVisualProfile newProfile)
    {
        if (newProfile == null) return;
        _profile = newProfile;
        ApplyFormStage(_formProvider?.FormStage ?? 0); // 프로필 교체 즉시 현재 단계로 재적용
    }

    public void ApplyFormStage(int stage)
    {
        var stageSet = _profile.GetStageSet(stage);
        foreach (var part in _parts)
        {
            var data = stageSet.parts.Find(p => p != null && p.slot == part.slot);

            if (data == null)
            {
                part.gameObject.SetActive(false); // 이 단계에 설정 자체가 없으면 안전하게 숨김 (예: 1단계엔 Wings 항목 없음)
                continue;
            }

            part.gameObject.SetActive(data.isVisible);
            if (data.overrideController != null && part.Animator != null)
                part.Animator.runtimeAnimatorController = data.overrideController;
        }
    }

    // 변신 시작 시점에, 목표 단계에서 보여야 할 파츠(예: 날개)를 미리 켜둔다.
    // (해제 방향일 땐 아무것도 미리 안 켜지므로, 날개는 접히는 클립이 끝날 때까지 자연스럽게 계속 보임)
    private void HandleTransformStarted()
    {
        if (_formProvider == null) return;

        int currentStage = _formProvider.FormStage;
        bool enteringHigherForm = !_formProvider.IsFlightForm;
        int targetStage = Mathf.Max(0, enteringHigherForm ? currentStage + 1 : currentStage - 1);

        var targetSet = _profile.GetStageSet(targetStage);
        foreach (var part in _parts)
        {
            var data = targetSet.parts.Find(p => p != null && p.slot == part.slot);
            if (data != null && data.isVisible)
            {
                part.gameObject.SetActive(true);
            }
        }
    }

    // 나중에 장비 시스템에서 호출: 특정 슬롯 하나만 다른 리소스로 교체 (헤어스타일/의상 변경 등)
    public void SetPartOverride(BodyPartSlot slot, AnimatorOverrideController overrideController)
    {
        foreach (var part in _parts)
            if (part.slot == slot && part.Animator != null)
                part.Animator.runtimeAnimatorController = overrideController;
    }

    public BodyPartSlot GetSlot(Animator anim)
        => _slotByAnimator.TryGetValue(anim, out var slot) ? slot : BodyPartSlot.Body;
}