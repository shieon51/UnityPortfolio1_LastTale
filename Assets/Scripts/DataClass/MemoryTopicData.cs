using UnityEngine;

// 여러 단계로 진화하는 "주제". 분류(카드)와는 다른 축이다.
// 분류 = 어느 카드에 들어가는가, 단계 = 같은 주제가 얼마나 깊어졌는가
[CreateAssetMenu(menuName = "LastMarchan/Narrative/Memory Topic")]
public class MemoryTopicData : ScriptableObject
{
    public string topicId; // 예: "liel_family"

    [Header("분류")]
    public string category;
    [Tooltip("이 주제가 들어갈 카드")]
    public MemoryCardCategory cardCategory = MemoryCardCategory.Traits;   // ★ 신규
    [Tooltip("이 주제가 걸리는 인물들")]
    public string[] relatedNpcs;                                          // ★ 신규
    [Tooltip("주제 제목 키 (문장 안에 '가족:' 같은 접두어를 넣지 말 것)")]
    public string titleKey;                                               // ★ 신규

    public int day;

    [System.Serializable]
    public class Stage
    {
        public string requiredFlagId; // 이 정보 조각을 얻으면 이 단계로 갱신됨
        public string localizationKey; // 실제 문장 대신 키만

        [Tooltip("이 단계 문장의 확실도. isFinal과 별개다 — 1단계도 확신할 수 있다")]
        public MemoryCertainty certainty = MemoryCertainty.Confirmed;
        public MemorySourceType sourceType = MemorySourceType.None;       // ★ 추가
        [Tooltip("출처 대상. 전언이면 NPC 이름")]
        public string sourceKey;                                          // ★ 추가
        [Tooltip("부연설명 키 (선택)")]
        public string footnoteKey;

        public bool isFinal; // 이 주제가 완전히 밝혀진 최종 단계인지
    }

    [Tooltip("배열 순서 = 진행 순서. 나중에 얻는 flagId일수록 뒤쪽에 배치")]
    public Stage[] stages;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if ((relatedNpcs == null || relatedNpcs.Length == 0) && !string.IsNullOrEmpty(category))
            relatedNpcs = new[] { category };
    }
#endif
}