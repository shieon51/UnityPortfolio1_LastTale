// EnterIconIndicator.cs (½Å±Ô)
using UnityEngine;

public class EnterIconIndicator : MonoBehaviour
{
    public GameObject icon;
    private void OnEnable() { DialogueManager.Instance.OnWaitingForInputChanged += SetVisible; icon.SetActive(DialogueManager.Instance.IsWaitingForInput); }
    private void OnDisable() { if (DialogueManager.Instance != null) DialogueManager.Instance.OnWaitingForInputChanged -= SetVisible; }
    private void SetVisible(bool visible) => icon.SetActive(visible);
}