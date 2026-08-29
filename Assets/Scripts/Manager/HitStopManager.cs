// HitStopManager.cs (신규)
using System.Collections;
using UnityEngine;

public class HitStopManager : Singleton<HitStopManager>
{
    private Coroutine _routine;

    public void Trigger(float duration, float timeScale = 0.05f)
    {
        if (_routine != null) StopCoroutine(_routine);
        Time.timeScale = timeScale;
        HitStopVisualOverlay.Instance?.Pulse(duration * 3f); // ★ 히트스톱보다 살짝 더 오래 잔향처럼
        _routine = StartCoroutine(Routine(duration));
    }

    private IEnumerator Routine(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f; // ★ 항상 고정값으로 복원
        _routine = null;
    }
}