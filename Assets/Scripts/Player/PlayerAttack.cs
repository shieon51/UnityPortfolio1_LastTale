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

    [Header("Combat Feel (타격감 조절)")]
    public float dashSpeed = 10f;       // 공격 시 앞으로 치고 나가는 속도
    public float dashDuration = 0.1f;   // 치고 나가는(움찔) 시간 
    public float hitStopDuration = 0.08f; // 적을 베었을 때 화면이 멈칫하는 시간
    public int _manaPerAttack = 1; //공격 당 소모 마나

    private Queue<int> _attackQueue = new Queue<int>();  // 입력 버퍼 (공격 번호 저장)
    private int _maxAttackNum = 2; //애니메이션 개수
    public int _currentAttack = 0;  // 현재 실행 중인 공격 단계
    public bool _isAttacking = false;  // 공격 중 여부

    // 디버그용 (빨간 박스 그리기)
    private bool _showHitbox = false;
    private Vector2 _lastHitboxCenter;
    private Vector2 _lastHitboxSize;

    // 타격감 조절(체공+역경직 관련)
    private float _originalGravity;
    private Coroutine _hitStopCoroutine; // 역경직 코루틴 관리용

    // 💡 [안전장치 추가] 애니메이션 증발 버그를 막기 위한 타이머 변수
    //private float _attackStartTime;
    //private float _maxAttackDuration = 1.0f; // 1초 뒤 무조건 강제 초기화

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
    }
    private void Start()
    {
        _player = transform.parent;
        _rb = _player.GetComponent<Rigidbody2D>(); // 부모의 Rigidbody 가져오기

        // 💡 게임 시작 시 캐릭터의 기본 중력값을 기억해 둡니다.
        if (_rb != null) _originalGravity = _rb.gravityScale;
    }

    private void Update()
    {
        // 💡 [안전장치(Failsafe)] 공격 중인데 1초가 넘도록 OnAttackEnd가 안 불렸다면? -> 애니가 끊긴 버그!
        //if (_isAttacking && Time.time > _attackStartTime + _maxAttackDuration)
        //{
        //    Debug.LogWarning("[PlayerAttack] 애니메이션이 끊겨 강제로 공격 상태를 초기화합니다!");
        //    CancelAttack();
        //}


        // 일반 공격 입력 (예: X키)
        if (Input.GetKeyDown(KeyCode.X) && PlayerManager.Instance.CurrentCharacter.currentMana >= _manaPerAttack) // 공격키 
        {
            if (!_isAttacking)  // 현재 공격 중이 아니면 즉시 실행
            {
                StartAttack();
            }
            else  // 공격 중이면 버퍼에 저장
            {
                if (_currentAttack < _maxAttackNum)  // 최대 2번까지 예약 가능
                {
                    _attackQueue.Enqueue(_currentAttack + 1);
                }
            }
        }

        // 💡 [참고용] 추후 궁극기 추가 시 예시
        // if (Input.GetKeyDown(KeyCode.V) && GetComponentInParent<PlayerController>()._isGrounded)
        // { ... StartUltimate(); ... }

    }

    private void StartAttack()
    {
        _isAttacking = true;  // 공격 중 상태 설정
        //_attackStartTime = Time.time; // 💡 시작 시간 기록

        // 💡 공격 시작! 슈퍼아머 장착 (넉백 무시)
        if (PlayerManager.Instance.CurrentCharacter != null)
            PlayerManager.Instance.CurrentCharacter.isSuperArmor = true;

        // 💡 [동시 입력 버그 방지] 공격을 시작하면, 혹시 예약되어 있던 점프 명령을 강제로 지워버림!
        _animator.ResetTrigger("Jump");

        _currentAttack++;  // 다음 공격 번호 증가 1
        _animator.Play("CloseAttack" + _currentAttack, -1, 0f);

        // 💡 코루틴 대신 함수 호출
        ApplyAttackDash();
        
    }

    public void OnAttackCombo()
    {
        if (_attackQueue.Count > 0 && _currentAttack < _maxAttackNum)  // 예약된 공격이 있다면 실행
        {
            //_attackStartTime = Time.time; // 💡 콤보 시작 시간 갱신
            _currentAttack = _attackQueue.Dequeue();
            _animator.Play("CloseAttack" + _currentAttack, -1, 0f);

            ApplyAttackDash(); 
        }
    }

    // 공격 애니메이션이 끝날 때 호출 (Animation Event)
    public void OnAttackEnd()
    {   
        _attackQueue.Clear();
        _currentAttack = 0;
        _isAttacking = false;

        // 💡 공격 종료! 슈퍼아머 해제
        if (PlayerManager.Instance.CurrentCharacter != null)
            PlayerManager.Instance.CurrentCharacter.isSuperArmor = false;

        // 💡 공격이 끝날 때 X축 밀림 방지. (중력은 이미 코루틴에서 복구됨)
        if (_rb != null)
        {
            _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y);
            _rb.gravityScale = _originalGravity;
        }
    }

    // 💡 [버그 해결] Transform 강제 조작 대신 Rigidbody에 짧은 힘을 주어 굳는 현상 방지
    private void ApplyAttackDash()
    {
        if (_rb != null)
        {
            float dir = _spriteRenderer.flipX ? 1f : -1f;
            StartCoroutine(JuicyDashRoutine(dir)); // 💡 부드러운 대시 코루틴으로 교체
        }
    }

    // 💡 [수정됨] 촥 나갔다가 원래 관성(X, Y 모두)을 되돌려주는 대시 코루틴
    private IEnumerator JuicyDashRoutine(float dir)
    {
        float elapsed = 0f;

        // 1. 공격 직전의 원래 속도(X축 이동 관성, Y축 점프/낙하 관성) 기억!
        Vector2 savedVelocity = _rb.linearVelocity;

        // 2. 0.1초 동안만 중력을 끄고 허공에 붙잡음 (체공)
        _rb.gravityScale = 0f;

        while (elapsed < dashDuration)
        {
            if (PlayerManager.Instance.CurrentCharacter != null &&
                !PlayerManager.Instance.CurrentCharacter.isSuperArmor &&
                PlayerManager.Instance.CurrentCharacter.isKnockedBack)
            {
                _rb.gravityScale = _originalGravity; // 넉백 맞으면 바로 복구
                yield break;
            }

            // 앞으로 전진 (Y축은 0으로 고정하여 공중에서 잠시 멈춤)
            float currentSpeed = Mathf.Lerp(dashSpeed, 0f, elapsed / dashDuration);
            //_rb.linearVelocity = new Vector2(dir * currentSpeed, _rb.linearVelocity.y);
            _rb.linearVelocity = new Vector2(dir * currentSpeed, 0f); //?

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 3. 💡 [핵심] 0.1초가 끝나면 즉시 중력을 복구하고, 아까 기억해둔 '원래 속도'를 그대로 돌려줌!
        _rb.gravityScale = _originalGravity;
        _rb.linearVelocity = savedVelocity;
        //_rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
    }

    //공격 범위 콜라이더 조정
    public void EnableAttackCollider()
    {
        // 💡 [정석] 진짜 타격이 발생하는 이 순간에 마나를 소모합니다!
        PlayerManager.Instance.CurrentCharacter.UseMana(_manaPerAttack);

        // 💡 단발성 판정 대신, 0.15초 동안 궤적을 긁는 코루틴 실행!
        StartCoroutine(ActiveHitboxRoutine());
    }

    // 💡 0.15초 동안 매 프레임 박스를 그리며 이동 궤적의 모든 적을 벱니다!
    private IEnumerator ActiveHitboxRoutine()
    {
        float activeDuration = 0.15f; // 공격 판정이 살아있는 시간 (필요 시 조절)
        float elapsed = 0f;

        // 다단히트(한 번 휘두를 때 여러 번 맞는 것) 방지용 리스트
        HashSet<Collider2D> alreadyHitEnemies = new HashSet<Collider2D>();

        Vector2 size = _currentAttack == 1 ? meleeFirstSize : meleeSecondSize;
        Vector2 offset = _currentAttack == 1 ? meleeFirstOffset : meleeSecondOffset;

        _lastHitboxSize = size; // 기즈모용 사이즈 고정

        while (elapsed < activeDuration)
        {
            // 매 프레임마다 플레이어의 현재 위치를 기반으로 박스 중심점 갱신 (이동 궤적 추적!)
            Vector2 currentOffset = offset;
            currentOffset.x *= _spriteRenderer.flipX ? -1 : 1;
            Vector2 center = (Vector2)_player.position + currentOffset;

            _lastHitboxCenter = center; // 기즈모 중심점 갱신
            _showHitbox = true; // 기즈모 켜기

            // 해당 프레임에 박스에 닿은 적 모두 추출
            Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f, LayerMask.GetMask("Enemy"));

            bool hitSomethingThisFrame = false; // 💡 이번 프레임에 타격이 있었는지 체크

            foreach (var hit in hits)
            {
                // 💡 이번 공격(휘두르기)에서 이미 때린 적은 무시! (다단히트 방지)
                if (!alreadyHitEnemies.Contains(hit))
                {
                    alreadyHitEnemies.Add(hit); // 때린 목록에 추가

                    CharacterStats enemyStats = hit.GetComponent<CharacterStats>();
                    if (enemyStats != null)
                    {
                        int damage = PlayerManager.Instance.CurrentCharacter.attack.GetValue();
                        ElementType element = PlayerManager.Instance.CurrentCharacter.currentElement;

                        enemyStats.TakeDamage(damage, element);

                        Vector2 knockbackDir = (hit.transform.position - _player.position).normalized;
                        enemyStats.ApplyKnockback(knockbackDir, 5f);

                        hitSomethingThisFrame = true; // 적을 썰었다!
                    }
                }
            }

            // 💡 [역경직 발동] 적을 썰어버린 프레임에 애니메이션을 잠깐 멈춤
            if (hitSomethingThisFrame)
            {
                if (_hitStopCoroutine != null) StopCoroutine(_hitStopCoroutine);
                _hitStopCoroutine = StartCoroutine(HitStopRoutine(hitStopDuration));
            }

            elapsed += Time.deltaTime;
            yield return null; // 다음 프레임으로 넘어가서 또 긁음
        }

        _showHitbox = false; // 판정 시간 끝나면 기즈모 끄기
    }

    // 💡 [역경직(Hit Stop) 코루틴] 화면이 멈칫하며 타격감 극대화
    private IEnumerator HitStopRoutine(float duration)
    {
        _animator.speed = 0f; // 애니메이션 일시정지
        yield return new WaitForSeconds(duration);
        _animator.speed = 1f; // 정상 속도 복구
    }

    // 💡 피격 코루틴 강제 정지를 위한 취소 함수 (PlayerStats 등에서 넉백/사망 시 호출 가능)
    //  -> StateMachineBehaviour나 피격 시 강제로 캔슬할 때 호출됨
    public void CancelAttack()
    {
        _isAttacking = false;
        _currentAttack = 0;
        _attackQueue.Clear();

        if (PlayerManager.Instance.CurrentCharacter != null)
            PlayerManager.Instance.CurrentCharacter.isSuperArmor = false;

        StopAllCoroutines();

        // 💡 [중력/속도 복구] 취소 시 멈췄던 애니메이션과 중력을 모두 돌려놓음
        _animator.speed = 1f;
        if (_rb != null)
        {
            _rb.gravityScale = _originalGravity;
            //_rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y); // 캔슬 시에는 강제로 속도를 되돌리지 않음 (넉백 등에 의해 날아가는 중일 수 있으므로)
        }
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
