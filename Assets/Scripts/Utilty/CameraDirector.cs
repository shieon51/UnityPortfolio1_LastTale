using System.Collections;
using UnityEngine;

// CameraDirector.cs (신규, 뼈대만 — 지금 다 구현 안 하셔도 됨)
public class CameraDirector : Singleton<CameraDirector>
{
    public CameraFollow follow;

    // ink 태그/컷씬 시스템이 나중에 부를 진입점들
    public void Shake(float duration, float intensity) => StartCoroutine(ShakeRoutine(duration, intensity));
    public void FocusOn(Transform target, float duration) { /* 나중에 구현 */ }
    public void ReturnToFollow() { /* 나중에 구현 */ }

    private IEnumerator ShakeRoutine(float duration, float intensity)
    {
        Vector3 original = follow.offset;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            follow.offset = original + (Vector3)Random.insideUnitCircle * intensity;
            elapsed += Time.deltaTime;
            yield return null;
        }
        follow.offset = original;
    }
}