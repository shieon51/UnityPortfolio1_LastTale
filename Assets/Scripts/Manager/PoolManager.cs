using System;
using System.Collections.Generic;
using UnityEngine;

public enum PoolType { Global, Zone }

public class PoolManager : Singleton<PoolManager>
{
    [Serializable]
    public class PoolInfo // 풀 정보
    {
        public string poolName;
        public GameObject prefab;
        public int initialSize;
        public PoolType type;
    }

    // 인스펙터에서 기본적으로 생성할 글로벌 객체들을 등록 (데미지 텍스트, 이펙트 등)
    public List<PoolInfo> basePools;

    // 글로벌 풀과 구역 풀을 딕셔너리로 분리
    private Dictionary<string, Queue<GameObject>> globalPools = new Dictionary<string, Queue<GameObject>>();
    private Dictionary<string, Queue<GameObject>> zonePools = new Dictionary<string, Queue<GameObject>>();

    private void Start()
    {
        // 게임 시작 시 글로벌 풀 초기화
        foreach (PoolInfo pool in basePools)
        {
            CreatePool(pool);
        }
    }

    // 풀 초기화 
    public void CreatePool(PoolInfo info)
    {
        var targetDictionary = info.type == PoolType.Global ? globalPools : zonePools;

        if (!targetDictionary.ContainsKey(info.poolName))
            targetDictionary.Add(info.poolName, new Queue<GameObject>());

        // 부모 오브젝트를 정리하기 위해 생성 (Hierarchy 깔끔하게)
        GameObject poolParent = new GameObject($"{info.poolName}_Pool");
        poolParent.transform.SetParent(this.transform);

        for (int i = 0; i < info.initialSize; i++)
        {
            GameObject obj = Instantiate(info.prefab, poolParent.transform);
            obj.name = info.poolName;
            obj.SetActive(false);
            targetDictionary[info.poolName].Enqueue(obj);
        }
    }

    public GameObject SpawnFromPool(string poolName, Vector3 position, Quaternion rotation, PoolType expectedType = PoolType.Global)
    {
        var targetDictionary = expectedType == PoolType.Global ? globalPools : zonePools;

        if (!targetDictionary.ContainsKey(poolName) || targetDictionary[poolName].Count == 0)
        {
            Debug.LogWarning($"[PoolManager] {poolName} 풀이 부족하거나 없습니다! 동적 생성이 필요할 수 있습니다.");
            return null; // 필요시 Instantiate 로직 추가 가능
        }

        GameObject obj = targetDictionary[poolName].Dequeue();
        obj.transform.position = position;
        obj.transform.rotation = rotation;
        obj.SetActive(true);
        return obj;
    }

    public void ReturnToPool(GameObject obj, PoolType type = PoolType.Global)
    {
        obj.SetActive(false);
        var targetDictionary = type == PoolType.Global ? globalPools : zonePools;

        if (targetDictionary.ContainsKey(obj.name))
        {
            targetDictionary[obj.name].Enqueue(obj);
        }
        else
        {
            Destroy(obj); // 풀에 등록되지 않은 객체면 삭제
        }
    }

    // 💡 맵의 구역(Zone)이 완전히 바뀔 때 호출하여 메모리 해제
    public void ClearZonePools()
    {
        foreach (var pool in zonePools.Values)
        {
            foreach (var obj in pool) Destroy(obj);
        }
        zonePools.Clear();
        Debug.Log("[PoolManager] 이전 Zone의 몬스터/이펙트 풀이 메모리에서 해제되었습니다.");
    }
}
