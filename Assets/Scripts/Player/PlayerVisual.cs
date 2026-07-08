using UnityEngine;

[RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
public class PlayerVisual : MonoBehaviour
{
    private Animator _animator;
    private SpriteRenderer _spriteRenderer;
    private PlayerController _controller;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();

        // 부모 객체에 있는 Controller를 가져옴
        _controller = GetComponentInParent<PlayerController>();
    }

    private void Update()
    {
        if (_controller == null) return;

        UpdateAnimations();
        UpdateSpriteDirection();
    }

    private void UpdateAnimations()
    {
        // Controller에 열려있는 public 속성(프로퍼티)들을 가져와서 애니메이터에 주입
        _animator.SetFloat("Speed", _controller.CurrentSpeed);
        _animator.SetBool("IsDash", _controller.IsDashing && _controller.CurrentSpeed > 0);
        _animator.SetBool("IsGrounded", _controller.IsGrounded);
        //_animator.SetBool("IsAscending", _controller.IsAscending);

        // 참고: 떨어지는 모션 등은 VelocityY를 직접 넘겨서 애니메이터 BlendTree로 제어하는 게 훨씬 깔끔함
        _animator.SetFloat("VelocityY", _controller.VelocityY);
    }

    private void UpdateSpriteDirection()
    {
        // 입력값에 따른 스프라이트 좌우 반전
        float inputX = Input.GetAxisRaw("Horizontal");
        if (inputX != 0)
        {
            _spriteRenderer.flipX = inputX > 0;
        }
    }
}