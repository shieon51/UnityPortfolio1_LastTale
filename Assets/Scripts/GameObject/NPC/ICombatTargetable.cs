
// npc 공격 가능 여부 판단
public interface ICombatTargetable
{
    bool IsValidCombatTarget(CharacterStats attacker);
}