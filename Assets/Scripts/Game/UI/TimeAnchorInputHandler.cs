// TimeAnchorInputHandler.cs (신규) — 입력만 담당
using UnityEngine;

public class TimeAnchorInputHandler : MonoBehaviour
{
    public TimeAnchorConfirmPopup popup;
    private void Update()
    {
        if (Input.GetKeyDown(InputBindings.TimeAnchor) && !DialogueManager.Instance.IsTalking)
            popup.Show();
    }
}