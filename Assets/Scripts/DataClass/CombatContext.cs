// 모든 전투 계산의 표준 입력
public struct CombatContext
{
    public int IncomingDamage;
    public CharacterStats Attacker; // 지금은 대부분 null (아래 설명 참고)
    public CharacterStats Defender;
    public ElementType AttackElement;
    public SkillBase Skill;
}