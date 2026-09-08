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
        if (!_lookup.TryGetValue(cueId, out var cue)) return;
        if (cue.cameraCue != null) CameraDirector.Instance?.PlayCue(cue.cameraCue);
        if (cue.screenFlash) ScreenFlashOverlay.Instance?.Flash(cue.flashColor, cue.flashDuration);
        if (!string.IsNullOrEmpty(cue.sfxKey)) SoundManager.Instance?.PlaySFX(cue.sfxKey);
        if (!string.IsNullOrEmpty(cue.bgmKey)) SoundManager.Instance?.PlayBGM(cue.bgmKey); // ★ 아래 SoundManager 확장 필요

        if (!string.IsNullOrEmpty(cue.animationKey))
        {
            var target = SpeakerResolver.Resolve(cue.targetCharacterKey);
            target?.GetComponent<CutsceneAnimationPlayer>()?.Play(cue.animationKey); // ★ 기존 그대로 재사용
        }
    }
}