using Ink.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.UI;

public class DialogueManager : Singleton<DialogueManager>
{
    public TextAsset inkJSON; // Ink 스크립트가 JSON으로 컴파일된 파일
    private Story story;
    private EventData curEventData;

    public event Action<EventData> OnDialogueEnd; //다이얼로그가 끝나면 실행됨

    [Header("선택지 등장 딜레이 (텍스트 다 나온 뒤)")]
    public float choiceRevealDelay = 0.3f;

    public bool IsWaitingForInput { get; private set; }
    public event Action<bool> OnWaitingForInputChanged;

    private bool isTalking = false; //현재 대화가 진행중일 때 -> EventTrigger에서 Z키 입력 불가, 엔터 키 입력 가능 처리.
    private bool isChoices = false; //선택지가 주어진 상태일 때 -> EventTrigger에서 엔터키 입력에 대한 예외처리

    private string pendingBattleNPC = ""; // 전투가 예약된 NPC 이름
    private string pendingBattleWinNode = "";
    private string pendingBattleLoseNode = "";
    private BossDifficultyTier pendingBattleDifficulty = BossDifficultyTier.Training; // ★ 추가

    private string _pendingSpeakerKey, _pendingSpeakerDisplayName; // ★ #speak/#system으로만 바뀜 — 매번 리셋 안 됨
    private bool _forcePanel;      // ★ 매 줄마다 리셋됨 — "이 줄만" 적용
    private float _autoAdvanceDelay; // ★ 매 줄마다 리셋됨
    private bool _lockInput;         // ★ 매 줄마다 리셋됨
    private bool _isAutoAdvancing;

    private List<Ink.Runtime.Choice> _pendingChoices;
    private SpeechBubbleController _currentActiveBubble; // ★ 지금 활성화된 말풍선(있으면) — 타이핑 체크/스킵용

    public bool IsTalking
    { get { return isTalking; } }
    public bool IsChoices        
    { get { return isChoices; } }

    private bool isProcessingLine = false;

    private void Start()
    {
        story = new Story(inkJSON.text);
        BindMemoryFunctions(); // ★ 추가
    }

    private void Update()
    {
        if (!IsTalking) return;

        if (IsChoices) // 선택지 엔터 선택
        {
            if (Input.GetKeyDown(KeyCode.UpArrow)) UIManager.Instance.dialogue.NavigateChoice(-1);
            else if (Input.GetKeyDown(KeyCode.DownArrow)) UIManager.Instance.dialogue.NavigateChoice(1);
            else if (Input.GetKeyDown(KeyCode.Return)) UIManager.Instance.dialogue.ConfirmSelectedChoice();
            return;
        }

        if (_lockInput || !Input.GetKeyDown(KeyCode.Return)) return;

        bool isTyping = _currentActiveBubble != null ? _currentActiveBubble.IsTyping : UIManager.Instance.dialogue.IsTyping;
        if (isTyping) { SkipCurrentTyping(); return; }
        if (_isAutoAdvancing) return;

        DisplayNextLine();
    }

    private void SkipCurrentTyping()
    {
        if (_currentActiveBubble != null) _currentActiveBubble.SkipTyping();
        else UIManager.Instance.dialogue.SkipTyping();
    }

    private void BindMemoryFunctions()
    {
        story.BindExternalFunction("has_memory", (string flagId) => MemoryManager.Instance.HasMemory(flagId));
        story.BindExternalFunction("acquire_memory", (string flagId) => { MemoryManager.Instance.AcquireMemory(flagId); return 0; }, lookaheadSafe: false);
        story.BindExternalFunction("erase_memory", (string flagId) => { MemoryManager.Instance.EraseMemory(flagId); return 0; }, lookaheadSafe: false);
        story.BindExternalFunction("get_counter", (string key) => MemoryManager.Instance.GetCounter(key));
        story.BindExternalFunction("increment_counter", (string key) => { MemoryManager.Instance.IncrementCounter(key); return 0; }, lookaheadSafe: false);
        story.BindExternalFunction("get_affection", (string npcName) => NPCManager.Instance.GetNPCData(npcName).hiddenAffection);
        story.BindExternalFunction("add_affection", (string npcName, int amount) =>
        {
            var data = NPCManager.Instance.GetNPCData(npcName);
            data.AddAffection(amount); // ★ 직접 필드 대입 대신 클램프 메서드로
            NPCManager.Instance.SaveNPCData(data);
            return 0;
        }, lookaheadSafe: false);
        story.BindExternalFunction("get_understanding_percent", (string npcName) =>
            (int)Mathf.Round(NPCManager.Instance.GetNPCData(npcName).UnderstandingPercent));
    }

