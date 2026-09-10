using System.Collections;
using TMPro;
using UnityEngine;

// FloatingTextInstance.cs (신규) — 프리팹에 부착 (World Space TextMeshPro, UGUI 아님)
public class FloatingTextInstance : MonoBehaviour
{
    public TextMeshPro textMesh;
    public float riseDistance = 1f;
    public float duration = 0.8f;
    public AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

    // FloatingTextInstance.cs — 지속시간/거리 오버라이드 가능하게
    public void Play(string text, Color color, float overrideDuration = -1f, float overrideRise = -1f)
    {
        textMesh.text = text;
        textMesh.color = color;
        float d = overrideDuration > 0f ? overrideDuration : duration;
        float r = overrideRise > 0f ? overrideRise : riseDistance;
        StopAllCoroutines();
        StartCoroutine(Animate(d, r));
    }

    private IEnumerator Animate(float duration, float riseDistance) // ★ 매개변수화
    {
        Vector3 start = transform.position;
        Vector3 end = start + Vector3.up * riseDistance;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            transform.position = Vector3.Lerp(start, end, t);
            Color c = textMesh.color;
            c.a = alphaCurve.Evaluate(t);
            textMesh.color = c;
            elapsed += Time.deltaTime;
            yield return null;
        }
        PoolManager.Instance.ReturnToPool(gameObject);
    }
}