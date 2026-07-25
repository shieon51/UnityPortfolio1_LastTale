
// 모든 시즌 캐릭터의 베이스 컨트롤러가 반드시 가져야 할 State 이름 계약.
// 새 캐릭터 애니메이터 만들 때 이 이름들 그대로 State를 만들면 PlayerVisual 로직이 그대로 재사용됨.
public static class PlayerAnimStateNames
{
    public const string Movement = "Movement";
    public const string JumpUp = "Player_JumpUp";
    public const string JumpTree = "JumpTree";
    public const string Ground = "Player_Ground";
    public const string Transform = "Transform";
    public const string Hit = "Hit";
}