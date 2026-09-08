// TypewriterText.cs (신규)
using System;
using System.Collections;
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
    private Coroutine _routine;

    public void Play(string text)
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(TypeRoutine(text));
    }

    private IEnumerator TypeRoutine(string text)
    {
        IsTyping = true;
        target.text = text;
        target.maxVisibleCharacters = 0;
        target.ForceMeshUpdate();

        var fitterRect = target.GetComponentInParent<ContentSizeFitter>()?.GetComponent<RectTransform>();
        if (fitterRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(fitterRect); // ★ 추가 — 배경(BubblePanel) 즉시 재계산

        int total = target.textInfo.characterCount;
        float delay = charsPerSecond > 0f ? 1f / charsPerSecond : 0f;
        for (int i = 0; i <= total; i++) { target.maxVisibleCharacters = i; yield return new WaitForSeconds(delay); }
        IsTyping = false; _routine = null;
        OnFullyDisplayed?.Invoke();
    }

    public void Skip()
    {
        if (!IsTyping || _routine == null) return;
        StopCoroutine(_routine);
        target.maxVisibleCharacters = target.textInfo.characterCount;
        IsTyping = false; _routine = null;
        OnFullyDisplayed?.Invoke();
    }
}