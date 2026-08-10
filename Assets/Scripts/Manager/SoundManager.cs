// SoundManager.cs (신규)
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SoundManager : Singleton<SoundManager>
{
    [System.Serializable]
    public class SoundEntry { public string soundKey; public AudioClip clip; [Range(0f, 1f)] public float volume = 1f; }

    [Header("SFX")]
    public List<SoundEntry> sfxLibrary = new();
    public AudioSource sfxSourcePrefab;
    public int sfxPoolSize = 8;

    [Header("BGM")]
    public AudioSource bgmSourceA;
    public AudioSource bgmSourceB;
    public float bgmCrossfadeDuration = 1.5f;
    private bool _usingSourceA = true;

    private Dictionary<string, AudioClip> _sfxDict;
    private Queue<AudioSource> _sfxPool;

    private void Awake()
    {
        _sfxDict = new Dictionary<string, AudioClip>();
        foreach (var e in sfxLibrary) _sfxDict[e.soundKey] = e.clip;

        _sfxPool = new Queue<AudioSource>();
        for (int i = 0; i < sfxPoolSize; i++) _sfxPool.Enqueue(Instantiate(sfxSourcePrefab, transform));
    }

    public void PlaySFX(string key, float volumeScale = 1f)
    {
        if (!_sfxDict.TryGetValue(key, out AudioClip clip) || clip == null)
        {
            Debug.LogWarning($"[SoundManager] SFX '{key}'가 등록되지 않았습니다.");
            return;
        }
        var src = _sfxPool.Dequeue();
        _sfxPool.Enqueue(src);
        float finalVolume = volumeScale * (GameSettings.Instance?.SFXVolume ?? 1f); // 볼륨 조절 설정
        src.PlayOneShot(clip, finalVolume);
    }

    public void PlayBGM(AudioClip clip) => StartCoroutine(CrossfadeBGM(clip));

    private IEnumerator CrossfadeBGM(AudioClip newClip)
    {
        AudioSource fadeOut = _usingSourceA ? bgmSourceA : bgmSourceB;
        AudioSource fadeIn = _usingSourceA ? bgmSourceB : bgmSourceA;
        _usingSourceA = !_usingSourceA;

        fadeIn.clip = newClip; fadeIn.volume = 0f; fadeIn.Play();

        float elapsed = 0f;
        float startVol = fadeOut.volume;
        while (elapsed < bgmCrossfadeDuration)
        {
            float t = elapsed / bgmCrossfadeDuration;
            fadeOut.volume = Mathf.Lerp(startVol, 0f, t);
            fadeIn.volume = Mathf.Lerp(0f, 1f, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        fadeOut.Stop(); fadeOut.volume = startVol; fadeIn.volume = 1f;
    }
}