using System.Collections;
using TMPro;
using UnityEngine;

public enum NotificationType { Info, Warning, Failure }

// 화면에 짧은 안내 문구를 띄우는 범용 시스템.
// "타겟이 없습니다", "마나가 부족합니다" 뿐 아니라 나중에 보스 공격 경고, 스킬 해금 안내 등도 이걸로 통일해서 사용.
public class NotificationManager : Singleton<NotificationManager>
{
    [Header("UI References")]
    public TextMeshProUGUI notificationText;
    public CanvasGroup notificationGroup;

    [Header("Timing")]
    public float displayDuration = 1.5f;
    public float fadeDuration = 0.3f;

    [Header("Colors")]
    public Color infoColor = Color.white;
    public Color warningColor = new Color(1f, 0.8f, 0.2f);
    public Color failureColor = new Color(1f, 0.35f, 0.35f);

    private Coroutine _currentRoutine;

    public void Show(string message, NotificationType type = NotificationType.Info)
    {
        if (string.IsNullOrEmpty(message) || notificationText == null || notificationGroup == null) return;

        if (_currentRoutine != null) StopCoroutine(_currentRoutine);
        _currentRoutine = StartCoroutine(ShowRoutine(message, type));
    }

    private IEnumerator ShowRoutine(string message, NotificationType type)
    {
        notificationText.text = message;
        notificationText.color = ResolveColor(type);
        notificationGroup.alpha = 1f;

        yield return new WaitForSeconds(displayDuration);

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            notificationGroup.alpha = Mathf.Lerp(1f, 0f, t / fadeDuration);
            yield return null;
        }
        notificationGroup.alpha = 0f;
        _currentRoutine = null;
    }

    private Color ResolveColor(NotificationType type) => type switch
    {
        NotificationType.Warning => warningColor,
        NotificationType.Failure => failureColor,
        _ => infoColor,
    };
}