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

    private bool isTalking = false; //현재 대화가 진행중일 때 -> EventTrigger에서 Z키 입력 불가, 엔터 키 입력 가능 처리.
    private bool isChoices = false; //선택지가 주어진 상태일 때 -> EventTrigger에서 엔터키 입력에 대한 예외처리

    private string pendingBattleNPC = ""; // 전투가 예약된 NPC 이름

    public bool IsTalking
    { get { return isTalking; } }
    public bool IsChoices        
    { get { return isChoices; } }

    private bool isProcessingLine = false;

    private void Start()
    {
        //CloseDialog();
        story = new Story(inkJSON.text);
    }

    private void Update()
    {
        //대화 중일 때 엔터 입력하면 -> 다음 대사 출력 (단, 선택지가 있을 경우 엔터 키 입력 막기)
        if (IsTalking && !IsChoices && Input.GetKeyDown(KeyCode.Return))
        {
            DisplayNextLine();
        }
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
            print(text);
            UIManager.Instance.UpdateDialogueText(text);

            // 태그 파싱: #battle:Liel 이 있으면 예약!
            foreach (string tag in story.currentTags)
            {
                string[] args = tag.Split(':');
                if (args[0] == "battle" && args.Length > 1)
                {
                    pendingBattleNPC = args[1]; // "Liel" 예약
                }
            }
            //// 태그를 읽어올 때 Split(':')을 사용
            //foreach (string tag in story.currentTags)
            //{
            //    string[] args = tag.Split(':');
            //    string command = args[0];

            //    switch (command)
            //    {
            //        case "cameraShake":
            //            float duration = float.Parse(args[1]); // 0.5
            //            float power = float.Parse(args[2]);    // 10.0
            //            CameraManager.Instance.Shake(duration, power);
            //            break;

            //        case "textSpeed":
            //            ChangeTextSpeed(args[1]); // "fast" 또는 "normal"
            //            break;

            //        case "hideUI":
            //            UIManager.Instance.HideDialogUI();
            //            break;
            //    }
            //}

            // 선택지가 있는지 확인 후 처리
            if (story.currentChoices.Count > 0)
            {
                UIManager.Instance.ShowChoices(story.currentChoices);
                isChoices = true;
            }
        }
        else
        {
            EndDialogue();
        }

        StartCoroutine(ResetProcessingFlag());
    }

    private void EndDialogue()
    {
        isTalking = false;
        UIManager.Instance.HideDialogUI();

        // 💡 2. 대화가 끝나는 순간! 잉크 속의 호감도 변수를 뽑아와서 NPCManager에 전달
        // (잉크에 선언된 변수 이름과 동일해야 함)
        int lielFriendship = (int)story.variablesState["Liel_friendship"];

        NPCData lielData = NPCManager.Instance.GetNPCData("Liel");
        lielData.hiddenAffection = lielFriendship; // 덮어씌우기
        NPCManager.Instance.SaveNPCData(lielData); // 영구 저장!

        OnDialogueEnd?.Invoke(curEventData); // 다이얼로그가 끝나면 실행하기

        // 💡 [신규] 대화가 완전히 끝난 직후, 예약된 전투가 있다면 실행!
        if (!string.IsNullOrEmpty(pendingBattleNPC))
        {
            NPCManager.Instance.TriggerBossBattle(pendingBattleNPC);
            pendingBattleNPC = ""; // 초기화
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

}
