using System.Collections;
using UnityEngine;

// AfterimageFader.cs — 잔상 프리팹에 부착 (SpriteRenderer만 있는 빈 프리팹)
public class AfterimageFader : MonoBehaviour
{
    private SpriteRenderer _sr;
    private void Awake() => _sr = GetComponent<SpriteRenderer>();

    public void Play(float duration)
    {
        StopAllCoroutines();
        StartCoroutine(FadeRoutine(duration));
    }

    private IEnumerator FadeRoutine(float duration)
    {
        Color start = _sr.color;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            _sr.color = new Color(start.r, start.g, start.b, Mathf.Lerp(start.a, 0f, elapsed / duration));
            elapsed += Time.deltaTime;
            yield return null;
        }
        PoolManager.Instance.ReturnToPool(gameObject);
    }
}