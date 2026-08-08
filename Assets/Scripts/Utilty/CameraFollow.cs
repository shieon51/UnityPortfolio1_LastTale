using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Targets")]
    public Transform primaryTarget;   // 플레이어
    public Transform secondaryTarget; // 보스 (전투 중에만 채워짐)

    [Header("Follow")]
    public float smoothSpeed = 5f;  // 카메라 이동 속도
    public Vector3 offset = new Vector3(0, 0, -10);  // 카메라 위치 조정

    [Header("Dual-Target Framing")]
    public float framingPadding = 2f;
    public float minOrthoSize = 4f;
    public float maxOrthoSize = 10f;
    [Tooltip("이 거리를 넘으면 '둘 다 담기'를 포기하고 보스 방향으로 살짝 치우침")]
    public float maxFramingDistance = 20f;
    [Range(0f, 1f)] public float biasTowardSecondary = 0.3f;

    private Camera _camera;
    private bool _instantSnapNextFrame = false;

    private void Awake() => _camera = GetComponent<Camera>();

    // W 스킬처럼 순간이동하는 연출에서 호출 — 다음 프레임엔 Lerp 없이 즉시 스냅
    public void SnapToNextFollowPosition() => _instantSnapNextFrame = true;

    private void LateUpdate()
    {
        if (primaryTarget == null) return;

        Vector3 desiredPosition;
        float desiredSize = minOrthoSize;

        if (secondaryTarget != null)
        {
            float dist = Vector2.Distance(primaryTarget.position, secondaryTarget.position);

            if (dist <= maxFramingDistance)
            {
                Vector3 midpoint = (primaryTarget.position + secondaryTarget.position) / 2f;
                desiredPosition = midpoint + offset;
                desiredSize = Mathf.Clamp(dist / 2f + framingPadding, minOrthoSize, maxOrthoSize);
            }
            else
            {
                // 너무 멀어지면 '같이 담기' 포기, 플레이어 기준으로 보스 방향에 살짝 치우침
                Vector3 towardSecondary = (secondaryTarget.position - primaryTarget.position).normalized * framingPadding;
                desiredPosition = primaryTarget.position + towardSecondary * biasTowardSecondary + offset;
                desiredSize = maxOrthoSize;
            }
        }
        else
        {
            desiredPosition = primaryTarget.position + offset;
        }

        if (_instantSnapNextFrame)
        {
            transform.position = desiredPosition;
            if (_camera != null) _camera.orthographicSize = desiredSize;
            _instantSnapNextFrame = false;
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
            if (_camera != null) _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, desiredSize, smoothSpeed * Time.deltaTime);
        }
    }
}
