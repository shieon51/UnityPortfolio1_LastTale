using UnityEngine;

// 스택에 올라갈 수 있는 UI의 종류. 숫자가 클수록 위에 쌓인다.
public enum UILayer { Window = 0, Popup = 1, Pause = 2 }

// 창·팝업·일시정지 메뉴의 공통 기반.
// 표시/숨김만 담당하고, 열고 닫는 순서는 UIWindowManager가 관리한다.
public abstract class UIStackElement : MonoBehaviour
{
    public abstract UILayer Layer { get; }

    [Header("스택 동작")]
    [Tooltip("ESC로 닫을 수 있는지")]
    public bool closeOnEscape = true;
    [Tooltip("열려 있는 동안 월드를 멈출지")]
    public bool pausesWorld = true;
    [Tooltip("대화 중에도 열 수 있는지")]
    public bool allowDuringDialogue = false;

    public bool IsOpen { get; private set; }

    // 매니저가 호출한다. 직접 부르지 말 것
    public void OpenInternal()
    {
        IsOpen = true;
        gameObject.SetActive(true);
        OnOpened();
    }

    public void CloseInternal()
    {
        IsOpen = false;
        OnClosed();
        gameObject.SetActive(false);
    }

    protected virtual void OnOpened() { }
    protected virtual void OnClosed() { }

    // true를 반환하면 이 요소가 ESC를 자체 처리했다는 뜻이라 닫히지 않는다
    // (예: 하위 탭이 열려 있으면 탭만 접기)
    public virtual bool HandleEscape() => false;
}