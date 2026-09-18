using TMPro;
using UnityEngine;

// 말풍선 배경을 글자 진행에 맞춰 키운다.
// 전체 문장의 배치는 이미 계산되어 있으므로 "몇 글자 뒤의 크기"를 미리 알 수 있다.
// 그 크기를 목표로 부드럽게 다가가면, 글자가 도착할 때는 이미 공간이 준비되어 있다.
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
    public float paddingRight = 20f;
    [Tooltip("본문 아래 여백 (엔터 아이콘 자리를 포함)")]
    public float paddingBottom = 56f;

    [Header("크기 제한")]
    public float minWidth = 200f;
    [Tooltip("대사가 아직 안 나왔을 때도 확보해둘 최소 줄 수")]
    public int minLines = 1;

    [Header("자라는 방식")]
    [Tooltip("체크하면 가로 폭을 대사 시작 시점에 확정한다. 세로만 글자를 따라 자란다")]
    public bool fixWidthAtStart = false;
    [Tooltip("몇 글자 앞을 미리 내다보고 자랄지. 클수록 여유롭게 앞서 자란다")]
    public int leadCharacters = 10;
    [Tooltip("목표 크기를 따라가는 속도. 클수록 빠릿하다")]
    public float growSpeed = 12f;

    private Vector2 _currentSize;

    // 새 대사를 시작할 때 호출한다
    public void ResetSize() => _currentSize = Vector2.zero;

    private void LateUpdate()
    {
        if (typewriter == null || bubbleRect == null || bodyRect == null) return;
        if (!bubbleRect.gameObject.activeInHierarchy) return;

        var text = typewriter.Target;
        if (text == null) return;

        var info = text.textInfo;
        int visible = Mathf.Clamp(typewriter.VisibleCharacterCount, 0, info.characterCount);

        // 지금 당장 필요한 크기 (이보다 작아지면 글자가 넘친다)
        Vector2 required = ToBubbleSize(Measure(text, visible));

        // 몇 글자 앞을 내다본 크기 — 이쪽을 목표로 부드럽게 따라간다
        int lead = Mathf.Clamp(visible + Mathf.Max(0, leadCharacters), 0, info.characterCount);
        Vector2 target = ToBubbleSize(Measure(text, lead));

        // 폭을 미리 확정하는 옵션
        if (fixWidthAtStart)
        {
            float fullWidth = ToBubbleSize(Measure(text, info.characterCount)).x;
            required.x = fullWidth;
            target.x = fullWidth;
        }

        if (_currentSize == Vector2.zero)
        {
            _currentSize = target;                      // 첫 프레임은 즉시 반영
        }
        else
        {
            float k = 1f - Mathf.Exp(-growSpeed * Time.unscaledDeltaTime);
            _currentSize = Vector2.Lerp(_currentSize, target, k);

            // ★ 안전장치: 부드럽게 따라가다 뒤처져도 글자가 넘치지는 않게
            _currentSize.x = Mathf.Max(_currentSize.x, required.x);
            _currentSize.y = Mathf.Max(_currentSize.y, required.y);
        }

        bubbleRect.sizeDelta = _currentSize;
    }

    // 글자 영역 크기 → 말풍선 전체 크기 (본문 위치와 여백을 더한다)
    private Vector2 ToBubbleSize(Vector2 textSize)
    {
        Vector2 offset = bodyRect.anchoredPosition;     // 본문은 왼쪽 위 기준 (x > 0, y < 0)
        return new Vector2(
            Mathf.Max(minWidth, offset.x + textSize.x + paddingRight),
            -offset.y + textSize.y + paddingBottom);
    }

    // count개의 글자가 차지하는 크기. minLines만큼은 항상 확보한다
    private Vector2 Measure(TMP_Text text, int count)
    {
        var info = text.textInfo;
        if (info.lineCount == 0) return Vector2.zero;

        float minX = float.MaxValue, maxX = float.MinValue;
        int lastLine = 0;

        for (int i = 0; i < count && i < info.characterCount; i++)
        {
            var ch = info.characterInfo[i];
            lastLine = ch.lineNumber;                   // 공백도 줄 번호는 유효하다
            if (!ch.isVisible) continue;                // 폭 계산에서는 공백 제외
            minX = Mathf.Min(minX, ch.bottomLeft.x);
            maxX = Mathf.Max(maxX, ch.topRight.x);
        }

        float width = (maxX > minX) ? maxX - minX : 0f;

        // ★ 최소 줄 수 확보 — 대사가 아직 안 나왔을 때도 한 줄 공간을 비워둔다
        int floorLine = Mathf.Clamp(minLines - 1, 0, info.lineCount - 1);
        lastLine = Mathf.Clamp(Mathf.Max(lastLine, floorLine), 0, info.lineCount - 1);

        float height = info.lineInfo[0].ascender - info.lineInfo[lastLine].descender;
        return new Vector2(width, height);
    }
}