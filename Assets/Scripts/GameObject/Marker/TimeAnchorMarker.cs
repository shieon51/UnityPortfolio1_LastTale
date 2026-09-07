// TimeAnchorMarker.cs (신규)
using UnityEngine;
using TMPro;

public class TimeAnchorMarker : MonoBehaviour
{
    public TimeAnchorSnapshot snapshotData;
    public GameObject tooltipPrefab; // 월드스페이스 툴팁 UI 프리팹
    private GameObject _activeTooltip;

    private void OnMouseEnter() // 2D 콜라이더(트리거) 필요
    {
        if (tooltipPrefab == null || snapshotData == null) return;
        _activeTooltip = Instantiate(tooltipPrefab, transform.position + Vector3.up * 1f, Quaternion.identity);
        var text = _activeTooltip.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            var sora = PlayerManager.Instance.CurrentCharacter as SoraStats;
            text.text = $"Day {snapshotData.day} {snapshotData.hour}시\n{sora?.loopCount ?? 0}회차";
        }
    }

    private void OnMouseExit()
    {
        if (_activeTooltip != null) { Destroy(_activeTooltip); _activeTooltip = null; }
    }
}