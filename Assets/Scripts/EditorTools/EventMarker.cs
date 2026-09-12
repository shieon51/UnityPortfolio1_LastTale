using UnityEngine;
using UnityEngine.LightTransport;

// 이 스크립트는 씬에 배치될 '이벤트 깃발'에 들어감

// 마커의 종류 구분 (아이콘 색상용)
public enum EventMarkerType
{
    Normal_NPC = 0,      // 10000번대 — 스케줄에 따라 배치되는 NPC
    System_Repeat = 1,   // 90000번대 — 잠자기/훈련 등 정적 이벤트
    Cutscene = 2,        // 50000번대 — 연출 전용, 등장 인물을 이 이벤트가 직접 소환
    Interactable = 3,    // 60000번대 — 사물 탐색/조사
}

public class EventMarker : MonoBehaviour
{
    [Header("Editor Settings")]
    public EventMarkerType markerType = EventMarkerType.Normal_NPC;

    [Header("CSV Data")]
    public int EventID;
    public string EventName = "New Event";
    public bool IsAnytime;
    public int Day = 1;
    public int StartTime = 9;
    public int EndTime = 18;
    public string InkNodeName = "node_name";
    public int SceneID;
    public int TimeTaken = 1;
    public bool AutoTrigger = false;
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

    // 게임 시작 시 자동 삭제 
    private void Awake()
    {
        // 게임 플레이 모드라면? -> 나(마커)는 필요 없으니 사라진다!
        if (Application.isPlaying)
        {
            Destroy(gameObject);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (IsAnytime) return; // 항시 이벤트는 시간 제약 없음

        int availableHours = EndTime - StartTime;
        if (maxTriggerCount > 0 && TimeTaken > 0)
        {
            int maxPossible = availableHours / TimeTaken;
            if (maxTriggerCount > maxPossible)
                Debug.LogWarning($"[EventMarker] '{EventName}'(ID:{EventID}) — 이벤트 가능 시간({StartTime}~{EndTime}시, {availableHours}시간) 안에 {TimeTaken}시간짜리 이벤트를 {maxTriggerCount}번 실행할 수 없습니다. 최대 {maxPossible}번까지 가능합니다.", this);
        }
        if (availableHours <= 0)
            Debug.LogWarning($"[EventMarker] '{EventName}'(ID:{EventID}) — EndTime({EndTime})이 StartTime({StartTime})보다 크지 않습니다.", this);
    }
#endif

    private void OnDrawGizmos()
    {
        // 타입에 따라 색상 다르게 표시
        Gizmos.color = markerType switch
        {
            EventMarkerType.Normal_NPC => Color.green,
            EventMarkerType.System_Repeat => Color.red,
            EventMarkerType.Cutscene => Color.magenta,
            EventMarkerType.Interactable => Color.yellow,
            _ => Color.white,
        };
        Gizmos.DrawSphere(transform.position, 0.5f);

        if (triggerZoneSize.x > 0f && triggerZoneSize.y > 0f)
        {
            Vector3 center = transform.position + (Vector3)triggerZoneOffset;
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.15f);
            Gizmos.DrawCube(center, triggerZoneSize);
            Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(center, triggerZoneSize);
        }

#if UNITY_EDITOR
        // 씬 뷰에서 ID와 이름이 보이도록 라벨 표시
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.8f, $"ID:{EventID}\n{EventName}");
#endif
    }
}