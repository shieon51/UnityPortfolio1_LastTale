using UnityEngine;

[CreateAssetMenu(menuName = "LastMarchan/Narrative/Memory Fragment")]
public class MemoryFragmentData : ScriptableObject
{
    [Header("날짜 (선택, 그룹핑용 — 특정 날짜 무관이면 0)")]
    public int day = 0;

    [Header("식별자 (ink에서 이 이름으로 조회함, 오타 주의)")]
    public string flagId;

    [Header("분류 (에디터 검색/필터용)")]
    public string category; // 예: "Liel", "세계관 진실", "소라 과거"

    [Header("기록장 표시용")]
    public string localizationKey; // 실제 문장 대신 키만

    [Tooltip("이 정보가 들어갈 카드")]
    public MemoryCardCategory cardCategory = MemoryCardCategory.Traits;   // ★ 신규

    [Tooltip("이 정보가 걸리는 인물들. 여기 적힌 모든 인물의 페이지에 표시된다")]
    public string[] relatedNpcs;                                          // ★ 신규 — category 한 개로는 두 인물에 걸린 정보를 표현할 수 없었음

    [Header("신뢰도")]
    [Tooltip("검은 글씨(확신) / 회색 글씨(불확실)")]
    public MemoryCertainty certainty = MemoryCertainty.Confirmed;         // ★ 신규
    public MemorySourceType sourceType = MemorySourceType.None;           // ★ 신규
    [Tooltip("출처 대상. 전언이면 NPC 이름, 증거면 물건 이름 키")]
    public string sourceKey;                                              // ★ 신규

    [Header("반증")]
    [Tooltip("이 정보를 얻으면 아래 flagId들이 '반증됨'(취소선)으로 표시된다")]
    public string[] refutesFlagIds;                                       // ★ 신규
    [Tooltip("반증한 이유를 한 줄로 덧붙일 때 쓰는 키 (선택)")]
    public string footnoteKey;                                            // ★ 신규

    [Header("옵션")]
    [Tooltip("체크 해제 시 이 기억은 지울 수 없음(충격적 사건 등)")]
    public bool isEraseable = true;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(flagId))
            Debug.LogWarning($"[MemoryFragmentData] '{name}' 애셋에 flagId가 비어있음 — ink에서 조회 불가", this);

        // ★ 기존 애셋 편의: relatedNpcs가 비어 있으면 category 값을 그대로 옮겨준다
        if ((relatedNpcs == null || relatedNpcs.Length == 0) && !string.IsNullOrEmpty(category))
            relatedNpcs = new[] { category };
    }
#endif
}