// TimeAnchorConfirmPopup.cs (신규) — E번 카테고리(잠자기 팝업)와 같은 성격
using UnityEngine;
using TMPro;

public class TimeAnchorConfirmPopup : MonoBehaviour
{
    public GameObject popupRoot;
    public TextMeshProUGUI messageText;

    public void Show()
    {
        var sora = PlayerManager.Instance.CurrentCharacter as SoraStats;
        int max = TimeLoopManager.Instance.MaxAnchorCount(sora.level);
        int current = TimeLoopManager.Instance.Anchors.Count;
        messageText.text = $"현재 위치에 시간 고정을 쓰시겠습니까? (현재 지정된 개수: {current}/{max})";
        popupRoot.SetActive(true);
        GlobalActionLock.Lock(this);
    }
    public void OnConfirm() 
    { 
        popupRoot.SetActive(false); 
        GlobalActionLock.Unlock(this); 
        TimeLoopManager.Instance.TrySetAnchor(); 
    }
    public void OnCancel() 
    { 
        popupRoot.SetActive(false); 
        GlobalActionLock.Unlock(this); 
    }
}