    public void StartStory(EventData eventData)
    {
        curEventData = eventData;
        _pendingSpeakerKey = null; _pendingSpeakerDisplayName = null; // 새 대화 시작할 때만 화자 초기화
        UIManager.Instance.ShowDialogUI();                             // ★ 복구 — 다이얼로그 UI 컨테이너 활성화
        story.ChoosePathString(curEventData.InkNodeName);               // ★ 복구 — 이게 핵심, 어느 노드부터 시작할지 지정
        isTalking = true;
        DisplayNextLine();
    }

    public void DisplayNextLine()
    {
        if (isProcessingLine) return; // 이미 실행 중이면 무시
        isProcessingLine = true; // 실행 시작

        if (story.canContinue)
        {
            string text = story.Continue();
            ParseTags();
            bool hasChoices = story.currentChoices.Count > 0;

            Debug.Log($"[DBG Ink] text=\"{text}\" hasChoices={hasChoices} canContinue={story.canContinue}");

            if (string.IsNullOrWhiteSpace(text) && !hasChoices) // 순수 빈 스텝 — 자동 스킵
            {
                isProcessingLine = false;
                DisplayNextLine();
                return;
            }

            if (string.IsNullOrWhiteSpace(text) && hasChoices) // ★ 7번 핵심 수정 — 화면은 그대로 두고 선택지만 이어서
            {
                _pendingChoices = story.currentChoices;
                StartCoroutine(ShowChoicesAfterDelay(_pendingChoices));
            }
            else // 진짜 표시할 텍스트가 있는 정상 케이스
            {
                bool useBubble = !string.IsNullOrEmpty(_pendingSpeakerKey) && !_forcePanel;
                Transform speaker = useBubble ? ResolveSpeakerTransform(_pendingSpeakerKey) : null;
                SpeechBubbleController bubble = speaker?.GetComponentInChildren<SpeechBubbleController>(true);

                if (bubble != null)
                {
                    UIManager.Instance.HideDialogUI();
                    SpeechBubbleManager.Instance?.ShowBubble(speaker, _pendingSpeakerDisplayName, text); // ★ 9번 수정 — bubble.transform 아니라 speaker
                    _currentActiveBubble = bubble;
                    if (hasChoices) { _pendingChoices = story.currentChoices; bubble.OnTextFullyDisplayed += HandleTextFullyDisplayed; }
                }
                else
                {
                    SpeechBubbleManager.Instance?.HideAll(); // ★ 6번 수정 — 패널로 나갈 땐 열린 말풍선 다 닫음
                    _currentActiveBubble = null;
                    UIManager.Instance.ShowDialogUI();
                    UIManager.Instance.UpdateDialogueText(text);
                    if (hasChoices) { _pendingChoices = story.currentChoices; UIManager.Instance.dialogue.OnTextFullyDisplayed += HandleTextFullyDisplayed; }
                }
            }

            if (_autoAdvanceDelay >= 0f)
            {
                SetWaitingForInput(false);
                _isAutoAdvancing = true;
                StartCoroutine(AutoAdvanceAfter(_autoAdvanceDelay));
            }
            else
            {
                _isAutoAdvancing = false;
                SetWaitingForInput(!hasChoices);
            }
        }
        else EndDialogue();

        StartCoroutine(ResetProcessingFlag());
    }

    private void SetWaitingForInput(bool waiting)
    {
        IsWaitingForInput = waiting;
        OnWaitingForInputChanged?.Invoke(waiting);
    }

