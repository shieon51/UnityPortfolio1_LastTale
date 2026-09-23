// 기억 조각·주제가 공통으로 쓰는 열거형 모음

// 기록장에서 어느 카드에 들어가는지
public enum MemoryCardCategory
{
    BasicInfo,   // 기본 정보
    Traits,      // 특이사항
    Past,        // 과거사
    Relations,   // 인물관계
    Family,      // 가족
    Combat,      // 전투
    World,       // 세계관·기타
}

// 소라가 이 정보를 얼마나 확신하는지 (기록장 글자색)
public enum MemoryCertainty
{
    Confirmed,   // 검은 글씨
    Uncertain,   // 회색 글씨 (소문·전언·추측)
}

// 이 정보를 어디서 얻었는지 (출처 칩)
public enum MemorySourceType
{
    None,        // 칩 없음
    Self,        // 본인 발언
    Hearsay,     // ○○ 왈
    Evidence,    // 일기장·책 등 증거
    Witnessed,   // 직접 목격
}