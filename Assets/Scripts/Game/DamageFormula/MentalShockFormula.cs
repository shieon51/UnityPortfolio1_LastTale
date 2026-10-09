using UnityEngine;

// ★ 정신력 충격 — 몸이 되돌아가는 회귀(경로 2, Day 1)에서 깎이는 정신력 (기획서 7-2).
//   기억을 안은 채 몸을 다시 맞추는 대가이므로, 몸이 되돌아간 레벨 폭이 0이어도 기본값만큼은 깎인다.
//   정신력 하락 = 기본 하락 + 레벨당 하락 × (회귀 전 몸 레벨 − 회귀 후 몸 레벨), 상한 적용
[CreateAssetMenu(menuName = "LastMarchan/Loop/Formulas/Mental Shock")]
public class MentalShockFormula : ScriptableObject
{
    [Tooltip("몸이 되돌아가기만 해도 깎이는 정신력 (레벨 폭 0일 때의 값)")]
    public int baseLoss = 10;
    [Tooltip("몸 레벨이 1 되돌아갈 때마다 더 깎이는 정신력")]
    public int lossPerLevel = 2;
    [Tooltip("한 번의 회귀로 깎이는 정신력의 상한. 0 이하면 상한 없음")]
    public int maxLoss = 50;

    public int Calculate(int levelBefore, int levelAfter)
    {
        int gap = Mathf.Max(0, levelBefore - levelAfter);   // 몸 레벨이 오히려 높아지는 경우는 폭 0으로 본다
        int loss = baseLoss + lossPerLevel * gap;
        if (maxLoss > 0) loss = Mathf.Min(loss, maxLoss);
        return Mathf.Max(0, loss);
    }
}
