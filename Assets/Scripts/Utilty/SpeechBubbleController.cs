// TypewriterText 재사용 - SpeechBubbleController.cs (신규)
using UnityEngine;
using TMPro;
using System;

// 말풍선 ui 컨트롤러
public class SpeechBubbleController : MonoBehaviour
{
    public GameObject bubbleRoot;
    public TextMeshProUGUI nameText;
    public TypewriterText bodyTypewriter; // 6번에서 만든 거 재사용
    public Vector3 offsetAboveTarget = new Vector3(0, 1.5f, 0);
    private Transform _followTarget;

    public event Action OnTextFullyDisplayed;

    // (카메라가 화자 위치를 알 수 있게)
    public Vector3 FollowTargetPosition => _followTarget != null ? _followTarget.position : transform.position;

    public bool IsTyping => bodyTypewriter.IsTyping;
    public void SkipTyping() => bodyTypewriter.Skip();

    private void Awake()
    {
        bodyTypewriter.OnFullyDisplayed += () => OnTextFullyDisplayed?.Invoke(); // ★ 추가 — TypewriterText 완료 신호를 그대로 전달
    }

    public void Show(Transform target, string speakerName, string text)
    {
        _followTarget = target;
        nameText.text = speakerName;
        bubbleRoot.SetActive(true);
        bodyTypewriter.Play(text);
    }
    public void Hide() => bubbleRoot.SetActive(false);

    private void LateUpdate() // 클램프 추가
    {
        if (_followTarget == null || !bubbleRoot.activeSelf) return;

        Vector3 desiredWorldPos = _followTarget.position + offsetAboveTarget;
        Camera cam = Camera.main;
        if (cam == null) { transform.position = desiredWorldPos; return; }

        Vector3 viewportPos = cam.WorldToViewportPoint(desiredWorldPos);
        float margin = 0.08f;
        viewportPos.x = Mathf.Clamp(viewportPos.x, margin, 1f - margin);
        viewportPos.y = Mathf.Clamp(viewportPos.y, margin, 1f - margin);
        transform.position = cam.ViewportToWorldPoint(viewportPos);
    }
}