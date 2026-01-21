using System.Collections;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

public class PlayerController : MonoBehaviour
{
    public float TIRED_RUN_SPEED = 1f; //피곤한 상태일 시 
    public float DEFALT_RUN_SPEED = 3f;
    public float DEFALT_DASH_SPEED = 6f;

    public float CurRunSpeed; //걷기
    public float CurDashSpeed; //뛰기

    public float JumpForce = 10f; //점프 파워
    //public float FallJumpMultiplier = 2.5f;
    //public float LowJumpMultiplier = 2.0f;
    public float GroundCheckDistance = 0.2f;  // 낭떠러지 감지 거리
    public float GroundCheckDistanceWidth = 5.0f;  // 낭떠러지 감지 거리(너비)
    public Vector3 _groundCheckLineOffset = new Vector3(0, -1, 0);

    private float _horizontalInput;
    private Animator _animator;
    private Rigidbody2D _rigidbody;
    private SpriteRenderer _spriteRenderer;
    private CircleCollider2D _playerGroundCollider;
    private PlayerAttack _playerAttack;
    private CapsuleCollider2D _playerHitbox;

    private bool _isDash;
    private bool _isGrounded;  //땅에 닿아있는 상태인지
    private bool _isAscending = false; //최고점 도달했는지
    private bool _goToUnder = false; //아래 지형 이동키 눌렀을 시

    private GameObject _groundPrefab;
    

    private void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
        _rigidbody = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        _playerGroundCollider = GetComponent<CircleCollider2D>();
        _playerAttack = GetComponentInChildren<PlayerAttack>();
        _playerHitbox = GetComponentInChildren<CapsuleCollider2D>();

        CurRunSpeed = DEFALT_RUN_SPEED;
        CurDashSpeed = DEFALT_DASH_SPEED;

        // 충돌 감지 방식을 Continuous로 설정
        _rigidbody.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    private void Start()
    {
        PlayerState.Instance.OnFatigueChanged += HandleFatigueChange;
    }

    private void Update()
    {
        Move();
        Jump();
        GoUnderGround();
        Air();
    }

    //피로도 일정 수준 이상 (상태)
    private void HandleFatigueChange(int currentFatigue)
    {
        if (currentFatigue >= 30)
        {
            CurRunSpeed = TIRED_RUN_SPEED;
            CurDashSpeed = TIRED_RUN_SPEED * 2; // 피로도 24 이상이면 이동 속도 감소
            _animator.speed = 0.5f;
        } 
        else
        {
            CurRunSpeed = DEFALT_RUN_SPEED;
            CurDashSpeed = DEFALT_DASH_SPEED;
            _animator.speed = 1f;
        }
    }
    //public LayerMask groundLayer;  // Inspector에서 "Ground" 설정
    //bool IsGrounded()
    //{
    //    float rayDistance = 0.2f;
    //    Vector2 left = (Vector2)transform.position + new Vector2(-0.2f, 0);
    //    Vector2 center = transform.position;
    //    Vector2 right = (Vector2)transform.position + new Vector2(0.2f, 0);
    //
    //    return Physics2D.Raycast(left, Vector2.down, rayDistance, groundLayer) ||
    //           Physics2D.Raycast(center, Vector2.down, rayDistance, groundLayer) ||
    //           Physics2D.Raycast(right, Vector2.down, rayDistance, groundLayer);
    //}

    private void Jump()
    {
        //대화중인 경우에는 움직이지 못하도록
        if (DialogueManager.Instance.IsTalking) return;
        //공격 중이라면 점프 못하게
        if (_playerAttack._isAttacking) return; 

        //점프 애니메이션
        if (Input.GetKeyDown(KeyCode.LeftControl) && _isGrounded)
        {
            _animator.SetTrigger("Jump");
            _animator.SetBool("IsAscending", true);
            _animator.SetBool("IsGrounded", false);
            //_rigidbody.linearVelocity = new Vector2(_rigidbody.linearVelocityX, JumpForce);
            _rigidbody.AddForce(Vector2.up * JumpForce);
            _isAscending = true;
            _isGrounded = false;
        }
    }

    private void GoUnderGround()
    {
        //대화중인 경우에는 움직이지 못하도록
        if (DialogueManager.Instance.IsTalking) return;

        //아래 지형으로 내려가기
        if (Input.GetKeyDown(KeyCode.DownArrow) && _isGrounded)
        {
            _goToUnder = true;

            // 잠시 후에 다시 충돌을 활성화
            StartCoroutine(ResetColliderTrigger());
        }
    }

