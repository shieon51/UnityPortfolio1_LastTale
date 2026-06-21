using System.Collections.Generic;
using System;
using UnityEngine;
using System.IO;
using TMPro;
using UnityEngine.SceneManagement;

[Serializable]
public class EventData
{
    public int EventID;
    public string EventName;
    public bool IsAnytime;
    public int Day;
    public int StartTime;
    public int EndTime;
    public string InkNodeName;
    public int SceneID;
    public Vector2 Position;
    public int TimeTaken;
}

public class EventManager : Singleton<EventManager>
{
    public enum EventID
    {
        NPC = 10000, 
        Training = 90000, 
        Practice = 90001,
        Sleep = 90002
    }

    private Transform player;
    private GameObject eventTriggerPrefab;

    // [트리거 관리 풀]
    private List<EventTrigger> activeTriggers = new List<EventTrigger>(); // 최대 10개까지만 관리
    private List<EventTrigger> dynamicTriggers = new List<EventTrigger>(); // NPC들이 씬에 나타나면 스스로 등록하는 리스트

    private EventTrigger closest = null;
    private bool canInteract = false;    // 상호작용 가능 여부 플래그

    // 전략 패턴 맵핑을 위한 딕셔너리
    private Dictionary<int, IEventBehavior> eventBehaviors;

    private void Awake()
    {
        eventTriggerPrefab = Resources.Load<GameObject>("Prefabs/EventTrigger");
        InitializeEventTriggers(10);
        InitializeBehaviors(); // 행동 전략 초기화
    }

