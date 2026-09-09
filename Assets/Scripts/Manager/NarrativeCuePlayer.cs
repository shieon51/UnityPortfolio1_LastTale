// NarrativeCuePlayer.cs (신규)
using System.Collections.Generic;
using UnityEngine;

public class NarrativeCuePlayer : Singleton<NarrativeCuePlayer>
{
    public string resourcesFolder = "NarrativeCues"; // MemoryManager와 같은 자동 로드 패턴
    private Dictionary<string, NarrativeCue> _lookup;

    private void Awake() => LoadCues();

    private void LoadCues()
    {
        _lookup = new Dictionary<string, NarrativeCue>();
        foreach (var cue in Resources.LoadAll<NarrativeCue>(resourcesFolder))
            if (!string.IsNullOrEmpty(cue.cueId)) _lookup[cue.cueId] = cue;
        Debug.Log($"[NarrativeCuePlayer] 큐 {_lookup.Count}개 로드 완료");
    }

    public void Play(string cueId)
    {
        if (!_lookup.TryGetValue(cueId, out var cue)) { Debug.LogWarning($"[NarrativeCuePlayer] 등록 안 된 cueId: {cueId}"); return; }
        if (cue.cameraCue != null) CameraDirector.Instance?.PlayCue(cue.cameraCue); // ★ 흔들림+플래시 다 여기서
        if (!string.IsNullOrEmpty(cue.sfxKey)) SoundManager.Instance?.PlaySFX(cue.sfxKey);
        if (!string.IsNullOrEmpty(cue.bgmKey)) SoundManager.Instance?.PlayBGM(cue.bgmKey);
        if (!string.IsNullOrEmpty(cue.animationKey))
        {
            var target = SpeakerResolver.Resolve(cue.targetCharacterKey);
            target?.GetComponent<CutsceneAnimationPlayer>()?.Play(cue.animationKey);
        }
    }

#if UNITY_EDITOR
    [ContextMenu("큐 다시 로드")]
    private void ReloadFromMenu() => LoadCues();
#endif
}