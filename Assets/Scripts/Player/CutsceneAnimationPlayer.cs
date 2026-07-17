using System.Collections;
using UnityEngine;

// Player 루트에 부착
public class CutsceneAnimationPlayer : MonoBehaviour, IActionLockSource
{
    [SerializeField] private CutsceneAnimationCatalog _catalog;
    [SerializeField] private Animator[] _bodyAnimators;
    [SerializeField] private Animator _faceAnimator; // 표정 파츠가 없으면 비워둠

    //private PlayerVisual _visual;
    private bool _isPlaying;

    public bool IsLocked => _isPlaying; // 재생 중엔 이동/공격 자동 잠금 

    //private void Awake() => _visual = GetComponentInChildren<PlayerVisual>();

    public void Play(string key)
    {
        var data = _catalog.Get(key);
        if (data == null)
        {
            Debug.LogWarning($"[CutsceneAnimationPlayer] 존재하지 않는 연출 키: {key}");
            return;
        }
        StartCoroutine(PlayRoutine(data));
    }

    private IEnumerator PlayRoutine(CutsceneAnimationData data)
    {
        _isPlaying = true;
        //_visual.PlayCutscene(data.bodyStateName, data.faceStateName);

        foreach (var anim in _bodyAnimators)
            if (anim != null && !string.IsNullOrEmpty(data.bodyStateName))
                anim.Play(data.bodyStateName, -1, 0f);

        if (_faceAnimator != null && !string.IsNullOrEmpty(data.faceStateName))
            _faceAnimator.Play(data.faceStateName, -1, 0f);

        if (data.duration >= 0f)
        {
            yield return new WaitForSeconds(data.duration);
            EndCutscene();
        }
        // duration == -1이면 클립 끝의 Animation Event가 EndCutscene()을 호출
    }

    public void EndCutscene()
    {
        _isPlaying = false;
        //_visual.ReturnToLocomotion();
    }
}