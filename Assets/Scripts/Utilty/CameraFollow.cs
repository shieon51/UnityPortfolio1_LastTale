// CameraFollow.cs 전체 교체
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Targets")]
    public Transform primaryTarget;   // 플레이어
    public Transform secondaryTarget; // 보스 (전투 중에만)

    [Header("Follow")]
    public float positionSmoothSpeed = 5f;
    public Vector3 offset = new Vector3(0, 0, -10);

    [Header("Deadzone (화면 4등분 시 1~3번째 선 사이)")]
    [Tooltip("화면 절반 너비 대비, 플레이어가 치우칠 수 있는 최대 비율")]
    [Range(0.1f, 0.9f)] public float deadzoneWidthRatio = 0.5f;

    [Header("Look-Ahead (마리오 스타일 — 방향 전환 즉시 안 따라가고 서서히 따라붙음)")]
    public float lookAheadDistance = 2f;
    public float lookAheadSmoothSpeed = 2f;
    private float _currentLookAhead = 0f;

    [Header("Secondary Target Bias")]
    [Range(0f, 1f)] public float secondaryBiasStrength = 0.5f;
    public float maxTrackingDistance = 20f; // 이 거리 넘으면 깨끗이 포기, 플레이어 위주 복귀

    [Header("Zoom")]
    public float minOrthoSize = 4f;
    public float maxOrthoSize = 10f;

    [Header("Bounds (지형 예외처리 완성되면 SetBounds()로 연결)")]
    public bool useBounds = false;
    public float minX, maxX, minY, maxY;

    private Camera _camera;
    private bool _instantSnapNextFrame = false;

    private void Awake() => _camera = GetComponent<Camera>();

    public void SnapToNextFollowPosition() => _instantSnapNextFrame = true;

    public void SetBounds(float minX, float maxX, float minY, float maxY)
    {
        this.minX = minX; this.maxX = maxX; this.minY = minY; this.maxY = maxY;
        useBounds = true;
    }

    private void LateUpdate()
    {
        if (primaryTarget == null) return;

        UpdateLookAhead();

        Vector3 desiredPosition;
        float desiredSize = minOrthoSize;

        if (secondaryTarget != null)
        {
            float dist = Vector2.Distance(primaryTarget.position, secondaryTarget.position);

            if (dist <= maxTrackingDistance)
            {
                float dirToSecondary = Mathf.Sign(secondaryTarget.position.x - primaryTarget.position.x);
                float screenHalfWidth = _camera != null ? _camera.orthographicSize * _camera.aspect : 8f;
                float biasOffset = dirToSecondary * screenHalfWidth * deadzoneWidthRatio * secondaryBiasStrength;

                desiredPosition = primaryTarget.position + new Vector3(_currentLookAhead + biasOffset, 0, 0) + offset;
                desiredSize = Mathf.Clamp(dist / 2f + 2f, minOrthoSize, maxOrthoSize);
            }
            else
            {
                // 너무 멀면 깨끗이 포기 — 플레이어 일반 추적으로 자연 복귀
                desiredPosition = primaryTarget.position + new Vector3(_currentLookAhead, 0, 0) + offset;
                desiredSize = minOrthoSize;
            }
        }
        else
        {
            desiredPosition = primaryTarget.position + new Vector3(_currentLookAhead, 0, 0) + offset;
        }

        if (useBounds)
        {
            desiredPosition.x = Mathf.Clamp(desiredPosition.x, minX, maxX);
            desiredPosition.y = Mathf.Clamp(desiredPosition.y, minY, maxY);
        }

        ApplyPosition(desiredPosition, desiredSize);
    }

    private void UpdateLookAhead()
    {
        float inputX = Input.GetAxisRaw("Horizontal");
        float targetLookAhead = inputX * lookAheadDistance;
        _currentLookAhead = Mathf.Lerp(_currentLookAhead, targetLookAhead, lookAheadSmoothSpeed * Time.deltaTime); // 서서히 따라붙음
    }

    private void ApplyPosition(Vector3 desiredPosition, float desiredSize)
    {
        if (_instantSnapNextFrame)
        {
            transform.position = desiredPosition;
            if (_camera != null) _camera.orthographicSize = desiredSize;
            _instantSnapNextFrame = false;
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, desiredPosition, positionSmoothSpeed * Time.deltaTime);
            if (_camera != null) _camera.orthographicSize = Mathf.Lerp(_camera.orthographicSize, desiredSize, positionSmoothSpeed * Time.deltaTime);
        }
    }
}