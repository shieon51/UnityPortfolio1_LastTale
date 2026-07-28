using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 다이얼로그 전담
public class DialogueUIPanel : MonoBehaviour
{
    public TextMeshProUGUI dialogueText;
    public GameObject dialoguePanel;
    public GameObject choiceContainer;
    private GameObject _choiceButtonPrefab;

    private void Awake() => _choiceButtonPrefab = Resources.Load<GameObject>("Prefabs/ChoiceButton");
    private void Start() => Hide();

    public void Show() => dialoguePanel.SetActive(true);
    public void Hide() { dialoguePanel.SetActive(false); UpdateText(""); ClearChoices(); }
    public void UpdateText(string text) => dialogueText.text = text;

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