// CameraFollow.cs 전체 교체 (최종본)
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Targets")]
    public Transform primaryTarget;
    public Transform secondaryTarget;

    [Header("Follow")]
    public float positionSmoothSpeed = 5f;
    public Vector3 offset = new Vector3(0, 0, -10);
    [Tooltip("플레이어를 화면 세로 중앙에서 얼마나 위/아래로 옮길지")]
    public float verticalOffset = 0f; // ★ 3번: 세로 오프셋

    [Header("Deadzone — 이 폭 안에서는 카메라가 안 움직임")]
    [Range(0.05f, 0.6f)] public float deadzoneWidthRatio = 0.15f;
    private float _cameraTargetX;

    [Header("Look-Ahead")]
    public float lookAheadDistance = 2f;
    public float lookAheadSmoothSpeed = 2f;
    private float _currentLookAhead = 0f;
    private Rigidbody2D _targetRb; // 입력이 아니라 '실제로 움직이고 있는지'를 속도로 판단

    [Header("Secondary Target Bias (보스전)")]
    [Range(0f, 1f)] public float secondaryBiasStrength = 0.5f;
    public float maxTrackingDistance = 20f;
    public float framingPadding = 2f;
    private float _currentSecondaryBiasX = 0f; // ★ 편향값 자체를 서서히 보간 — "휙" 방지
    private float _currentTrackingSize; // ★ 줌도 서서히 보간

    [Header("Zoom")]
    public float minOrthoSize = 4f;
    public float maxOrthoSize = 10f;

    [Header("Bounds")]
    public bool useBounds = false;
    public float minX, maxX, minY, maxY;

    private Camera _camera;
    private bool _instantSnapNextFrame = false;
    private Vector3 _shakeOffset = Vector3.zero;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        if (primaryTarget != null)
        {
            _cameraTargetX = primaryTarget.position.x;
            _targetRb = primaryTarget.GetComponent<Rigidbody2D>();
        }
        _currentTrackingSize = minOrthoSize;
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
        UpdateDeadzone();

        float baseX = _cameraTargetX + _currentLookAhead;
        float desiredY = primaryTarget.position.y + verticalOffset;

        // --- 보조 타겟(보스) 편향 — 목표값만 계산하고, 실제 반영은 서서히 ---
        float targetBiasX = 0f;
        float targetTrackingSize = minOrthoSize;

        if (secondaryTarget != null)
        {
            float dist = Vector2.Distance(primaryTarget.position, secondaryTarget.position);
            if (dist <= maxTrackingDistance)
            {
                float dirToSecondary = Mathf.Sign(secondaryTarget.position.x - primaryTarget.position.x);
                float screenHalfWidth = _camera != null ? _camera.orthographicSize * _camera.aspect : 8f;
                targetBiasX = dirToSecondary * screenHalfWidth * 0.5f * secondaryBiasStrength;
                targetTrackingSize = Mathf.Clamp(dist / 2f + framingPadding, minOrthoSize, maxOrthoSize);
            }
        }

        // ★ 편향값/줌 크기 자체를 매 프레임 서서히 따라가게 — 부호가 뒤집혀도 순간이동하듯 안 튐
        _currentSecondaryBiasX = Mathf.Lerp(_currentSecondaryBiasX, targetBiasX, positionSmoothSpeed * Time.deltaTime);
        _currentTrackingSize = Mathf.Lerp(_currentTrackingSize, targetTrackingSize, positionSmoothSpeed * Time.deltaTime);

        baseX += _currentSecondaryBiasX;
        float desiredSize = _currentTrackingSize;

        Vector3 desiredPosition = new Vector3(baseX, desiredY, 0) + offset;

        // --- 지형 경계: 카메라 '중심'이 아니라 '화면 가장자리'가 넘어가지 않게 ---
        if (useBounds)
        {
            float halfWidth = desiredSize * (_camera != null ? _camera.aspect : 1.78f);
            float halfHeight = desiredSize;

            float clampMinX = minX + halfWidth, clampMaxX = maxX - halfWidth;
            float clampMinY = minY + halfHeight, clampMaxY = maxY - halfHeight;

            desiredPosition.x = clampMinX <= clampMaxX ? Mathf.Clamp(desiredPosition.x, clampMinX, clampMaxX) : (minX + maxX) / 2f;
            desiredPosition.y = clampMinY <= clampMaxY ? Mathf.Clamp(desiredPosition.y, clampMinY, clampMaxY) : (minY + maxY) / 2f;
        }

        ApplyPosition(desiredPosition, desiredSize);
        transform.position += _shakeOffset;
    }

    private void UpdateDeadzone()
    {
        float screenHalfWidth = _camera != null ? _camera.orthographicSize * _camera.aspect : 8f;
        float deadzoneHalfWidth = screenHalfWidth * deadzoneWidthRatio;

        float diff = primaryTarget.position.x - _cameraTargetX;
        if (diff > deadzoneHalfWidth) _cameraTargetX = primaryTarget.position.x - deadzoneHalfWidth;
        else if (diff < -deadzoneHalfWidth) _cameraTargetX = primaryTarget.position.x + deadzoneHalfWidth;
    }

    // 키 입력이 아니라 '실제 속도'로 판단 — lock 걸려서 실제로 안 움직이면 룩어헤드도 자동으로 0 (5번 버그와 연결)
    private void UpdateLookAhead()
    {
        float velX = _targetRb != null ? _targetRb.linearVelocity.x : 0f;
        float targetLookAhead = Mathf.Abs(velX) > 0.1f ? Mathf.Sign(velX) * lookAheadDistance : 0f; //** Mathf.Sign(velX) 앞에 마이너스(-)만 붙이시면 정확히 반대로 뒤집힙 
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