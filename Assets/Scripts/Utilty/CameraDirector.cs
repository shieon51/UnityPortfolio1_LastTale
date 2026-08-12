using System.Collections;
using UnityEditor.Rendering;
using UnityEngine;

// CameraDirector.cs (신규, 뼈대만 — 지금 다 구현 안 하셔도 됨)
public class CameraDirector : Singleton<CameraDirector>
{
    public CameraFollow follow;

    public void SetSecondaryTarget(Transform target) { if (follow != null) follow.secondaryTarget = target; }
    public void ClearSecondaryTarget() { if (follow != null) follow.secondaryTarget = null; }
    public void SnapToCurrentTargets() => follow?.SnapToNextFollowPosition();

    // ink 태그/컷씬 시스템이 나중에 부를 진입점들
    public void Shake(float duration, float intensity)
    {
        if (GameSettings.Instance != null && !GameSettings.Instance.ScreenShakeEnabled) return; // 카메라 셰이크 설정 끈 경우 return

        StartCoroutine(ShakeRoutine(duration, intensity));
    }

    public void DirectionalShake(float duration, float intensity, Vector2 direction)
    {
        if (GameSettings.Instance != null && !GameSettings.Instance.ScreenShakeEnabled) return;
        StartCoroutine(DirectionalShakeRoutine(duration, intensity, direction.normalized));
    }

    private IEnumerator DirectionalShakeRoutine(float duration, float intensity, Vector2 direction)
    {
        if (follow == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float damped = intensity * (1f - t); // 시간 지날수록 잦아듦
            float wave = Mathf.Sin(t * Mathf.PI * 8f);
            follow.ApplyShakeOffset((Vector3)(direction * wave * damped));
            elapsed += Time.deltaTime;
            yield return null;
        }
        follow.ApplyShakeOffset(Vector3.zero);
    }

    public void FocusOn(Transform target, float duration) { /* 나중에 구현 */ }
    public void ReturnToFollow() { /* 나중에 구현 */ }

    private IEnumerator ShakeRoutine(float duration, float intensity)
    {
        if (follow == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            follow.ApplyShakeOffset((Vector3)(Random.insideUnitCircle * intensity));
            elapsed += Time.deltaTime;
            yield return null;
        }
        follow.ApplyShakeOffset(Vector3.zero);
    }

    public void PlayCue(CameraCue cue)
    {
        if (cue == null) return;
        if (cue.instantSnap) SnapToCurrentTargets();
        if (cue.shake) Shake(cue.shakeDuration, cue.shakeIntensity);
        if (cue.flash) ScreenFlashOverlay.Instance?.Flash(cue.flashColor, cue.flashDuration);
    }

    public void TriggerRecenter(float duration = -1f) => follow?.TriggerRecenter(duration);
}