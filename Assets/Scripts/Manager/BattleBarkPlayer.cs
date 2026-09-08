// BattleBarkPlayer.cs (신규, 재설계)
using Ink.Runtime;
using System.Collections;
using UnityEngine;

public class BattleBarkPlayer : Singleton<BattleBarkPlayer>
{
    public TextAsset barkInkJSON; // 메인 대화 Story랑 별개 — 상태 섞이면 안 되니까
    private Story _barkStory;

    private void Awake() => _barkStory = new Story(barkInkJSON.text);

    public void PlayKnot(string knotName, string speakerKey, string speakerDisplayName, float displayDuration = 3f)
    {
        try
        {
            _barkStory.ChoosePathString(knotName);
            if (!_barkStory.canContinue) return;
            string line = _barkStory.Continue(); // 짧은 한 줄만(분기/선택지 없음이 기본)
            var speaker = SpeakerResolver.Resolve(speakerKey);
            if (speaker == null) return;
            SpeechBubbleManager.Instance?.ShowBubble(speaker, speakerDisplayName, line, exclusive: false);
            StartCoroutine(AutoHideAfter(speaker, displayDuration));
        }
        catch (System.Exception e) { Debug.LogWarning($"[BattleBarkPlayer] '{knotName}' 재생 실패: {e.Message}"); }
    }

    private IEnumerator AutoHideAfter(Transform speaker, float duration)
    {
        yield return new WaitForSeconds(duration);
        speaker.GetComponentInChildren<SpeechBubbleController>(true)?.Hide();
    }
}