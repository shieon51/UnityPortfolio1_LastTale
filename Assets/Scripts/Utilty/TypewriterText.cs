using System;
using System.Collections;
using TMPro;
using UnityEngine;

// 글자 타이핑 효과.
// 전체 문장을 미리 배치해두고 보이는 글자 수만 늘린다.
// (한 글자씩 text에 붙이면 줄이 늘어날 때마다 레이아웃이 다시 잡혀 글이 들썩인다)
public class TypewriterText : MonoBehaviour
{
    public TextMeshProUGUI target;

    [Tooltip("초당 표시할 글자 수")]
    public float charsPerSecond = 40f;
    [Tooltip("일시정지(timeScale 0) 중에도 타이핑이 진행되게 할지")]
    public bool useUnscaledTime = true;

    public event Action OnFullyDisplayed;
    public bool IsTyping { get; private set; }

    public TMP_Text Target => target;                                   // ★ 추가
    public int TotalVisibleCharacters => _totalVisible;                 // ★ 추가
    public int VisibleCharacterCount                                    // ★ 추가
        => target == null ? 0 : Mathf.Min(target.maxVisibleCharacters, _totalVisible);

    private string _fullText;
    private int _totalVisible;
    private Coroutine _routine;

    [Tooltip("한 프레임에 진행할 수 있는 최대 시간. 첫 프레임 지연으로 글자가 한꺼번에 나오는 것을 막는다")]
    public float maxStepSeconds = 0.05f;

    public void Play(string text)
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }

        _fullText = text ?? string.Empty;
        target.text = _fullText;
        target.maxVisibleCharacters = 0;
        target.ForceMeshUpdate();
        _totalVisible = target.textInfo.characterCount;

        IsTyping = false;

        if (_totalVisible == 0) { target.maxVisibleCharacters = int.MaxValue; return; }

        // ★ 꺼져 있으면 코루틴을 돌릴 수 없으므로 전문을 즉시 표시하고 끝낸다
        if (!isActiveAndEnabled)
        {
            target.maxVisibleCharacters = int.MaxValue;
            IsTyping = false;
            return;
        }

        _routine = StartCoroutine(TypeRoutine());
    }

    private IEnumerator TypeRoutine()
    {
        IsTyping = true;

        // ★ 대사 시작 프레임은 로드·레이아웃 때문에 시간이 크게 튄다.
        //   그 프레임을 그대로 반영하면 첫 줄이 한꺼번에 찍히므로 한 프레임 건너뛴다
        yield return null;

        float shown = 0f;
        while (shown < _totalVisible)
        {
            float delta = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            delta = Mathf.Min(delta, maxStepSeconds);   // ★ 프레임이 튀어도 한 번에 몰리지 않게 제한

            shown += (charsPerSecond > 0f ? charsPerSecond : float.MaxValue) * delta;
            target.maxVisibleCharacters = Mathf.Min(_totalVisible, Mathf.FloorToInt(shown));
            yield return null;
        }

        Finish();
    }

    public void Skip()
    {
        if (!IsTyping || _routine == null) return;
        StopCoroutine(_routine);
        Finish();
    }

    private void Finish()
    {
        target.maxVisibleCharacters = int.MaxValue;
        IsTyping = false;
        _routine = null;
        OnFullyDisplayed?.Invoke();
    }
}