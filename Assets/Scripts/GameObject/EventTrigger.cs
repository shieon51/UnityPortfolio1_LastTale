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
            }
            Debug.Log($"[EventTrigger] '{data.EventName}' 소진 → 대체 노드 '{data.exhaustedInkNode}' 실행");
            data = CloneWithNode(data, data.exhaustedInkNode);
        }

        if (!string.IsNullOrEmpty(data.summonNPCs)) // ★ 추가 — 연출용 NPC 소환
        {
            foreach (var name in data.summonNPCs.Split(','))
            {
                string trimmed = name.Trim();
                if (!string.IsNullOrEmpty(trimmed)) NPCManager.Instance.SummonNPCAt(trimmed, data.Position);
            }
        }
        DialogueManager.Instance.StartStory(data);
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
        TimeTaken = 0, // 대체 대사는 시간 소모 없음
        AutoTrigger = source.AutoTrigger,
        maxTriggerCount = source.maxTriggerCount,
        exhaustedInkNode = source.exhaustedInkNode,
        summonNPCs = source.summonNPCs,
        despawnAfterEvent = source.despawnAfterEvent,
    };

    public void UpdateTrigger(EventData data)
    {
        // 데이터 갱신 시 상태 초기화 
        ShowInteractionButton(false);

        eventData = data;
        tmpText.text = eventData.EventName;

        Debug.Log($"[EventTrigger] {data.EventName} 로드됨 — Auto:{data.AutoTrigger} Max:{data.maxTriggerCount} Exhausted:'{data.exhaustedInkNode}'");
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
