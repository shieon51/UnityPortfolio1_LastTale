using UnityEngine;

// ink 태그로 호출할 연출용 애니메이션
[CreateAssetMenu(menuName = "LastMarchan/Visual/Cutscene Animation")]
public class CutsceneAnimationData : ScriptableObject
{
    [Tooltip("ink 태그(#emote:xxx)와 매칭될 고유 키")]
    public string key;
    public string bodyStateName; // Face를 제외한 파츠들이 재생할 State
    public string faceStateName; // Face 전용 표정 State (몸 동작과 독립적으로 재생 가능)
    [Tooltip("-1이면 클립 길이만큼 재생 후 자동 복귀, 0 이상이면 지정 시간 후 복귀")]
    public float duration = -1f;
}