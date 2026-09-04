using UnityEngine;

// NPCVFXSlot.cs (신규)
[System.Serializable]
public class NPCVFXSlot
{
    [Tooltip("씬에 미리 안 심어도 됨 — 프로젝트 폴더의 프리팹 애셋을 그대로 드래그")]
    public GameObject prefab;
    [HideInInspector] public GameObject instance; // 런타임에 딱 한 번만 생성됨

    public GameObject GetOrCreateInstance(Transform parent)
    {
        if (instance == null && prefab != null)
        {
            instance = Object.Instantiate(prefab, parent);
            instance.SetActive(false);
        }
        return instance;
    }
}