    private IEnumerator AutoAdvanceAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        _isAutoAdvancing = false;
        DisplayNextLine();
    }

    private Transform ResolveSpeakerTransform(string key) => SpeakerResolver.Resolve(key);

    private void HandleTextFullyDisplayed() // ★ 패널/말풍선 공용으로 하나로 통합
    {
        UIManager.Instance.dialogue.OnTextFullyDisplayed -= HandleTextFullyDisplayed;
        if (_currentActiveBubble != null) _currentActiveBubble.OnTextFullyDisplayed -= HandleTextFullyDisplayed;
        if (_pendingChoices != null) StartCoroutine(ShowChoicesAfterDelay(_pendingChoices));
        _pendingChoices = null;
    }

    private IEnumerator ShowChoicesAfterDelay(List<Ink.Runtime.Choice> choices)
    {
        yield return new WaitForSeconds(choiceRevealDelay);
        UIManager.Instance.ShowChoices(choices);
        isChoices = true;
    }

    // ★ 태그 파싱 중복 제거 (기존 foreach 두 번 반복되던 걸 메서드로 뽑음)
    private void ParseTags()
    {
        _forcePanel = false;      // ★ 매번 리셋 — 한 줄만 적용
        _autoAdvanceDelay = -1f;  // ★ 매번 리셋 (5번에서 찾은 순서 버그 수정 — ParseTags 안에서 처리)
        _lockInput = false;       // ★ 매번 리셋

        foreach (string tag in story.currentTags)
        {
            string[] args = tag.Split(':');
            if (args[0] == "battle" && args.Length > 1)
            {
                pendingBattleNPC = args[1];
                pendingBattleWinNode = args.Length > 2 ? args[2] : $"{pendingBattleNPC}_Battle_Win";
                pendingBattleLoseNode = args.Length > 3 ? args[3] : $"{pendingBattleNPC}_Battle_Lose";
                pendingBattleDifficulty = (args.Length > 4 && Enum.TryParse(args[4], out BossDifficultyTier parsedTier)) ? parsedTier : BossDifficultyTier.Training;
            }
            else if (args[0] == "speak" && args.Length > 2) { _pendingSpeakerKey = args[1]; _pendingSpeakerDisplayName = args[2]; }
            else if (args[0] == "panel") _forcePanel = true;
            else if (args[0] == "system") { _pendingSpeakerKey = null; _pendingSpeakerDisplayName = null; }
            else if (args[0] == "cue" && args.Length > 1) NarrativeCuePlayer.Instance?.Play(args[1]);
            else if (args[0] == "auto" && args.Length > 1) float.TryParse(args[1], out _autoAdvanceDelay);
            else if (args[0] == "lockinput") _lockInput = true;
        }
    }

    private void EndDialogue()
    {
        isTalking = false;
        UIManager.Instance.HideDialogUI();
        SpeechBubbleManager.Instance?.HideAll();
        _currentActiveBubble = null;

        OnDialogueEnd?.Invoke(curEventData);

        if (!string.IsNullOrEmpty(pendingBattleNPC))
        {
            NPCManager.Instance.TriggerBossBattle(pendingBattleNPC, pendingBattleDifficulty, pendingBattleWinNode, pendingBattleLoseNode);
            pendingBattleNPC = ""; pendingBattleWinNode = ""; pendingBattleLoseNode = "";
            pendingBattleDifficulty = BossDifficultyTier.Training;
        }
    }

    // 코루틴 추가 (짧은 딜레이 후 다시 입력 가능)
    private IEnumerator ResetProcessingFlag()
    {
        yield return new WaitForSeconds(0.1f); // 0.1초 후 다시 입력 가능
        isProcessingLine = false;
    }

    // 선택지를 선택했을 때 실행
    public void OnChoiceSelected(int choiceIndex)
    {
        story.ChooseChoiceIndex(choiceIndex);
        if (story.canContinue) story.Continue();
        UIManager.Instance.ClearChoices();
        isChoices = false;
        DisplayNextLine();
    }

    public void ResetStoryState() // ink 자체 지역변수(만남 카운터 등)를 완전히 새로 시작
    {
        story = new Story(inkJSON.text);
        BindMemoryFunctions();
    }

    // 플레그 ink에 전달하기 (필요 없을 것 같긴 한데... 일단 넣어놓기)
    //public void SetFlag(string flagName, bool value)
    //{
    //    if (story.variablesState[flagName] != null)
    //    {
    //        story.variablesState[flagName] = value;
    //    }
    //}

    


    //private void HandlePanelTextFullyDisplayed()
    //{
    //    UIManager.Instance.dialogue.OnTextFullyDisplayed -= HandlePanelTextFullyDisplayed;
    //    if (_pendingChoices != null) StartCoroutine(ShowChoicesAfterDelay(_pendingChoices));
    //    _pendingChoices = null;
    //}

    //private void HandleBubbleTextFullyDisplayed()
    //{
    //    if (_subscribedBubble != null) _subscribedBubble.OnTextFullyDisplayed -= HandleBubbleTextFullyDisplayed;
    //    _subscribedBubble = null;
    //    if (_pendingChoices != null) StartCoroutine(ShowChoicesAfterDelay(_pendingChoices));
    //    _pendingChoices = null;
    //}
}
