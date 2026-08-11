using System.Collections;
using UnityEngine;

// AfterimageEffect.cs (신규) — 플레이어에 부착
// 대시/스킬 잔상(비행 대시, Q 스킬) — 범용 컴포넌트로, 스프라이트를 매 프레임 복사해서 흐려지며 사라지는 방식입니다.
public class AfterimageEffect : MonoBehaviour
{
    public Transform visualRoot; // ★ Player > Visual — 이 밑의 모든 파츠를 자동으로 찾음
    public float spawnInterval = 0.03f;
    public float fadeDuration = 0.25f;
    public Color afterimageColor = new Color(1f, 1f, 1f, 0.5f);
    public string poolName = "Afterimage";

    private SpriteRenderer[] _sourceRenderers;
    private Coroutine _routine;

    private void Awake()
    {
        if (visualRoot != null) _sourceRenderers = visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
    }

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
            SpawnSnapshot();
            yield return new WaitForSeconds(spawnInterval);
            elapsed += spawnInterval;
        }
    }

    // 그 순간 켜져있는 모든 파츠(얼굴/머리카락/옷 등)를 한꺼번에 복제
    private void SpawnSnapshot()
    {
        if (_sourceRenderers == null) return;
        foreach (var src in _sourceRenderers)
        {
            if (src == null || !src.gameObject.activeInHierarchy) continue; // 지금 꺼져있는 파츠(날개 등)는 건너뜀
            GameObject ghost = PoolManager.Instance.SpawnFromPool(poolName, src.transform.position, Quaternion.identity);
            if (ghost == null) continue;
            var sr = ghost.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = src.sprite;
                sr.flipX = src.flipX;
                sr.color = afterimageColor;
                sr.sortingLayerID = src.sortingLayerID; // ★ 추가 — 레이어 자체를 원본과 똑같이 맞춤
                sr.sortingOrder = src.sortingOrder; // 파츠 그리기 순서까지 맞춰서 안 뒤섞이게
            }
            ghost.GetComponent<AfterimageFader>()?.Play(fadeDuration);
        }
    }
}