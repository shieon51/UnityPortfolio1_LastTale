using TMPro;
using UnityEngine;

// 시간의 닻 설치 확인 팝업 — 창 스택의 Popup 레이어에 올라간다
// (ESC = 취소, 열려 있는 동안 입력 잠금과 월드 정지는 UIWindowManager가 처리)
public class TimeAnchorConfirmPopup : UIStackElement
{
    public override UILayer Layer => UILayer.Popup;

    [Header("참조")]
    public TextMeshProUGUI messageText;
    [Tooltip("마나 소모와 닻 개수를 표시할 텍스트 (없으면 생략)")]
    public TextMeshProUGUI costText;

    [Header("문구 키")]
    public string confirmKey = "popup_anchor_confirm";   // 이곳에 시간의 닻을 내리시겠습니까?
    public string costKey = "popup_anchor_cost";         // 마나 {0} 소모 · 내린 닻 {1}/{2}

    public void Show()
    {
        var loop = TimeLoopManager.Instance;
        if (loop == null) return;

        // ★ 검사를 팝업보다 먼저 — 불가능하면 팝업 대신 이유를 알려준다
        if (!loop.CanSetAnchor(out string reasonKey, out object[] reasonArgs))
        {
            loop.NotifyReason(reasonKey, reasonArgs);
            return;
        }

        var sora = PlayerManager.Instance.CurrentCharacter as SoraStats;
        int max = loop.MaxAnchorCountFor(sora);
        int current = loop.Anchors.Count;

        var loc = LocalizationManager.Instance;
        if (messageText != null) messageText.text = loc != null ? loc.Get(confirmKey) : confirmKey;
        if (costText != null) costText.text = loc != null ? loc.GetFormat(costKey, loop.setAnchorManaCost, current, max) : string.Empty;

        UIWindowManager.Instance?.Open(this);
    }

    public void OnConfirm()
    {
        UIWindowManager.Instance?.Close(this);
        TimeLoopManager.Instance?.TrySetAnchor();
    }

    public void OnCancel() => UIWindowManager.Instance?.Close(this);
}