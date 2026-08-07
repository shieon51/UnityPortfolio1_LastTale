using UnityEngine;

// BossPhaseTransitionLock.cs (신규) — 플레이어 루트에 부착
public class BossPhaseTransitionLock : MonoBehaviour, IActionLockSource
{
    public bool IsLocked { get; private set; }
    private IFormStageProvider _current;

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
    }

    private void HandleStarted() => IsLocked = true;
    private void HandleEnded(int _) => IsLocked = false;
}