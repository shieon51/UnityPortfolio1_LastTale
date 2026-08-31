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

    [Header("색수차 조절")]
    public float defaultMaxIntensity = 0.6f;

    private LensDistortion _lensDistortion; // 렌즈 왜곡

    private void Awake()
    {
        if (globalVolume != null)
        {
            globalVolume.profile.TryGet(out _chromatic);
            globalVolume.profile.TryGet(out _lensDistortion); 
        }
    }

    public void Pulse(float duration) => Pulse(duration, defaultMaxIntensity);
    public void Pulse(float duration, float maxIntensity)
    {
        if (_chromatic != null) 
        { 
            if (_routine != null) 
                StopCoroutine(_routine); 
            _routine = StartCoroutine(PulseRoutine(duration, maxIntensity)); 
        }
        if (_lensDistortion != null)
        {
            StartCoroutine(LensPulseRoutine(duration)); // ★ 추가
        }
    }

    private IEnumerator LensPulseRoutine(float duration)
    {
        float attackTime = duration * 0.15f;
        float t = 0f;
        while (t < attackTime) 
        { 
            t += Time.unscaledDeltaTime; 
            _lensDistortion.intensity.value = Mathf.Lerp(0f, -0.4f, t / attackTime); 
            yield return null; 
        }
        t = 0f;
        float releaseTime = duration - attackTime;
        while (t < releaseTime) 
        { t += Time.unscaledDeltaTime; 
            _lensDistortion.intensity.value = Mathf.Lerp(-0.4f, 0f, t / releaseTime); 
            yield return null; 
        }
        _lensDistortion.intensity.value = 0f;
    }

    private IEnumerator PulseRoutine(float duration, float maxIntensity)
    {
        float attackTime = duration * 0.15f; // 확 튀는 구간
        float holdTime = duration * 0.25f;   // 정점에서 머무는 구간 
        float releaseTime = duration - attackTime - holdTime;

        float t = 0f;
        while (t < attackTime) { t += Time.unscaledDeltaTime; _chromatic.intensity.value = Mathf.Lerp(0f, maxIntensity, t / attackTime); yield return null; }
        _chromatic.intensity.value = maxIntensity;
        yield return new WaitForSecondsRealtime(holdTime);
        t = 0f;
        while (t < releaseTime) { t += Time.unscaledDeltaTime; _chromatic.intensity.value = Mathf.Lerp(maxIntensity, 0f, t / releaseTime); yield return null; }
        _chromatic.intensity.value = 0f;
        _routine = null;
    }
}