    // 잠시 후에 다시 충돌을 활성화하는 코루틴
    private IEnumerator ResetColliderTrigger()
    {
        // 현재 서 있는 플랫폼 가져오기
        PlatformEffector2D effector = GetCurrentPlatformEffector();
        if (effector != null)
        {
            Physics2D.IgnoreCollision(_playerGroundCollider, effector.GetComponent<Collider2D>(), true);
            yield return new WaitForSeconds(0.5f);
            Physics2D.IgnoreCollision(_playerGroundCollider, effector.GetComponent<Collider2D>(), false);
        }
        _goToUnder = false;
    }

    private void Air()
    {
        //RaycastHit2D hit;
        RaycastHit2D leftHit, rightHit;

        Vector3 pos = transform.position + _groundCheckLineOffset + Vector3.right * GroundCheckDistanceWidth;
        leftHit = Physics2D.Raycast(pos, Vector2.down, GroundCheckDistance, LayerMask.GetMask("Ground"));

        pos = transform.position + _groundCheckLineOffset - Vector3.right * GroundCheckDistanceWidth;
        rightHit = Physics2D.Raycast(pos, Vector2.down, GroundCheckDistance, LayerMask.GetMask("Ground"));

        bool isHit = false;
        if (!_goToUnder) //지형 아래로 내려가는 중이 아닐 때 -> ray 충돌 확인
        {
            isHit |= leftHit.collider != null;
            isHit |= rightHit.collider != null;
        }

        if (!_isGrounded && isHit) //착지한 경우
        {
            _animator.SetBool("IsGrounded", true);
        }
        if (_isGrounded && !isHit) //낭떠러지인 경우
        {
            if (_rigidbody.linearVelocityY <= 0)
                _animator.SetBool("IsAscending", false);
        }
        if (!_isGrounded) //땅에 있지 않을 때
        {
            if (_isAscending && _rigidbody.linearVelocityY < 0)
            {
                _animator.SetBool("IsAscending", false);
                _isAscending = false;
            }
        }

        _isGrounded = isHit;
        _animator.SetBool("IsGrounded", _isGrounded);

    }

    private PlatformEffector2D GetCurrentPlatformEffector()
    {
        // 플레이어 아래 위치한 플랫폼 Effector 찾기
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 1.2f, LayerMask.GetMask("Ground"));
        if (hit.collider != null)
        {
            return hit.collider.GetComponent<PlatformEffector2D>();
        }
        return null;
    }

    private void Move()
    {
        //대화중인 경우에는 움직이지 못하도록
        if (DialogueManager.Instance.IsTalking) return;
        //공격 중이라면 점프 못하게
        if (_playerAttack._isAttacking) return;


        _horizontalInput = Input.GetAxis("Horizontal");
        _isDash = _isGrounded ? Input.GetKey(KeyCode.LeftShift) : _isDash; //(점프 동작 중에는 마지막 대시 상태를 넘기기)

        if (_horizontalInput != 0)
        {
            _spriteRenderer.flipX = _horizontalInput > 0;
        }

        if (_isGrounded)
        {
            //걷기 & 달리기 애니메이션
            _animator.SetFloat("Speed", Mathf.Abs(_horizontalInput));
            _animator.SetBool("IsDash", _isDash && _horizontalInput != 0);
        }

        //이동
        float moveSpeed = _isDash ? CurDashSpeed : CurRunSpeed;
        _rigidbody.linearVelocity = new Vector2(_horizontalInput * moveSpeed, _rigidbody.linearVelocity.y);
    }

    private void OnDrawGizmos()
    {
        if (_isGrounded)
            Gizmos.color = Color.red;
        else
            Gizmos.color = Color.yellow;

        Vector3 pos = transform.position + _groundCheckLineOffset + Vector3.right * GroundCheckDistanceWidth;
        Gizmos.DrawLine(pos, pos + Vector3.down * GroundCheckDistance);
        pos = transform.position + _groundCheckLineOffset - Vector3.right * GroundCheckDistanceWidth;
        Gizmos.DrawLine(pos, pos + Vector3.down * GroundCheckDistance);
    }

    
}
