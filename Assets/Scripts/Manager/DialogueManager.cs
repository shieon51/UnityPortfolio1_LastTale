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

    private List<Ink.Runtime.Choice> _pendingChoices;

    private bool isTalking = false; //현재 대화가 진행중일 때 -> EventTrigger에서 Z키 입력 불가, 엔터 키 입력 가능 처리.
    private bool isChoices = false; //선택지가 주어진 상태일 때 -> EventTrigger에서 엔터키 입력에 대한 예외처리

    private string pendingBattleNPC = ""; // 전투가 예약된 NPC 이름
    private string pendingBattleWinNode = "";
    private string pendingBattleLoseNode = "";
    private BossDifficultyTier pendingBattleDifficulty = BossDifficultyTier.Training; // ★ 추가

    private string _pendingSpeakerKey, _pendingSpeakerDisplayName;

    public bool IsTalking
    { get { return isTalking; } }
    public bool IsChoices        
    { get { return isChoices; } }

    private bool isProcessingLine = false;

    private void Start()
    {
        //CloseDialog();
        story = new Story(inkJSON.text);
        BindMemoryFunctions(); // ★ 추가
    }

    private void Update()
    {
        //대화 중일 때 엔터 입력하면 -> 다음 대사 출력 (단, 선택지가 있을 경우 엔터 키 입력 막기)
        if (IsTalking && !IsChoices && Input.GetKeyDown(KeyCode.Return))
        {
            if (UIManager.Instance.dialogue.IsTyping) UIManager.Instance.dialogue.SkipTyping(); // ★ 타이핑 중 첫 엔터는 스킵
            else DisplayNextLine(); // 다 나온 뒤 엔터는 다음 줄
        }
    }

    private void BindMemoryFunctions()
    {
        story.BindExternalFunction("has_memory", (string flagId) => MemoryManager.Instance.HasMemory(flagId));
        story.BindExternalFunction("acquire_memory", (string flagId) =>
        {
            MemoryManager.Instance.AcquireMemory(flagId);
            return 0;
        }, lookaheadSafe: false);
        story.BindExternalFunction("erase_memory", (string flagId) =>
        {
            MemoryManager.Instance.EraseMemory(flagId);
            return 0;
        }, lookaheadSafe: false);

        story.BindExternalFunction("get_counter", (string key) => MemoryManager.Instance.GetCounter(key));
        story.BindExternalFunction("increment_counter", (string key) =>
        {
            MemoryManager.Instance.IncrementCounter(key);
            return 0;
        }, lookaheadSafe: false);

        story.BindExternalFunction("get_affection", (string npcName) => NPCManager.Instance.GetNPCData(npcName).hiddenAffection); // ★ 신규
        story.BindExternalFunction("add_affection", (string npcName, int amount) => // ★ 신규
        {
            var data = NPCManager.Instance.GetNPCData(npcName);
            data.hiddenAffection += amount;
            NPCManager.Instance.SaveNPCData(data);
            return 0;
        }, lookaheadSafe: false);

        story.BindExternalFunction("get_understanding_percent", (string npcName) =>
            (int)Mathf.Round(NPCManager.Instance.GetNPCData(npcName).UnderstandingPercent));
    }

    public void StartStory(EventData eventData)
    {
        curEventData = eventData;
        UIManager.Instance.ShowDialogUI();
        story.ChoosePathString(curEventData.InkNodeName); //대화 내용 불러오기
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

            if (!string.IsNullOrWhiteSpace(text) && !hasChoices && story.canContinue)
            {
                string peek = story.Continue();
                ParseTags();
                hasChoices = story.currentChoices.Count > 0;
                if (!string.IsNullOrWhiteSpace(peek))
                {
                    Debug.LogWarning("[DBG Ink] 예상 밖 추가 텍스트 발견, 이어붙임: " + peek); // ★ 혹시 다른 케이스면 여기서 바로 알 수 있음
                    text += peek;
                }
            }

            if (string.IsNullOrWhiteSpace(text) && !hasChoices)
            {
                isProcessingLine = false;
                DisplayNextLine();
                return;
            }

            if (hasChoices)
            {
                _pendingChoices = story.currentChoices;
                UIManager.Instance.dialogue.OnTextFullyDisplayed += HandleTextFullyDisplayed; // ★ 텍스트 다 나오면 알려달라고 구독
            }

            if (!string.IsNullOrEmpty(_pendingSpeakerKey))
            {
                var speaker = ResolveSpeakerTransform(_pendingSpeakerKey);
                if (speaker != null) SpeechBubbleManager.Instance?.ShowBubble(speaker, _pendingSpeakerDisplayName, text);
            }
            else
            {
                UIManager.Instance.UpdateDialogueText(text); // 태그 없으면 기존 하단 패널(시스템/내레이션/회상용)
            }
        }
        else EndDialogue();

        StartCoroutine(ResetProcessingFlag());
    }

    private Transform ResolveSpeakerTransform(string key)
    {
        if (key == "Player") return PlayerManager.Instance.CurrentCharacter.transform;
        return System.Linq.Enumerable.FirstOrDefault(FindObjectsOfType<NPC>(), n => n.npcName == key)?.transform;
    }

    private void HandleTextFullyDisplayed()
    {
        UIManager.Instance.dialogue.OnTextFullyDisplayed -= HandleTextFullyDisplayed;
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
            else if (args[0] == "speak" && args.Length > 2) // ★ 신규 — #speak:Liel:???
            {
                _pendingSpeakerKey = args[1];
                _pendingSpeakerDisplayName = args[2];
            }
            else if (args[0] == "cue" && args.Length > 1) 
                NarrativeCuePlayer.Instance?.Play(args[1]);
        }
    }

    private void EndDialogue()
    {
        isTalking = false;
        UIManager.Instance.HideDialogUI();

        // 2. 대화가 끝나는 순간 Ink 속의 호감도 변수를 뽑아와 NPCManager에 전달
        // (잉크에 선언된 변수 이름과 동일해야 함)
        //int lielFriendship = (int)story.variablesState["Liel_friendship"];

        //NPCData lielData = NPCManager.Instance.GetNPCData("Liel");
        //lielData.hiddenAffection = lielFriendship; // 덮어씌우기
        //NPCManager.Instance.SaveNPCData(lielData); // 영구 저장

        OnDialogueEnd?.Invoke(curEventData); // 다이얼로그가 끝나면 실행하기

        // 대화가 완전히 끝난 직후 예약된 전투가 있다면 실행
        if (!string.IsNullOrEmpty(pendingBattleNPC))
        {
            NPCManager.Instance.TriggerBossBattle(pendingBattleNPC, pendingBattleDifficulty, pendingBattleWinNode, pendingBattleLoseNode); 
            pendingBattleNPC = "";
            pendingBattleWinNode = "";
            pendingBattleLoseNode = "";
            pendingBattleDifficulty = BossDifficultyTier.Training;
        }
    }

    // 코루틴 추가 (짧은 딜레이 후 다시 입력 가능)
    private IEnumerator ResetProcessingFlag()
    {
        yield return new WaitForSeconds(0.1f); // 0.1초 후 다시 입력 가능
        isProcessingLine = false;
    }

    // 선택지 UI 표시
    //private void DisplayChoices()
    //{
    //    foreach (Choice choice in story.currentChoices)
    //    {
    //        GameObject choiceButton = Instantiate(choiceButtonPrefab, choiceContainer.transform);
    //        choiceButton.GetComponentInChildren<TextMeshProUGUI>().text = choice.text;
    //        choiceButton.GetComponent<Button>().onClick.AddListener(() => OnChoiceSelected(choice.index));
    //    }
    //}

    // 선택지를 선택했을 때 실행
    public void OnChoiceSelected(int choiceIndex)
    {
        story.ChooseChoiceIndex(choiceIndex);
        if (story.canContinue)
        {
            story.Continue(); //(선택지 문장은 출력에서 제외)
        }

        UIManager.Instance.ClearChoices();
        isChoices = false;

        DisplayNextLine();  // 선택 후 다음 줄 실행
    }

    // 선택지 정리 (다음 선택지를 위해 기존 UI 제거)
    //private void ClearChoices()
    //{
    //    foreach (Transform child in choiceContainer.transform)
    //    {
    //        Destroy(child.gameObject);
    //    }
    //}

    // 플레그 ink에 전달하기 (필요 없을 것 같긴 한데... 일단 넣어놓기)
    public void SetFlag(string flagName, bool value)
    {
        if (story.variablesState[flagName] != null)
        {
            story.variablesState[flagName] = value;
        }
    }

    public void ResetStoryState() // ink 자체 지역변수(만남 카운터 등)를 완전히 새로 시작
    {
        story = new Story(inkJSON.text);
        BindMemoryFunctions();
    }
}
