using TMPro;
using UnityEngine;
using UnityEngine.UI;

// HUD 퀵버튼. 창 ID만 지정하면 열고 닫기와 단축키 글자 표시를 담당한다.
[RequireComponent(typeof(Button))]
public class UIWindowButton : MonoBehaviour
{
    [Header("대상")]
    public UIWindowId windowId;
    [Tooltip("이 창을 여는 단축키. 버튼에 글자를 표시할 때 사용한다")]
    public InputAction hotkey = InputAction.Journal;

    [Header("표시")]
    [Tooltip("단축키 글자를 표시할 텍스트 (없으면 생략)")]
    public TMP_Text hotkeyLabel;
    [Tooltip("갱신 알림 빨간 점 (없으면 생략)")]
    public GameObject unreadDot;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(OnClicked);
    }

    private void OnEnable()
    {
        InputBindings.OnBindingsChanged += RefreshHotkeyLabel;
        RefreshHotkeyLabel();
        SetUnread(false);
    }

    private void OnDisable() => InputBindings.OnBindingsChanged -= RefreshHotkeyLabel;

    private void OnClicked()
    {
        if (UIWindowManager.Instance == null) return;
        UIWindowManager.Instance.Toggle(windowId);
        SetUnread(false);            // 열어봤으면 알림 점을 끈다
    }

    // 기록장 등에 새 내용이 생기면 외부에서 호출한다
    public void SetUnread(bool hasUnread)
    {
        if (unreadDot != null) unreadDot.SetActive(hasUnread);
    }

    private void RefreshHotkeyLabel()
    {
        if (hotkeyLabel != null) hotkeyLabel.text = InputBindings.GetKeyLabel(hotkey);
    }
}