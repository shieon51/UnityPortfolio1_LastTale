using UnityEngine;

// CameraFollow.cs 전체 교체 (최종본)
public class CameraFollow : MonoBehaviour
{
    [Header("Targets")]
    public Transform primaryTarget;
    public Transform secondaryTarget;

    [Header("Follow")]
    public float positionSmoothSpeed = 5f;
    public Vector3 offset = new Vector3(0, 0, -10);
    public float verticalOffset = 0f;

    [Header("Deadzone")]
    [Range(0.05f, 0.6f)] public float deadzoneWidthRatio = 0.15f;
    private float _cameraTargetX;

    [Header("Look-Ahead")]
    [Tooltip("데드존 폭보다 확실히 커야 방향이 올바르게 나옵니다")]
    public float lookAheadDistance = 3f;
    public float lookAheadSmoothSpeed = 2f;
    private float _currentLookAhead = 0f;
    private IPlayerMotor _targetMotor;

    [Header("Secondary Target Bias (보스전)")]
    [Range(0f, 1f)] public float secondaryBiasStrength = 0.5f;
    public float maxTrackingDistance = 20f;
    public float framingPadding = 2f;
    private float _currentSecondaryBiasX = 0f;
    private float _currentTrackingSize;

    [Header("Recenter (W 스킬 등 순간이동 직후 임시 중점 프레이밍)")]
    public float recenterDuration = 0.4f;
    private float _recenterTimer = 0f;

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
            _targetMotor = primaryTarget.GetComponent<IPlayerMotor>();
        }
        _currentTrackingSize = minOrthoSize;
    }

    public void SnapToNextFollowPosition() => _instantSnapNextFrame = true;
    public void ApplyShakeOffset(Vector3 offset) => _shakeOffset = offset;
    public void TriggerRecenter(float duration = -1f) => _recenterTimer = duration > 0f ? duration : recenterDuration;

    public void SetBounds(float minX, float maxX, float minY, float maxY)
    {
        this.minX = minX; this.maxX = maxX; this.minY = minY; this.maxY = maxY;
        useBounds = true;
    }

    private void LateUpdate()
    {
        if (primaryTarget == null) return;

        UpdateLookAhead();

        float desiredX;
        float desiredY = primaryTarget.position.y + verticalOffset;
        float desiredSize = minOrthoSize;

        if (_recenterTimer > 0f)
        {
            // ★ 순간이동 직후 잠깐: 데드존/편향을 무시하고 순수 중점을 비춤
            _recenterTimer -= Time.deltaTime;
            if (secondaryTarget != null)
            {
                desiredX = (primaryTarget.position.x + secondaryTarget.position.x) / 2f;
                float dist = Vector2.Distance(primaryTarget.position, secondaryTarget.position);
                desiredSize = Mathf.Clamp(dist / 2f + framingPadding, minOrthoSize, maxOrthoSize);
            }
            else
            {
                desiredX = primaryTarget.position.x;
            }
            _cameraTargetX = desiredX; // 복귀 시 이어지도록 갱신
            _currentTrackingSize = desiredSize;
        }
        else
        {
            // ★ 룩어헤드가 반영된 '가상 목표점'을 데드존이 추적 — 서로 경쟁하지 않고 하나로 통일
            float virtualTargetX = primaryTarget.position.x + _currentLookAhead;
            UpdateDeadzone(virtualTargetX);
            desiredX = _cameraTargetX;

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

            _currentSecondaryBiasX = Mathf.Lerp(_currentSecondaryBiasX, targetBiasX, positionSmoothSpeed * Time.deltaTime);
            _currentTrackingSize = Mathf.Lerp(_currentTrackingSize, targetTrackingSize, positionSmoothSpeed * Time.deltaTime);

            desiredX += _currentSecondaryBiasX;
            desiredSize = _currentTrackingSize;
        }

        Vector3 desiredPosition = new Vector3(desiredX, desiredY, 0) + offset;

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

    // 룩어헤드가 반영된 가상 목표점을 데드존이 추적
    private void UpdateDeadzone(float virtualTargetX)
    {
        float screenHalfWidth = _camera != null ? _camera.orthographicSize * _camera.aspect : 8f;
        float deadzoneHalfWidth = screenHalfWidth * deadzoneWidthRatio;

        float diff = virtualTargetX - _cameraTargetX;
        if (diff > deadzoneHalfWidth) _cameraTargetX = virtualTargetX - deadzoneHalfWidth;
        else if (diff < -deadzoneHalfWidth) _cameraTargetX = virtualTargetX + deadzoneHalfWidth;
    }

    // ★ 속도 대신 입력을 직접 확인 — 노이즈 없음. 잠금 중엔 입력 자체를 안 읽음(Lock 버그 재발 방지)
    private void UpdateLookAhead()
    {
        float inputX = 0f;
        bool locked = _targetMotor != null && (_targetMotor.IsActionLocked || _targetMotor.IsGuarding);
        if (!locked) inputX = Input.GetAxisRaw("Horizontal");
        float targetLookAhead = Mathf.Abs(inputX) > 0.01f ? Mathf.Sign(inputX) * lookAheadDistance : 0f;
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