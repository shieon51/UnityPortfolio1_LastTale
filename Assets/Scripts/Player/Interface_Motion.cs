
// '행동 불가' 상태를 만드는 소스를 일반화. 대화/공격/넉백 외에
// 폼체인지 딜레이처럼 앞으로 추가될 잠금 원인들을 PlayerController 코드를 건드리지 않고 추가 가능하게 함.
using System;

public interface IActionLockSource
{
    bool IsLocked { get; }
}

// 폼체인지(요정화)를 지원하는 캐릭터만 구현. 시즌2/3 캐릭터는 구현 안 해도 전체 시스템이 정상 동작함(null 허용).
public interface IFormStageProvider
{
    int FormStage { get; }        // 0=1단계, 1=2단계(비행), 2=3단계
    bool IsFlightForm { get; }    // 비행 관련 이동/모션 적용 여부
    bool IsTransforming { get; } // 요정화 변신중

    event Action OnFormTransformStarted;  // 변신 시작 (연출 모션 트리거용)
    event Action<int> OnFormStageChanged; // 변신 완료 (파츠 교체용)
}