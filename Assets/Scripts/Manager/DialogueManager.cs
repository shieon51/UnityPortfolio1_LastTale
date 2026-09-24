using Ink.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DialogueManager : Singleton<DialogueManager>
{
    public TextAsset inkJSON; // Ink 스크립트가 JSON으로 컴파일된 파일
    private Story story;
    private EventData curEventData;

    public event Action<EventData> OnDialogueEnd; //다이얼로그가 끝나면 실행됨

    public bool IsWaitingForInput { get; private set; }
    public event Action<bool> OnWaitingForInputChanged;
    public SpeechBubbleController CurrentActiveBubble => _currentActiveBubble; // ★ 추가

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
    private bool _choicesReadyToReveal; // ★ "텍스트는 다 나왔고 엔터만 누르면 선택지 뜸" 상태
    private Coroutine _autoAdvanceCoroutine;

    private float _queuedAutoDelay;
    private bool _queuedForcePanel, _queuedLockInput;
    private string _queuedSpeakerKey, _queuedSpeakerName;

    private string _queuedText; // ★ 신규 — 엿보다 발견한 "진짜 다음 줄"을 저장해두는 큐
    private bool _queuedHasChoices;

    private List<Ink.Runtime.Choice> _pendingChoices;
    private SpeechBubbleController _currentActiveBubble; // ★ 지금 활성화된 말풍선(있으면) — 타이핑 체크/스킵용

    public bool IsTalking
    { get { return isTalking; } }
    public bool IsChoices        
    { get { return isChoices; } }

    // 이 정보를 알려준 대상. 방금 말한 화자를 우선하고, 없으면 대화 상대 NPC
    public string CurrentSpeakerOrEventNpc
    {
        get
        {
            if (!string.IsNullOrEmpty(_pendingSpeakerKey)) return _pendingSpeakerKey;
            if (curEventData != null && EventManager.IsNPCEvent(curEventData.EventID)) return curEventData.EventName;
            return null;
        }
    }

    private bool isProcessingLine = false;

    private void Start()
    {
        story = new Story(inkJSON.text);
        BindMemoryFunctions(); 
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.F9))   // ★ 진단용 — 에디터에서만
        {
            Debug.Log($"[진단] IsTalking={isTalking} IsChoices={isChoices} choicesReady={_choicesReadyToReveal} " +
                      $"autoAdvancing={_isAutoAdvancing} lockInput={_lockInput} GlobalLock={GlobalActionLock.IsLocked}");
        }
