// EnterIconIndicator.cs (재설계)
using UnityEngine;

public class EnterIconIndicator : MonoBehaviour
{
    public CanvasGroup iconGroup; // ★ Image가 아니라 CanvasGroup으로 alpha만 제어
    [Tooltip("말풍선 전용이면 연결, 하단 패널용이면 비워둠")]
    public SpeechBubbleController ownerBubble;

    private void OnEnable()
    {
        if (iconGroup == null) 
        { 
            Debug.LogWarning($"[EnterIconIndicator] {name}에 Icon Group이 연결 안 됨", this); 
            return; 
        } 
        DialogueManager.Instance.OnWaitingForInputChanged += HandleChanged;
        SetVisible(false);
    }
    private void OnDisable()
    {
        if (DialogueManager.Instance != null) DialogueManager.Instance.OnWaitingForInputChanged -= HandleChanged;
    }
    private void HandleChanged(bool waiting)
    {
        bool isMine = ownerBubble == null
            ? DialogueManager.Instance.CurrentActiveBubble == null
            : DialogueManager.Instance.CurrentActiveBubble == ownerBubble;
        SetVisible(waiting && isMine);
    }
    private void SetVisible(bool visible) => iconGroup.alpha = visible ? 1f : 0f;
}