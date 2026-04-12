using UnityEngine;

// 💡 abstract 키워드를 붙여서, 'NPC'라는 객체 자체는 생성하지 못하고 무조건 상속받게 만듭니다.
public abstract class NPC : CharacterStats
{
    //  관계 등급(내면) & 표현 성향(표현 방식)
    public enum RelationshipTier { Hostile, Wary, Acquaintance, Friend, Trusted, Romance }
    public enum PersonalityTrait { Honest, Tsundere, Shy, Cold }

    [Header("NPC Data & Relations")]
    public int understanding = 0; // 표면적 이해도 (우정)
    public int hiddenAffection = 0; // 숨겨진 호감도 (애정)
    public PersonalityTrait myPersonality = PersonalityTrait.Honest;

    // 💡 호감도/이해도 수치를 바탕으로 현재 '관계 등급'을 계산해 주는 프로퍼티
    public RelationshipTier CurrentRelationship
    {
        get
        {
            // (임시 공식) 기획에 맞게 숫자를 조절하세요!
            int totalScore = understanding + (hiddenAffection * 2);
            if (totalScore < 10) return RelationshipTier.Hostile;
            if (totalScore < 30) return RelationshipTier.Wary;
            if (totalScore < 60) return RelationshipTier.Acquaintance;
            if (totalScore < 100) return RelationshipTier.Friend;
            if (totalScore < 150) return RelationshipTier.Trusted;
            return RelationshipTier.Romance;
        }
    }

    public enum NPCMode { Normal, Attack }
    [Header("Current Mode")]
    public NPCMode currentMode = NPCMode.Normal;

    // 대화 상호작용을 위한 트리거 (NPC 프리팹 자식에 부착되어 있음)
    private EventTrigger myEventTrigger;
    protected Animator animator;
    protected Transform player;

    protected override void Awake()
    {
        base.Awake();
        animator = GetComponentInChildren<Animator>();

        // 💡 자식 오브젝트에 달려 있는 EventTrigger를 찾음
        myEventTrigger = GetComponentInChildren<EventTrigger>(true);
    }

    private void Start()
    {
        if (PlayerManager.Instance != null && PlayerManager.Instance.CurrentCharacter != null)
            player = PlayerManager.Instance.CurrentCharacter.transform;
    }

    // 💡 [핵심] EventManager가 NPC를 켤 때 직접 "지금 시간의 이벤트 데이터"를 주입해 줍니다. (대사 꼬임 완벽 해결)
    public void SetupCurrentEvent(EventData currentData)
    {
        if (myEventTrigger != null)
        {
            myEventTrigger.UpdateTrigger(currentData);
        }
    }


    private void OnEnable()
    {
        // 💡 SetActive(true)가 될 때마다 매니저에 나를 등록합니다.
        if (myEventTrigger != null && currentMode == NPCMode.Normal)
        {
            EventManager.Instance.RegisterDynamicTrigger(myEventTrigger);

            //// 데이터 매니저에서 이 NPC의 ID와 현재 시간/날짜에 맞는 EventData를 가져와서 넣어줌
            //EventData myData = DataManager.Instance.EventDict[npcID]; // (예시)
            //myEventTrigger.UpdateTrigger(myData);
        }
    }

    private void OnDisable()
    {
        // 💡 SetActive(false)가 되면 매니저에서 뺍니다.
        if (myEventTrigger != null)
        {
            EventManager.Instance.UnregisterDynamicTrigger(myEventTrigger);
        }
    }

    private void Update()
    {
        if (isKnockedBack) return; // 넉백 중엔 행동 불가

        // 현재 모드에 따라 행동 분기
        if (currentMode == NPCMode.Normal)
        {
            HandleNormalModeAI();
        }
        else if (currentMode == NPCMode.Attack)
        {
            HandleAttackModeAI(); // 💡 여기에 1/2/3 페이즈 유틸리티 AI (가중치 계산) 적용 예정!
        }
    }

    // 💡 [핵심] 자식 클래스(Liel, Gaon 등)가 무조건 각자의 방식으로 오버라이드(구현)해야 하는 함수들!
    protected abstract void HandleNormalModeAI();
    protected abstract void HandleAttackModeAI();


    // 💡 평상시(일반 모드) 행동 패턴
    //private void HandleNormalModeAI()
    //{
    //    if (player == null) return;
    //    float dist = Vector2.Distance(transform.position, player.position);

    //    // 예시: 디아베르가 비밀을 들켰고 호감도가 높을 때, 플레이어가 보이면 도망감!
    //    //if (npcID == 1001 /* 디아베르 ID */ && hiddenAffection > 50 && dist < 5f)
    //    //{
    //    //    animator.SetBool("IsBlushing", true); // 얼굴 붉히기

    //    //    // 대화 불가능하게 트리거 끄기
    //    //    EventManager.Instance.UnregisterDynamicTrigger(myEventTrigger);
    //    //    myEventTrigger.gameObject.SetActive(false);

    //    //    // 반대 방향으로 도망가는 로직 실행...
    //    //    //RunAwayFromPlayer();
    //    //}
    //    //// 예시: 평범한 NPC이고 호감도가 높으면 멀리서 쳐다봄
    //    //else if (understanding > 30 && dist < 7f)
    //    //{
    //    //    //LookAtPlayer();
    //    //}
    //}

    //// 💡 전투 시(공격 모드) 보스 패턴
    //private void HandleAttackModeAI()
    //{
    //    // [유틸리티 AI 뼈대]
    //    // 1. 현재 내 체력 
    //    // 2. 플레이어와의 거리
    //    // 3. 내 호감도 (핸디캡 적용)
    //    // 위 요소를 종합해 점수를 매겨 (평타/돌진/마법/의도적 Miss) 중 하나를 선택해 실행
    //}

    // 공격 모드로 진입하는 함수 (스토리나 특정 조건 만족 시 호출됨)
    public void SwitchToAttackMode()
    {
        currentMode = NPCMode.Attack;
        if (myEventTrigger != null)
        {
            EventManager.Instance.UnregisterDynamicTrigger(myEventTrigger);
            myEventTrigger.gameObject.SetActive(false);
        }
        Debug.Log($"{gameObject.name}이(가) 공격 모드로 돌입했습니다!");
    }
}