#endif
        if (!IsTalking) return;

        if (IsChoices)
        {
            if (InputBindings.GetKeyDown(InputAction.NavigateUp)) UIManager.Instance.dialogue.NavigateChoice(-1);
            else if (InputBindings.GetKeyDown(InputAction.NavigateDown)) UIManager.Instance.dialogue.NavigateChoice(1);
            else if (InputBindings.GetKeyDown(InputAction.Confirm)) UIManager.Instance.dialogue.ConfirmSelectedChoice();
            return;
        }

        if (_isAutoAdvancing)
        {
            if (InputBindings.GetKeyDown(InputAction.Confirm))
            {
                if (_autoAdvanceCoroutine != null) StopCoroutine(_autoAdvanceCoroutine);
                _isAutoAdvancing = false;
                DisplayNextLine();
            }
            return;
        }

        if (_lockInput || !InputBindings.GetKeyDown(InputAction.Confirm)) return;

        bool isTyping = _currentActiveBubble != null ? _currentActiveBubble.IsTyping : UIManager.Instance.dialogue.IsTyping;
        if (isTyping) { SkipCurrentTyping(); return; }

        if (_choicesReadyToReveal) { RevealPendingChoices(); return; } // ★ 5번 — 엔터로 선택지 공개

        DisplayNextLine();
    }

    // ★ 타이핑 완료 핸들러를 모두 떼어낸다.
    //   이벤트가 발생해야만 해제되던 구조라, 대화가 중간에 끊기면 구독이 남아
    //   다음 대화에서 엉뚱하게 깨어나는 문제가 있었다
    private void UnsubscribeTypingHandlers()
    {
        var panel = UIManager.Instance?.dialogue;
        if (panel != null)
        {
            panel.OnTextFullyDisplayed -= HandleChoicesTextFullyDisplayed;
            panel.OnTextFullyDisplayed -= HandleWaitTextFullyDisplayed;
            panel.OnTextFullyDisplayed -= HandleAutoAdvanceTextFullyDisplayed;
        }
        if (_currentActiveBubble != null)
        {
            _currentActiveBubble.OnTextFullyDisplayed -= HandleChoicesTextFullyDisplayed;
            _currentActiveBubble.OnTextFullyDisplayed -= HandleWaitTextFullyDisplayed;
            _currentActiveBubble.OnTextFullyDisplayed -= HandleAutoAdvanceTextFullyDisplayed;
        }
    }

    private void SkipCurrentTyping()
    {
        if (_currentActiveBubble != null) _currentActiveBubble.SkipTyping();
        else UIManager.Instance.dialogue.SkipTyping();
    }

    private void RevealPendingChoices()
    {
        _choicesReadyToReveal = false;
        SetWaitingForInput(false);
        UIManager.Instance.ShowChoices(_pendingChoices);
        isChoices = true;
        _pendingChoices = null;
    }

    private void BindMemoryFunctions()
    {
        story.BindExternalFunction("has_memory", (string flagId) => MemoryManager.Instance.HasMemory(flagId));
        // ★ lookaheadSafe: false 필수 — 기본값(true)이면 ink가 앞을 미리 계산할 때
        //   함수가 먼저(또는 두 번) 실행되어 정보 획득과 들음 기록이 중복된다
        story.BindExternalFunction("acquire_memory", (string flagId) =>
        {
            MemoryManager.Instance.AcquireMemory(flagId, CurrentSpeakerOrEventNpc);
            return 0;
        }, lookaheadSafe: false);
        story.BindExternalFunction("erase_memory", (string flagId) => { MemoryManager.Instance.EraseMemory(flagId); return 0; }, lookaheadSafe: false);
        story.BindExternalFunction("get_counter", (string key) => MemoryManager.Instance.GetCounter(key));
        story.BindExternalFunction("increment_counter", (string key) => { MemoryManager.Instance.IncrementCounter(key); return 0; }, lookaheadSafe: false);
        story.BindExternalFunction("get_affection", (string npcName) => NPCManager.Instance.GetNPCData(npcName).hiddenAffection);
        story.BindExternalFunction("add_affection", (string npcName, int amount) =>
        {
            var data = NPCManager.Instance.GetNPCData(npcName);
            data.AddAffection(amount);
            NPCManager.Instance.SaveNPCData(data);
            return 0;
        }, lookaheadSafe: false);
        story.BindExternalFunction("get_understanding_percent", (string npcName) =>
            (int)Mathf.Round(NPCManager.Instance.GetNPCData(npcName).UnderstandingPercent));
        story.BindExternalFunction("add_suspicion", (string npcName, int amount) =>
        { SuspicionManager.Instance.AddDirectSuspicion(npcName, amount); return 0; }, lookaheadSafe: false);
        story.BindExternalFunction("get_suspicion", (string npcName) => SuspicionManager.Instance.GetSuspicion(npcName));
        story.BindExternalFunction("add_suspicion_for", (string npcName, int amount, string counterKey) =>
        { SuspicionManager.Instance.AddDirectSuspicion(npcName, amount, counterKey); return 0; }, lookaheadSafe: false);
        story.BindExternalFunction("can_observe", (string npcName, string counterKey) =>
            SuspicionManager.Instance.CanObserve(npcName, counterKey));
        story.BindExternalFunction("resolve_confession", (string npc) => SuspicionManager.Instance.ResolveConfession(npc), lookaheadSafe: false);
        story.BindExternalFunction("get_trust_earned", (string npc) => SuspicionManager.Instance.GetTrustEarned(npc));
        story.BindExternalFunction("add_trust_earned", (string npc, int amt) => { SuspicionManager.Instance.AddTrustEarned(npc, amt); return 0; }, lookaheadSafe: false);
        story.BindExternalFunction("get_line_crossed", (string npc) => SuspicionManager.Instance.GetLineCrossed(npc));
        story.BindExternalFunction("add_line_crossed", (string npc, int amt) => { SuspicionManager.Instance.AddLineCrossed(npc, amt); return 0; }, lookaheadSafe: false);
        story.BindExternalFunction("get_personal_bond", (string npc) => (PlayerManager.Instance.CurrentCharacter as SoraStats)?.GetPersonalBond(npc) ?? 0);
        story.BindExternalFunction("add_personal_bond", (string npc, int amt) => { (PlayerManager.Instance.CurrentCharacter as SoraStats)?.AddPersonalBond(npc, amt); return 0; }, lookaheadSafe: false);
        story.BindExternalFunction("get_mental_ratio", () => (int)(((PlayerManager.Instance.CurrentCharacter as SoraStats)?.MentalRatio ?? 1f) * 100f));
        story.BindExternalFunction("recover_mental", (int amount) =>
        { (PlayerManager.Instance.CurrentCharacter as SoraStats)?.RecoverMental(amount); return 0; }, lookaheadSafe: false);
        story.BindExternalFunction("lose_mental", (int amount) =>
        { (PlayerManager.Instance.CurrentCharacter as SoraStats)?.LoseMental(amount); return 0; }, lookaheadSafe: false);
        story.BindExternalFunction("move_npc", (string npcName, float x, float y, float duration) => // ink에서 걸어오게 하기 // *
        {
            NPCManager.Instance.MoveNPCTo(npcName, new Vector2(x, y), duration);
            return 0;
        }, lookaheadSafe: false);
        story.BindExternalFunction("move_npc_rel", (string npcName, float ox, float oy, float duration) =>
        {
            Vector2 target = (curEventData != null ? curEventData.Position : Vector2.zero) + new Vector2(ox, oy);
            NPCManager.Instance.MoveNPCTo(npcName, target, duration);
            return 0;
        }, lookaheadSafe: false);
    }

    public void StartStory(EventData eventData)
    {
        // ★ 이전 대화가 중간에 끊겼을 수 있으므로 상태를 전부 초기화한다
        UnsubscribeTypingHandlers();
        if (_autoAdvanceCoroutine != null) { StopCoroutine(_autoAdvanceCoroutine); _autoAdvanceCoroutine = null; }
        _isAutoAdvancing = false;
        _choicesReadyToReveal = false;
        _pendingChoices = null;
        isChoices = false;
        isProcessingLine = false;
        _currentActiveBubble = null;
        UIManager.Instance.ClearChoices();

        curEventData = eventData;
        _pendingSpeakerKey = null; _pendingSpeakerDisplayName = null;
        _queuedText = null;

        UIManager.Instance.ShowDialogUI();

        // ★ 노드 이름이 틀리면 여기서 예외가 나며 대화가 멈춘다 — 원인을 바로 알 수 있게
        try { story.ChoosePathString(curEventData.InkNodeName); }
        catch (System.Exception e)
        {
            Debug.LogError($"[DialogueManager] ink 노드를 찾을 수 없음: '{curEventData.InkNodeName}' (이벤트 {curEventData.EventName}) — {e.Message}");
            UIManager.Instance.HideDialogUI();
            return;
        }

        isTalking = true;
        DisplayNextLine();
    }

    public void DisplayNextLine()
    {
        if (isProcessingLine) return;
        isProcessingLine = true;

        string text;
        bool hasChoices;

        if (_queuedText != null) // ★ 이미 엿봐둔 줄이 있으면 Continue() 없이 그대로 사용
        {
            text = _queuedText;
            hasChoices = _queuedHasChoices;
            _autoAdvanceDelay = _queuedAutoDelay;    // ★ 저장해둔 태그 복원
            _forcePanel = _queuedForcePanel; _lockInput = _queuedLockInput;
            _pendingSpeakerKey = _queuedSpeakerKey; _pendingSpeakerDisplayName = _queuedSpeakerName;
            _queuedText = null;
        }
        else if (story.canContinue)
        {
            text = story.Continue();
            ParseTags();
            hasChoices = story.currentChoices.Count > 0;

            while (string.IsNullOrWhiteSpace(text) && !hasChoices && story.canContinue) // 순수 빈 스텝만 자동 흡수
            {
                text = story.Continue();
                ParseTags();
                hasChoices = story.currentChoices.Count > 0;
            }
        }
        else { EndDialogue(); StartCoroutine(ResetProcessingFlag()); return; }

        //Debug.Log($"[DBG Ink] text=\"{text}\" hasChoices={hasChoices} canContinue={story.canContinue}");

        if (string.IsNullOrWhiteSpace(text) && !hasChoices)
        {
            isProcessingLine = false;
            DisplayNextLine();
            return;
        }

        if (string.IsNullOrWhiteSpace(text) && hasChoices)
        {
            _pendingChoices = story.currentChoices;
            _choicesReadyToReveal = true;
            SetWaitingForInput(true);
            StartCoroutine(ResetProcessingFlag());
            return;
        }

        // ★ 여기부터 진짜 텍스트가 있는 정상 라인
        bool useBubble = !string.IsNullOrEmpty(_pendingSpeakerKey) && !_forcePanel;
        Transform speaker = useBubble ? ResolveSpeakerTransform(_pendingSpeakerKey) : null;
        SpeechBubbleController bubble = speaker?.GetComponentInChildren<SpeechBubbleController>(true);

        // ★ 선택지가 아직 없으면, "조건 확정용 빈 스텝인지" 딱 한 번 엿봄 (표시 전에 처리)
        if (!hasChoices && story.canContinue)
        {
            float savedAuto = _autoAdvanceDelay;      // ★ 현재 줄의 태그 값을 백업
            bool savedPanel = _forcePanel, savedLock = _lockInput;
            string savedSpeakerKey = _pendingSpeakerKey, savedSpeakerName = _pendingSpeakerDisplayName;

            string peek = story.Continue();
            ParseTags();
            bool peekHasChoices = story.currentChoices.Count > 0;

            //Debug.Log($"[DBG Ink Peek] peek=\"{peek}\" peekHasChoices={peekHasChoices} canContinue={story.canContinue}"); // ★ 추가 — 이게 빠져있어서 여기서 무슨 일이 있었는지 안 보였음

            if (string.IsNullOrWhiteSpace(peek))
            {
                hasChoices = peekHasChoices; // 예상한 케이스 — 이번 줄에 선택지 귀속
                _autoAdvanceDelay = savedAuto; _forcePanel = savedPanel; _lockInput = savedLock; // ★ 복원
                _pendingSpeakerKey = savedSpeakerKey; _pendingSpeakerDisplayName = savedSpeakerName;
            }
            else
            {
                _queuedText = peek;                    // 다음 줄용으로 태그도 함께 저장
                _queuedHasChoices = peekHasChoices;
                _queuedAutoDelay = _autoAdvanceDelay;
                _queuedForcePanel = _forcePanel; _queuedLockInput = _lockInput;
                _queuedSpeakerKey = _pendingSpeakerKey; _queuedSpeakerName = _pendingSpeakerDisplayName;

                _autoAdvanceDelay = savedAuto; _forcePanel = savedPanel; _lockInput = savedLock; // ★ 현재 줄 값 복원
                _pendingSpeakerKey = savedSpeakerKey; _pendingSpeakerDisplayName = savedSpeakerName;
            }
        }

        UnsubscribeTypingHandlers();   // ★ 이전 줄의 구독을 먼저 정리
        SetWaitingForInput(false); // ★ 추가 — 새 줄 표시(타이핑 시작)하는 순간 무조건 끔
        if (bubble != null)
        {
            UIManager.Instance.HideDialogUI();
            SpeechBubbleManager.Instance?.ShowBubble(speaker, _pendingSpeakerDisplayName, text);
            _currentActiveBubble = bubble;
            if (hasChoices) { _pendingChoices = story.currentChoices; bubble.OnTextFullyDisplayed += HandleChoicesTextFullyDisplayed; }
        }
        else
        {
            SpeechBubbleManager.Instance?.HideAll();
            _currentActiveBubble = null;
            UIManager.Instance.ShowDialogUI();
            UIManager.Instance.UpdateDialogueText(text);
            if (hasChoices) { _pendingChoices = story.currentChoices; UIManager.Instance.dialogue.OnTextFullyDisplayed += HandleChoicesTextFullyDisplayed; }
        }

        if (!hasChoices)
        {
            if (_autoAdvanceDelay >= 0f)
            {
                if (_currentActiveBubble != null) _currentActiveBubble.OnTextFullyDisplayed += HandleAutoAdvanceTextFullyDisplayed;
                else UIManager.Instance.dialogue.OnTextFullyDisplayed += HandleAutoAdvanceTextFullyDisplayed;
            }
            else
            {
                if (_currentActiveBubble != null) _currentActiveBubble.OnTextFullyDisplayed += HandleWaitTextFullyDisplayed;
                else UIManager.Instance.dialogue.OnTextFullyDisplayed += HandleWaitTextFullyDisplayed;
            }
        }

        StartCoroutine(ResetProcessingFlag());
    }

    private void SetWaitingForInput(bool waiting)
    {
        IsWaitingForInput = waiting;
        OnWaitingForInputChanged?.Invoke(waiting);
    }

    private void HandleWaitTextFullyDisplayed() // ★ 선택지 없는 줄 — 타이핑 끝나면 대기 표시
    {
        UIManager.Instance.dialogue.OnTextFullyDisplayed -= HandleWaitTextFullyDisplayed;
        if (_currentActiveBubble != null) _currentActiveBubble.OnTextFullyDisplayed -= HandleWaitTextFullyDisplayed;
        SetWaitingForInput(true);
    }

    private void HandleAutoAdvanceTextFullyDisplayed() // ★ 타이핑 끝난 뒤부터 자동진행 타이머 시작
    {
        UIManager.Instance.dialogue.OnTextFullyDisplayed -= HandleAutoAdvanceTextFullyDisplayed;
        if (_currentActiveBubble != null) _currentActiveBubble.OnTextFullyDisplayed -= HandleAutoAdvanceTextFullyDisplayed;
        _isAutoAdvancing = true;
        _autoAdvanceCoroutine = StartCoroutine(AutoAdvanceAfter(_autoAdvanceDelay));
    }

    private IEnumerator AutoAdvanceAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        _isAutoAdvancing = false;
        DisplayNextLine();
    }

    private Transform ResolveSpeakerTransform(string key) => SpeakerResolver.Resolve(key);

    private void HandleChoicesTextFullyDisplayed()
    {
        UIManager.Instance.dialogue.OnTextFullyDisplayed -= HandleChoicesTextFullyDisplayed;
        if (_currentActiveBubble != null) _currentActiveBubble.OnTextFullyDisplayed -= HandleChoicesTextFullyDisplayed;
        if (_pendingChoices != null) { _choicesReadyToReveal = true; SetWaitingForInput(true); }
    }

    //private void HandleTextFullyDisplayed() // ★ 타이핑 끝난 시점 — 선택지가 있으면 "엔터로 공개" 대기 상태로 전환
    //{
    //    UIManager.Instance.dialogue.OnTextFullyDisplayed -= HandleTextFullyDisplayed;
    //    if (_currentActiveBubble != null) _currentActiveBubble.OnTextFullyDisplayed -= HandleTextFullyDisplayed;

    //    if (_pendingChoices != null)
    //    {
    //        _choicesReadyToReveal = true;
    //        SetWaitingForInput(true);
    //    }
    //}

    // ★ 태그 파싱 중복 제거 (기존 foreach 두 번 반복되던 걸 메서드로 뽑음)
    private void ParseTags()
    {
        _forcePanel = false;
        _autoAdvanceDelay = -1f;
        _lockInput = false;

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
        UnsubscribeTypingHandlers();   // ★ 추가
        isTalking = false;
        UIManager.Instance.HideDialogUI();
        SpeechBubbleManager.Instance?.HideAll();
        _currentActiveBubble = null;
        SetWaitingForInput(false);
        _choicesReadyToReveal = false;
        _queuedText = null;

        OnDialogueEnd?.Invoke(curEventData);

        // ★ 예약은 무조건 비운다. 남겨두면 다음 대화가 끝날 때 엉뚱하게 발동한다
        string battleNpc = pendingBattleNPC;
        string winNode = pendingBattleWinNode, loseNode = pendingBattleLoseNode;
        var difficulty = pendingBattleDifficulty;
        pendingBattleNPC = ""; pendingBattleWinNode = ""; pendingBattleLoseNode = "";
        pendingBattleDifficulty = BossDifficultyTier.Training;

        if (!string.IsNullOrEmpty(battleNpc))
            NPCManager.Instance.TriggerBossBattle(battleNpc, difficulty, winNode, loseNode);
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
}
