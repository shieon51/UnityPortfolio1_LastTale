using UnityEngine;

// VFXManager.cs — PoolManager 위의 편의 레이어. "한 줄로 재생"이 목표.
public class VFXManager : Singleton<VFXManager>
{
    public GameObject Play(string vfxName, Vector3 position, Quaternion rotation, PoolType poolType = PoolType.Global, Transform parent = null)
    {
        GameObject vfx = PoolManager.Instance.SpawnFromPool(vfxName, position, rotation, poolType);
        if (vfx == null) return null;
        vfx.transform.SetParent(parent, worldPositionStays: true);
        return vfx;
    }

    // 근접 스윙처럼 방향에 따라 좌우 반전이 필요한 경우
    public GameObject Play(string vfxName, Vector3 position, float facingDir, PoolType poolType = PoolType.Global, Transform parent = null)
    {
        GameObject vfx = Play(vfxName, position, Quaternion.identity, poolType, parent);
        if (vfx != null)
        {
            var sr = vfx.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.flipX = facingDir < 0f;
        }
        return vfx;
    }

    // 지속형 재생/정지 메서드 추가
    public GameObject PlayPersistent(string vfxName, Vector3 position, Quaternion rotation, Transform parent = null, PoolType poolType = PoolType.Global)
    {
        GameObject vfx = PoolManager.Instance.SpawnFromPool(vfxName, position, rotation, poolType);
        if (vfx == null) return null;
        vfx.transform.SetParent(parent, worldPositionStays: true);
        vfx.GetComponent<PooledVFX>()?.CancelAutoReturn(); // ★ 자동 반납 안 되게 막음
        return vfx;
    }

    public void StopPersistent(GameObject vfx, PoolType poolType = PoolType.Global)
    {
        if (vfx != null) PoolManager.Instance.ReturnToPool(vfx, poolType);
    }
}