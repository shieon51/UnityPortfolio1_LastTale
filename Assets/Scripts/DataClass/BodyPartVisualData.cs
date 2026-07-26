// BodyPartVisualData.cs (신규)
using UnityEngine;

public enum BodyPartSlot
{
    Body, Face, Hair, HairAccessory, Top, Bottom, Dress, Shoes, Wings, HandAccessory, Ear
}

[CreateAssetMenu(menuName = "LastMarchan/Visual/Body Part Visual")]
public class BodyPartVisualData : ScriptableObject
{
    public BodyPartSlot slot;
    public AnimatorOverrideController overrideController; // 이 파츠가 이 상황에서 재생할 모션 세트

    [Tooltip("이 폼 단계에서 이 파츠를 보이게 할지 여부")]
    public bool isVisible = true;
}