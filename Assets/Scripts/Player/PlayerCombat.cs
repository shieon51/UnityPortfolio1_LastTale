using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;
using System.Collections;

public class PlayerCombat : MonoBehaviour
{
    private Rigidbody2D _rb;
    private Animator _animator;
    private CharacterStats _stats;
    private SoraStats _soraStats;
    private SpriteRenderer _spriteRenderer;

    public bool IsAttacking { get; private set; }
    private float _originalGravity;

    [Header("Skill Slots (Polymorphic)")]
    // [수정됨] 이제 껍데기뿐인 SkillData 대신 동작이 정의된 SkillBase(또는 자식 클래스)를 받음
    public SkillBase skillQ;
    public SkillBase skillW;
    public SkillBase skillE;
    public SkillBase skillR;

    // 스킬 선입력 버퍼
    private Queue<SkillBase> _attackQueue = new Queue<SkillBase>();
    private SkillBase _currentSkill;

    // 디버그용 (스킬 클래스에서 호출받음)
    private bool _showHitbox = false;
    private Vector2 _lastHitboxCenter;
    private Vector2 _lastHitboxSize;

    // ** 스킬 클래스(MeleeDashSkill 등)가 플레이어가 바라보는 방향을 쉽게 알 수 있도록 열어주는 프로퍼티
    public float FacingDirection => (_spriteRenderer != null && _spriteRenderer.flipX) ? -1f : 1f;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _stats = GetComponent<CharacterStats>();
        _soraStats = GetComponent<SoraStats>();
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        _animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        // 게임 시작 시 캐릭터의 기본 중력값을 기억해둠
        if (_rb != null) _originalGravity = _rb.gravityScale;
    }

    private void Update()
    {
        if (IsAttacking || _stats.isKnockedBack || DialogueManager.Instance.IsTalking) return;

        // 키 입력 -> 큐에 등록 (단, 콤보가 허용되는 구조 내에서만)
        if (Input.GetKeyDown(KeyCode.Q)) HandleSkillInput(skillQ);
        else if (Input.GetKeyDown(KeyCode.W)) HandleSkillInput(skillW);
        else if (Input.GetKeyDown(KeyCode.E)) HandleSkillInput(skillE);
        else if (Input.GetKeyDown(KeyCode.R)) HandleSkillInput(skillR);

        // 공격 중이 아니면 큐에서 꺼내서 실행
        if (!IsAttacking && _attackQueue.Count > 0)
        {
            TryExecuteSkill(_attackQueue.Dequeue());
        }
    }

    private void HandleSkillInput(SkillBase skillInput)
    {
        if (skillInput == null) return;

        if (!IsAttacking)
        {
            BufferSkill(skillInput);
        }
        else if (_currentSkill != null && _currentSkill.nextComboSkill != skillInput) //? 로직 이상함
        {
            // 현재 스킬에 콤보가 정의되어 있다면, 플레이어가 누른 키가 뭐든 일단 다음 콤보 스킬을 큐에 저장  //?
            BufferSkill(_currentSkill.nextComboSkill);
            Debug.Log($"[PlayerCombat] 콤보 스킬 예약됨: {skillInput.skillName}");
        }
    }

    private void BufferSkill(SkillBase skill)
    {
        if (_attackQueue.Count < 2) _attackQueue.Enqueue(skill);
    }

    private void TryExecuteSkill(SkillBase skill)
    {
        // 1. 폼체인지 단계에 따른 마나 사용 효율 계산 (다형성)
        int actualManaCost = _stats.CalculateManaCost(skill.requiredMana);

        // 2. 오버캐스트 연산 로직 (마나 부족 -> 피로도 -> HP 순으로 갉아먹음)
        if (_stats.currentMana < actualManaCost)
        {
            int deficit = actualManaCost - _stats.currentMana;
            _stats.UseMana(_stats.currentMana); // 남은 마나 전부 털기

            Debug.Log($"[오버캐스트] 마나 부족! 피로도와 HP를 갉아먹으며 {skill.skillName} 강제 발동!");

            if (_soraStats != null)
            {
                _soraStats.IncreaseFatigue(deficit * 2);
            }
            _stats.TakeDamage(deficit, ElementType.Normal); // HP 페널티
        }
        else
        {
            _stats.UseMana(actualManaCost); // 정상 마나 소모
        }

        IsAttacking = true;
        _stats.isSuperArmor = true;
        _currentSkill = skill;

        // Any State 무시하고 코드로 애니메이션 강제 재생
        _animator.Play(skill.animStateName, -1, 0f);

        // 각 스킬 클래스에 정의된 고유 동작 실행
        StartCoroutine(skill.ExecuteSkillBehavior(this, _rb, _animator, _stats));
    }

    // --- 애니메이션 이벤트 (PlayerAnimationRelay에서 전달받음) ---
    public void EnableAttackCollider()
    {
        if (_currentSkill != null)
            StartCoroutine(_currentSkill.ExecuteHitbox(this, transform, _stats));
    }

    public void OnAttackCombo()
    {
        // 콤보 입력 허용 타이밍에 큐를 검사해서 다음 스킬이 있으면 바로 실행
        if (_attackQueue.Count > 0)
        {
            SkillBase nextSkill = _attackQueue.Dequeue();
            Debug.Log($"[PlayerCombat] 콤보 이어가기: {nextSkill.skillName}");
            TryExecuteSkill(nextSkill);
        }
    }

    public void OnAttackEnd()
    {
        IsAttacking = false;
        _stats.isSuperArmor = false;
        _currentSkill = null;
        _attackQueue.Clear(); // 콤보 입력 안 했으면 남은 큐 비우기

        // X축 속도 밀림 방지
        if (_rb != null)
        {
            _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y);
            _rb.gravityScale = _originalGravity;
        }

        // 애니메이터 강제 복귀 (공격 섬 노드에서 빠져나오기 위함)
        // 블렌드 트리 파라미터에 맞춰 부드럽게 돌아가도록 0.1초 크로스페이드
        _animator.CrossFade("Movement", 0.1f);
    }

    public void CancelAttack()
    {
        StopAllCoroutines();
        IsAttacking = false;
        _stats.isSuperArmor = false;
        _currentSkill = null;
        _attackQueue.Clear();
        _animator.speed = 1f;
        if (_rb != null) _rb.gravityScale = _originalGravity;

        _animator.Play("Movement", -1, 0f); // 피격/캔슬 시 무브먼트로 강제 복귀
    }

    // --- 헬퍼 함수 (SkillBase 자식 클래스들이 타격감/기즈모를 위해 호출) ---
    public void SetDebugHitbox(Vector2 center, Vector2 size) { _showHitbox = true; _lastHitboxCenter = center; _lastHitboxSize = size; }
    public void ClearDebugHitbox() { _showHitbox = false; }
    public void TriggerHitStop(float duration) { StartCoroutine(HitStopRoutine(duration)); }

    private IEnumerator HitStopRoutine(float duration)
    {
        _animator.speed = 0f;
        yield return new WaitForSecondsRealtime(duration);
        _animator.speed = 1f;
    }

