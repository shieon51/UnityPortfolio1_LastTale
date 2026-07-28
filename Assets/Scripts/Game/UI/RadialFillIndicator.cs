using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// RadialFillIndicator.cs — 스킬 쿨타임, 보스 공격 예고 등 공용
public class RadialFillIndicator : MonoBehaviour
{
    public Image fillImage; // Image Type: Filled, Fill Method: Radial 360

    public void SetProgress(float t) => fillImage.fillAmount = Mathf.Clamp01(t);

    public IEnumerator PlayCountdown(float duration, System.Action onComplete = null)
    {
        float elapsed = 0f;
        fillImage.fillAmount = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            fillImage.fillAmount = elapsed / duration;
            yield return null;
        }
        fillImage.fillAmount = 1f;
        onComplete?.Invoke();
    }
}