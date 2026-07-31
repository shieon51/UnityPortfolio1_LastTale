using System.Collections;
using UnityEngine;

// WorldSpaceTelegraphIndicator.cs — RadialFillIndicator를 보스 머리 위에서 재사용
public class WorldSpaceTelegraphIndicator : Singleton<WorldSpaceTelegraphIndicator>
{
    public CanvasGroup canvasGroup;
    public RadialFillIndicator radialFill;
    public Vector3 offset = new Vector3(0, 1.2f, 0);
    private Transform _followTarget;

    private void Awake() => Hide();

    public void Show() { if (canvasGroup != null) { canvasGroup.alpha = 1f; canvasGroup.blocksRaycasts = true; } }
    public void Hide() { if (canvasGroup != null) { canvasGroup.alpha = 0f; canvasGroup.blocksRaycasts = false; } }

    private void LateUpdate()
    {
        if (_followTarget != null) transform.position = _followTarget.position + offset;
    }

    public void SetFollowTarget(Transform target) => _followTarget = target;

    public IEnumerator PlayCountdown(float duration)
    {
        Show();
        yield return radialFill.PlayCountdown(duration);
        Hide();
    }
}