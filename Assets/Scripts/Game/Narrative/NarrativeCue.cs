// NarrativeCue.cs (신규)
using UnityEngine;

[CreateAssetMenu(menuName = "LastMarchan/Narrative/Narrative Cue")]
public class NarrativeCue : ScriptableObject
{
    public string cueId; // ink 태그(#cue:xxx)와 매칭
    [Header("카메라")] public CameraCue cameraCue;
    [Header("화면 효과")] public bool screenFlash; public Color flashColor = Color.white; public float flashDuration = 0.1f;
    [Header("캐릭터 포즈/표정 (기존 CutsceneAnimationCatalog 그대로 재사용)")]
    public string targetCharacterKey; // "Player" 또는 NPC 이름
    public string animationKey; // ★ bodyState/faceState 직접 안 두고, 기존 카탈로그의 키만 참조
    [Header("사운드")] public string sfxKey; public string bgmKey;
    [Header("일러스트/컷씬 (추후 확장)")] public Sprite standingIllustration;
}