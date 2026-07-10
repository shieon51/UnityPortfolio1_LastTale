using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// '발밑이 플랫폼 표면보다 위에 있는가'를 매 FixedUpdate마다 직접 계산해서
// 충돌 여부를 강제 확정한다. Effector2D의 모서리 판정 애매함을 원천 차단.
[RequireComponent(typeof(Collider2D))]
public class OneWayPlatformController : MonoBehaviour
{
    [Tooltip("원웨이 플랫폼으로 취급할 레이어")]
    public LayerMask oneWayPlatformLayer;

    [Tooltip("발밑 판정 기준점 오프셋 (PlayerController의 groundCheckOffset과 동일하게)")]
    public Vector3 feetOffset = new Vector3(0, -0.5f, 0);

    [Tooltip("표면보다 이만큼 위에 있어야 '위에 있다'고 인정하는 여유 마진")]
    public float surfaceTolerance = 0.05f;

    [Tooltip("주변 원웨이 플랫폼을 탐색할 반경")]
    public float detectionRadius = 1.5f;

    [Tooltip("아래 방향키로 강제 통과시킬 때 유지되는 시간(초)")]
    public float passThroughDuration = 0.5f;

    private Collider2D _selfCollider;
    private readonly Dictionary<Collider2D, bool> _autoIgnoreState = new Dictionary<Collider2D, bool>();
    private readonly HashSet<Collider2D> _manualPassThrough = new HashSet<Collider2D>();
    private readonly Collider2D[] _overlapBuffer = new Collider2D[8];

    private void Awake() => _selfCollider = GetComponent<Collider2D>();

    private void FixedUpdate()
    {
        float feetY = transform.position.y + feetOffset.y;
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, detectionRadius, _overlapBuffer, oneWayPlatformLayer);

        for (int i = 0; i < count; i++)
        {
            Collider2D platform = _overlapBuffer[i];
            if (platform == null || _manualPassThrough.Contains(platform)) continue;

            float platformTopY = platform.bounds.max.y;
            bool shouldIgnore = feetY < platformTopY - surfaceTolerance;

            if (!_autoIgnoreState.TryGetValue(platform, out bool current) || current != shouldIgnore)
            {
                Physics2D.IgnoreCollision(_selfCollider, platform, shouldIgnore);
                _autoIgnoreState[platform] = shouldIgnore;
            }
        }
    }

    public bool IsOneWayPlatformLayer(int layer) => (oneWayPlatformLayer.value & (1 << layer)) != 0;

    public bool TryPassThrough(Collider2D platform)
    {
        if (platform == null || !IsOneWayPlatformLayer(platform.gameObject.layer)) return false;
        StartCoroutine(PassThroughRoutine(platform));
        return true;
    }

    private IEnumerator PassThroughRoutine(Collider2D platform)
    {
        _manualPassThrough.Add(platform);
        Physics2D.IgnoreCollision(_selfCollider, platform, true);
        yield return new WaitForSeconds(passThroughDuration);
        Physics2D.IgnoreCollision(_selfCollider, platform, false);
        _autoIgnoreState[platform] = false;
        _manualPassThrough.Remove(platform);
    }
}