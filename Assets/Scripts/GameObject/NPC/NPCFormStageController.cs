using System;
using System.Collections;
using UnityEngine;

// 보스 AI(FSM)가 페이즈 전환 조건을 판단해서 직접 호출해주는 폼체인지 컨트롤러.
// SoraStats와 트리거 방식만 다를 뿐, 인터페이스는 동일해서 CharacterAppearance가 그대로 반응함.
public class NPCFormStageController : MonoBehaviour, IFormStageProvider
{
    public int FormStage { get; private set; } = 0;
    public bool IsFlightForm { get; private set; } = false;

    public event Action OnFormTransformStarted;
    public event Action<int> OnFormStageChanged;

    [Tooltip("변신 연출 딜레이(초)")]
    public float transformDuration = 1.0f;

    public bool IsTransforming { get; private set; }

    // 예: Liel_BattleIdleState에서 HP가 임계치 아래로 떨어지면 이걸 호출
    public void TransitionToStage(int newStage, bool isFlightForm = false)
    {
        if (IsTransforming || newStage == FormStage) return;
        StartCoroutine(TransformRoutine(newStage, isFlightForm));
    }

    private IEnumerator TransformRoutine(int newStage, bool isFlightForm)
    {
        IsTransforming = true;
        OnFormTransformStarted?.Invoke();

        yield return new WaitForSeconds(transformDuration);

        FormStage = newStage;
        IsFlightForm = isFlightForm;
        OnFormStageChanged?.Invoke(FormStage);

        IsTransforming = false;
    }
}