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
        if (_formProvider != null) _formProvider.OnFormStageChanged += ApplyFormStage;
    }

    private void OnDisable()
    {
        if (_formProvider != null) _formProvider.OnFormStageChanged -= ApplyFormStage;
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
            var data = stageSet.parts.Find(p => p.slot == part.slot);
            if (data != null && part.Animator != null)
                part.Animator.runtimeAnimatorController = data.overrideController;
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