using UnityEngine;

// DirectionalPart.cs (신규) — 애니메이션이 있는 파츠용 (몸, 팔, 날개 등): 애니메이션 있고 방향별 전용 그림 필요
public class DirectionalPart : MonoBehaviour
{
    public RuntimeAnimatorController leftController;
    public RuntimeAnimatorController rightController;

    private Animator _animator;
    private SpriteRenderer _sr;
    private bool _lastFacingRight;
    private bool _initialized = false;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _sr = GetComponent<SpriteRenderer>();
    }

    public void ApplyFacing(bool facingRight)
    {
        if (_initialized && facingRight == _lastFacingRight) return;
        _initialized = true;
        _lastFacingRight = facingRight;

        if (_animator != null)
            _animator.runtimeAnimatorController = facingRight ? rightController : leftController;

        if (_sr != null) _sr.flipX = false; // ★ 전용 아트라서 반전 자체를 절대 안 씀
    }
}