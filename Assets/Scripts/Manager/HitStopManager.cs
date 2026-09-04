// HitStopManager.cs (신규)
using System.Collections;
using UnityEngine;

public class HitStopManager : Singleton<HitStopManager>
{
    private Coroutine _routine;

    [Header("느린 속도 조절")]
    [Tooltip("히트스톱 동안 애니메이터 재생 속도. 0에 가까울수록 거의 멈춘 듯, 1에 가까울수록 정상속도")]
    public float hitStopSlowSpeed = 0.08f;

    public void Trigger(CharacterStats a, CharacterStats b, float duration, float chromaticIntensity = -1f, float lensIntensity = -1f)
    {
        ApplyToCharacter(a, duration);
        ApplyToCharacter(b, duration);
        if (chromaticIntensity > 0f) HitStopVisualOverlay.Instance?.Pulse(duration * 3f, chromaticIntensity, lensIntensity);
        else HitStopVisualOverlay.Instance?.Pulse(duration * 3f);
    }

    public void TriggerSingle(CharacterStats target, float duration, float chromaticIntensity = -1f, float lensIntensity = -1f)
    {
        ApplyToCharacter(target, duration);
        if (chromaticIntensity > 0f) HitStopVisualOverlay.Instance?.Pulse(duration * 3f, chromaticIntensity, lensIntensity);
        else HitStopVisualOverlay.Instance?.Pulse(duration * 3f);
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
        nv.SetAnimatorSpeed(hitStopSlowSpeed); // ★ 0f → hitStopSlowSpeed
        yield return new WaitForSecondsRealtime(duration);
        nv.SetAnimatorSpeed(1f);
    }
}