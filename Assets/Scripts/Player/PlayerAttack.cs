using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;
using System.Collections;

public class PlayerAttack : MonoBehaviour
{
    private Animator _animator;
    private Transform _player;
    private SpriteRenderer _spriteRenderer;
    private Rigidbody2D _rb; // 💡 Rigidbody 참조 추가

    // 이제 콜라이더 컴포넌트가 필요 없습니다! 크기와 위치 데이터만 씁니다.
    [Header("Hitbox Settings")]
    [SerializeField] private Vector2 meleeFirstSize = new Vector2(2.6f, 1.6f);
    [SerializeField] private Vector2 meleeFirstOffset = new Vector2(-0.4f, 1.1f);
    [SerializeField] private Vector2 meleeSecondSize = new Vector2(3.8f, 1.8f);
    [SerializeField] private Vector2 meleeSecondOffset = new Vector2(-0.3f, 1.3f);

    private Queue<int> _attackQueue = new Queue<int>();  // 입력 버퍼 (공격 번호 저장)
    private int _maxAttackNum = 2; //애니메이션 개수
    public int _currentAttack = 0;  // 현재 실행 중인 공격 단계
    public bool _isAttacking = false;  // 공격 중 여부

    public float _attackDistance = 0.5f; //공격시 앞으로 나가는 정도
    public float _attackDashSpeed = 10f;
    public int _manaPerAttack = 5; //공격 당 소모 마나

    // 디버그용 (빨간 박스 그리기)
    private bool _showHitbox = false;
    private Vector2 _lastHitboxCenter;
    private Vector2 _lastHitboxSize;

    // 💡 [안전장치 추가] 애니메이션 증발 버그를 막기 위한 타이머 변수
    private float _attackStartTime;
    private float _maxAttackDuration = 1.0f; // 1초 뒤 무조건 강제 초기화

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }
    private void Start()
    {
        _player = transform.parent;
        _rb = _player.GetComponent<Rigidbody2D>(); // 부모의 Rigidbody 가져오기
    }

    private void Update()
    {
        // 💡 [안전장치(Failsafe)] 공격 중인데 1초가 넘도록 OnAttackEnd가 안 불렸다면? -> 애니가 끊긴 버그!
        if (_isAttacking && Time.time > _attackStartTime + _maxAttackDuration)
        {
            Debug.LogWarning("[PlayerAttack] 애니메이션이 끊겨 강제로 공격 상태를 초기화합니다!");
            CancelAttack();
        }

        if (Input.GetKeyDown(KeyCode.X) && PlayerManager.Instance.CurrentCharacter.currentMana >= _manaPerAttack) // 공격키 
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
        _attackStartTime = Time.time; // 💡 시작 시간 기록

        // 💡 공격 시작! 슈퍼아머 장착 (넉백 무시)
        PlayerManager.Instance.CurrentCharacter.isSuperArmor = true;

        // 💡 [동시 입력 버그 방지] 공격을 시작하면, 혹시 예약되어 있던 점프 명령을 강제로 지워버림!
        _animator.ResetTrigger("Jump");

        _currentAttack++;  // 다음 공격 번호 증가 1
        _animator.Play("CloseAttack" + _currentAttack, -1, 0f);

        //StartCoroutine(SmoothMoveTowards(_attackDistance, _attackDashSpeed)); //뒤로 살짝 밀림
        // 💡 코루틴 대신 함수 호출
        ApplyAttackDash();
        PlayerManager.Instance.CurrentCharacter.UseMana(_manaPerAttack);
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
            _attackStartTime = Time.time; // 💡 콤보 시작 시간 갱신

            _currentAttack = _attackQueue.Dequeue();
            _animator.Play("CloseAttack" + _currentAttack, -1, 0f);

            ApplyAttackDash(); // 뒤로 살짝 밀림
            PlayerManager.Instance.CurrentCharacter.UseMana(_manaPerAttack);
        }
    }

    // 공격 애니메이션이 끝날 때 호출 (Animation Event)
    public void OnAttackEnd()
    {   
        _attackQueue.Clear();
        _currentAttack = 0;
        _isAttacking = false;

        // 💡 공격 종료! 슈퍼아머 해제
        PlayerManager.Instance.CurrentCharacter.isSuperArmor = false;

        // 공격 끝날 때 X축 멈춤 (밀림 방지)
        if (_rb != null) _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y);
    }

    // 💡 [버그 해결] Transform 강제 조작 대신 Rigidbody에 짧은 힘을 주어 굳는 현상 방지
    private void ApplyAttackDash()
    {
        if (_rb != null)
        {
            float dir = _spriteRenderer.flipX ? 1f : -1f;
            // X축 속도만 강제로 설정하여 짧게 전진시킴 (Y축 중력 유지)
            _rb.linearVelocity = new Vector2(dir * _attackDashSpeed, _rb.linearVelocity.y);
            //StartCoroutine(SnappyDashRoutine(dir));
        }
    }

    // 💡 [신규] 촥! 가고 뚝! 멈추는 탄력 대시 코루틴
    private IEnumerator SnappyDashRoutine(float dir)
    {
        // 1. 촥! 전진 (매우 짧은 시간 동안 강한 속도)
        float dashDuration = 0.1f; // 대시 지속 시간 (0.1초)
        float elapsed = 0f;

        while (elapsed < dashDuration)
        {
            // 공격 중 피격당해 넉백 상태가 되면 대시 즉시 취소
            if (!PlayerManager.Instance.CurrentCharacter.isSuperArmor && PlayerManager.Instance.CurrentCharacter.isKnockedBack)
                yield break;

            _rb.linearVelocity = new Vector2(dir * _attackDashSpeed, _rb.linearVelocity.y);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 2. 뚝! 멈춤 (관성 제거)
        _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
    }

    //공격 범위 콜라이더 조정
    public void EnableAttackCollider()
    {
        // 1. 공격 단계에 따른 박스 크기와 위치 계산
        Vector2 size = _currentAttack == 1 ? meleeFirstSize : meleeSecondSize;
        Vector2 offset = _currentAttack == 1 ? meleeFirstOffset : meleeSecondOffset;
        offset.x *= _spriteRenderer.flipX ? -1 : 1; // 방향 전환

        Vector2 center = (Vector2)_player.position + offset;

        // 디버깅 기즈모용 저장
        _lastHitboxCenter = center;
        _lastHitboxSize = size;
        StartCoroutine(ShowHitboxGizmo());

        // 2. 💡 [문제 완벽 해결] 물리 충돌 매트릭스 꼬임 원천 차단!
        // 오직 "Enemy" 레이어만 감지하는 가상의 박스를 그려서 닿은 놈들을 가져옴
        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f, LayerMask.GetMask("Enemy"));

        foreach (var hit in hits)
        {
            CharacterStats enemyStats = hit.GetComponent<CharacterStats>();
            if (enemyStats != null)
            {
                int damage = PlayerManager.Instance.CurrentCharacter.attack.GetValue();
                ElementType element = PlayerManager.Instance.CurrentCharacter.currentElement;

                enemyStats.TakeDamage(damage, element);

                // 적 넉백
                Vector2 knockbackDir = (hit.transform.position - _player.position).normalized;
                enemyStats.ApplyKnockback(knockbackDir, 5f);

                //Debug.Log($"[PlayerAttack] 적을 벴습니다! 데미지: {damage}");
            }
        }
    }

    // 💡 피격 코루틴 강제 정지를 위한 취소 함수 (PlayerStats 등에서 넉백/사망 시 호출 가능)
    public void CancelAttack()
    {
        _isAttacking = false;
        _currentAttack = 0;
        _attackQueue.Clear();
        PlayerManager.Instance.CurrentCharacter.isSuperArmor = false;

        
        StopAllCoroutines(); //?
    }

    private IEnumerator ShowHitboxGizmo()
    {
        _showHitbox = true;
        yield return new WaitForSeconds(0.15f);
        _showHitbox = false;
    }

