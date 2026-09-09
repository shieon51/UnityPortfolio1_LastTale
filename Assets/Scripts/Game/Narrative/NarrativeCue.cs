// NarrativeCue.cs (신규)
using UnityEngine;

[CreateAssetMenu(menuName = "LastMarchan/Narrative/Narrative Cue")]
public class NarrativeCue : ScriptableObject
{
    public string cueId; // ink 태그(#cue:xxx)와 매칭
    [Header("카메라 (흔들림+플래시 다 여기서 처리됨)")]
    public CameraCue cameraCue;
    [Header("캐릭터 포즈/표정")]
    public string targetCharacterKey;
    public string animationKey;
    [Header("사운드")]
    public string sfxKey;
    public string bgmKey;
    [Header("일러스트/컷씬 (추후 확장)")]
    public Sprite standingIllustration;
}