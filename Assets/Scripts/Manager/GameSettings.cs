using UnityEngine;

// GameSettings.cs — PlayerPrefs로 영구 저장
public class GameSettings : Singleton<GameSettings>
{
    private const string BGM_KEY = "Settings_BGMVolume";
    private const string SFX_KEY = "Settings_SFXVolume";
    private const string SHAKE_KEY = "Settings_ScreenShake";
    private const string FLASH_KEY = "Settings_FlashEffects";
    private const string CLOCK24_KEY = "Settings_Clock24h";        // ★

    [Header("처음 실행할 때의 기본값")]
    [Tooltip("체크하면 처음 실행 시 24시간제로 표시한다 (기획서 5-4, 기본값 [미결])")]
    [SerializeField] private bool default24HourClock = false;      // ★

    public float BGMVolume { get; private set; } = 1f;
    public float SFXVolume { get; private set; } = 1f;
    public bool ScreenShakeEnabled { get; private set; } = true;
    public bool FlashEffectsEnabled { get; private set; } = true;
    public bool Use24HourClock { get; private set; }               // ★

    private void Awake()
    {
        BGMVolume = PlayerPrefs.GetFloat(BGM_KEY, 1f);
        SFXVolume = PlayerPrefs.GetFloat(SFX_KEY, 1f);
        ScreenShakeEnabled = PlayerPrefs.GetInt(SHAKE_KEY, 1) == 1;
        FlashEffectsEnabled = PlayerPrefs.GetInt(FLASH_KEY, 1) == 1;

        Use24HourClock = PlayerPrefs.GetInt(CLOCK24_KEY, default24HourClock ? 1 : 0) == 1;   // ★
        GameTimeFormatter.SetUse24Hour(Use24HourClock);                                      // ★ 모든 시각 표시에 반영
    }

    public void SetBGMVolume(float v) { BGMVolume = v; PlayerPrefs.SetFloat(BGM_KEY, v); }
    public void SetSFXVolume(float v) { SFXVolume = v; PlayerPrefs.SetFloat(SFX_KEY, v); }
    public void SetScreenShakeEnabled(bool e) { ScreenShakeEnabled = e; PlayerPrefs.SetInt(SHAKE_KEY, e ? 1 : 0); }
    public void SetFlashEffectsEnabled(bool e) { FlashEffectsEnabled = e; PlayerPrefs.SetInt(FLASH_KEY, e ? 1 : 0); }

    // ★ 설정 화면의 시간제 토글이 부른다. 바꾸는 즉시 HUD·기록장 등 모든 시각 표시가 다시 그려진다
    public void SetUse24HourClock(bool e)
    {
        Use24HourClock = e;
        PlayerPrefs.SetInt(CLOCK24_KEY, e ? 1 : 0);
        GameTimeFormatter.SetUse24Hour(e);
    }
}