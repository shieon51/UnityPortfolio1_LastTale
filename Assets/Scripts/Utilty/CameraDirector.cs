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
    public void Shake(float duration, float intensity) => StartCoroutine(ShakeRoutine(duration, intensity));
    public void FocusOn(Transform target, float duration) { /* 나중에 구현 */ }
    public void ReturnToFollow() { /* 나중에 구현 */ }

    private IEnumerator ShakeRoutine(float duration, float intensity)
    {
        if (follow == null) yield break;
        Vector3 originalOffset = follow.offset;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            follow.offset = originalOffset + (Vector3)(Random.insideUnitCircle * intensity);
            elapsed += Time.deltaTime;
            yield return null;
        }
        follow.offset = originalOffset;
    }

    public void PlayCue(CameraCue cue)
    {
        if (cue == null) return;
        if (cue.instantSnap) SnapToCurrentTargets();
        if (cue.shake) Shake(cue.shakeDuration, cue.shakeIntensity);
        if (cue.flash) ScreenFlashOverlay.Instance?.Flash(cue.flashColor, cue.flashDuration);
    }
}