#if UNITY_EDITOR
    // [기즈모 복원 완료!] 인스펙터에서 수정하면 게임 안 켜도 실시간으로 볼 수 있음
    private void OnDrawGizmosSelected()
    {
        // 런타임이 아닐 때도 박스 보이게 하기 위해 위치 수동 계산
        Vector3 basePos = transform.position;
        SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
        float dir = (sr != null && sr.flipX) ? -1f : 1f;

        // 에디터에서 선택된 스킬 데이터를 미리 보여줌
        if (!Application.isPlaying && skillQ != null && skillQ is Sora_Q_MeleeDashSkill meleeSkill)
        {
            Gizmos.color = Color.cyan;
            Vector2 previewCenter = (Vector2)basePos + new Vector2(meleeSkill.hitboxOffset.x * dir, meleeSkill.hitboxOffset.y);
            Gizmos.DrawWireCube(previewCenter, meleeSkill.hitboxSize);

            // 2타(nextCombo)가 있으면 그것도 그려줌
            if (meleeSkill.nextComboSkill != null && meleeSkill.nextComboSkill is Sora_Q_MeleeDashSkill comboSkill)
            {
                Gizmos.color = Color.red;
                Vector2 comboCenter = (Vector2)basePos + new Vector2(comboSkill.hitboxOffset.x * dir, comboSkill.hitboxOffset.y);
                Gizmos.DrawWireCube(comboCenter, comboSkill.hitboxSize);
            }
        }

        // 게임 실행 중 실제 때리는 타격 박스 (빨간색 반투명)
        if (Application.isPlaying && _showHitbox)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
            Gizmos.DrawCube(_lastHitboxCenter, _lastHitboxSize);
        }
    }
