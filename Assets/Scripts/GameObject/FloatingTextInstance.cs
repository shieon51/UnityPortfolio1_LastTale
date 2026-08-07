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

    public void Play(string text, Color color)
    {
        textMesh.text = text;
        textMesh.color = color;
        StopAllCoroutines();
        StartCoroutine(Animate());
    }

    private IEnumerator Animate()
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