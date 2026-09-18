using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum NotificationType { Info, Warning, Failure }

// 화면에 짧은 안내 문구를 띄우는 범용 시스템.
// 연달아 들어온 알림은 큐에 쌓아 순서대로 보여준다.
public class NotificationManager : Singleton<NotificationManager>
{
    private class NotificationRequest
    {
        public string message;
        public NotificationType type;
    }

    [Header("UI References")]
    public TextMeshProUGUI notificationText;
    public CanvasGroup notificationGroup;

    [Header("Timing")]
    [Tooltip("문구가 완전히 보이는 시간")]
    public float displayDuration = 1.5f;
    [Tooltip("사라질 때 걸리는 시간")]
    public float fadeDuration = 0.3f;
    [Tooltip("다음 알림이 뜨기 전 간격")]
    public float gapBetweenMessages = 0.1f;

    [Header("Queue")]
    [Tooltip("큐에 쌓아둘 최대 개수. 넘치면 가장 오래된 것부터 버린다")]
    public int maxQueueSize = 5;
    [Tooltip("같은 문구가 연속으로 들어오면 무시할지")]
    public bool ignoreConsecutiveDuplicates = true;

    [Header("Colors")]
    public Color infoColor = Color.white;
    public Color warningColor = new Color(1f, 0.8f, 0.2f);
    public Color failureColor = new Color(1f, 0.35f, 0.35f);

    private readonly Queue<NotificationRequest> _queue = new();
    private Coroutine _playRoutine;
    private string _lastQueuedMessage;

    private void Start()
    {
        if (notificationGroup != null) notificationGroup.alpha = 0f;
    }

    // 로컬라이제이션 키로 띄우는 기본 진입점
    public void ShowKey(string localizationKey, NotificationType type = NotificationType.Info)
    {
        var loc = LocalizationManager.Instance;
        Show(loc != null ? loc.Get(localizationKey) : localizationKey, type);
    }

    // "마나가 {0} 부족합니다" 처럼 값이 들어가는 문구용
    public void ShowKeyFormat(string localizationKey, NotificationType type, params object[] args)
    {
        var loc = LocalizationManager.Instance;
        Show(loc != null ? loc.GetFormat(localizationKey, args) : localizationKey, type);
    }

    // 완성된 문구를 직접 띄울 때 (디버그·임시용)
    public void Show(string message, NotificationType type = NotificationType.Info)
    {
        if (string.IsNullOrEmpty(message) || notificationText == null || notificationGroup == null) return;

        if (ignoreConsecutiveDuplicates && message == _lastQueuedMessage && _queue.Count > 0) return;

        if (_queue.Count >= maxQueueSize) _queue.Dequeue();   // 넘치면 오래된 것부터 버림
        _queue.Enqueue(new NotificationRequest { message = message, type = type });
        _lastQueuedMessage = message;

        if (_playRoutine == null) _playRoutine = StartCoroutine(PlayQueueRoutine());
    }

    // 회귀·씬 전환처럼 화면이 통째로 바뀔 때 남은 알림을 비운다
    public void ClearAll()
    {
        _queue.Clear();
        _lastQueuedMessage = null;
        if (_playRoutine != null) { StopCoroutine(_playRoutine); _playRoutine = null; }
        if (notificationGroup != null) notificationGroup.alpha = 0f;
    }

    private IEnumerator PlayQueueRoutine()
    {
        while (_queue.Count > 0)
        {
            var request = _queue.Dequeue();

            notificationText.text = request.message;
            notificationText.color = ResolveColor(request.type);
            notificationGroup.alpha = 1f;

            // ★ 일시정지(timeScale 0) 중에도 흐르도록 unscaled 시간 사용
            yield return new WaitForSecondsRealtime(displayDuration);

            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                notificationGroup.alpha = Mathf.Lerp(1f, 0f, t / fadeDuration);
                yield return null;
            }
            notificationGroup.alpha = 0f;

            if (_queue.Count > 0 && gapBetweenMessages > 0f)
                yield return new WaitForSecondsRealtime(gapBetweenMessages);
        }

        _lastQueuedMessage = null;
        _playRoutine = null;
    }

    private Color ResolveColor(NotificationType type) => type switch
    {
        NotificationType.Warning => warningColor,
        NotificationType.Failure => failureColor,
        _ => infoColor,
    };
}