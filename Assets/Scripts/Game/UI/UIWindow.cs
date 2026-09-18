using Unity.VisualScripting;
using UnityEngine;

public enum UIWindowId { Inventory, SkillTree, Journal, Map, SaveLoad, EndingGallery, StoryTree, Profile }

// 열람형 창의 기반
public abstract class UIWindow : UIStackElement
{
    public abstract UIWindowId WindowId { get; }
    public override UILayer Layer => UILayer.Window;

    // 기존 호출부 호환용 — 실제 처리는 매니저를 거친다
    public virtual void Open() => UIWindowManager.Instance.Open(WindowId);
    public virtual void Close() => UIWindowManager.Instance.Close(this);
}

// 껍데기 — 각 창 상세 구현은 나중에
//public class InventoryWindow : UIWindow { public override UIWindowId WindowId => UIWindowId.Inventory; }
//public class SkillTreeWindow : UIWindow { public override UIWindowId WindowId => UIWindowId.SkillTree; }
//public class MapWindow : UIWindow { public override UIWindowId WindowId => UIWindowId.Map; }
//public class SaveLoadWindow : UIWindow { public override UIWindowId WindowId => UIWindowId.SaveLoad; }
//public class EndingGalleryWindow : UIWindow { public override UIWindowId WindowId => UIWindowId.EndingGallery; }
//public class StoryTreeWindow : UIWindow { public override UIWindowId WindowId => UIWindowId.StoryTree; }
//public class ProfileWindow : UIWindow { public override UIWindowId WindowId => UIWindowId.Profile; }