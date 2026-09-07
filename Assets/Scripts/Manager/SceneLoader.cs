using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine.SceneManagement;
using System.IO;

/* SceneLoader - 현재 씬에 맞춰 맵 Scene을 로드 */

public class SceneLoader : Singleton<SceneLoader>
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
