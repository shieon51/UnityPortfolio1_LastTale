using UnityEngine;

public class GameManager : Singleton<GameManager>
{
    public IGameMode CurrentGameMode { get; private set; }

    private void Awake()
    {
        // 임시로 게임 시작 시 1부(Season1) 모드로 설정
        SetGameMode(new Season1Mode());
    }

    public void SetGameMode(IGameMode mode)
    {
        CurrentGameMode = mode;
        Debug.Log($"[GameManager] 게임 모드 변경됨: {mode.GetType().Name}");
    }
}
