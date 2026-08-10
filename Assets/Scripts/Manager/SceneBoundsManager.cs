using UnityEngine;

// SceneBoundsManager.cs (신규)
public class SceneBoundsManager : Singleton<SceneBoundsManager>
{
    public void RegisterBounds(SceneBounds bounds)
    {
        var cam = Camera.main?.GetComponent<CameraFollow>();
        if (cam != null && bounds != null)
            cam.SetBounds(bounds.min.x, bounds.max.x, bounds.min.y, bounds.max.y); // 카메라도 자동으로 같은 범위로 제한
    }
}