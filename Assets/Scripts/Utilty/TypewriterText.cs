// TypewriterText.cs (신규)
using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 글자 타이핑 효과
public class TypewriterText : MonoBehaviour
{
    public TextMeshProUGUI target;
    public float charsPerSecond = 40f;
    public event Action OnFullyDisplayed;
    public bool IsTyping { get; private set; }

    private string _fullText;
    private Coroutine _routine;

    public void Play(string text)
    {
        if (_routine != null) StopCoroutine(_routine);
        _fullText = text;
        _routine = StartCoroutine(TypeRoutine(text));
    }

    private IEnumerator TypeRoutine(string text)
    {
        IsTyping = true;
        target.text = "";
        var sb = new System.Text.StringBuilder();
        float delay = charsPerSecond > 0f ? 1f / charsPerSecond : 0f;

        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == '<') // ★ 리치 텍스트 태그: 통째로 한 번에 추가하고 딜레이 없이 넘어감
            {
                int close = text.IndexOf('>', i);
                if (close != -1)
                {
                    sb.Append(text, i, close - i + 1);
                    i = close + 1;
                    target.text = sb.ToString();
                    continue; // 태그는 글자로 세지 않으니 대기도 없음
                }
            }

            sb.Append(text[i]);
            i++;
            target.text = sb.ToString();
            yield return new WaitForSeconds(delay);
        }

        IsTyping = false; _routine = null;
        OnFullyDisplayed?.Invoke();
    }

    public void Skip()
    {
        if (!IsTyping || _routine == null) return;
        StopCoroutine(_routine);
        target.text = _fullText;
        IsTyping = false; _routine = null;
        OnFullyDisplayed?.Invoke();
    }
}