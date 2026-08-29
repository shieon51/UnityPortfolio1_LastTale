// Liel_ConcentrationState.cs (신규)
using UnityEngine;

public class Liel_ConcentrationState : NPCState
{
    private Liel_AI liel;
    private float _channelElapsed = 0f;
    private bool _isRetreating = true;
    private bool _interrupted = false;

    private float _manaAccumulator = 0f; // 마나 정수 반올림 문제 해결

    public Liel_ConcentrationState(Liel_AI npc, NPCVisual visual, Transform p) : base(npc, visual, p) { liel = npc; }

    public override void Enter()
    {
        liel.canRotate = false;
        _channelElapsed = 0f; _isRetreating = true; _interrupted = false;
        liel.OnAttackReceived += HandleInterrupted; // ★ 결과 무관, 공격이 오면 즉시 중단 판정
        visual.PlayIfChanged(NPCAnimStateNames.Walk);
        _manaAccumulator = 0f;
    }

    public override void Execute()
    {
        var tuning = NPCCombatTuning.Instance;

        if (_isRetreating)
        {
            float dist = player != null ? Vector2.Distance(liel.transform.position, player.position) : 999f;
            if (dist >= tuning.ConcentrationRetreatDistance || _interrupted)
            {
                _isRetreating = false;
                liel.canRotate = true; // ★ 채널 중엔 다시 플레이어를 볼 수 있게
                liel.Rb.linearVelocity = Vector2.zero;
                liel.StartGuard(); // ★ 채널 중엔 방어 자세 — 패링/완벽방어 기회 생김
                visual.PlayIfChanged(NPCAnimStateNames.Concentration); // ★ 전용 모션
                return;
            }
            float awayDir = (liel.transform.position.x > player.position.x) ? 1f : -1f;
            liel.Rb.linearVelocity = new Vector2(awayDir * tuning.ConcentrationRetreatSpeed, liel.Rb.linearVelocity.y);
            return;
        }

        liel.LookAtPlayer_Public(); // 방어 중엔 플레이어를 계속 바라봄

        if (_interrupted || liel.currentMana >= liel.maxMana) // ★ 방해받거나 마나 다 찼으면 종료
        {
            EndChannel();
            return;
        }

        _channelElapsed += Time.deltaTime;
        _manaAccumulator += liel.maxMana * tuning.ConcentrationRecoverRatioPerSecond * Time.deltaTime; // ★ 누적
        if (_manaAccumulator >= 1f)
        {
            int whole = Mathf.FloorToInt(_manaAccumulator);
            liel.RecoverMana(whole);
            _manaAccumulator -= whole;
        }

        int manaPerFrame = Mathf.RoundToInt(liel.maxMana * tuning.ConcentrationRecoverRatioPerSecond * Time.deltaTime);
        if (manaPerFrame > 0) liel.RecoverMana(manaPerFrame); // ★ 프레임마다 조금씩 — 중단돼도 이미 받은 만큼은 유지됨

        if (_channelElapsed >= tuning.ConcentrationDuration) EndChannel();
    }

    private void HandleInterrupted(CharacterStats attacker) => _interrupted = true;

    private void EndChannel() => liel.StateMachine.ChangeState(new Liel_UtilityDecisionState(liel, visual, player));

    public override void Exit()
    {
        liel.canRotate = true;
        liel.StopGuard(); // ★ 방어 해제
        liel.OnAttackReceived -= HandleInterrupted;
        liel.LastConcentrationEndTime = Time.time; // ★ 재진입 쿨다운 시작점 기록
    }
}