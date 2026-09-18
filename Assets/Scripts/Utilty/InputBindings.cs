using System;
using System.Collections.Generic;
using UnityEngine;

// 게임 내 모든 입력 액션. 새 액션이 필요하면 여기와 Defaults 두 곳만 추가하면 된다.
public enum InputAction
{
    // 전투
    Attack1, Attack2, Attack3, Ultimate, Guard, Dodge, Dash, Jump, Transform, Drop,
    // 능력
    TimeAnchor,
    // 상호작용
    Interact,
    // UI 창
    Journal, Profile, Inventory, Map, Pause,
    // 대화·메뉴 조작
    Confirm, Cancel, NavigateUp, NavigateDown
}

public static class InputBindings
{
    // PlayerPrefs 키 접두어. 액션 이름이 뒤에 붙는다. (예: bind_Attack1)
    private const string PrefsKeyPrefix = "bind_";

    // 기본 키. 사용자가 바꾸기 전까지 이 값이 쓰인다.
    private static readonly Dictionary<InputAction, KeyCode> Defaults = new()
    {
        { InputAction.Attack1,     KeyCode.Q },
        { InputAction.Attack2,     KeyCode.W },
        { InputAction.Attack3,     KeyCode.E },
        { InputAction.Ultimate,    KeyCode.R },
        { InputAction.Guard,       KeyCode.F },
        { InputAction.Dodge,       KeyCode.D },          // 소라의 '틈입'
        { InputAction.Dash,        KeyCode.LeftShift },
        { InputAction.Jump,        KeyCode.Space },
        { InputAction.Drop,        KeyCode.DownArrow },  // 아래 지형 통과
        { InputAction.Transform,   KeyCode.Tab },        // 요정화
        { InputAction.TimeAnchor,  KeyCode.T },          // 시간의 닻
        { InputAction.Interact,    KeyCode.Z },
        { InputAction.Journal,     KeyCode.J },
        { InputAction.Profile,     KeyCode.C },
        { InputAction.Inventory,   KeyCode.I },
        { InputAction.Map,         KeyCode.M },
        { InputAction.Pause,       KeyCode.Escape },
        { InputAction.Confirm,     KeyCode.Return },
        { InputAction.Cancel,      KeyCode.Escape },
        { InputAction.NavigateUp,   KeyCode.UpArrow },
        { InputAction.NavigateDown, KeyCode.DownArrow },
    };

    // HUD에 찍을 글자. 여기 없는 키는 KeyCode 이름을 그대로 쓴다.
    private static readonly Dictionary<KeyCode, string> KeyLabels = new()
    {
        { KeyCode.LeftShift,   "Shift" },
        { KeyCode.RightShift,  "Shift" },
        { KeyCode.LeftControl, "Ctrl" },
        { KeyCode.RightControl,"Ctrl" },
        { KeyCode.LeftAlt,     "Alt" },
        { KeyCode.RightAlt,    "Alt" },
        { KeyCode.Escape,      "ESC" },
        { KeyCode.Return,      "Enter" },
        { KeyCode.KeypadEnter, "Enter" },
        { KeyCode.Space,       "Space" },
        { KeyCode.UpArrow,     "↑" },
        { KeyCode.DownArrow,   "↓" },
        { KeyCode.LeftArrow,   "←" },
        { KeyCode.RightArrow,  "→" },
    };

    private static readonly Dictionary<InputAction, KeyCode> _current = new();
    private static bool _loaded = false;

    // 키가 바뀌면 HUD의 키 글자 등이 스스로 갱신할 수 있게 알림
    public static event Action OnBindingsChanged;

    // ---------------- 조회 ----------------

    public static KeyCode Get(InputAction action)
    {
        EnsureLoaded();
        return _current.TryGetValue(action, out var key) ? key : KeyCode.None;
    }

    public static bool GetKeyDown(InputAction action)
    {
        var key = Get(action);
        return key != KeyCode.None && Input.GetKeyDown(key);
    }

    public static bool GetKey(InputAction action)
    {
        var key = Get(action);
        return key != KeyCode.None && Input.GetKey(key);
    }

    public static bool GetKeyUp(InputAction action)
    {
        var key = Get(action);
        return key != KeyCode.None && Input.GetKeyUp(key);
    }

    // HUD 스킬 슬롯·메뉴 버튼에 찍을 글자 (키 글자 하드코딩 방지)
    public static string GetKeyLabel(InputAction action)
    {
        var key = Get(action);
        return KeyLabels.TryGetValue(key, out var label) ? label : key.ToString();
    }

    // ---------------- 저장·불러오기 ----------------

    private static void EnsureLoaded()
    {
        if (!_loaded) Load();
    }

    public static void Load()
    {
        _current.Clear();
        foreach (var pair in Defaults)
        {
            int saved = PlayerPrefs.GetInt(PrefsKeyPrefix + pair.Key, (int)pair.Value);
            _current[pair.Key] = (KeyCode)saved;
        }
        _loaded = true;
        OnBindingsChanged?.Invoke();
    }

    // 이미 같은 키를 쓰는 다른 액션이 있는지 확인 (설정 창에서 경고를 띄울 때 사용)
    public static bool TryFindConflict(InputAction action, KeyCode key, out InputAction conflicting)
    {
        EnsureLoaded();
        foreach (var pair in _current)
        {
            if (pair.Key != action && pair.Value == key)
            {
                conflicting = pair.Key;
                return true;
            }
        }
        conflicting = action;
        return false;
    }

    // allowDuplicate = false 이면 중복 키일 때 바꾸지 않고 false를 돌려준다
    public static bool Rebind(InputAction action, KeyCode newKey, bool allowDuplicate = false)
    {
        EnsureLoaded();
        if (!allowDuplicate && TryFindConflict(action, newKey, out var conflicting))
        {
            Debug.LogWarning($"[InputBindings] '{newKey}' 키는 이미 '{conflicting}'에 배정되어 있음");
            return false;
        }

        _current[action] = newKey;
        PlayerPrefs.SetInt(PrefsKeyPrefix + action, (int)newKey);
        PlayerPrefs.Save();
        OnBindingsChanged?.Invoke();
        return true;
    }

    public static void ResetToDefaults()
    {
        foreach (var pair in Defaults)
        {
            _current[pair.Key] = pair.Value;
            PlayerPrefs.DeleteKey(PrefsKeyPrefix + pair.Key);
        }
        PlayerPrefs.Save();
        _loaded = true;
        OnBindingsChanged?.Invoke();
    }

    // ---------------- 기존 표기 호환 ----------------
    // 예전 코드의 InputBindings.TimeAnchor 같은 표기를 그대로 쓸 수 있게 남겨둠

    public static KeyCode Attack1 => Get(InputAction.Attack1);
    public static KeyCode Attack2 => Get(InputAction.Attack2);
    public static KeyCode Attack3 => Get(InputAction.Attack3);
    public static KeyCode Ultimate => Get(InputAction.Ultimate);
    public static KeyCode Guard => Get(InputAction.Guard);
    public static KeyCode Dodge => Get(InputAction.Dodge);
    public static KeyCode Dash => Get(InputAction.Dash);
    public static KeyCode Jump => Get(InputAction.Jump);
    public static KeyCode Transform => Get(InputAction.Transform);
    public static KeyCode TimeAnchor => Get(InputAction.TimeAnchor);
}