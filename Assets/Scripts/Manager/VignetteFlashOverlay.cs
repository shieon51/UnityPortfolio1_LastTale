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

    [Header("비네트 조절")]
    public float defaultMaxIntensity = 0.4f;
    public Color defaultColor = Color.red;

    private void Awake()
    {
        if (globalVolume == null) { Debug.LogWarning($"[{name}] Global Volume이 연결 안 됨"); return; }
        if (!globalVolume.profile.TryGet(out Vignette v)) Debug.LogWarning($"[{name}] Vignette 오버라이드를 프로필에서 못 찾음");
        else _vignette = v;
    }

    public void Flash(float duration) => Flash(defaultColor, duration, defaultMaxIntensity);
    public void Flash(Color color, float duration) => Flash(color, duration, defaultMaxIntensity);
    public void Flash(Color color, float duration, float maxIntensity)
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