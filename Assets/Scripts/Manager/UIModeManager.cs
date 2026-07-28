using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum UIMode { Normal, Battle, Cutscene }

// 보스전 진입 시 전환
public class UIModeManager : Singleton<UIModeManager>
{
    [System.Serializable]
    public class ModeGroup 
    {   
        public UIMode mode; 
        public GameObject panelRoot; 
    } // CanvasGroup 필요

    public List<ModeGroup> modeGroups;
    public float transitionDuration = 0.25f;
    public UIMode CurrentMode { get; private set; } = UIMode.Normal;

    private void Start() => ApplyModeImmediate(UIMode.Normal);

    public void SetMode(UIMode newMode)
    {
        if (newMode == CurrentMode) return;
        StartCoroutine(TransitionRoutine(newMode));
    }

    private IEnumerator TransitionRoutine(UIMode newMode)
    {
        var from = FindGroup(CurrentMode);
        if (from != null) { yield return Fade(from.panelRoot, 1f, 0f); from.panelRoot.SetActive(false); }

        CurrentMode = newMode;

        var to = FindGroup(newMode);
        if (to != null) { to.panelRoot.SetActive(true); yield return Fade(to.panelRoot, 0f, 1f); }
    }

    private void ApplyModeImmediate(UIMode mode)
    {
        foreach (var g in modeGroups) g.panelRoot.SetActive(g.mode == mode);
        CurrentMode = mode;
    }

    private ModeGroup FindGroup(UIMode mode) => modeGroups.Find(g => g.mode == mode);

    private IEnumerator Fade(GameObject root, float from, float to)
    {
        var cg = root.GetComponent<CanvasGroup>();
        if (cg == null) yield break;
        float t = 0f;
        while (t < transitionDuration)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, t / transitionDuration);
            yield return null;
        }
        cg.alpha = to;
    }
}