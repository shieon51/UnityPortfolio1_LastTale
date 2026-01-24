using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine.SceneManagement;
using System.IO;

/* SceneLoader - 현재 씬에 맞춰 맵 Scene을 로드*/

public class SceneLoader : Singleton<SceneLoader>
{
    private Dictionary<int, string> sceneDict = new Dictionary<int, string>(); // SceneTable.csv 데이터 받아오기
    private string currentMapScene = "";
    public int CurrentSceneID { get; private set; }

    public GameObject player; // 인스펙터에서 할당
    public GameObject portalPrefab; // 인스펙터에서 할당

    private void Awake()
    {
        LoadSceneData();   // 씬 id : 씬 이름 대응 정보 불러오기
    }

    private void Start()
    {
        LoadScene(1, player.transform.position); // ++ 임시 코드 (BiginnerTown Scene)

    }

    public string GetSceneName(int sceneID)
    {
        return sceneDict.ContainsKey(sceneID) ? sceneDict[sceneID] : null;
    }

    //테이블에서 데이터 가져오기
    private void LoadSceneData()
    {
        string filePath = Path.Combine(Application.streamingAssetsPath, "Datas", "SceneTable.csv");
        if (!File.Exists(filePath))
        {
            Debug.LogError("SceneTable.csv 없음!");
            return;
        }

        string[] lines = File.ReadAllLines(filePath);
        for (int i = 1; i < lines.Length; i++) // 첫 줄은 헤더
        {
            if (string.IsNullOrEmpty(lines[i])) continue;
            string[] values = lines[i].Split(',');

            int id = int.Parse(values[0]);
            string name = values[1].Trim(); // 공백 제거 안전장치

            if (!sceneDict.ContainsKey(id))
                sceneDict.Add(id, name);
        }
    }

    //// (씬 데이터 등록 함수 ++ Csv 반영 되도록 나중에 수정)
    //public void RegisterSceneData(List<SceneData> sceneList)
    //{
    //    sceneDict.Clear();
    //    foreach (var s in sceneList)
    //        sceneDict[s.SceneID] = s.SceneName;
    //}

    public void LoadScene(int targetSceneID, Vector2 spawnPos) //vec?
    {
        StartCoroutine(LoadSceneAsync(targetSceneID, spawnPos));
    }

    // (씬을 모두 로드하기 전까진 플레이어 위치 조정 하지 않도록)
    private IEnumerator LoadSceneAsync(int sceneID, Vector2 spawnPos)
    {
        if (!sceneDict.ContainsKey(sceneID))
        {
            Debug.LogError("씬 ID를 찾을 수 없음: " + sceneID);
            yield break;
        }

        // 현재 씬 ID 
        CurrentSceneID = sceneID;
        string nextSceneName = sceneDict[sceneID];

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
            player.transform.position = spawnPos;
            // 물리 충돌로 튕겨나가지 않게 잠시 물리 끄거나 위치 강제 동기화
            Physics2D.SyncTransforms();
        }

        // 플레이어 이동이 끝난 후 이벤트를 갱신
        if (EventManager.Instance != null)
        {
            EventManager.Instance.UpdateEventTriggers();
        }
    }

}
