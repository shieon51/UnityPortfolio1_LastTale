using UnityEngine;
using UnityEngine.LightTransport;

// 이 스크립트는 씬에 배치될 '이벤트 깃발'에 들어감

// 마커의 종류 구분 (아이콘 색상용)
public enum EventMarkerType
{
    Normal_NPC = 0, // 10000번대 (초록색)
    System_Repeat = 1 // 90000번대 (빨간색)
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

    // 게임 시작 시 자동 삭제 
    private void Awake()
    {
        // 게임 플레이 모드라면? -> 나(마커)는 필요 없으니 사라진다!
        if (Application.isPlaying)
        {
            Destroy(gameObject);
        }
    }

    private void OnDrawGizmos()
    {
        // 타입에 따라 색상 다르게 표시
        Gizmos.color = markerType == EventMarkerType.Normal_NPC ? Color.green : Color.red;
        Gizmos.DrawSphere(transform.position, 0.5f);

        #if UNITY_EDITOR
        // 씬 뷰에서 ID와 이름이 보이도록 라벨 표시
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.8f, $"ID:{EventID}\n{EventName}");
        #endif
    }
}