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
    public bool AutoTrigger;

    [Tooltip("이 이벤트를 최대 몇 번까지 실행할 수 있는지. 0이면 무제한")]
    public int maxTriggerCount = 0;
    [Tooltip("횟수를 다 쓰면 이 노드로 대체 (비우면 이벤트 자체가 숨겨짐)")]
    public string exhaustedInkNode = "";

    [Tooltip("연출 시작 시 이 위치로 소환할 NPC 이름들 (쉼표 구분)")]
    public string summonNPCs = "";
    [Tooltip("소환된 NPC가 연출 후 사라질지")]
    public bool despawnAfterEvent = true;

    [Header("발동 영역")]
    [Tooltip("사각 발동 영역 크기. (0,0)이면 기존 원형 반경 방식 사용")]
    public Vector2 triggerZoneSize = Vector2.zero;
    [Tooltip("마커 기준 영역 중심 오프셋")]
    public Vector2 triggerZoneOffset = Vector2.zero;
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

    [Header("이벤트 선택 우선순위")]
    [Tooltip("바라보는 방향에 있는 이벤트를 우선할지")]
    public bool preferFacingDirection = true;
    [Tooltip("방향 판정 시 무시할 x축 오차 (이보다 가까우면 정면으로 간주)")]
    public float facingDeadzone = 0.3f;

    private float _lastFacingDir = 1f; // 1 = 오른쪽, -1 = 왼쪽

    // [트리거 관리 풀]
    private List<EventTrigger> activeTriggers = new List<EventTrigger>(); // 최대 10개까지만 관리
    private List<EventTrigger> dynamicTriggers = new List<EventTrigger>(); // NPC들이 씬에 나타나면 스스로 등록하는 리스트

    private EventTrigger closest = null;
    private bool canInteract = false;    // 상호작용 가능 여부 플래그

    // EventManager.cs — 필드 추가 (매 프레임 리스트 생성하던 것도 같이 해결)
    private List<EventTrigger> _allTriggersCache = new List<EventTrigger>();

    // 전략 패턴 맵핑을 위한 딕셔너리
    private Dictionary<int, IEventBehavior> eventBehaviors;

    // 이벤트 실행 횟수 판정 헬퍼 추가
    private string GetTriggerCountKey(EventData data) => $"event_trigger_{data.EventID}";

    private string GetAutoFiredKey(EventData data) => $"auto_fired_{data.EventID}";

    public bool HasAutoTriggered(EventData data)
        => MemoryManager.Instance.GetCounter(GetAutoFiredKey(data)) > 0;

    public static bool IsNPCEvent(int eventID) => eventID >= 10000 && eventID < 50000;

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
        float inputX = Input.GetAxisRaw("Horizontal");
        if (Mathf.Abs(inputX) > 0.01f) _lastFacingDir = Mathf.Sign(inputX);

        UpdateNearestEvent();

        // Z키 입력 시 상호작용
        // (조건: 대화중이 아님 + 상호작용 가능한 거리임 + 대상이 존재함 + Z키 누름)
        if (!DialogueManager.Instance.IsTalking &&
             !GlobalActionLock.IsLocked &&
             canInteract &&
             closest != null &&
             Input.GetKeyDown(KeyCode.Z))
        {
            Debug.Log($"상호작용 시작: {closest.eventData.EventName}");

            // 상호작용 대상이 NPC라면 대화가 시작되었다고 알림
            if (IsNPCEvent(closest.eventData.EventID))
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
                // ★ 추가 — 횟수를 다 썼고 대체 노드도 없으면 목록에서 완전히 제외
                if (IsEventExhausted(eventEntry) && string.IsNullOrEmpty(eventEntry.exhaustedInkNode)) continue;

                // 이벤트 ID가 NPC 대역인지 확인
                if (IsNPCEvent(eventEntry.EventID))
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

        // **
        //Debug.Log($"[EventManager] 유효 이벤트 — 정적:{validEvents.Count}개, NPC:{validNPCEvents.Count}개");
        //foreach (var e in validEvents) Debug.Log($"  · 정적: {e.EventName}(ID:{e.EventID}) Auto:{e.AutoTrigger} Zone:{e.triggerZoneSize}");

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
        foreach (var t in activeTriggers) t.ShowInteractionButton(false);   // ★ 추가
        foreach (var t in dynamicTriggers) t.ShowInteractionButton(false);  // ★ 추가
        closest = null;
        canInteract = false;
    }

    private void UpdateNearestEvent() //가장 가까운 트리거 찾기
    {
        _allTriggersCache.Clear();
        _allTriggersCache.AddRange(activeTriggers);
        _allTriggersCache.AddRange(dynamicTriggers);

        if (DialogueManager.Instance.IsTalking) // ★ 대화 중엔 근접 판정 자체를 건너뜀
        {
            foreach (var t in _allTriggersCache) t.ShowInteractionButton(false); // ★ 전부 끔
            closest = null;
            canInteract = false;
            return;
        }

        // 1. 후보 선별: 범위 안에 있는 것들만
        EventTrigger bestFacing = null, bestAny = null;
        float bestFacingDist = Mathf.Infinity, bestAnyDist = Mathf.Infinity;

        foreach (EventTrigger trigger in _allTriggersCache)
        {
            if (!trigger.gameObject.activeSelf) continue;

            float dx = trigger.transform.position.x - player.position.x;
            float dist = Vector2.Distance(player.position, trigger.transform.position);
            bool inRange = trigger.IsPlayerInRange(player.position);

            if (inRange && dist < bestAnyDist) { bestAnyDist = dist; bestAny = trigger; }

            // 바라보는 방향에 있는가 (거의 정면이면 방향 무관하게 인정)
            bool isFacing = Mathf.Abs(dx) <= facingDeadzone || Mathf.Sign(dx) == _lastFacingDir;
            if (inRange && isFacing && dist < bestFacingDist) { bestFacingDist = dist; bestFacing = trigger; }
        }

        // 2. 방향 우선, 없으면 아무거나 (범위 안 대상이 아예 없으면 가장 가까운 것을 closest로 유지 — 버튼은 안 켜짐)
        closest = preferFacingDirection ? (bestFacing ?? bestAny) : bestAny;

        if (closest == null) // 범위 안에 아무것도 없을 때: 가장 가까운 것만 참조용으로 잡아둠
        {
            float minDist = Mathf.Infinity;
            foreach (var t in _allTriggersCache)
            {
                if (!t.gameObject.activeSelf) continue;
                float d = Vector2.Distance(player.position, t.transform.position);
                if (d < minDist) { minDist = d; closest = t; }
            }
        }

        bool closestInRange = closest != null && closest.IsPlayerInRange(player.position);
        foreach (var t in _allTriggersCache)
            t.ShowInteractionButton(t == closest && closestInRange);

        canInteract = closestInRange;

        // 3. 자동 발동 이벤트 처리
        if (closest != null
            && closest.eventData != null
            && closest.eventData.AutoTrigger
            && closestInRange
            && !GlobalActionLock.IsLocked
            && !HasAutoTriggered(closest.eventData)
            && !IsEventExhausted(closest.eventData))
        {
            Debug.Log($"[자동 이벤트 발동] {closest.eventData.EventName} ({closest.eventData.InkNodeName})");
            MemoryManager.Instance.IncrementCounter(GetAutoFiredKey(closest.eventData));

            if (IsNPCEvent(closest.eventData.EventID))
                NPCManager.Instance.StartNPCDialogue(closest.eventData.EventName);

            closest.ShowInteractionButton(false);
            closest.StartDialogue();
        }
    }

    // 이벤트에 있는 모든 주요 대사가 소진되었을 경우 처리
    public bool IsEventExhausted(EventData data)
    {
        if (data == null || data.maxTriggerCount <= 0) return false; // 0이면 무제한
        return MemoryManager.Instance.GetCounter(GetTriggerCountKey(data)) >= data.maxTriggerCount;
    }


    //Dialogue가 끝났을 때 Invoke되는 함수
    public void EventResult(EventData eventData)
    {
        MemoryManager.Instance.IncrementCounter(GetTriggerCountKey(eventData)); // ★ 추가 — 실행 횟수 누적

        if (eventData.despawnAfterEvent && !string.IsNullOrEmpty(eventData.summonNPCs)) // ★ 추가
            NPCManager.Instance.DespawnEventNPCs();

        // 시간 코인 소모 로직을 GameMode에게 위임! (1부면 코인 소모, 2부면 행동력 소모)
        GameManager.Instance.CurrentGameMode.ConsumeResourceForEvent(eventData.TimeTaken);

        // 대화가 끝난 게 NPC라면 대화 종료 알림
        if (IsNPCEvent(eventData.EventID))
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

        UpdateEventTriggers(); // ★ 대화 종료 시점마다 현재 시각 기준으로 NPC 위치·상태 재동기화
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        if(closest != null) 
            Gizmos.DrawLine(closest.transform.position, player.position);
    }

}
