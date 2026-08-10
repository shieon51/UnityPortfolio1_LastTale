using UnityEngine;

// SceneBoundsManager.cs (신규)
public class SceneBoundsManager : Singleton<SceneBoundsManager>
{
    public bool HasBounds => _current != null;
    public Vector2 ClampToBounds(Vector2 point)
    {
        if (_current == null) return point;
        return new Vector2(Mathf.Clamp(point.x, _current.min.x, _current.max.x), Mathf.Clamp(point.y, _current.min.y, _current.max.y));
    }
    private SceneBounds _current; // RegisterBounds()에서 이미 채워지고 있던 것에 필드만 추가
    public void RegisterBounds(SceneBounds bounds)
    {
        _current = bounds; // ★ 추가 — 지난번엔 카메라에 넘겨주기만 하고 저장을 안 해뒀었습니다
        var cam = Camera.main?.GetComponent<CameraFollow>();
        if (cam != null && _current != null)
            cam.SetBounds(_current.min.x, _current.max.x, _current.min.y, _current.max.y); // 카메라도 자동으로 같은 범위로 제한
    }
    public bool IsWellWithinBounds(Vector2 point, float margin = 2f)
    {
        if (_current == null) return true;
        return point.x >= _current.min.x - margin && point.x <= _current.max.x + margin &&
               point.y >= _current.min.y - margin && point.y <= _current.max.y + margin;
    }
}