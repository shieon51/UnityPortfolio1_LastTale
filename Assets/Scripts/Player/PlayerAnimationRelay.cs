using UnityEngine;

// Visual 객체의 애니메이션 이벤트를 부모(PlayerCombat)로 토스해주는 역할

// ⚠ 이 컴포넌트는 이제 Visual이 아니라 Body 자식 오브젝트에 부착합니다.
// Animation Event는 클립을 재생 중인 Animator와 '같은 GameObject'의 컴포넌트만 호출 가능하기 때문입니다.
public class PlayerAnimationRelay : MonoBehaviour
{
    private PlayerCombat _combat;
    private PlayerVisual _visual;

    private void Awake()
    {
        _combat = GetComponentInParent<PlayerCombat>();
        _visual = GetComponentInParent<PlayerVisual>(); // 임시
    }

    // 전투 관련
    public void EnableAttackCollider() => _combat.EnableAttackCollider();
    public void OnAttackCombo() => _combat.OnAttackCombo();
    public void OnAttackEnd() => _combat.OnAttackEnd();

    // 이동/점프 관련
    public void OnJumpApex() => _visual.OnJumpApex();
    public void OnGroundEnd() => _visual.OnGroundEnd();

    // 이펙트 관련
    public void PlaySkillVFX(string cueId) => _combat.PlayCurrentSkillVFX(cueId);
}