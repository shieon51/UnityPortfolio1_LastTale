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
    [Range(0.05f, 0.6f)] public float deadzoneWidthRatio = 0.15f;

    [Header("Look-Ahead (마리오 스타일 — 방향 전환 즉시 안 따라가고 서서히 따라붙음)")]
    public float lookAheadDistance = 2f;
    public float lookAheadSmoothSpeed = 2f;
    private float _currentLookAhead = 0f;
    private float _cameraTargetX;

    [Header("Secondary Target Bias (보스전 전용, secondaryTarget이 있을 때만 추가 적용)")]
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
    private Vector3 _shakeOffset = Vector3.zero;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        if (primaryTarget != null) _cameraTargetX = primaryTarget.position.x;
    }

    public void SnapToNextFollowPosition() => _instantSnapNextFrame = true;
    public void ApplyShakeOffset(Vector3 offset) => _shakeOffset = offset;

    public void SetBounds(float minX, float maxX, float minY, float maxY)
    {
        this.minX = minX; this.maxX = maxX; this.minY = minY; this.maxY = maxY;
        useBounds = true;
    }

    private void LateUpdate()
    {
        if (primaryTarget == null) return;

        UpdateLookAhead();
        UpdateDeadzone(); // ★ 평시/전투 공통 — 여기서 secondaryTarget 여부를 안 가림

        float desiredX = _cameraTargetX + _currentLookAhead;
        float desiredY = primaryTarget.position.y;
        float desiredSize = minOrthoSize;

        if (secondaryTarget != null)
        {
            float dist = Vector2.Distance(primaryTarget.position, secondaryTarget.position);
            if (dist <= maxTrackingDistance)
            {
                float dirToSecondary = Mathf.Sign(secondaryTarget.position.x - primaryTarget.position.x);
                float screenHalfWidth = _camera != null ? _camera.orthographicSize * _camera.aspect : 8f;
                desiredX += dirToSecondary * screenHalfWidth * 0.5f * secondaryBiasStrength; // 데드존 위에 추가로 얹는 편향
                desiredSize = Mathf.Clamp(dist / 2f + 2f, minOrthoSize, maxOrthoSize);
            }
        }

        Vector3 desiredPosition = new Vector3(desiredX, desiredY, 0) + offset;

        if (useBounds)
        {
            desiredPosition.x = Mathf.Clamp(desiredPosition.x, minX, maxX);
            desiredPosition.y = Mathf.Clamp(desiredPosition.y, minY, maxY);
        }

        ApplyPosition(desiredPosition, desiredSize);
        transform.position += _shakeOffset; // ★ 흔들림은 Lerp 밖에서 마지막에 직접 더해져서 뭉개지지 않음
    }

    // 플레이어가 데드존을 벗어나야만 카메라의 추적 기준점(_cameraTargetX)이 따라 움직임
    private void UpdateDeadzone()
    {
        float screenHalfWidth = _camera != null ? _camera.orthographicSize * _camera.aspect : 8f;
        float deadzoneHalfWidth = screenHalfWidth * deadzoneWidthRatio;

        float diff = primaryTarget.position.x - _cameraTargetX;
        if (diff > deadzoneHalfWidth) _cameraTargetX = primaryTarget.position.x - deadzoneHalfWidth;
        else if (diff < -deadzoneHalfWidth) _cameraTargetX = primaryTarget.position.x + deadzoneHalfWidth;
    }

    private void UpdateLookAhead()
    {
        float inputX = Input.GetAxisRaw("Horizontal");
        float targetLookAhead = inputX * lookAheadDistance;
        _currentLookAhead = Mathf.Lerp(_currentLookAhead, targetLookAhead, lookAheadSmoothSpeed * Time.deltaTime);
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