// SpeechBubbleManager.cs (신규)
using System.Collections.Generic;
using UnityEngine;

public class SpeechBubbleManager : Singleton<SpeechBubbleManager>
{
    private List<SpeechBubbleController> _activeBubbles = new();

    public void ShowBubble(Transform target, string speakerName, string text, bool exclusive = true)
    {
        var controller = target.GetComponentInChildren<SpeechBubbleController>(true);
        if (controller == null) return;

        if (exclusive) HideAll();
        controller.Show(target, speakerName, text);
        _activeBubbles.RemoveAll(b => b == null); // ★ 죽은 참조 정리
        if (!_activeBubbles.Contains(controller)) _activeBubbles.Add(controller);
    }

    public void HideAll()
    {
        foreach (var b in _activeBubbles)
            if (b != null) b.Hide(); // ★ null 체크 추가
        _activeBubbles.Clear();
    }

    public Vector3 GetActiveSpeakersMidpoint()
    {
        if (_activeBubbles.Count == 0) return Camera.main.transform.position;
        Vector3 sum = Vector3.zero; int count = 0;
        foreach (var b in _activeBubbles) if (b != null) { sum += b.FollowTargetPosition; count++; }
        return count > 0 ? sum / count : Camera.main.transform.position;
    }
}