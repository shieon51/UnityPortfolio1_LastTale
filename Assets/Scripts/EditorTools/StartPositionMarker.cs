// StartPositionMarker.cs (신규)
using UnityEngine;

public class StartPositionMarker : MonoBehaviour
{
    private void Awake() { if (Application.isPlaying) Destroy(gameObject); }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(transform.position, 0.4f);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.6f, "시작 위치");
#endif
    }
}