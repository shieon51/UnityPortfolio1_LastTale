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
    public float InteractionRange => interactionRange;


    private void Awake()
    {
        tmpText = GetComponentInChildren<TextMeshProUGUI>();
        buttonImage = GetComponentInChildren<Image>();

        tmpText.text = "";
    }



    private void Start()
    {
        //tmpText.gameObject.SetActive(false);
        //buttonImage.gameObject.SetActive(false);
        // 초기화 시 확실하게 끄기
        ShowInteractionButton(false);
    }

    public void SetInkNode(string node)
    {
        eventData.InkNodeName = node;
    }

    public void StartDialogue() //버튼 클릭 시 실행
    {
        var data = eventData;
        if (EventManager.Instance.IsEventExhausted(data))
        {
            if (string.IsNullOrEmpty(data.exhaustedInkNode))
            {
                Debug.Log($"[EventTrigger] '{data.EventName}' 이벤트는 소진되어 실행하지 않습니다.");
                return;
            } // 숨김 처리된 경우 아예 시작 안 함

            data = CloneWithNode(data, data.exhaustedInkNode); // 대체 노드도 전부 ink에서 관리
        }
        DialogueManager.Instance.StartStory(eventData);
        
    }

    private EventData CloneWithNode(EventData source, string node) => new EventData
    {
        EventID = source.EventID,
        EventName = source.EventName,
        InkNodeName = node,
        IsAnytime = source.IsAnytime,
        Day = source.Day,
        StartTime = source.StartTime,
        EndTime = source.EndTime,
        SceneID = source.SceneID,
        Position = source.Position,
        TimeTaken = 0,
        AutoTrigger = source.AutoTrigger,
        maxTriggerCount = source.maxTriggerCount,
        exhaustedInkNode = source.exhaustedInkNode
    };

    public void UpdateTrigger(EventData data)
    {
        //기존 버튼 프리펩 Active 끄기 (예외처리)
        //if (buttonImage != null)
        //{
        //Vector3 pos = new Vector3(data.Position.x, data.Position.y, 0);
        //tmpText.transform.position = Vector3.zero;
        //buttonImage.transform.position = Vector3.zero;
        //}

        // 데이터 갱신 시 상태 초기화 
        ShowInteractionButton(false);

        eventData = data;
        tmpText.text = eventData.EventName;
    }

    public void ShowInteractionButton(bool show) //**이벤트 매니저에서 호출
    {
        // UI만 껐다 켰다 함
        if (tmpText != null) tmpText.gameObject.SetActive(show);
        if (buttonImage != null) buttonImage.gameObject.SetActive(show);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position + Vector3.left * 1f, transform.position + Vector3.right * 1f);
    }
    
}
