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

    private void Awake()
    {
        if (globalVolume == null) { Debug.LogWarning($"[{name}] Global Volume이 연결 안 됨"); return; }
        if (!globalVolume.profile.TryGet(out ChromaticAberration ca)) Debug.LogWarning($"[{name}] ChromaticAberration 오버라이드를 프로필에서 못 찾음");
        else _chromatic = ca;
    }

    public void Pulse(float duration) => Pulse(duration, defaultMaxIntensity);
    public void Pulse(float duration, float maxIntensity)
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