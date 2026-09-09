using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 다이얼로그 전담
public class DialogueUIPanel : MonoBehaviour
{
    public TypewriterText typewriter; // 인스펙터에서 dialogueText 오브젝트에 이 컴포넌트 추가해서 연결

    public event Action OnTextFullyDisplayed; // ★ 신규 — 나중에 타자기 효과가 다 친 시점으로 옮기면 됨
    
    public TextMeshProUGUI dialogueText;
    public GameObject dialoguePanel;
    public GameObject choiceContainer;
    private GameObject _choiceButtonPrefab;

    // 선택지 엔터 클릭 관련
    private List<GameObject> _choiceButtons = new();
    private int _selectedChoiceIndex = 0;

    private void Awake()
    {
        _choiceButtonPrefab = Resources.Load<GameObject>("Prefabs/ChoiceButton");
        typewriter.OnFullyDisplayed += () => OnTextFullyDisplayed?.Invoke(); // ★ 지난번 만든 선택지 딜레이 로직이 자동으로 여기 물림
    }
    private void Start() => Hide();

    public void Show() => dialoguePanel.SetActive(true);
    public void Hide() { dialoguePanel.SetActive(false); UpdateText(""); ClearChoices(); }
    public void UpdateText(string text) => typewriter.Play(text);

    public bool IsTyping => typewriter.IsTyping;

    public void SkipTyping() => typewriter.Skip();

    public void ShowChoices(List<Ink.Runtime.Choice> choices)
    {
        _choiceButtons.Clear();
        for (int i = 0; i < choices.Count; i++)
        {
            int capturedIndex = i; // 클로저 캡처용
            var choice = choices[i];
            GameObject btn = Instantiate(_choiceButtonPrefab, choiceContainer.transform);
            btn.GetComponentInChildren<TextMeshProUGUI>().text = choice.text;
            btn.GetComponent<Button>().onClick.AddListener(() => DialogueManager.Instance.OnChoiceSelected(choice.index));

            var highlighter = btn.AddComponent<ChoiceButtonHighlighter>(); // ★ 추가
            highlighter.index = capturedIndex;
            highlighter.onPressed = SetSelectedIndex;

            _choiceButtons.Add(btn);
        }
        _selectedChoiceIndex = 0;
        HighlightChoice(0);
    }

    public void SetSelectedIndex(int index) // ★ 신규 — 마우스/키보드 공용 진입점
    {
        _selectedChoiceIndex = index;
        HighlightChoice(index);
    }

    public void ClearChoices() 
    { 
        foreach (Transform c in choiceContainer.transform) 
            Destroy(c.gameObject); 
        _choiceButtons.Clear(); 
    }

    public void NavigateChoice(int direction)
    {
        if (_choiceButtons.Count == 0) return;
        _selectedChoiceIndex = (_selectedChoiceIndex + direction + _choiceButtons.Count) % _choiceButtons.Count;
        HighlightChoice(_selectedChoiceIndex);
    }
    public void ConfirmSelectedChoice()
    {
        if (_choiceButtons.Count == 0) return;
        _choiceButtons[_selectedChoiceIndex].GetComponent<Button>().onClick.Invoke();
    }

    private void HighlightChoice(int index)
    {
        for (int i = 0; i < _choiceButtons.Count; i++)
        {
            var outline = _choiceButtons[i].GetComponent<Outline>();
            if (outline != null) outline.enabled = (i == index);
        }
    }
}