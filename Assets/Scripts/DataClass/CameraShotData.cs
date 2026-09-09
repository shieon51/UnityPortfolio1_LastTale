using UnityEngine;

// CameraShotData.cs (신규) — 대화/연출용 카메라 샷 정의
[CreateAssetMenu(menuName = "LastMarchan/Camera/Camera Shot")]
public class CameraShotData : ScriptableObject
{
    public enum FollowMode { SingleTarget, Midpoint, FixedPosition, Path }

    [Header("타겟팅")]
    public FollowMode followMode = FollowMode.Midpoint;
    [Tooltip("SingleTarget일 때 사용할 화자 키(예: 'Liel', 'Player'). Midpoint는 현재 말하고 있는 인물들의 중심")]
    public string targetKey;

    [Header("전환 속도")]
    public float blendDuration = 0.8f;
    public AnimationCurve blendCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("줌")]
    public bool overrideZoom;
    public float targetOrthoSize = 5f;

    [Header("커스텀 경로 (위에서 슉 내려오는 연출 등)")]
    public bool usePath;
    public Vector2[] pathWaypoints; // 상대 오프셋 기준 웨이포인트
    public float pathDuration = 1f;

    [Header("도착 시 흔들림 (착지 연출 등)")]
    public bool shakeOnArrival;
    public float arrivalShakeIntensity = 0.3f;
    public float arrivalShakeDuration = 0.2f;
}