#endif

    //    private void StartAttack()
    //    {
    //        _isAttacking = true;  // 공격 중 상태 설정
    //        //_attackStartTime = Time.time; // 시작 시간 기록

    //        // 공격 시작! 슈퍼아머 장착 (넉백 무시)
    //        if (PlayerManager.Instance.CurrentCharacter != null)
    //            PlayerManager.Instance.CurrentCharacter.isSuperArmor = true;

    //        // [동시 입력 버그 방지] 공격을 시작하면 예약되어 있던 점프 명령을 강제로 지워버림
    //        _animator.ResetTrigger("Jump");

    //        _currentAttack++;  // 다음 공격 번호 증가 1
    //        _animator.Play("CloseAttack" + _currentAttack, -1, 0f);

    //        // 코루틴 대신 함수 호출
    //        ApplyAttackDash();

    //    }

    //    public void OnAttackCombo()
    //    {
    //        if (_attackQueue.Count > 0 && _currentAttack < _maxAttackNum)  // 예약된 공격이 있다면 실행
    //        {
    //            //_attackStartTime = Time.time; // 콤보 시작 시간 갱신
    //            _currentAttack = _attackQueue.Dequeue();
    //            _animator.Play("CloseAttack" + _currentAttack, -1, 0f);

    //            ApplyAttackDash(); 
    //        }
    //    }

    //    // 공격 애니메이션이 끝날 때 호출 (Animation Event)
    //    public void OnAttackEnd()
    //    {   
    //        _attackQueue.Clear();
    //        _currentAttack = 0;
    //        _isAttacking = false;

    //        // 공격 종료. 슈퍼아머 해제
    //        if (PlayerManager.Instance.CurrentCharacter != null)
    //            PlayerManager.Instance.CurrentCharacter.isSuperArmor = false;

    //        // 공격이 끝날 때 X축 밀림 방지. (중력은 이미 코루틴에서 복구됨)
    //        if (_rb != null)
    //        {
    //            _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y);
    //            _rb.gravityScale = _originalGravity;
    //        }
    //    }

    //    // Transform 강제 조작 대신 Rigidbody에 짧은 힘을 주어 굳는 현상 방지
    //    private void ApplyAttackDash()
    //    {
    //        if (_rb != null)
    //        {
    //            float dir = _spriteRenderer.flipX ? 1f : -1f;
    //            StartCoroutine(JuicyDashRoutine(dir)); // 부드러운 대시 코루틴으로 교체
    //        }
    //    }

    //    // 순간 돌진으로 앞으로 나갔다가 원래 관성(X, Y 모두)을 되돌려주는 대시 코루틴
    //    private IEnumerator JuicyDashRoutine(float dir)
    //    {
    //        float elapsed = 0f;

    //        // 1. 공격 직전의 원래 속도(X축 이동 관성, Y축 점프/낙하 관성) 기억!
    //        Vector2 savedVelocity = _rb.linearVelocity;

    //        // 2. 0.1초 동안만 중력을 끄고 허공에 붙잡음 (체공)
    //        _rb.gravityScale = 0f;

    //        while (elapsed < dashDuration)
    //        {
    //            if (PlayerManager.Instance.CurrentCharacter != null &&
    //                !PlayerManager.Instance.CurrentCharacter.isSuperArmor &&
    //                PlayerManager.Instance.CurrentCharacter.isKnockedBack)
    //            {
    //                _rb.gravityScale = _originalGravity; // 넉백 맞으면 바로 복구
    //                yield break;
    //            }

    //            // 앞으로 전진 (Y축은 0으로 고정하여 공중에서 잠시 멈춤)
    //            float currentSpeed = Mathf.Lerp(dashSpeed, 0f, elapsed / dashDuration);
    //            //_rb.linearVelocity = new Vector2(dir * currentSpeed, _rb.linearVelocity.y);
    //            _rb.linearVelocity = new Vector2(dir * currentSpeed, 0f); //?

    //            elapsed += Time.deltaTime;
    //            yield return null;
    //        }

    //        // 3. 0.1초가 끝나면 즉시 중력을 복구하고, 아까 기억해둔 '원래 속도'를 그대로 돌려줌!
    //        _rb.gravityScale = _originalGravity;
    //        _rb.linearVelocity = savedVelocity;
    //        //_rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
    //    }

    //    //공격 범위 콜라이더 조정
    //    public void EnableAttackCollider()
    //    {
    //        // 진짜 타격이 발생하는 이 순간에 마나를 소모
    //        PlayerManager.Instance.CurrentCharacter.UseMana(_manaPerAttack);

    //        // 단발성 판정 대신 0.15초 동안 궤적을 긁는 코루틴 실행
    //        StartCoroutine(ActiveHitboxRoutine());
    //    }

    //    // 0.15초 동안 매 프레임 박스를 그리며 이동 궤적의 모든 적을 벰
    //    private IEnumerator ActiveHitboxRoutine()
    //    {
    //        float activeDuration = 0.15f; // 공격 판정이 살아있는 시간 
    //        float elapsed = 0f;

    //        // 다단히트(한 번 휘두를 때 여러 번 맞는 것) 방지용 리스트
    //        HashSet<Collider2D> alreadyHitEnemies = new HashSet<Collider2D>();

    //        Vector2 size = _currentAttack == 1 ? meleeFirstSize : meleeSecondSize;
    //        Vector2 offset = _currentAttack == 1 ? meleeFirstOffset : meleeSecondOffset;

    //        _lastHitboxSize = size; // 기즈모용 사이즈 고정

    //        while (elapsed < activeDuration)
    //        {
    //            // 매 프레임마다 플레이어의 현재 위치를 기반으로 박스 중심점 갱신 (이동 궤적 추적)
    //            Vector2 currentOffset = offset;
    //            currentOffset.x *= _spriteRenderer.flipX ? -1 : 1;
    //            Vector2 center = (Vector2)_player.position + currentOffset;

    //            _lastHitboxCenter = center; // 기즈모 중심점 갱신
    //            _showHitbox = true;         // 기즈모 켜기

    //            // 해당 프레임에 박스에 닿은 적 모두 추출
    //            Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f, LayerMask.GetMask("Enemy"));

    //            bool hitSomethingThisFrame = false; // 이번 프레임에 타격이 있었는지 체크

    //            foreach (var hit in hits)
    //            {
    //                // 이번 공격(휘두르기)에서 이미 때린 적은 무시 (다단히트 방지)
    //                if (!alreadyHitEnemies.Contains(hit))
    //                {
    //                    alreadyHitEnemies.Add(hit); // 때린 목록에 추가

    //                    CharacterStats enemyStats = hit.GetComponent<CharacterStats>();
    //                    if (enemyStats != null)
    //                    {
    //                        int damage = PlayerManager.Instance.CurrentCharacter.attack.GetValue();
    //                        ElementType element = PlayerManager.Instance.CurrentCharacter.currentElement;

    //                        enemyStats.TakeDamage(damage, element);

    //                        Vector2 knockbackDir = (hit.transform.position - _player.position).normalized;
    //                        enemyStats.ApplyKnockback(knockbackDir, 5f);

    //                        hitSomethingThisFrame = true; // 적을 타격하였음
    //                    }
    //                }
    //            }

    //            // [역경직 발동] 적을 썰어버린 프레임에 애니메이션을 잠깐 멈춤
    //            if (hitSomethingThisFrame)
    //            {
    //                if (_hitStopCoroutine != null) StopCoroutine(_hitStopCoroutine);
    //                _hitStopCoroutine = StartCoroutine(HitStopRoutine(hitStopDuration));
    //            }

    //            elapsed += Time.deltaTime;
    //            yield return null; // 다음 프레임으로 넘어가기
    //        }

    //        _showHitbox = false; // 판정 시간 끝나면 기즈모 끄기
    //    } // ActiveHitboxRoutine 끝

    //    // [역경직(Hit Stop) 코루틴] 화면이 멈칫하며 타격감 극대화
    //    private IEnumerator HitStopRoutine(float duration)
    //    {
    //        _animator.speed = 0f; // 애니메이션 일시정지
    //        yield return new WaitForSeconds(duration);
    //        _animator.speed = 1f; // 정상 속도 복구
    //    }

    //    // 피격 코루틴 강제 정지를 위한 취소 함수 (PlayerStats 등에서 넉백/사망 시 호출 가능)
    //    //  -> StateMachineBehaviour나 피격 시 강제로 캔슬할 때 호출됨
    //    public void CancelAttack()
    //    {
    //        _isAttacking = false;
    //        _currentAttack = 0;
    //        _attackQueue.Clear();

    //        if (PlayerManager.Instance.CurrentCharacter != null)
    //            PlayerManager.Instance.CurrentCharacter.isSuperArmor = false;

    //        StopAllCoroutines();

    //        // [중력/속도 복구] 취소 시 멈췄던 애니메이션과 중력을 모두 돌려놓음
    //        _animator.speed = 1f;
    //        if (_rb != null)
    //        {
    //            _rb.gravityScale = _originalGravity;
    //            //_rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y); // 캔슬 시에는 강제로 속도를 되돌리지 않음 (넉백 등에 의해 날아가는 중일 수 있으므로)
    //        }
    //    }

    //    private IEnumerator ShowHitboxGizmo()
    //    {
    //        _showHitbox = true;
    //        yield return new WaitForSeconds(0.15f);
    //        _showHitbox = false;
    //    }

    //#if UNITY_EDITOR

    //    private void OnDrawGizmosSelected()
    //    {
    //        // 게임 실행 전(Edit Mode)에는 _player 변수가 세팅되지 않았으므로 부모 위치를 직접 찾기
    //        Vector3 basePosition = transform.parent != null ? transform.parent.position : transform.position;

    //        // 현재 에디터 상에서 캐릭터가 왼쪽을 보고 있는지(flipX) 확인
    //        SpriteRenderer sr = GetComponent<SpriteRenderer>();
    //        float dir = (sr != null && sr.flipX) ? -1f : 1f;

    //        // 1타 공격 범위 미리보기 (청록색 박스)
    //        Gizmos.color = Color.cyan;
    //        Vector2 center1 = (Vector2)basePosition + new Vector2(meleeFirstOffset.x * dir, meleeFirstOffset.y);
    //        Gizmos.DrawWireCube(center1, meleeFirstSize);

    //        // 2타 공격 범위 미리보기 (빨간색 박스)
    //        Gizmos.color = Color.red;
    //        Vector2 center2 = (Vector2)basePosition + new Vector2(meleeSecondOffset.x * dir, meleeSecondOffset.y);
    //        Gizmos.DrawWireCube(center2, meleeSecondSize);
    //    }

    //    // 기존에 있던 OnDrawGizmos (게임 실행 중에 진짜 타격 순간에만 켜지는 빨간 박스)
    //    private void OnDrawGizmos()
    //    {
    //        if (_showHitbox)
    //        {
    //            Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
    //            Gizmos.DrawCube(_lastHitboxCenter, _lastHitboxSize);
    //        }
    //    }
    //#endif
}
