using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 다이얼로그 전담
public class DialogueUIPanel : MonoBehaviour
{
    public event Action OnTextFullyDisplayed; // ★ 신규 — 나중에 타자기 효과가 다 친 시점으로 옮기면 됨
    
    public TextMeshProUGUI dialogueText;
    public GameObject dialoguePanel;
    public GameObject choiceContainer;
    private GameObject _choiceButtonPrefab;

    private void Awake() => _choiceButtonPrefab = Resources.Load<GameObject>("Prefabs/ChoiceButton");
    private void Start() => Hide();

    public void Show() => dialoguePanel.SetActive(true);
    public void Hide() { dialoguePanel.SetActive(false); UpdateText(""); ClearChoices(); }
    public void UpdateText(string text)
    {
        dialogueText.text = text;
        OnTextFullyDisplayed?.Invoke(); // ★ 지금은 즉시 다 나오니 바로 발행. 타자기 도입 시 이 줄을 타이핑 코루틴 끝으로 옮기면 끝 // **
    }

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