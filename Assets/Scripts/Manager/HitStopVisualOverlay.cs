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
    private LensDistortion _lensDistortion; // 렌즈 왜곡

    [Header("색수차 기본값")]
    public float defaultChromaticIntensity = 0.6f;

    [Header("렌즈 왜곡 기본값")]
    [Tooltip("호출부가 값을 안 넘겼을 때 쓰이는 기본 강도")]
    public float defaultLensIntensity = 0.3f;

    private void Awake()
    {
        if (globalVolume != null)
        {
            globalVolume.profile.TryGet(out _chromatic);
            globalVolume.profile.TryGet(out _lensDistortion); 
        }
    }

    public void Pulse(float duration) => Pulse(duration, defaultChromaticIntensity, defaultLensIntensity);
    public void Pulse(float duration, float chromaticIntensity) => Pulse(duration, chromaticIntensity, defaultLensIntensity);
    public void Pulse(float duration, float chromaticIntensity, float lensIntensity)
    {
        if (_chromatic != null)
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(PulseRoutine(duration, chromaticIntensity));
        }
        if (_lensDistortion != null) StartCoroutine(LensPulseRoutine(duration, -lensIntensity)); // ★ 음수로 변환해서 오목하게
    }

    private IEnumerator LensPulseRoutine(float duration, float targetIntensity) // ★ 하드코딩 -0.4f 제거, 매개변수로
    {
        float attackTime = duration * 0.15f; // 정점까지 튀어오르는 시간
        float releaseTime = duration - attackTime; // 정점에서 원상복구되는 시간
        float t = 0f;
        while (t < attackTime) 
        { 
            t += Time.unscaledDeltaTime; 
            _lensDistortion.intensity.value = Mathf.Lerp(0f, targetIntensity, t / attackTime); 
            yield return null; 
        }
        _lensDistortion.intensity.value = targetIntensity;
        t = 0f;
        while (t < releaseTime) 
        { 
            t += Time.unscaledDeltaTime; 
            _lensDistortion.intensity.value = Mathf.Lerp(targetIntensity, 0f, t / releaseTime); 
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