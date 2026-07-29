using System.Collections;
using UnityEngine;

// PooledVFX.cs — 정적 이펙트(스파크, 궤적, 시전 효과) 공용. 일정 시간 후 자동 반납.
public class PooledVFX : MonoBehaviour
{
    public string poolName;
    public PoolType poolType = PoolType.Global;
    public float lifetime = 1f;
    private Coroutine _returnRoutine;

    private void OnEnable()
    {
        if (_returnRoutine != null) StopCoroutine(_returnRoutine);
        _returnRoutine = StartCoroutine(ReturnAfterLifetime());
    }

    private IEnumerator ReturnAfterLifetime()
    {
        yield return new WaitForSeconds(lifetime);
        PoolManager.Instance.ReturnToPool(gameObject, poolType);
    }
}