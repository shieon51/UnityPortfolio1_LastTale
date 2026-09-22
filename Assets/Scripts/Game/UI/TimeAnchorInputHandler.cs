using UnityEngine;

// 시간의 닻 입력만 담당. 항상 켜져 있는 오브젝트에 붙일 것 (팝업 오브젝트에 붙이면 안 됨)
public class TimeAnchorInputHandler : MonoBehaviour
{
    public TimeAnchorConfirmPopup popup;

    private void Update()
    {
        if (!InputBindings.GetKeyDown(InputAction.TimeAnchor)) return;
        if (GlobalActionLock.IsLocked) return;                                       // 창·팝업이 열려 있으면 무시
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsTalking) return;

        if (popup != null) popup.Show();
    }
}