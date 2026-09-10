// EnterIconIndicator.cs (재설계)
using UnityEditor.ShaderGraph.Serialization;
using UnityEngine;

public class EnterIconIndicator : MonoBehaviour
{
    [Tooltip("실제로 껐다 켤 화살표 아이콘 오브젝트 (이 스크립트는 항상 켜져있는 부모에 붙일 것)")]
    public GameObject iconObject;

    [Tooltip("말풍선 전용이면 연결, 하단 패널용이면 비워둠")]
    public SpeechBubbleController ownerBubble;

    private void OnEnable()
    {
        if (iconObject == null) { Debug.LogWarning($"[EnterIconIndicator] {name}에 Icon Object 미연결", this); return; }
        if (DialogueManager.Instance != null) DialogueManager.Instance.OnWaitingForInputChanged += HandleChanged;
        iconObject.SetActive(false);
    }
    private void OnDisable()
    {
        if (DialogueManager.Instance != null) DialogueManager.Instance.OnWaitingForInputChanged -= HandleChanged;
    }

    private void HandleChanged(bool waiting)
    {
        if (iconObject == null) return;
        bool isMine = ownerBubble == null
            ? DialogueManager.Instance.CurrentActiveBubble == null
            : DialogueManager.Instance.CurrentActiveBubble == ownerBubble;
        iconObject.SetActive(waiting && isMine);
    }
}