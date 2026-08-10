// BossPhaseTransitionLock.cs 전체 교체
using System.Collections;
using UnityEngine;

public class BossPhaseTransitionLock : MonoBehaviour, IActionLockSource
{
    public bool IsLocked { get; private set; }
    [Tooltip("변신 종료 이벤트가 이 시간 안에 안 오면 강제로 잠금 해제 (영구 잠김 방지)")]
    public float maxLockDuration = 3f;

    private IFormStageProvider _current;
    private Coroutine _timeoutRoutine;

    public void SetLockedNPC(IFormStageProvider bossFormProvider)
    {
        UnbindCurrent();
        _current = bossFormProvider;
        if (_current != null)
        {
            _current.OnFormTransformStarted += HandleStarted;
            _current.OnFormStageChanged += HandleEnded;
        }
    }

    public void UnbindCurrent()
    {
        if (_current != null)
        {
            _current.OnFormTransformStarted -= HandleStarted;
            _current.OnFormStageChanged -= HandleEnded;
        }
        _current = null;
        IsLocked = false;
        if (_timeoutRoutine != null) { StopCoroutine(_timeoutRoutine); _timeoutRoutine = null; }
    }

    private void HandleStarted()
    {
        IsLocked = true;
        if (_timeoutRoutine != null) StopCoroutine(_timeoutRoutine);
        _timeoutRoutine = StartCoroutine(TimeoutRoutine());
    }

    private void HandleEnded(int _)
    {
        IsLocked = false;
        if (_timeoutRoutine != null) { StopCoroutine(_timeoutRoutine); _timeoutRoutine = null; }
    }

    private IEnumerator TimeoutRoutine()
    {
        yield return new WaitForSeconds(maxLockDuration);
        Debug.LogWarning("[BossPhaseTransitionLock] 변신 종료 이벤트가 안 와서 강제로 잠금을 해제합니다.");
        IsLocked = false;
    }
}