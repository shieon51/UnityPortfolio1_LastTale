// NarrativeCuePlayer.cs (신규)
using System.Collections.Generic;
using UnityEngine;

public class NarrativeCuePlayer : Singleton<NarrativeCuePlayer>
{
    private Dictionary<string, NarrativeCue> _lookup;
    public string resourcesFolder = "NarrativeCues"; // MemoryManager와 같은 자동 로드 패턴

    private void Awake()
    {
        _lookup = new Dictionary<string, NarrativeCue>();
        foreach (var cue in Resources.LoadAll<NarrativeCue>(resourcesFolder))
            if (!string.IsNullOrEmpty(cue.cueId)) _lookup[cue.cueId] = cue;
    }

    public void Play(string cueId)
    {
        if (!_lookup.TryGetValue(cueId, out var cue)) { Debug.LogWarning($"[NarrativeCuePlayer] 등록 안 된 cueId: {cueId}"); return; }
        if (cue.cameraCue != null) CameraDirector.Instance?.PlayCue(cue.cameraCue);
        if (cue.screenFlash) ScreenFlashOverlay.Instance?.Flash(cue.flashColor, cue.flashDuration);
        if (!string.IsNullOrEmpty(cue.sfxKey)) SoundManager.Instance?.PlaySFX(cue.sfxKey);
        // bgmKey, bodyState/faceState 재생은 SoundManager/CharacterAppearance 쪽 실제 메서드 확인 후 연결
    }
}