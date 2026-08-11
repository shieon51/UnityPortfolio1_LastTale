using UnityEngine;

// GameSettings.cs (신규) — PlayerPrefs로 영구 저장
public class GameSettings : Singleton<GameSettings>
{
    private const string BGM_KEY = "Settings_BGMVolume";
    private const string SFX_KEY = "Settings_SFXVolume";
    private const string SHAKE_KEY = "Settings_ScreenShake";
    private const string FLASH_KEY = "Settings_FlashEffects";

    public float BGMVolume { get; private set; } = 1f;
    public float SFXVolume { get; private set; } = 1f;
    public bool ScreenShakeEnabled { get; private set; } = true;
    public bool FlashEffectsEnabled { get; private set; } = true;

    private void Awake()
    {
        BGMVolume = PlayerPrefs.GetFloat(BGM_KEY, 1f);
        SFXVolume = PlayerPrefs.GetFloat(SFX_KEY, 1f);
        ScreenShakeEnabled = PlayerPrefs.GetInt(SHAKE_KEY, 1) == 1;
        FlashEffectsEnabled = PlayerPrefs.GetInt(FLASH_KEY, 1) == 1;
    }

    public void SetBGMVolume(float v) { BGMVolume = v; PlayerPrefs.SetFloat(BGM_KEY, v); }
    public void SetSFXVolume(float v) { SFXVolume = v; PlayerPrefs.SetFloat(SFX_KEY, v); }
    public void SetScreenShakeEnabled(bool e) { ScreenShakeEnabled = e; PlayerPrefs.SetInt(SHAKE_KEY, e ? 1 : 0); }
    public void SetFlashEffectsEnabled(bool e) { FlashEffectsEnabled = e; PlayerPrefs.SetInt(FLASH_KEY, e ? 1 : 0); }
}