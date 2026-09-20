using TMPro;
using UnityEngine;

// 말풍선 배경을 글자 진행에 맞춰 키운다.
//
// TMP는 maxVisibleCharacters로 가려진 글자의 isVisible을 false로 만들기 때문에,
// 매 프레임 textInfo를 그대로 재면 "지금 보이는 글자"까지밖에 측정할 수 없다.
// 그래서 대사가 시작될 때 한 번만 전체 글자를 드러내 배치 정보를 기록해두고,
// 이후에는 그 배열만 조회한다. (폭 고정과 앞서 자라기가 정확히 동작하는 이유)
//
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

    [Header("자라는 방식 — 가로")]
    [Tooltip("체크하면 가로 폭을 대사 전체 길이에 맞춰 고정한다. 세로만 글자를 따라 자란다")]
    public bool fixWidthAtStart = false;
    [Tooltip("몇 글자 앞을 미리 내다보고 자랄지. 클수록 여유롭게 앞서 자란다")]
    public int leadCharacters = 10;
    [Tooltip("가로가 목표 폭을 따라가는 속도. 클수록 빠릿하다")]
    public float widthGrowSpeed = 12f;          // ★ 분리

    [Header("자라는 방식 — 세로")]
    [Tooltip("세로가 목표 높이를 따라가는 속도. 클수록 빠릿하다")]
    public float heightGrowSpeed = 12f;         // ★ 분리

    [Tooltip("넘침 방지용 여유 폭. 글자가 살짝 삐져나오면 이 값을 키운다")]
    public float overflowSafety = 4f;

    private Vector2 _currentSize;
    private bool _needsSnap = true;

    // ---- 대사마다 한 번만 계산해두는 배치 정보 ----
    private int[] _lineOfChar;        // 글자 i가 몇 번째 줄인지
    private float[] _rightEdgeUpTo;   // 글자 i까지 중 가장 오른쪽 끝
    private float[] _lineDescender;   // 줄별 아래 끝
    private float _leftEdge;
    private float _topAscender;
    private int _profiledCount = -1;
    private string _profiledText;

    public void ResetSize()
    {
        _currentSize = Vector2.zero;
        _needsSnap = true;
        _profiledCount = -1;          // 새 대사이므로 배치 정보도 다시 계산
    }

    private void LateUpdate()
    {
        if (typewriter == null || bubbleRect == null || bodyRect == null) return;
        if (!bubbleRect.gameObject.activeInHierarchy) return;

        var text = typewriter.Target;
        if (text == null) return;

        if (_profiledCount < 0 || _profiledText != text.text) BuildProfile(text);
        if (_profiledCount <= 0) return;

        int visible = Mathf.Clamp(typewriter.VisibleCharacterCount, 0, _profiledCount);

        // ---- 가로: 앞을 내다본다 (글자가 오른쪽으로 넘치면 안 되므로) ----
        float requiredWidth = ToBubbleSize(Measure(visible)).x + overflowSafety;

        int lead = Mathf.Clamp(visible + Mathf.Max(0, leadCharacters), 0, _profiledCount);
        float targetWidth = Mathf.Max(ToBubbleSize(Measure(lead)).x, requiredWidth);

        if (fixWidthAtStart)
            targetWidth = ToBubbleSize(Measure(_profiledCount)).x + overflowSafety;

        // ---- 세로: 지금 보이는 글자만 기준으로 한다 ----
        //   여기에 lead를 쓰면 짧은 대사는 첫 프레임부터 목표가 전체 높이가 되어
        //   "처음부터 다 자란 상태"로 보인다
        float targetHeight = ToBubbleSize(Measure(visible)).y;

        if (_needsSnap || _currentSize == Vector2.zero)
        {
            // 시작은 최소 줄 수 높이로 (아래에서부터 부드럽게 자라도록)
            _currentSize = new Vector2(targetWidth, ToBubbleSize(Measure(0)).y);
            _needsSnap = false;
        }
        else
        {
            // ★ 가로·세로를 각각의 속도로 따라간다
            float kx = 1f - Mathf.Exp(-widthGrowSpeed * Time.unscaledDeltaTime);
            float ky = 1f - Mathf.Exp(-heightGrowSpeed * Time.unscaledDeltaTime);

            _currentSize.x = fixWidthAtStart
                ? targetWidth                                          // 폭 고정은 보간 없이
                : Mathf.Max(Mathf.Lerp(_currentSize.x, targetWidth, kx), requiredWidth);

            _currentSize.y = Mathf.Lerp(_currentSize.y, targetHeight, ky);
        }

        bubbleRect.sizeDelta = _currentSize;
    }

    // 전체 글자를 잠시 드러내 배치 정보를 기록한다 (대사당 한 번)
    private void BuildProfile(TMP_Text text)
    {
        int savedVisible = text.maxVisibleCharacters;

        text.maxVisibleCharacters = int.MaxValue;
        text.ForceMeshUpdate();

        var info = text.textInfo;
        int count = info.characterCount;

        if (count <= 0 || info.lineCount <= 0)
        {
            _profiledCount = 0;
            _profiledText = text.text;
            text.maxVisibleCharacters = savedVisible;
            text.ForceMeshUpdate();
            return;
        }

        _lineOfChar = new int[count];
        _rightEdgeUpTo = new float[count];
        _leftEdge = float.MaxValue;

        float running = 0f;
        bool anyVisible = false;

        for (int i = 0; i < count; i++)
        {
            var ch = info.characterInfo[i];
            _lineOfChar[i] = ch.lineNumber;

            if (ch.isVisible)                       // 공백은 폭 계산에서 제외
            {
                _leftEdge = Mathf.Min(_leftEdge, ch.bottomLeft.x);
                running = anyVisible ? Mathf.Max(running, ch.topRight.x) : ch.topRight.x;
                anyVisible = true;
            }
            _rightEdgeUpTo[i] = running;
        }
        if (!anyVisible) _leftEdge = 0f;

        _lineDescender = new float[info.lineCount];
        for (int l = 0; l < info.lineCount; l++) _lineDescender[l] = info.lineInfo[l].descender;
        _topAscender = info.lineInfo[0].ascender;

        _profiledCount = count;
        _profiledText = text.text;

        text.maxVisibleCharacters = savedVisible;
        text.ForceMeshUpdate();
    }

    // count개의 글자가 차지하는 크기 (기록해둔 배열만 조회)
    private Vector2 Measure(int count)
    {
        if (_profiledCount <= 0) return Vector2.zero;

        int c = Mathf.Clamp(count, 0, _profiledCount);
        float width = c > 0 ? Mathf.Max(0f, _rightEdgeUpTo[c - 1] - _leftEdge) : 0f;

        int lastLine = c > 0 ? _lineOfChar[c - 1] : 0;
        lastLine = Mathf.Clamp(Mathf.Max(lastLine, minLines - 1), 0, _lineDescender.Length - 1);

        float height = _topAscender - _lineDescender[lastLine];
        return new Vector2(width, height);
    }

    // 글자 영역 크기 → 말풍선 전체 크기
    private Vector2 ToBubbleSize(Vector2 textSize)
    {
        Vector2 offset = bodyRect.anchoredPosition;     // 본문은 왼쪽 위 기준 (x > 0, y < 0)
        return new Vector2(
            Mathf.Max(minWidth, offset.x + textSize.x + paddingRight),
            -offset.y + textSize.y + paddingBottom);
    }
}