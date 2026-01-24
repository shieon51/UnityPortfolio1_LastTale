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
    private enum EventID
    {
        NPC = 10000, 
        Training = 90000, 
        Practice = 90001,
        Sleep = 90002
    }

    private Transform player;
    //private Vector3 playerPosOffset = new Vector3();

    private GameObject eventTriggerPrefab;
    private Dictionary<int, EventData> eventDict = new Dictionary<int, EventData>();
    private List<EventTrigger> activeTriggers = new List<EventTrigger>(); // 최대 10개까지만 관리

    private EventTrigger closest = null;
    private bool canInteract = false;    // 상호작용 가능 여부 플래그

    private void Awake()
    {
        eventTriggerPrefab = Resources.Load<GameObject>("Prefabs/EventTrigger");
        LoadEventData();

        InitializeEventTriggers(10);
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

    private void LoadEventData()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, "Datas", "EventTable.csv");
        string[] lines = File.ReadAllLines(filePath);

        for (int i = 1; i < lines.Length; i++) // 첫 줄은 헤더
        {
            string[] values = lines[i].Split(',');

            EventData eventData = new EventData
            {
                EventID = int.Parse(values[0]),
                EventName = values[1],
                IsAnytime = bool.Parse(values[2]),
                Day = int.Parse(values[3]),
                StartTime = int.Parse(values[4]),
                EndTime = int.Parse(values[5]),
                InkNodeName = values[6],
                SceneID = int.Parse(values[7]),
                Position = new Vector2(float.Parse(values[8]), float.Parse(values[9])),
                TimeTaken = int.Parse(values[10]),
            };

            eventDict.Add(eventData.EventID, eventData);
        }
    }

    public void UpdateEventTriggers()
    {
        // 만약 씬 로더가 아직 초기화 안 됐거나 ID가 없으면 중단 (예외 처리)
        if (SceneLoader.Instance == null) return;

        int currentDay = TimeManager.Instance.currentDay;
        int currentTime = TimeManager.Instance.currentHour; // 24시간 기준 예: 9(9시), 18(18시)

        // SceneLoader에게 현재 씬 ID 물어보기
        int currentSceneID = SceneLoader.Instance.CurrentSceneID;

        // 현재 씬에서 유효한 이벤트 필터링
        List<EventData> validEvents = new List<EventData>();
        foreach (EventData eventEntry in eventDict.Values)
        {
            // 현재 씬(currentSceneID)과 이벤트의 씬(eventEntry.SceneID)이 같은지 체크
            bool isCorrectScene = (eventEntry.SceneID == currentSceneID);

            //현재 날짜, 시간 범위 내에 해당되는 이벤트의 경우
            // 날짜/시간 조건 체크
            bool isCorrectTime = eventEntry.IsAnytime ||
                                 (eventEntry.Day == currentDay &&
                                  currentTime >= eventEntry.StartTime &&
                                  currentTime < eventEntry.EndTime);

            // 두 조건이 모두 맞아야 리스트에 추가
            if (isCorrectScene && isCorrectTime)
            {
                validEvents.Add(eventEntry);
            }
        }

        // 필요한 EventTrigger 개수 결정 (최대 10개까지만 유지)
        int requiredTriggers = Mathf.Min(validEvents.Count, 10);

        // 기존 트리거 재활용
        for (int i = 0; i < requiredTriggers; i++)
        {
            if (i < activeTriggers.Count)
            {
                // 기존 트리거 업데이트
                activeTriggers[i].UpdateTrigger(validEvents[i]);
                
            }
            else
            {
                // 새 트리거 생성
                GameObject obj = Instantiate(eventTriggerPrefab);
                EventTrigger newTrigger = obj.GetComponent<EventTrigger>();
                newTrigger.UpdateTrigger(validEvents[i]);
                activeTriggers.Add(newTrigger);
            }
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

        // 1. 가장 가까운 트리거 위치 찾기
        foreach (EventTrigger trigger in activeTriggers) 
        {
            if (!trigger.gameObject.activeSelf) continue;

            float distance = Vector2.Distance(player.position, trigger.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = trigger;
            }
        }

        // 2. 해당 트리거가 플레이어 범위 내에 들어왔는지 확인 (범위 내로 들어왔으면 버튼 상호작용 가능)
        if (closest != null) 
        {
            if (minDistance <= closest.InteractionRange) //가장 가까운 트리거 범위 내에 플레이어가 있다면
            {
                closest.ShowInteractionButton(true);
                canInteract = true; // 플래그 ON

                if (temp != null && temp != closest) //만약 가장 가까운 트리거가 다른 것으로 변경된 경우엔 이전 것은 범위 내에 있어도 활성화 끄기
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

    //Dialogue가 끝났을 때 Invoke되는 함수 ++ OCP 위배인듯. 수정 필요
    public void EventResult(EventData eventData)
    {
        //시간 변화
        TimeManager.Instance.UseTimeCoins(eventData.TimeTaken); 

        if (eventData.EventID == (int)EventID.Sleep) //'잠자기' 이벤트인 경우
        {
            PlayerState.Instance.RecoverFatigue(eventData.TimeTaken * 2);
            PlayerState.Instance.FullHP();
            PlayerState.Instance.RecoverMana(eventData.TimeTaken);
        }
        else if (eventData.EventID == (int)EventID.Training) //'훈련하기' 이벤트인 경우
        {
            PlayerState.Instance.IncreaseFatigue(eventData.TimeTaken * 2);
            PlayerState.Instance.GainExperience(eventData.TimeTaken * 10); //**임시
            //++ 추후 공격력, 방어력, 민첩성 수치 증가 추가하기
        }
        else if (eventData.EventID == (int)EventID.Practice) //'수련하기' 이벤트인 경우
        {
            PlayerState.Instance.IncreaseFatigue(eventData.TimeTaken);
            PlayerState.Instance.GainExperience(eventData.TimeTaken * 15); //**임시
            PlayerState.Instance.RecoverMana(eventData.TimeTaken * 20); //**임시
            PlayerState.Instance.Heal(eventData.TimeTaken * 5);
        }
        else //기타 (npc 대화 등)
        {
            PlayerState.Instance.IncreaseFatigue(eventData.TimeTaken);
        }
    }


    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        if(closest != null) 
            Gizmos.DrawLine(closest.transform.position, player.position);
    }

    
    //// 이벤트 딕셔너리에 등록
    //public void RegisterEvent(string eventID, Action eventAction)
    //{
    //    if (!eventDictionary.ContainsKey(eventID))
    //    {
    //        eventDictionary.Add(eventID, eventAction);
    //    }
    //}

    //// 시간에 따른 이벤트 딕셔너리에 등록
    //public void RegisterEventTimeCondition(string eventID, int day, int startTime, int endTime, string text)
    //{
    //    if (!eventTimeConditions.ContainsKey(eventID))
    //    {
    //        eventTimeConditions[eventID] = (day, startTime, endTime, text);
    //    }
    //}

    //// 이벤트 실행
    //public void TriggerEvent(string eventID)
    //{
    //    if (eventDictionary.ContainsKey(eventID))
    //    {
    //        eventDictionary[eventID].Invoke();
    //    }
    //}


}
