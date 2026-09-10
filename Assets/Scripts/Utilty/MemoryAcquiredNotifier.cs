// MemoryAcquiredNotifier.cs — 최종본 (Manager 오브젝트에 부착)
using UnityEngine;

public class MemoryAcquiredNotifier : MonoBehaviour
{
    [Tooltip("정보 획득 알림이 뜰 높이(플레이어 기준)")]
    public float heightOffset = 1.5f;

    private void Start()
    {
        if (MemoryManager.Instance != null) MemoryManager.Instance.OnMemoryAcquired += HandleAcquired;
    }
    private void OnDestroy()
    {
        if (MemoryManager.Instance != null) MemoryManager.Instance.OnMemoryAcquired -= HandleAcquired;
    }

    private void HandleAcquired(string flagId)
    {
        var player = PlayerManager.Instance?.CurrentCharacter;
        if (player == null || FloatingTextManager.Instance == null) return;
        FloatingTextManager.Instance.ShowNewInfo(player.transform.position + Vector3.up * heightOffset);
    }
}