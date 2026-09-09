using System.Collections;
using UnityEngine;

// CameraDirector.cs (신규, 뼈대만 — 지금 다 구현 안 하셔도 됨)
public class CameraDirector : Singleton<CameraDirector>
{
    public CameraFollow follow;

    private bool _isCinematicOverride = false;
    public bool IsCinematicOverride => _isCinematicOverride; // ★ 일반 추적 카메라가 이 값을 보고 잠시 양보해야 함 (아래 설명)


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

    private IEnumerator ShakeRoutine(float duration, float intensity) //?
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

    public void PlayShot(CameraShotData shot)
    {
        if (follow == null) { Debug.LogWarning("[CameraDirector] follow가 연결 안 됨"); return; }
        StartCoroutine(PlayShotRoutine(shot));
    }

    private IEnumerator PlayShotRoutine(CameraShotData shot)
    {
        _isCinematicOverride = true;

        Transform camTransform = follow.transform; // ★ 수정 — this.transform이 아니라 실제 카메라
        Vector3 startPos = camTransform.position;

        if (shot.usePath && shot.pathWaypoints.Length > 0)
            yield return FollowPathRoutine(camTransform, startPos, shot);
        else
            yield return BlendToPositionRoutine(camTransform, startPos, ResolveTargetPosition(shot), shot.blendDuration, shot.blendCurve);

        if (shot.overrideZoom) yield return BlendZoomRoutine(shot.targetOrthoSize, shot.blendDuration);
        if (shot.shakeOnArrival) Shake(shot.arrivalShakeDuration, shot.arrivalShakeIntensity); // ★ 인자 순서 수정

        _isCinematicOverride = false;
    }

    private Vector3 ResolveTargetPosition(CameraShotData shot)
    {
        Vector3 camPos = follow.transform.position; // ★ 수정
        switch (shot.followMode)
        {
            case CameraShotData.FollowMode.SingleTarget:
                var t = SpeakerResolver.Resolve(shot.targetKey);
                return t != null ? new Vector3(t.position.x, t.position.y, camPos.z) : camPos;
            case CameraShotData.FollowMode.Midpoint:
                var mid = SpeechBubbleManager.Instance?.GetActiveSpeakersMidpoint() ?? camPos;
                return new Vector3(mid.x, mid.y, camPos.z);
            default:
                return camPos;
        }
    }

    public void TriggerRecenter(float duration = -1f) => follow?.TriggerRecenter(duration);

    private IEnumerator BlendToPositionRoutine(Transform camTransform, Vector3 from, Vector3 to, float duration, AnimationCurve curve) // ★ camTransform 매개변수 추가
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            camTransform.position = Vector3.Lerp(from, to, curve.Evaluate(Mathf.Clamp01(t / duration)));
            yield return null;
        }
        camTransform.position = to;
    }

    private IEnumerator FollowPathRoutine(Transform camTransform, Vector3 startPos, CameraShotData shot) // ★ 매개변수 추가
    {
        Vector3 prev = startPos;
        float segmentDuration = shot.pathDuration / shot.pathWaypoints.Length;
        foreach (var offset in shot.pathWaypoints)
        {
            Vector3 next = startPos + (Vector3)offset;
            yield return BlendToPositionRoutine(camTransform, prev, next, segmentDuration, shot.blendCurve);
            prev = next;
        }
    }

    private IEnumerator BlendZoomRoutine(float targetSize, float duration)
    {
        var cam = follow.Cam != null ? follow.Cam : Camera.main; // ★ 수정 — follow가 들고 있는 실제 카메라 참조
        if (cam == null) yield break;
        float startSize = cam.orthographicSize;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            cam.orthographicSize = Mathf.Lerp(startSize, targetSize, Mathf.Clamp01(t / duration));
            yield return null;
        }
        cam.orthographicSize = targetSize;
    }
}