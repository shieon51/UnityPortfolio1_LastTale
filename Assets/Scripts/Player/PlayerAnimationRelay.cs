using UnityEngine;

// Visual 객체의 애니메이션 이벤트를 부모(PlayerCombat)로 토스해주는 역할
public class PlayerAnimationRelay : MonoBehaviour
{
    private PlayerCombat _combat;
    private PlayerVisual _visual;

    private void Awake()
    {
        _combat = GetComponentInParent<PlayerCombat>();
        _visual = GetComponent<PlayerVisual>(); // 임시
    }

    // 전투 관련
    public void EnableAttackCollider() => _combat.EnableAttackCollider();
    public void OnAttackCombo() => _combat.OnAttackCombo();
    public void OnAttackEnd() => _combat.OnAttackEnd();

    // 이동/점프 관련
    public void OnJumpApex() => _visual.OnJumpApex();
    public void OnGroundEnd() => _visual.OnGroundEnd();
}