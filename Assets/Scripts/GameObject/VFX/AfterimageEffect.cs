using System.Collections;
using UnityEngine;

// AfterimageEffect.cs (신규) — 플레이어에 부착
// 대시/스킬 잔상(비행 대시, Q 스킬) — 범용 컴포넌트로, 스프라이트를 매 프레임 복사해서 흐려지며 사라지는 방식입니다.
public class AfterimageEffect : MonoBehaviour
{
    public SpriteRenderer sourceRenderer;
    public float spawnInterval = 0.03f;
    public float fadeDuration = 0.25f;
    public Color afterimageColor = new Color(1f, 1f, 1f, 0.5f);
    public string poolName = "Afterimage";

    private Coroutine _routine;

    public void Play(float duration)
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(SpawnRoutine(duration));
    }

    public void Stop() { if (_routine != null) StopCoroutine(_routine); _routine = null; }

    private IEnumerator SpawnRoutine(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            SpawnOne();
            yield return new WaitForSeconds(spawnInterval);
            elapsed += spawnInterval;
        }
    }

    private void SpawnOne()
    {
        GameObject ghost = PoolManager.Instance.SpawnFromPool(poolName, sourceRenderer.transform.position, Quaternion.identity);
        if (ghost == null) return;
        var sr = ghost.GetComponent<SpriteRenderer>();
        if (sr != null) { sr.sprite = sourceRenderer.sprite; sr.flipX = sourceRenderer.flipX; sr.color = afterimageColor; }
        ghost.GetComponent<AfterimageFader>()?.Play(fadeDuration);
    }
}