using System;
using System.Collections.Generic;
using UnityEngine;

public class NPCManager : Singleton<NPCManager>
{
    // NPC 이름을 Key로 하여 데이터를 영구 보관하는 딕셔너리
    private Dictionary<string, NPCData> npcDataDict = new Dictionary<string, NPCData>();


    // 💡 2. 씬 내에서 껐다 켜기 위한 껍데기(프리팹) 보관소
    private Dictionary<string, GameObject> npcPool = new Dictionary<string, GameObject>();

    private void Awake()
    {
        // 나중에는 여기서 Save 파일 데이터를 불러와서 npcDataDict에 덮어씌웁니다.
        // 지금은 세이브 기능이 없으니 임시로 초기 데이터를 세팅합니다.
        InitializeDefaultNPCData();
    }

    private void InitializeDefaultNPCData()
    {
        // 💡 게임에 등장하는 모든 NPC의 초기 상태를 등록해 둡니다.
        npcDataDict.Add("Liel", new NPCData("Liel"));
        //npcDataDict.Add("Diaber", new NPCData("Diavalu"));
        //npcDataDict.Add("Gaon", new NPCData("Gaon"));
    }

    // NPC가 스폰될 때 자신의 데이터를 요구하는 함수
    public NPCData GetNPCData(string npcName)
    {
        if (npcDataDict.TryGetValue(npcName, out NPCData data))
        {
            return data;
        }
        else
        {
            Debug.LogWarning($"[NPCManager] {npcName}의 데이터가 없습니다! 새로 생성합니다.");
            NPCData newData = new NPCData(npcName);
            npcDataDict.Add(npcName, newData);
            return newData;
        }
    }

    // NPC 호감도나 상태가 변했을 때 저장하는 함수 (나중에 호감도 이벤트 시 호출)
    public void SaveNPCData(NPCData updatedData)
    {
        if (npcDataDict.ContainsKey(updatedData.npcName))
        {
            npcDataDict[updatedData.npcName] = updatedData;
        }
    }

    // =========================================================
    // 💡 [핵심] EventManager에게서 위임받은 NPC 스폰 & 배치 기능 (필요한 애들만 업데이트)
    // =========================================================
    public void UpdateNPCsOnMap(List<EventData> activeNPCEvents)
    {
        // 1. 이번 타임에 활성화되어야 할 NPC 이름 목록 수집
        HashSet<string> activeNames = new HashSet<string>();
        foreach (var data in activeNPCEvents) activeNames.Add(data.EventName);

        // 2. 이번 타임에 없는 NPC만 풀에서 꺼버림 (살아남을 애들은 안 건드림!)
        foreach (var kvp in npcPool)
        {
            if (!activeNames.Contains(kvp.Key) && kvp.Value != null)
            {
                kvp.Value.SetActive(false);
            }
        }

        // 3. 켜야 할 애들 스폰 및 업데이트 진행
        foreach (EventData data in activeNPCEvents)
        {
            SpawnOrUpdateNPC(data);
        }
    }

    private void SpawnOrUpdateNPC(EventData data)
    {
        GameObject npcObj = null;
        bool isAlreadyActive = false; // 💡 현재 켜져 있는지 확인용

        if (npcPool.TryGetValue(data.EventName, out npcObj) && npcObj != null)
        {
            isAlreadyActive = npcObj.activeSelf;
        }

        if (npcObj == null)
        {
            GameObject prefab = Resources.Load<GameObject>($"Prefabs/NPC/{data.EventName}");
            if (prefab == null) return;
            npcObj = Instantiate(prefab);
            npcPool[data.EventName] = npcObj;
        }

        NPC npcScript = npcObj.GetComponent<NPC>();

        // -----------------------------------------------------
        // 💡 [버그 해결] 스마트 연속 이벤트 처리
        // 이미 씬에 켜져 있고 위치가 동일하면, 
        // 절대 Transform을 건드리지 않고 대사만 주입하고 끝냅니다!
        // -----------------------------------------------------
        if (isAlreadyActive && npcScript != null && Vector2.Distance(npcScript.originalCsvPos, data.Position) < 0.1f)
        {
            npcScript.SetupCurrentEvent(data);
            return;
        }

        // -----------------------------------------------------
        // 이하 위치 갱신 및 바닥 스냅 로직 (장소가 바뀌었거나 첫 스폰일 때만)
        // -----------------------------------------------------
        npcObj.transform.position = data.Position;
        npcObj.SetActive(true);

        if (npcScript != null)
        {
            npcScript.originalCsvPos = data.Position;
            npcScript.SetupCurrentEvent(data);
        }


        // 💡 [바닥 자동 안착 시스템] (완벽 보정판)
        float startOffset = 1.0f;
        float rayDistance = 3.0f;
        Vector2 rayStart = data.Position + Vector2.up * startOffset;

        RaycastHit2D hit = Physics2D.Raycast(rayStart, Vector2.down, rayDistance, LayerMask.GetMask("Ground"));
        Debug.DrawRay(rayStart, Vector2.down * rayDistance, Color.magenta, 5f); // NPC는 눈에 띄게 보라색 레이저

        if (hit.collider != null)
        {
            Collider2D col = npcObj.GetComponentInChildren<Collider2D>();
            if (col != null)
            {
                Physics2D.SyncTransforms();
                float pivotToBottom = npcObj.transform.position.y - col.bounds.min.y;
                npcObj.transform.position = new Vector3(data.Position.x, hit.point.y + pivotToBottom, 0);
            }
        }
        else
        {
            npcObj.transform.position = data.Position;
        }
    }

    // 💡 [신규] EventManager에서 호출할 대화 연결 함수
    public void StartNPCDialogue(string npcName)
    {
        if (npcPool.TryGetValue(npcName, out GameObject npcObj) && npcObj != null)
        {
            NPC npcScript = npcObj.GetComponent<NPC>();
            if (npcScript != null) npcScript.OnDialogueStart();
        }
    }

    public void EndNPCDialogue(string npcName)
    {
        if (npcPool.TryGetValue(npcName, out GameObject npcObj) && npcObj != null)
        {
            NPC npcScript = npcObj.GetComponent<NPC>();
            if (npcScript != null) npcScript.OnDialogueEnd();
        }
    }

    // 💡 [참고] 나중에 보스전 진입 시, EventManager가 아니라 여기서 처리하는 게 맞습니다!
    public void TriggerBossBattle(string targetNpcName)
    {
        NPCData targetData = GetNPCData(targetNpcName);
        targetData.currentMode = NPC.NPCMode.Attack;
        // 전투 씬으로 넘기거나, 씬에 있는 NPC에게 즉시 전투 모드 돌입 명령
    }
}
