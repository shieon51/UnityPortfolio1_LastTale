using System.Collections.Generic;
using UnityEngine;

public enum UIWindowId { Inventory, SkillTree, Journal, Map, SaveLoad, EndingGallery, StoryTree, Profile }

public abstract class UIWindow : MonoBehaviour
{
    public abstract UIWindowId WindowId { get; }
    public virtual void Open() => gameObject.SetActive(true);
    public virtual void Close() => gameObject.SetActive(false);
}

// UI 창 구조
public class UIWindowManager : Singleton<UIWindowManager>
{
    [Tooltip("실제 창(UIWindow)들이 들어있는 컨테이너 (Canvas > Windows)")]
    public Transform windowsContainer;

    private Dictionary<UIWindowId, UIWindow> _windows = new();
    private UIWindow _current;

    private void Awake()
    {
        var source = windowsContainer != null ? windowsContainer : transform;
        foreach (var w in source.GetComponentsInChildren<UIWindow>(true)) { _windows[w.WindowId] = w; w.Close(); }
    }

    public void Open(UIWindowId id)
    {
        if (UIModeManager.Instance != null && UIModeManager.Instance.CurrentMode != UIMode.Normal) return; // 전투 중엔 창 열기 제한

        _current?.Close();
        if (_windows.TryGetValue(id, out var window)) { window.Open(); _current = window; }
    }

    public void CloseCurrent() { _current?.Close(); _current = null; }
    public bool IsAnyWindowOpen => _current != null;
}

// 껍데기 — 각 창 상세 구현은 나중에
public class InventoryWindow : UIWindow { public override UIWindowId WindowId => UIWindowId.Inventory; }
public class SkillTreeWindow : UIWindow { public override UIWindowId WindowId => UIWindowId.SkillTree; }
public class JournalWindow : UIWindow { public override UIWindowId WindowId => UIWindowId.Journal; }
public class MapWindow : UIWindow { public override UIWindowId WindowId => UIWindowId.Map; }
public class SaveLoadWindow : UIWindow { public override UIWindowId WindowId => UIWindowId.SaveLoad; }
public class EndingGalleryWindow : UIWindow { public override UIWindowId WindowId => UIWindowId.EndingGallery; }
public class StoryTreeWindow : UIWindow { public override UIWindowId WindowId => UIWindowId.StoryTree; }
public class ProfileWindow : UIWindow { public override UIWindowId WindowId => UIWindowId.Profile; }