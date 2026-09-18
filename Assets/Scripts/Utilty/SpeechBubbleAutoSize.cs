using TMPro;
using UnityEngine;

// 말풍선 배경을 "지금까지 나온 글자"에 맞춰 키운다.
// 본문 텍스트의 폭은 고정이므로 줄바꿈 위치는 변하지 않고, 배경만
// 가로 → (최대 폭 도달) → 세로 순서로 자란다.
// BubblePanel에 Layout Group / Content Size Fitter가 붙어 있으면 충돌하므로 제거할 것.
[DisallowMultipleComponent]
public class SpeechBubbleAutoSize : MonoBehaviour
{
    [Header("참조")]
    public TypewriterText typewriter;
    [Tooltip("크기를 조절할 말풍선 배경 (BubblePanel)")]
    public RectTransform bubbleRect;
    [Tooltip("본문 텍스트의 RectTransform (BubblePanel 기준 왼쪽 위에 배치되어 있어야 한다)")]
    public RectTransform bodyRect;

    [Header("여백")]
    [Tooltip("본문 오른쪽에 둘 여백")]
    public float paddingRight = 20f;
    [Tooltip("본문 아래에 둘 여백 (엔터 아이콘 자리를 포함)")]
    public float paddingBottom = 32f;

    [Header("크기 제한")]
    public float minWidth = 200f;
    public float minHeight = 90f;

    [Header("자라는 속도")]
    [Tooltip("0이면 즉시 반영. 값이 클수록 빠르게 따라간다")]
    public float growSpeed = 25f;

    private Vector2 _currentSize;

    // 새 대사를 시작할 때 호출한다 (다시 처음부터 자라도록)
    public void ResetSize() => _currentSize = Vector2.zero;

    private void LateUpdate()
    {
        if (typewriter == null || bubbleRect == null || bodyRect == null) return;
        if (!bubbleRect.gameObject.activeInHierarchy) return;

        var text = typewriter.Target;
        if (text == null) return;

        Vector2 visible = MeasureVisible(text);

        // 본문은 BubblePanel의 왼쪽 위 기준으로 배치되어 있다 (x > 0, y < 0)
        Vector2 offset = bodyRect.anchoredPosition;

        Vector2 target = new Vector2(
            offset.x + visible.x + paddingRight,
            -offset.y + visible.y + paddingBottom);

        target.x = Mathf.Max(minWidth, target.x);
        target.y = Mathf.Max(minHeight, target.y);

        if (growSpeed <= 0f || _currentSize == Vector2.zero)
            _currentSize = target;
        else
            _currentSize = Vector2.Lerp(_currentSize, target, 1f - Mathf.Exp(-growSpeed * Time.unscaledDeltaTime));

        bubbleRect.sizeDelta = _currentSize;
    }

    // 지금 보이는 글자들이 차지하는 실제 크기
    private Vector2 MeasureVisible(TMP_Text text)
    {
        var info = text.textInfo;
        int visible = Mathf.Min(typewriter.VisibleCharacterCount, info.characterCount);
        if (visible <= 0 || info.lineCount == 0) return Vector2.zero;

        float minX = float.MaxValue, maxX = float.MinValue;
        int lastLine = 0;

        for (int i = 0; i < visible; i++)
        {
            var ch = info.characterInfo[i];
            lastLine = ch.lineNumber;          // 공백도 줄 번호는 유효하다
            if (!ch.isVisible) continue;       // 폭 계산에서는 공백 제외
            minX = Mathf.Min(minX, ch.bottomLeft.x);
            maxX = Mathf.Max(maxX, ch.topRight.x);
        }

        float width = (maxX > minX) ? maxX - minX : 0f;

        lastLine = Mathf.Clamp(lastLine, 0, info.lineCount - 1);
        float height = info.lineInfo[0].ascender - info.lineInfo[lastLine].descender;

        return new Vector2(width, height);
    }
}