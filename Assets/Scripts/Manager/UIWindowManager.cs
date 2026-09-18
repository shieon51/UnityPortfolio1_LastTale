using System;
using System.Collections.Generic;
using UnityEngine;

// 창·팝업·일시정지를 하나의 스택으로 관리한다.
// ESC는 맨 위부터 닫고, 스택이 비었을 때만 일시정지 메뉴를 연다.
public class UIWindowManager : Singleton<UIWindowManager>
{
    [Serializable]
    public class WindowHotkey
    {
        public UIWindowId windowId;
        public InputAction action;
    }

    [Header("참조")]
    [Tooltip("실제 창(UIWindow)들이 들어있는 컨테이너 (Canvas > Windows)")]
    public Transform windowsContainer;
    [Tooltip("일시정지 메뉴. 없으면 ESC로 아무것도 열리지 않는다")]
    public UIStackElement pauseMenu;

    [Header("단축키")]
    public List<WindowHotkey> hotkeys = new()
    {
        new WindowHotkey { windowId = UIWindowId.Journal,   action = InputAction.Journal },
        new WindowHotkey { windowId = UIWindowId.Profile,   action = InputAction.Profile },
        new WindowHotkey { windowId = UIWindowId.Inventory, action = InputAction.Inventory },
        new WindowHotkey { windowId = UIWindowId.Map,       action = InputAction.Map },
    };

    [Header("동작")]
    [Tooltip("월드를 멈출 요소가 열려 있으면 timeScale을 0으로 만든다")]
    public bool pauseWorld = true;
    [Tooltip("대화 중에도 ESC로 일시정지 메뉴를 열 수 있게 할지")]
    public bool allowPauseDuringDialogue = true;
    [Tooltip("창을 열 수 있는 UI 모드 (보통 탐색만)")]
    public UIMode allowedModeForWindows = UIMode.Normal;

    public event Action<UIStackElement> OnOpened;
    public event Action<UIStackElement> OnClosed;

    private readonly Dictionary<UIWindowId, UIWindow> _windows = new();
    private readonly List<UIStackElement> _stack = new();   // 마지막 항목이 맨 위
    private float _savedTimeScale = 1f;
    private bool _lockApplied;

    public bool IsAnyOpen => _stack.Count > 0;
    public UIStackElement Top => _stack.Count > 0 ? _stack[^1] : null;
    public bool IsOpen(UIWindowId id) => _windows.TryGetValue(id, out var w) && w.IsOpen;

    private void Awake()
    {
        var source = windowsContainer != null ? windowsContainer : transform;
        foreach (var window in source.GetComponentsInChildren<UIWindow>(true))
        {
            _windows[window.WindowId] = window;
            window.gameObject.SetActive(false);
        }

        if (pauseMenu != null) pauseMenu.gameObject.SetActive(false);
    }

    private void Update()
    {
        HandleEscapeInput();
        HandleHotkeys();
    }

    // ---------------- 입력 ----------------

    private void HandleEscapeInput()
    {
        if (!InputBindings.GetKeyDown(InputAction.Pause)) return;

        // 스택에 뭔가 있으면 맨 위부터 닫는다
        if (_stack.Count > 0)
        {
            var top = Top;
            if (top.HandleEscape()) return;      // 요소가 자체 처리했다면 닫지 않는다
            if (top.closeOnEscape) Close(top);
            return;
        }

        // 스택이 비었을 때만 일시정지 메뉴
        if (IsTalking && !allowPauseDuringDialogue) return;
        if (pauseMenu != null) Open(pauseMenu);
    }

    private void HandleHotkeys()
    {
        // 팝업·일시정지가 떠 있으면 창 단축키를 받지 않는다
        if (Top != null && Top.Layer != UILayer.Window) return;

        foreach (var hotkey in hotkeys)
        {
            if (!InputBindings.GetKeyDown(hotkey.action)) continue;
            Toggle(hotkey.windowId);
            return;
        }
    }

    // ---------------- 열기·닫기 ----------------

    public void Toggle(UIWindowId id)
    {
        if (IsOpen(id)) { Close(_windows[id]); return; }
        Open(id);
    }

    public bool Open(UIWindowId id)
    {
        if (!_windows.TryGetValue(id, out var window))
        {
            Debug.LogWarning($"[UIWindowManager] '{id}' 창이 등록되어 있지 않음 — Windows 컨테이너 아래에 있는지 확인");
            return false;
        }
        return Open(window);
    }

    public bool Open(UIStackElement element)
    {
        if (element == null || element.IsOpen) return false;

        if (!CanOpen(element)) return false;

        // 열람형 창은 서로 배타적이다 — 이미 열린 창은 닫는다
        if (element.Layer == UILayer.Window)
        {
            for (int i = _stack.Count - 1; i >= 0; i--)
                if (_stack[i].Layer == UILayer.Window) Close(_stack[i]);
        }

        _stack.Add(element);
        element.OpenInternal();
        ApplyStackState();
        OnOpened?.Invoke(element);
        return true;
    }

    private bool CanOpen(UIStackElement element)
    {
        // 대화 중에는 허용된 요소만
        if (IsTalking && !element.allowDuringDialogue) return false;

        // 창은 지정된 모드에서만 (전투·컷씬 중 차단)
        if (element.Layer == UILayer.Window
            && UIModeManager.Instance != null
            && UIModeManager.Instance.CurrentMode != allowedModeForWindows) return false;

        return true;
    }

    public void Close(UIStackElement element)
    {
        if (element == null || !_stack.Contains(element)) return;

        _stack.Remove(element);
        element.CloseInternal();
        ApplyStackState();
        OnClosed?.Invoke(element);
    }

    public void CloseTop()
    {
        if (Top != null) Close(Top);
    }

    public void CloseAll()
    {
        for (int i = _stack.Count - 1; i >= 0; i--)
        {
            var element = _stack[i];
            _stack.RemoveAt(i);
            element.CloseInternal();
            OnClosed?.Invoke(element);
        }
        ApplyStackState();
    }

    // ---------------- 잠금·일시정지 ----------------

    private void ApplyStackState()
    {
        bool anyOpen = _stack.Count > 0;

        // 열려 있는 동안 플레이어 조작·상호작용 차단
        if (anyOpen && !_lockApplied) { GlobalActionLock.Lock(this); _lockApplied = true; }
        else if (!anyOpen && _lockApplied) { GlobalActionLock.Unlock(this); _lockApplied = false; }

        if (!pauseWorld) return;

        bool shouldPause = false;
        foreach (var element in _stack) if (element.pausesWorld) { shouldPause = true; break; }

        if (shouldPause && Time.timeScale != 0f)
        {
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
        else if (!shouldPause && Time.timeScale == 0f)
        {
            Time.timeScale = _savedTimeScale <= 0f ? 1f : _savedTimeScale;
        }
    }

    private bool IsTalking => DialogueManager.Instance != null && DialogueManager.Instance.IsTalking;

    private void OnDestroy()
    {
        if (_lockApplied) GlobalActionLock.Unlock(this);
        if (pauseWorld && Time.timeScale == 0f) Time.timeScale = 1f;   // 안전장치
    }

#if UNITY_EDITOR
    [ContextMenu("기록장 열기/닫기")] private void DebugJournal() => Toggle(UIWindowId.Journal);
    [ContextMenu("프로필 열기/닫기")] private void DebugProfile() => Toggle(UIWindowId.Profile);
    [ContextMenu("전부 닫기")] private void DebugCloseAll() => CloseAll();
#endif
}