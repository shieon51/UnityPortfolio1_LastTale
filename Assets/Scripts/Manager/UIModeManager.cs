using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum UIMode { Normal, Battle, Cutscene }

// 탐색/전투/컷씬 모드에 따라 HUD 그룹을 전환한다.
// 그룹 루트는 항상 활성 상태로 두고 CanvasGroup만 조절한다.
// (비활성화하면 그 안의 스크립트가 Start/OnEnable을 못 받는 문제가 생김)
public class UIModeManager : Singleton<UIModeManager>
{
    [Serializable]
    public class ModeGroup
    {
        public UIMode mode;
        [Tooltip("이 모드에서 보여줄 패널 루트. CanvasGroup이 반드시 있어야 한다")]
        public GameObject panelRoot;

        [NonSerialized] public CanvasGroup cached;
    }

    [Header("모드별 패널")]
    public List<ModeGroup> modeGroups = new();

    [Header("전환")]
    [Tooltip("페이드에 걸리는 시간")]
    public float transitionDuration = 0.25f;
    [Tooltip("일시정지(timeScale 0) 중에도 전환이 진행되게 할지")]
    public bool useUnscaledTime = true;
    [Tooltip("시작 시 적용할 모드")]
    public UIMode startMode = UIMode.Normal;

    public UIMode CurrentMode { get; private set; } = UIMode.Normal;
    public bool IsTransitioning => _transitionRoutine != null;

    // HUD나 입력 처리가 모드 변화를 구독할 수 있게
    public event Action<UIMode> OnModeChanged;

    private Coroutine _transitionRoutine;

    private void Start() => ApplyModeImmediate(startMode);

    public bool IsMode(UIMode mode) => CurrentMode == mode;

    public void SetMode(UIMode newMode)
    {
        if (newMode == CurrentMode) return;

        // ★ B-1: 페이드가 끝난 뒤가 아니라 "호출 즉시" 모드를 갱신한다.
        //   기존에는 전환 0.25초 동안 CurrentMode가 이전 값이라,
        //   전투 진입 직후 곧바로 Normal 복귀를 부르면 무시되고 Battle로 끝났음.
        CurrentMode = newMode;
        OnModeChanged?.Invoke(newMode);

        if (_transitionRoutine != null) StopCoroutine(_transitionRoutine);

        // 비활성 상태에서는 코루틴이 못 돌므로 즉시 반영
        if (!isActiveAndEnabled) { ApplyAlphaImmediate(newMode); return; }

        _transitionRoutine = StartCoroutine(TransitionRoutine(newMode));
    }

    public void ApplyModeImmediate(UIMode mode)
    {
        if (_transitionRoutine != null) { StopCoroutine(_transitionRoutine); _transitionRoutine = null; }

        bool changed = CurrentMode != mode;
        CurrentMode = mode;
        ApplyAlphaImmediate(mode);
        if (changed) OnModeChanged?.Invoke(mode);
    }

    private IEnumerator TransitionRoutine(UIMode target)
    {
        float duration = Mathf.Max(0.0001f, transitionDuration);

        // 전환이 겹칠 때 알파가 튀지 않도록, 지금 값에서 이어서 페이드한다
        var startAlphas = new Dictionary<ModeGroup, float>();
        foreach (var group in modeGroups)
        {
            var cg = Resolve(group);
            if (cg != null) startAlphas[group] = cg.alpha;
        }

        float t = 0f;
        while (t < duration)
        {
            t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);

            foreach (var group in modeGroups)
            {
                var cg = Resolve(group);
                if (cg == null || !startAlphas.ContainsKey(group)) continue;
                cg.alpha = Mathf.Lerp(startAlphas[group], group.mode == target ? 1f : 0f, k);
            }
            yield return null;
        }

        ApplyAlphaImmediate(target);
        _transitionRoutine = null;
    }

    private void ApplyAlphaImmediate(UIMode mode)
    {
        foreach (var group in modeGroups)
        {
            var cg = Resolve(group);
            if (cg == null) continue;

            bool on = group.mode == mode;
            cg.alpha = on ? 1f : 0f;
            cg.interactable = on;
            cg.blocksRaycasts = on;   // 꺼진 패널이 클릭을 가로채지 않게
        }
    }

    private CanvasGroup Resolve(ModeGroup group)
    {
        if (group == null || group.panelRoot == null) return null;
        if (group.cached != null) return group.cached;

        group.cached = group.panelRoot.GetComponent<CanvasGroup>();
        if (group.cached == null)
            Debug.LogError($"[UIModeManager] '{group.panelRoot.name}'에 CanvasGroup이 없음 — {group.mode} 모드 전환이 동작하지 않음", group.panelRoot);

        return group.cached;
    }

#if UNITY_EDITOR
    [ContextMenu("모드: 탐색")] private void DebugNormal() => SetMode(UIMode.Normal);
    [ContextMenu("모드: 전투")] private void DebugBattle() => SetMode(UIMode.Battle);
    [ContextMenu("모드: 컷씬")] private void DebugCutscene() => SetMode(UIMode.Cutscene);

    [ContextMenu("연속 전환 테스트 (전투 → 즉시 탐색)")]
    private void DebugRapidSwitch()
    {
        SetMode(UIMode.Battle);
        SetMode(UIMode.Normal);   // 수정 전에는 이 호출이 무시되어 Battle로 끝났다
    }
#endif
}