// MemoryTopicData.cs (신규) — 기존 MemoryFragmentData 대신, 여러 단계로 진화하는 "주제"
using UnityEngine;

[CreateAssetMenu(menuName = "LastMarchan/Narrative/Memory Topic")]
public class MemoryTopicData : ScriptableObject
{
    public string topicId; // 예: "liel_family"
    public string category;
    public int day;

    [System.Serializable]
    public class Stage
    {
        public string requiredFlagId; // 이 정보 조각을 얻으면 이 단계로 갱신됨
        public string localizationKey; // ★ 실제 문장 대신 키만
        public bool isFinal; // 이 주제가 완전히 밝혀진 최종 단계인지
    }
    [Tooltip("배열 순서 = 진행 순서. 나중에 얻는 flagId일수록 뒤쪽에 배치")]
    public Stage[] stages;
}