// SummonPointMarker.cs (신규)
using UnityEngine;

public class SummonPointMarker : MonoBehaviour
{
    [Tooltip("여기서 등장할 NPC 이름 (프리팹 이름과 일치)")]
    public string npcName = "Liel";

    private void Awake() { if (Application.isPlaying) Destroy(gameObject); }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.4f, 1f, 0.9f);
        Gizmos.DrawSphere(transform.position, 0.35f);
        var parent = GetComponentInParent<EventMarker>();
        if (parent != null)
        {
            Gizmos.color = new Color(1f, 0.4f, 1f, 0.4f);
            Gizmos.DrawLine(transform.position, parent.transform.position);
        }
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, $"등장: {npcName}");
#endif
    }
}