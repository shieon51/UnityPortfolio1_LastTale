using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 다이얼로그 전담
public class DialogueUIPanel : MonoBehaviour
{
    [Header("본문")]
    public TypewriterText typewriter;      // 인스펙터에서 dialogueText 오브젝트에 이 컴포넌트 추가해서 연결
    public TextMeshProUGUI dialogueText;
    [Tooltip("대화 중에만 켜지는 루트. 패널 배경·본문·선택지를 모두 자식으로 둔다")]
    public GameObject dialogueRoot;        // ★ 이름 변경 (기존 dialoguePanel)

    [Header("선택지")]
    public GameObject choiceContainer;
    [Tooltip("선택지 버튼 프리팹. 비워두면 Resources 경로로 대체 로드한다")]
    public GameObject choiceButtonPrefab;                       // ★ 경로 하드코딩 제거
    [Tooltip("choiceButtonPrefab이 비었을 때 사용할 Resources 경로")]
    public string choiceButtonResourcePath = "Prefabs/ChoiceButton";

    public event Action OnTextFullyDisplayed;

    private readonly List<GameObject> _choiceButtons = new();
    private int _selectedChoiceIndex = 0;

    private void Awake()
    {
        if (choiceButtonPrefab == null)
        {
            choiceButtonPrefab = Resources.Load<GameObject>(choiceButtonResourcePath);
            if (choiceButtonPrefab == null)
                Debug.LogError($"[DialogueUIPanel] 선택지 프리팹을 찾을 수 없음 — 인스펙터에 연결하거나 '{choiceButtonResourcePath}'를 확인", this);
        }

        if (typewriter != null)
            typewriter.OnFullyDisplayed += () => OnTextFullyDisplayed?.Invoke();
    }

    private void Start() => Hide();

    public void Show() => dialogueRoot.SetActive(true);

    public void Hide()
    {
        // ★ 끄기 전에 먼저 정리한다.
        //   반대 순서면 꺼진 DialogueText에서 코루틴을 시작하려다 에러가 난다
        UpdateText("");
        ClearChoices();
        dialogueRoot.SetActive(false);
    }

    public void UpdateText(string text) => typewriter.Play(text);

    public bool IsTyping => typewriter.IsTyping;
    public void SkipTyping() => typewriter.Skip();

    public void ShowChoices(List<Ink.Runtime.Choice> choices)
    {
        ClearChoices();                                          // ★ 리스트만 비우던 것 → 남은 버튼까지 정리
        if (choiceButtonPrefab == null || choices == null) return;

        for (int i = 0; i < choices.Count; i++)
        {
            int capturedIndex = i;                               // 클로저 캡처용
            var choice = choices[i];

            GameObject btn = Instantiate(choiceButtonPrefab, choiceContainer.transform);

            var label = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = choice.text;

            var button = btn.GetComponent<Button>();
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => DialogueManager.Instance.OnChoiceSelected(choice.index));
            }

            // ★ 프리팹에 이미 붙어 있으면 그대로 쓰고, 없을 때만 추가한다
            var highlighter = btn.GetComponent<ChoiceButtonHighlighter>();
            if (highlighter == null) highlighter = btn.AddComponent<ChoiceButtonHighlighter>();
            highlighter.index = capturedIndex;
            highlighter.onPressed = SetSelectedIndex;

            _choiceButtons.Add(btn);
        }

        _selectedChoiceIndex = 0;
        HighlightChoice(0);
    }

    public void SetSelectedIndex(int index)                      // 마우스·키보드 공용 진입점
    {
        _selectedChoiceIndex = index;
        HighlightChoice(index);
    }

    public void ClearChoices()
    {
        // ★ Destroy는 프레임 끝에 처리되므로, 같은 프레임에 새 선택지를 만들면
        //   옛 버튼이 레이아웃에 잠시 남아 위치가 튄다. 부모에서 먼저 떼어낸다.
        var parent = choiceContainer.transform;
        for (int i = parent.childCount - 1; i >= 0; i--)   // ★ 뒤에서부터 제거해야 건너뛰지 않는다
        {
            var child = parent.GetChild(i);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
        _choiceButtons.Clear();
        _selectedChoiceIndex = 0;
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
        var button = _choiceButtons[_selectedChoiceIndex].GetComponent<Button>();
        if (button != null) button.onClick.Invoke();
    }

    private void HighlightChoice(int index)
    {
        for (int i = 0; i < _choiceButtons.Count; i++)
        {
            if (_choiceButtons[i] == null) continue;
            var outline = _choiceButtons[i].GetComponent<Outline>();
            if (outline != null) outline.enabled = (i == index);
        }
    }
}