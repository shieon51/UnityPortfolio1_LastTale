// CounterDefinition.cs (신규)
using UnityEngine;

[CreateAssetMenu(menuName = "LastMarchan/Narrative/Counter Definition")]
public class CounterDefinition : ScriptableObject
{
    [Tooltip("ink에서 사용할 고유 키")]
    public string counterId;

    [Tooltip("분류용 — 보통 NPC 이름")]
    public string category;

    public enum CounterKind { Meet, TopicTold, ChoiceMade, EventProgress, Other }
    [Tooltip("성격 분류 — 목록에서 구분하기 위함")]
    public CounterKind kind = CounterKind.Other;

    [Tooltip("에디터에 표시될 설명")]
    public string displayName;

    [TextArea(2, 4)]
    [Tooltip("이 카운터가 언제 증가하는지, 어떤 정보와 얽히는지 메모")]
    public string note;

    [Tooltip("이 카운터와 관련된 기억 조각 (연관 파악용)")]
    public string[] relatedMemoryFlags;
}