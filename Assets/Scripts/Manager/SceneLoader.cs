using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine.SceneManagement;
using System.IO;

/* SceneLoader - 현재 씬에 맞춰 맵 Scene을 로드 */

public class SceneLoader : Singleton<SceneLoader>, IRecordable   // ★ 2-C-2 — 씬·위치를 범용 스냅샷에 포함 (World)
{
    private string currentMapScene = "";
    public int CurrentSceneID { get; private set; }

    public GameObject player; // 인스펙터에서 할당
    public GameObject portalPrefab; // 인스펙터에서 할당

    [Header("Ground Snap")]
    public LayerMask groundSnapLayer; // NPCManager와 같은 레이어로 연결

    public GameStartConfig startConfig; // ★ 인스펙터에 연결

    public event Action<int> OnSceneLoaded;

    private void Awake()
    {
        //LoadSceneData();   // 씬 id: 씬 이름 대응 정보 불러오기
        RecordSystem.Register(this);   // ★ 2-C-2
    }
    private void OnDestroy() => RecordSystem.Unregister(this);

    // ---------------- IRecordable (★ 기록 시스템 2-C-2) ----------------
    // 회귀에서는 RestoreLayers가 이 덩어리를 건너뛴다 — 씬 이동은 비동기이고 회귀 기록 뒤에 해야 하므로
    // TimeLoopManager.LoadScene이 맡는다. ReadState는 세이브 불러오기(4단계)처럼 바로 적용할 때 쓰인다.
    // ※ 파트 시작 스냅샷은 첫 씬이 로드되기 전에 찍히므로 이 덩어리 값이 비어 있다. 경로 3은 startConfig로 간다
    private const string StateKeySceneId = "scene_id";   // 덩어리 안의 키. 바꾸지 않는다
    private const string StateKeyPosX = "pos_x";
    private const string StateKeyPosY = "pos_y";

    public string RecordId => RecordIds.SceneLocation;
    public RecordLayer Layer => RecordLayer.World;
    public int StateVersion => 1;

    public void WriteState(StateWriter writer)
    {
        Vector2 pos = player != null ? (Vector2)player.transform.position : Vector2.zero;
        writer.WriteInt(StateKeySceneId, CurrentSceneID);
        writer.WriteFloat(StateKeyPosX, pos.x);
        writer.WriteFloat(StateKeyPosY, pos.y);
    }

    public void ReadState(StateReader reader, int version)
    {
        int sceneId = reader.ReadInt(StateKeySceneId, CurrentSceneID);
        if (sceneId <= 0) return;   // 첫 씬 로드 전에 찍힌 덩어리
        TravelTimeTracker.Instance?.CancelJourney();   // 걸어서 온 이동이 아니다
        LoadScene(sceneId, new Vector2(reader.ReadFloat(StateKeyPosX), reader.ReadFloat(StateKeyPosY)));
    }

    private void Start()
    {
        LoadScene(startConfig.startSceneID, startConfig.startPosition); // ★ 하드코딩된 1/player.position 대신 (비기너 타운)
    }

    public string GetSceneName(int sceneID)
    {
        // DataManager에게 물어봄
        if (DataManager.Instance.SceneDict.TryGetValue(sceneID, out string name)) return name;
        return null;
    }

    public void LoadScene(int targetSceneID, Vector2 spawnPos) //vec?
    {
        StartCoroutine(LoadSceneAsync(targetSceneID, spawnPos));
    }

    // (씬을 모두 로드하기 전까진 플레이어 위치 조정 하지 않도록)
    private IEnumerator LoadSceneAsync(int sceneID, Vector2 spawnPos)
    {
        
        if (!DataManager.Instance.SceneDict.ContainsKey(sceneID)) //?
        {
            Debug.LogError("씬 ID를 찾을 수 없음: " + sceneID);
            yield break;
        }

        // 현재 씬 ID 
        int previousSceneId = CurrentSceneID;   // ★ 기록용
        CurrentSceneID = sceneID;
        string nextSceneName = DataManager.Instance.SceneDict[sceneID]; //?

        // 이전 맵 씬 unload
        if (!string.IsNullOrEmpty(currentMapScene))
        {
            yield return SceneManager.UnloadSceneAsync(currentMapScene);
        }

        // 씬(맵지형) 로드하기(Additive)
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(nextSceneName, LoadSceneMode.Additive);
        while (!asyncLoad.isDone)
            yield return null;

        currentMapScene = nextSceneName;

        // 로드된 씬을 Active로 설정 (라이팅 및 오브젝트 생성 위치 보정)
        Scene loadedScene = SceneManager.GetSceneByName(nextSceneName);
        if (loadedScene.IsValid())
        {
            SceneManager.SetActiveScene(loadedScene);
        }

        // 씬 다 켜졌으니 해당 씬(sceneID)에 맞는 포탈들 심기
        if (portalPrefab != null)
        {
            PortalManager.Instance.SpawnPortalsForScene(sceneID, portalPrefab);
        }
        else
        {
            Debug.LogError("SceneLoader에 Portal Prefab이 연결되지 않았습니다!");
        }

        // Player 위치 이동
        if (player != null)
        {
            player.transform.position = ComputeSnappedPosition(spawnPos); // ★ 스냅 적용
            // 물리 충돌로 튕겨나가지 않게 잠시 물리 끄거나 위치 강제 동기화
            Physics2D.SyncTransforms();
        }

        // 플레이어 이동이 끝난 후 이벤트를 갱신
        if (EventManager.Instance != null)
        {
            EventManager.Instance.UpdateEventTriggers();
        }

        // ★ 씬 진입과 도착 위치를 남긴다 (위치 복원·버그 추적용)
        PlayerActionLog.Instance?.Record(RecordType.SceneEnter, sceneID.ToString(), previousSceneId, sceneID,
            payload: PlayerActionLog.EncodePosition(player != null ? (Vector2)player.transform.position : spawnPos));

        OnSceneLoaded?.Invoke(sceneID); // ★ 맨 마지막에 추가
    }

    // 땅에 스냅
    private Vector3 ComputeSnappedPosition(Vector2 desiredPos, float startOffset = 1f, float rayDistance = 3f)
    {
        Vector2 rayStart = desiredPos + Vector2.up * startOffset;
        RaycastHit2D hit = Physics2D.Raycast(rayStart, Vector2.down, rayDistance, groundSnapLayer);
        if (hit.collider == null) return new Vector3(desiredPos.x, desiredPos.y, 0);

        Collider2D col = player.GetComponentInChildren<Collider2D>();
        if (col == null) return new Vector3(desiredPos.x, desiredPos.y, 0);

        float pivotToBottom = player.transform.position.y - col.bounds.min.y;
        return new Vector3(desiredPos.x, hit.point.y + pivotToBottom, 0);
    }
}
