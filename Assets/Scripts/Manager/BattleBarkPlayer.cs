// BattleBarkPlayer.cs (신규)
using System.Collections;
using UnityEngine;

public class BattleBarkPlayer : Singleton<BattleBarkPlayer>
{
    public void Play(BattleBarkData bark, bool exclusive = false) // ★ 기본 exclusive=false — 플레이어 대사랑 겹쳐도 안 지워지게
    {
        if (bark == null || bark.possibleLines.Length == 0) return;
        string line = bark.possibleLines[Random.Range(0, bark.possibleLines.Length)];
        var speaker = SpeakerResolver.Resolve(bark.speakerKey);
        if (speaker == null) return;

        SpeechBubbleManager.Instance?.ShowBubble(speaker, bark.speakerDisplayName, line, exclusive);
        StartCoroutine(AutoHideAfter(speaker, bark.displayDuration));
    }

    private IEnumerator AutoHideAfter(Transform speaker, float duration)
    {
        yield return new WaitForSeconds(duration);
        speaker.GetComponentInChildren<SpeechBubbleController>(true)?.Hide();
    }
}