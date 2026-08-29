using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

// HitStopVisualOverlay.cs (신규) - 히트스톱 시 색수차 효과
public class HitStopVisualOverlay : Singleton<HitStopVisualOverlay>
{
    public Volume globalVolume;
    private ChromaticAberration _chromatic;
    private Coroutine _routine;

    private void Awake()
    {
        if (globalVolume != null && globalVolume.profile.TryGet(out ChromaticAberration ca)) _chromatic = ca;
    }

    public void Pulse(float duration, float maxIntensity = 0.6f)
    {
        if (_chromatic == null) return;
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(PulseRoutine(duration, maxIntensity));
    }

    private IEnumerator PulseRoutine(float duration, float maxIntensity)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            _chromatic.intensity.value = Mathf.Lerp(maxIntensity, 0f, t / duration); // 확 튀었다가 빠르게 잦아듦
            yield return null;
        }
        _chromatic.intensity.value = 0f;
        _routine = null;
    }
}