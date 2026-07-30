using System.Collections;
using UnityEngine;

// WorldSpaceTelegraphIndicator.cs (신규) — RadialFillIndicator를 보스 머리 위에서 재사용
public class WorldSpaceTelegraphIndicator : MonoBehaviour
{
    public static WorldSpaceTelegraphIndicator Instance { get; private set; }
    public RadialFillIndicator radialFill;
    public Vector3 offset = new Vector3(0, 1.2f, 0);
    private Transform _followTarget;

    private void Awake() { Instance = this; gameObject.SetActive(false); }

    private void LateUpdate()
    {
        if (_followTarget != null) transform.position = _followTarget.position + offset;
    }

    public void SetFollowTarget(Transform target) => _followTarget = target;

    public IEnumerator PlayCountdown(float duration)
    {
        gameObject.SetActive(true);
        yield return radialFill.PlayCountdown(duration);
        gameObject.SetActive(false);
    }
}