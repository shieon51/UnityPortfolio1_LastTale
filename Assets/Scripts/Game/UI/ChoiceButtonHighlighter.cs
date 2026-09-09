// ChoiceButtonHighlighter.cs (신규) — 선택지 버튼에 자동으로 붙는 헬퍼
using UnityEngine;
using UnityEngine.EventSystems;

public class ChoiceButtonHighlighter : MonoBehaviour, IPointerDownHandler
{
    public int index;
    public System.Action<int> onPressed;
    public void OnPointerDown(PointerEventData eventData) => onPressed?.Invoke(index); // ★ 누르는 순간 하이라이트 이동
}