using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

// VignetteFlashOverlay.cs (신규) — 씬의 Global Volume에 Vignette 오버라이드가 이미 있어야 함
public class VignetteFlashOverlay : Singleton<VignetteFlashOverlay>
{
    public Volume globalVolume; // 인스펙터에서 프로젝트 Global Volume 연결
    private Vignette _vignette;
    private Coroutine _routine;

    private void Awake()
    {
        if (globalVolume != null && globalVolume.profile.TryGet(out Vignette v)) _vignette = v;
    }

    public void Flash(Color color, float duration, float maxIntensity = 0.4f)
    {
        if (_vignette == null) return;
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(FlashRoutine(color, duration, maxIntensity));
    }

    private IEnumerator FlashRoutine(Color color, float duration, float maxIntensity)
    {
        _vignette.color.value = color;
        float half = duration / 2f;
        float t = 0f;
        while (t < half) { t += Time.unscaledDeltaTime; _vignette.intensity.value = Mathf.Lerp(0f, maxIntensity, t / half); yield return null; }
        t = 0f;
        while (t < half) { t += Time.unscaledDeltaTime; _vignette.intensity.value = Mathf.Lerp(maxIntensity, 0f, t / half); yield return null; }
        _vignette.intensity.value = 0f;
        _routine = null;
    }
}