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

            // 선택지가 있는지 확인 후 처리
            if (story.currentChoices.Count > 0)
            {
                UIManager.Instance.ShowChoices(story.currentChoices);
                isChoices = true;
            }
        }
        else
        {
            //TimeManager.Instance.UseTimeCoins(curEventData.TimeTaken, curEventData.EventName != "Sleep"); //invoke 해놓을까? -> 수련, 훈련, 잠자기
            //isTalking = false;
            //dialogueText.text = "";
            //CloseDialog();
            EndDialogue();
        }

        StartCoroutine(ResetProcessingFlag());
    }

    private void EndDialogue()
    {
        isTalking = false;
        UIManager.Instance.HideDialogUI();

        OnDialogueEnd?.Invoke(curEventData); // 다이얼로그가 끝나면 실행하기
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
