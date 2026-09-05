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
    public string displayName;
    [TextArea(2, 5)] public string recordText;

    [Header("옵션")]
    [Tooltip("체크 해제 시 이 기억은 지울 수 없음(충격적 사건 등)")]
    public bool isEraseable = true;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(flagId))
            Debug.LogWarning($"[MemoryFragmentData] '{name}' 애셋에 flagId가 비어있음 — ink에서 조회 불가", this);
    }
#endif
}