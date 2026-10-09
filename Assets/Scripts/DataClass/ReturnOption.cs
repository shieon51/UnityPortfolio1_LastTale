// ★ 기록 시스템 3-B — 닻 선택 화면의 선택지 하나 (기획서 7-2 "돌아갈 지점 선택").
//   닻 선택 화면은 이것만 보고 대가를 그린다. 값은 고르기 전에 미리 계산한 예상치이며,
//   실제 적용(TimeLoopManager.ApplyReturn)도 같은 계산 함수를 써서 화면과 결과가 어긋나지 않는다
public class ReturnOption
{
    public TimeLoopManager.ReturnPath path;   // Normal(경로 1) / Forced(경로 2) / Day1(경로 3)
    public TimeAnchorSnapshot anchor;         // 돌아갈 닻. Day 1이면 null

    public int manaCost;                      // 돌아가는 데 쓰는 마나 (경로 1만)
    public bool keepsBody;                    // 몸(레벨·공방민)을 유지하는가 (경로 1만)
    public int levelBefore, levelAfter;       // 몸 레벨 전후
    public int healthAfter, maxHealthAfter;   // 돌아간 직후 체력 (예상)
    public int manaAfter, maxManaAfter;       // 돌아간 직후 마나 (예상)
    public int mentalLoss;                    // 정신력 하락
    public int anchorsLost;                   // 이 선택으로 사라지는 다른 닻 수

    public bool IsDay1 => anchor == null;
}
