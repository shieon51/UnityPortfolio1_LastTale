using UnityEngine;

// Visual 객체의 애니메이션 이벤트를 부모(PlayerCombat)로 토스해주는 역할
public class PlayerAnimationRelay : MonoBehaviour
{
    private PlayerCombat _combat;

    private void Awake()
    {
        _combat = GetComponentInParent<PlayerCombat>();
    }

    public void EnableAttackCollider() => _combat.EnableAttackCollider();
    public void OnAttackCombo() => _combat.OnAttackCombo();
    public void OnAttackEnd() => _combat.OnAttackEnd();
}