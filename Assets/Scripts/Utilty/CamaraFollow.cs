using UnityEngine;

public class CamaraFollow : MonoBehaviour
{
    public Transform target;  // 따라갈 대상 (플레이어)
    public float smoothSpeed = 5f;  // 카메라 이동 속도
    public Vector3 offset;  // 카메라 위치 조정

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + offset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        transform.position = smoothedPosition;
    }
}