#if UNITY_EDITOR
    // 💡 OnDrawGizmosSelected: 하이어라키에서 이 오브젝트를 클릭했을 때만 씬 창에 그려집니다!
    private void OnDrawGizmosSelected()
    {
        // 게임 실행 전(Edit Mode)에는 _player 변수가 세팅되지 않았으므로 부모 위치를 직접 찾습니다.
        Vector3 basePosition = transform.parent != null ? transform.parent.position : transform.position;

        // 현재 에디터 상에서 캐릭터가 왼쪽을 보고 있는지(flipX) 확인
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        float dir = (sr != null && sr.flipX) ? -1f : 1f;

        // 1타 공격 범위 미리보기 (청록색 박스)
        Gizmos.color = Color.cyan;
        Vector2 center1 = (Vector2)basePosition + new Vector2(meleeFirstOffset.x * dir, meleeFirstOffset.y);
        Gizmos.DrawWireCube(center1, meleeFirstSize);

        // 2타 공격 범위 미리보기 (빨간색 박스)
        Gizmos.color = Color.red;
        Vector2 center2 = (Vector2)basePosition + new Vector2(meleeSecondOffset.x * dir, meleeSecondOffset.y);
        Gizmos.DrawWireCube(center2, meleeSecondSize);
    }

    // 💡 기존에 있던 OnDrawGizmos (게임 실행 중에 진짜 타격 순간에만 켜지는 빨간 박스)
    private void OnDrawGizmos()
    {
        if (_showHitbox)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
            Gizmos.DrawCube(_lastHitboxCenter, _lastHitboxSize);
        }
    }
#endif
}
