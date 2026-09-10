// TypewriterText 재사용 - SpeechBubbleController.cs (신규)
using UnityEngine;
using TMPro;
using System;

// 말풍선 ui 컨트롤러
public class SpeechBubbleController : MonoBehaviour
{
    [Header("자동 오프셋")]
    [Tooltip("바라보는 방향 쪽으로 밀어낼 거리")]
    public float facingOffsetX = 0.5f;
    [Tooltip("다른 화자와 이 거리 안에 있으면 서로 반대쪽으로 밀어냄")]
    public float crowdDistance = 3f;
    public float crowdPushX = 1.2f;

    [Tooltip("화면 가장자리에서 이만큼은 안쪽에 머무름 (0~0.5)")]
    [Range(0f, 0.5f)] public float screenMargin = 0.08f;

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

    private void LateUpdate()
    {
        if (_followTarget == null || !bubbleRoot.activeSelf) return;

        Vector3 desiredWorldPos = _followTarget.position + offsetAboveTarget;
        desiredWorldPos.x += CalculateHorizontalOffset(); // ★ 추가

        Camera cam = Camera.main;
        if (cam == null) { transform.position = desiredWorldPos; return; }

        Vector3 viewportPos = cam.WorldToViewportPoint(desiredWorldPos);

        viewportPos.x = Mathf.Clamp(viewportPos.x, screenMargin, 1f - screenMargin);
        viewportPos.y = Mathf.Clamp(viewportPos.y, screenMargin, 1f - screenMargin);
        transform.position = cam.ViewportToWorldPoint(viewportPos);
    }

    private float CalculateHorizontalOffset()
    {
        // 1) 기본: 바라보는 방향 쪽으로 살짝
        float facingDir = 1f;
        var sr = _followTarget.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) facingDir = sr.flipX ? -1f : 1f;
        float offset = facingDir * facingOffsetX;

        // 2) 다른 화자와 가까우면 서로 반대쪽으로 밀어냄 (겹침 방지가 우선)
        var other = SpeechBubbleManager.Instance?.FindNearestOtherSpeaker(this);
        if (other != null)
        {
            float dx = _followTarget.position.x - other.FollowTargetPosition.x;
            if (Mathf.Abs(dx) < crowdDistance)
                offset = Mathf.Sign(dx == 0f ? 1f : dx) * crowdPushX; // 겹칠 땐 방향 오프셋 대신 밀어내기 우선
        }
        return offset;
    }
}