    // 나중에 새로운 이벤트가 생기면 이 곳에 한 줄만 추가하면 됨 ***
    private void InitializeBehaviors()
    {
        eventBehaviors = new Dictionary<int, IEventBehavior>
        {
            { (int)EventID.Sleep, new SleepEventBehavior() },
            { (int)EventID.Training, new TrainingEventBehavior() },
            { (int)EventID.Practice, new PracticeEventBehavior() }
        };
    }


    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        UpdateEventTriggers();
        DialogueManager.Instance.OnDialogueEnd += EventResult;
    }

    private void Update()
    {
        UpdateNearestEvent();

        // Z키 입력 시 상호작용
        // (조건: 대화중이 아님 + 상호작용 가능한 거리임 + 대상이 존재함 + Z키 누름)
        if (!DialogueManager.Instance.IsTalking &&
             canInteract &&
             closest != null &&
             Input.GetKeyDown(KeyCode.Z))
        {
            Debug.Log($"상호작용 시작: {closest.eventData.EventName}");

            // 상호작용 대상이 NPC라면 대화가 시작되었다고 알림
            if (closest.eventData.EventID >= (int)EventID.NPC && closest.eventData.EventID < 90000)
            {
                NPCManager.Instance.StartNPCDialogue(closest.eventData.EventName);
            }

            closest.StartDialogue(); // 트리거의 대화 시작 함수 호출
        }

    }

    private void InitializeEventTriggers(int count)
    {
        activeTriggers = new List<EventTrigger>(count); // 리스트 용량 지정 (단, 실제 요소 추가 X)

        for (int i = 0; i < count; i++)
        {
            GameObject newEvent = Instantiate(eventTriggerPrefab); // 새 객체 생성
            EventTrigger newTrigger = newEvent.GetComponent<EventTrigger>();
            activeTriggers.Add(newTrigger);
            newTrigger.gameObject.SetActive(false);
        }
    }

    // NPC가 스폰될 때 이 함수를 불러서 자기를 리스트에 넣음
    public void RegisterDynamicTrigger(EventTrigger trigger)
    {
        if (!dynamicTriggers.Contains(trigger))
        {
            dynamicTriggers.Add(trigger);
        }
    }

    // NPC가 맵에서 사라지거나 죽을 때 리스트에서 뺌
    public void UnregisterDynamicTrigger(EventTrigger trigger)
    {
        if (dynamicTriggers.Contains(trigger))
        {
            dynamicTriggers.Remove(trigger);
        }
    }

    public void UpdateEventTriggers()
    {
        // 만약 씬 로더, 데이터 로드가 아직 초기화 안 됐거나 ID가 없으면 중단 (예외 처리)
        if (SceneLoader.Instance == null || GameManager.Instance.CurrentGameMode == null) return;

        // SceneLoader에게 현재 씬 ID 물어보기
        int currentSceneID = SceneLoader.Instance.CurrentSceneID;
        // 현재 씬에서 유효한 이벤트 필터링
        List<EventData> validEvents = new List<EventData>();

        // NPC 전용 리스트 분리
        List<EventData> validNPCEvents = new List<EventData>();

        // 2. 이벤트 데이터 순회
        foreach (EventData eventEntry in DataManager.Instance.EventDict.Values)
        {
            // 현재 씬과 시간에 유효한 이벤트인지 확인
            if (GameManager.Instance.CurrentGameMode.IsEventValid(eventEntry, currentSceneID))
            {
                // 이벤트 ID가 NPC 대역(10000 ~ 89999)인지 확인
                if (eventEntry.EventID >= (int)EventID.NPC && eventEntry.EventID < 90000)
                {
                    validNPCEvents.Add(eventEntry); // NPC 리스트에 추가
                }
                else
                {
                    // NPC가 아닌 일반 이벤트(수련, 잠자기 등)는 기존처럼 풀링 리스트에 추가
                    validEvents.Add(eventEntry);
                }
            }
        }

        // NPC 배치는 NPCManager에게 위임
        if (NPCManager.Instance != null)
        {
            NPCManager.Instance.UpdateNPCsOnMap(validNPCEvents);
        }

        // 3. 정적(일반) 트리거 재활용 및 활성화
        // 필요한 EventTrigger 개수 결정 (최대 10개까지만 유지)
        int requiredTriggers = Mathf.Min(validEvents.Count, 10);

        // 기존 트리거 재활용
        for (int i = 0; i < requiredTriggers; i++)
        {
            if (i >= activeTriggers.Count)
            {
                GameObject obj = Instantiate(eventTriggerPrefab);
                activeTriggers.Add(obj.GetComponent<EventTrigger>());
            }
            activeTriggers[i].UpdateTrigger(validEvents[i]);
            activeTriggers[i].gameObject.transform.position = validEvents[i].Position;
            activeTriggers[i].gameObject.SetActive(true);
        }

        // 필요 없는 트리거는 비활성화
        for (int i = requiredTriggers; i < activeTriggers.Count; i++)
        {
            activeTriggers[i].gameObject.SetActive(false);
        }

        // 씬 이동 직후 closest 초기화
        closest = null;
        canInteract = false;
    }

    private void UpdateNearestEvent() //가장 가까운 트리거 찾기
    {
        float minDistance = Mathf.Infinity;
        EventTrigger temp = closest;
        closest = null;

        // 매 프레임 초기화
        canInteract = false;

        // 1. 정적 이벤트(activeTriggers) + 동적 NPC 이벤트(dynamicTriggers) 모두 검사
        List<EventTrigger> allTriggers = new List<EventTrigger>();
        allTriggers.AddRange(activeTriggers);
        allTriggers.AddRange(dynamicTriggers); // 합쳐서 검사

        // 1. 가장 가까운 트리거 위치 찾기
        foreach (EventTrigger trigger in allTriggers) 
        {
            if (!trigger.gameObject.activeSelf) continue;

            float distance = Vector2.Distance(player.position, trigger.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = trigger;
            }
        }

        // 2. 해당 트리거가 플레이어 범위 내에 들어왔는지 확인
        // (범위 내로 들어왔으면 버튼 상호작용 가능)
        if (closest != null) 
        {
            //가장 가까운 트리거 범위 내에 플레이어가 있다면
            if (minDistance <= closest.InteractionRange) 
            {
                closest.ShowInteractionButton(true);
                canInteract = true; // 플래그 ON

                //만약 가장 가까운 트리거가 다른 것으로 변경된 경우엔
                //이전 것은 범위 내에 있어도 활성화 끄기
                if (temp != null && temp != closest) 
                {
                    temp.ShowInteractionButton(false);
                }
            }
            else
            {
                closest.ShowInteractionButton(false); //멀어지면 끄기
                canInteract = false; // 플래그 OFF
            }
        }
        else //트리거가 아무것도 없다면
        {
            if (temp != null)
            {
                temp.ShowInteractionButton(false); //이전에 남아있던 것도 없애기
            }
        }
    }

    //Dialogue가 끝났을 때 Invoke되는 함수
    public void EventResult(EventData eventData)
    {
        // 시간 코인 소모 로직을 GameMode에게 위임! (1부면 코인 소모, 2부면 행동력 소모)
        GameManager.Instance.CurrentGameMode.ConsumeResourceForEvent(eventData.TimeTaken);

        // 대화가 끝난 게 NPC라면 대화 종료 알림
        if (eventData.EventID >= (int)EventID.NPC && eventData.EventID < 90000)
        {
            NPCManager.Instance.EndNPCDialogue(eventData.EventName);
        }

        // 2. 전략 패턴으로 분기 처리 없이 실행
        if (eventBehaviors.TryGetValue(eventData.EventID, out IEventBehavior behavior))
        {
            behavior.Execute(eventData); // 매핑된 특별 행동 실행
        }
        else
        {
            new DefaultEventBehavior().Execute(eventData); // 매핑 안 된 일반 NPC 대화 등
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        if(closest != null) 
            Gizmos.DrawLine(closest.transform.position, player.position);
    }

}
