using Ink.Runtime;
using System;
using System.Xml;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class EventTrigger : MonoBehaviour
{
    //이벤트 정보
    public EventData eventData;

    //이벤트 버튼 표시 이미지 & 텍스트
    private TextMeshProUGUI tmpText;
    private Image buttonImage;

    private float interactionRange = 1.5f;
    public float InteractionRange
    {
        get { return interactionRange; }
    }

    private bool isPlayerInRange = false;

    private void Awake()
    {
        tmpText = GetComponentInChildren<TextMeshProUGUI>();
        buttonImage = GetComponentInChildren<Image>();

        tmpText.text = "";
    }

    private void Start()
    {
        tmpText.gameObject.SetActive(false);
        buttonImage.gameObject.SetActive(false);
    }
    private void Update()
    {
        // Z키를 눌렀을 때 대화 시작
        if (isPlayerInRange && !DialogueManager.Instance.IsTalking
            && Input.GetKeyDown(KeyCode.Z))
        {
            StartDialogue();
        }

        //대화 중일 때 엔터 입력하면 -> 다음 대사 출력 (단, 선택지가 있을 경우 엔터 키 입력 막기)
        if (DialogueManager.Instance.IsTalking &&
            !DialogueManager.Instance.IsChoices && Input.GetKeyDown(KeyCode.Return))
        {
            DialogueManager.Instance.DisplayNextLine();
        }
    }
    public void SetInkNode(string node)
    {
        eventData.InkNodeName = node;
    }

    public void StartDialogue() //버튼 클릭 시 실행
    {
        //Story story = DialogueManager.Instance.GetStory();
        //story.variablesState["current_time"] = TimeManager.Instance.currentHour; //** Ink 변수 만들기 (타임 변수)
        DialogueManager.Instance.StartStory(eventData);
        
    }

    public void UpdateTrigger(EventData data)
    {
        //기존 버튼 프리펩 Active 끄기 (예외처리)
        //if (buttonImage != null)
        //{
        //Vector3 pos = new Vector3(data.Position.x, data.Position.y, 0);
        //tmpText.transform.position = Vector3.zero;
        //buttonImage.transform.position = Vector3.zero;
        //}

        tmpText.gameObject.SetActive(false);
        buttonImage.gameObject.SetActive(false);

        eventData = data;
        tmpText.text = eventData.EventName;
    }

    public void ShowInteractionButton(bool show) //**이벤트 매니저에서 호출
    {
        tmpText.gameObject.SetActive(show);
        buttonImage.gameObject.SetActive(show);
        isPlayerInRange = show;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position + Vector3.left * 1f, transform.position + Vector3.right * 1f);
    }
    
}
