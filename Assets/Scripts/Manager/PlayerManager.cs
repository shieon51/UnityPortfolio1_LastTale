using System;
using UnityEngine;

// 플레이어 관리자 (싱글톤) - 현재 플레이어가 조정하는 캐릭터를 지정
public class PlayerManager : Singleton<PlayerManager>
{
    // 현재 플레이어가 조종 중인 캐릭터의 스탯 (소라일 수도, 리엘일 수도 있음)
    public PlayableCharacter CurrentCharacter { get; private set; }

    // UI나 카메라가 구독할 이벤트
    public event Action<PlayableCharacter> OnCharacterPossessed;

    // 게임 시작 시, 씬에 있는 플레이어를 자동 찾아서 빙의
    private void Start()
    {
        // 태그가 Player인 녀석을 찾아 빙의 (초기 셋팅용)
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            PlayableCharacter startChar = playerObj.GetComponent<PlayableCharacter>(); 
            if (startChar != null) PossessCharacter(startChar);
        }
    }

    // 빙의(시점 변경) 함수
    public void PossessCharacter(PlayableCharacter newCharacter)
    {
        // 1. 기존 캐릭터에서 빙의 해제 (조작 끄기, AI 켜기 등)
        if (CurrentCharacter != null)
        {
            CurrentCharacter.OnUnpossessed();
            SetCharacterControl(CurrentCharacter, false); // 조작 비활성화
        }

        CurrentCharacter = newCharacter;

        // 2. 새 캐릭터에 빙의 (조작 켜기, AI 끄기)
        if (CurrentCharacter != null)
        {
            CurrentCharacter.OnPossessed();
            SetCharacterControl(CurrentCharacter, true); // 조작 활성화

            // UI 매니저 등에게 캐릭터가 바뀌었다고 알림
            OnCharacterPossessed?.Invoke(CurrentCharacter);
            Debug.Log($"현재 플레이 캐릭터가 {CurrentCharacter.gameObject.name}(으)로 변경되었습니다.");
        }
    }

    // 컴포넌트를 켜고 끄는 마법의 헬퍼 함수
    private void SetCharacterControl(PlayableCharacter character, bool isPlayer)
    {
        // 조작 스크립트 켜고 끄기
        var controller = character.GetComponent<PlayerController>();
        if (controller != null) controller.enabled = isPlayer;

        var attack = character.GetComponentInChildren<PlayerAttack>();
        if (attack != null) attack.enabled = isPlayer;

        // 나중에 추가할 NPC 전용 AI 스크립트(예: BossFSM)는 플레이어일 땐 꺼야 함
        // var ai = character.GetComponent<BossAI>();
        // if (ai != null) ai.enabled = !isPlayer; // 조종 중일 땐 AI 끄기, 조종 안할 땐 AI 켜기
    }

    // 외부에서 데미지를 주거나 힐을 할 때 사용하는 헬퍼 함수
    public void TakeDamageToCurrentCharacter(int damage, ElementType element = ElementType.Normal)
    {
        if (CurrentCharacter != null)
            CurrentCharacter.TakeDamage(damage, element);
    }
}
