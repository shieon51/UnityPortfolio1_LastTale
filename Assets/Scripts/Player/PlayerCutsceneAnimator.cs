using System.Collections;
using UnityEngine;

// Player 루트에 부착
public class PlayerCutsceneAnimator : MonoBehaviour, IActionLockSource
{
    [SerializeField] private CutsceneAnimationCatalog _catalog;
    private PlayerVisual _visual;
    private bool _isPlaying;

    public bool IsLocked => _isPlaying; // 재생 중엔 이동/공격 자동 잠금 

    private void Awake() => _visual = GetComponentInChildren<PlayerVisual>();

    public void Play(string key)
    {
        var data = _catalog.Get(key);
        if (data == null)
        {
            Debug.LogWarning($"[PlayerCutsceneAnimator] 존재하지 않는 연출 키: {key}");
            return;
        }
        StartCoroutine(PlayRoutine(data));
    }

    private IEnumerator PlayRoutine(CutsceneAnimationData data)
    {
        _isPlaying = true;
        _visual.PlayCutscene(data.bodyStateName, data.faceStateName);

        if (data.duration >= 0f)
        {
            yield return new WaitForSeconds(data.duration);
            EndCutscene();
        }
        // duration == -1이면 클립 마지막 프레임의 Animation Event → PlayerAnimationRelay → EndCutscene() 로 연결
    }

    public void EndCutscene()
    {
        _isPlaying = false;
        _visual.ReturnToLocomotion();
    }
}