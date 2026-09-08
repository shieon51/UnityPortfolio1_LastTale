// SpeechBubbleManager.cs (신규)
using System.Collections.Generic;
using UnityEngine;

public class SpeechBubbleManager : Singleton<SpeechBubbleManager>
{
    private List<SpeechBubbleController> _activeBubbles = new();

    public void ShowBubble(Transform target, string speakerName, string text, bool exclusive = true)
    {
        var controller = target.GetComponentInChildren<SpeechBubbleController>(true);
        if (controller == null) { Debug.LogWarning($"[SpeechBubbleManager] {target.name}에 SpeechBubbleController가 없음"); return; }

        if (exclusive) HideAll(); // 기본은 한 명씩(VN 스타일). 동시 대사는 false로 호출 — 6번 참고
        controller.Show(target, speakerName, text);
        if (!_activeBubbles.Contains(controller)) _activeBubbles.Add(controller);
    }

    public void HideAll()
    {
        foreach (var b in _activeBubbles) b.Hide();
        _activeBubbles.Clear();
    }
}