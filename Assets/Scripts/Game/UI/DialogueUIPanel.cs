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
        foreach (var choice in choices)
        {
            GameObject btn = Instantiate(_choiceButtonPrefab, choiceContainer.transform);
            btn.GetComponentInChildren<TextMeshProUGUI>().text = choice.text;
            btn.GetComponent<Button>().onClick.AddListener(() => DialogueManager.Instance.OnChoiceSelected(choice.index));
        }
    }

    public void ClearChoices() { foreach (Transform child in choiceContainer.transform) Destroy(child.gameObject); }
}