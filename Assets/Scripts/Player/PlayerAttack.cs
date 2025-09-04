using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;
using System.Collections;

public class PlayerAttack : MonoBehaviour
{
    private Animator _animator;
    private CapsuleCollider2D _playerHitbox;
    private Transform _player;
    private SpriteRenderer _spriteRenderer;

    //공격 콜라이더
    [SerializeField] private BoxCollider2D attackCollider;
    [SerializeField] private Vector2 meleeFirstSize;
    [SerializeField] private Vector2 meleeFirstOffset;
    [SerializeField] private Vector2 meleeSecondSize;
    [SerializeField] private Vector2 meleeSecondOffset;

    private Queue<int> _attackQueue = new Queue<int>();  // 입력 버퍼 (공격 번호 저장)
    private int _maxAttackNum = 2; //애니메이션 개수
    public int _currentAttack = 0;  // 현재 실행 중인 공격 단계
    public bool _isAttacking = false;  // 공격 중 여부

    public float _attackDistance = 0.5f; //공격시 앞으로 나가는 정도
    public float _attackDashSpeed = 10f;
    public int _manaPerAttack = 5; //공격 당 소모 마나

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _playerHitbox = GetComponent<CapsuleCollider2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        attackCollider = GetComponent<BoxCollider2D>();
    }
    private void Start()
    {
        _player = transform.parent;
        attackCollider.enabled = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.X) && PlayerState.Instance.currentMana >= _manaPerAttack) // 공격키 
        {
            if (!_isAttacking)  // 현재 공격 중이 아니면 즉시 실행
            {
                StartAttack();
            }
            else  // 공격 중이면 버퍼에 저장
            {
                if (_currentAttack < 2)  // 최대 2번까지 예약 가능
                {
                    _attackQueue.Enqueue(_currentAttack + 1);
                }
            }
        }
    }

    private void StartAttack()
    {
        _isAttacking = true;  // 공격 중 상태 설정
        _currentAttack++;  // 다음 공격 번호 증가 1
        _animator.Play("CloseAttack" + _currentAttack, -1, 0f);

        StartCoroutine(SmoothMoveTowards(_attackDistance, _attackDashSpeed)); //뒤로 살짝 밀림
        PlayerState.Instance.UseMana(_manaPerAttack);
    }

    IEnumerator SmoothMoveTowards(float distance, float speed)
    {
        Vector3 targetPos = _player.position + new Vector3(distance * (_spriteRenderer.flipX ? -1 : 1), 0, 0);

        while (_player.position != targetPos)
        {
            _player.position = Vector3.MoveTowards(_player.position, targetPos, speed * Time.deltaTime);
            yield return null;
        }
    }

    public void OnAttackCombo()
    {
        if (_attackQueue.Count > 0 && _currentAttack < _maxAttackNum)  // 예약된 공격이 있다면 실행
        {
            _currentAttack = _attackQueue.Dequeue();
            _animator.Play("CloseAttack" + _currentAttack, -1, 0f);

            StartCoroutine(SmoothMoveTowards(_attackDistance, _attackDashSpeed)); //뒤로 살짝 밀림
            PlayerState.Instance.UseMana(_manaPerAttack);
        }
    }

    // 공격 애니메이션이 끝날 때 호출 (Animation Event)
    public void OnAttackEnd()
    {   
        _attackQueue.Clear();
        _currentAttack = 0;
        _isAttacking = false;
    }

    //공격 범위 콜라이더 조정
    public void EnableAttackCollider()
    {
        switch(_currentAttack)
        {
            case 1:
                attackCollider.size = meleeFirstSize;
                attackCollider.offset = new Vector2(meleeFirstOffset.x * (_spriteRenderer.flipX ? -1 : 1), meleeFirstOffset.y);
                break;
            case 2:
                attackCollider.size = meleeSecondSize;
                attackCollider.offset = new Vector2(meleeSecondOffset.x * (_spriteRenderer.flipX ? -1 : 1), meleeSecondOffset.y);
                break;
        }

        attackCollider.enabled = true;
        StopCoroutine("DisableColliderAfterTime"); // 기존 코루틴이 실행 중이라면 중단 
        StartCoroutine(DisableColliderAfterTime(0.15f));
    }

    private IEnumerator DisableColliderAfterTime(float time)
    {
        yield return new WaitForSeconds(time);
        attackCollider.enabled = false;
    }
}
