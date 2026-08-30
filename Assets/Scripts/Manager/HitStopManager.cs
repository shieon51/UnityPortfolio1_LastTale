// HitStopManager.cs (신규)
using System.Collections;
using UnityEngine;

public class HitStopManager : Singleton<HitStopManager>
{
    private Coroutine _routine;

    public void Trigger(CharacterStats a, CharacterStats b, float duration)
    {
        ApplyToCharacter(a, duration);
        ApplyToCharacter(b, duration);
        HitStopVisualOverlay.Instance?.Pulse(duration * 3f);
    }

    private void ApplyToCharacter(CharacterStats c, float duration)
    {
        if (c == null) return;
        var pv = c.GetComponentInChildren<PlayerVisual>();
        if (pv != null) { pv.TriggerHitStop(duration); return; } // 기존 있던 기능 재사용
        var nv = c.GetComponentInChildren<NPCVisual>();
        if (nv != null) StartCoroutine(NPCHitStopRoutine(nv, duration));
    }

    private IEnumerator NPCHitStopRoutine(NPCVisual nv, float duration)
    {
        nv.SetAnimatorSpeed(0f);
        yield return new WaitForSecondsRealtime(duration);
        nv.SetAnimatorSpeed(1f);
    }
}