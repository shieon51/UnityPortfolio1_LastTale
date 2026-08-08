using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// ScreenFlashOverlay.cs — 전체 화면 덮는 반투명 Image 위에 부착
public class ScreenFlashOverlay : Singleton<ScreenFlashOverlay>
{
    public Image overlayImage;

    public void Flash(Color color, float duration)
    {
        StopAllCoroutines();
        StartCoroutine(FlashRoutine(color, duration));
    }

    private IEnumerator FlashRoutine(Color color, float duration)
    {
        overlayImage.color = new Color(color.r, color.g, color.b, 0.5f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float a = Mathf.Lerp(0.5f, 0f, elapsed / duration);
            overlayImage.color = new Color(color.r, color.g, color.b, a);
            elapsed += Time.deltaTime;
            yield return null;
        }
        overlayImage.color = new Color(color.r, color.g, color.b, 0f);
    }
}