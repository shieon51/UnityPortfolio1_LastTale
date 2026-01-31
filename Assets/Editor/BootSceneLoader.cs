#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public class BootSceneLoader
{
    // 상단 메뉴바에 생길 경로
    private const string MENU_PATH = "Tools/Always Start From Scene 0";

    static BootSceneLoader()
    {
        // 에디터가 켜질 때 저장된 설정을 불러와서 적용
        EditorApplication.delayCall += () => {
            bool isEnabled = EditorPrefs.GetBool(MENU_PATH, false);
            SetPlayModeStartScene(isEnabled);
        };
    }

    // 메뉴 클릭 시 실행 (On/Off 토글)
    [MenuItem(MENU_PATH)]
    private static void ToggleMode()
    {
        bool isEnabled = EditorPrefs.GetBool(MENU_PATH, false);
        bool newState = !isEnabled;

        EditorPrefs.SetBool(MENU_PATH, newState); // 설정 저장
        SetPlayModeStartScene(newState);          // 기능 적용

        Debug.Log($"[BootLoader] Always Start Scene 0: {(newState ? "ON" : "OFF")}");
    }

    // 메뉴의 체크 표시(v) 갱신
    [MenuItem(MENU_PATH, true)]
    private static bool ToggleModeValidate()
    {
        Menu.SetChecked(MENU_PATH, EditorPrefs.GetBool(MENU_PATH, false));
        return true;
    }

    // 실제 기능을 수행하는 함수
    private static void SetPlayModeStartScene(bool enable)
    {
        if (enable)
        {
            // Build Settings에 등록된 씬 목록 중 0번째를 가져옴.
            if (EditorBuildSettings.scenes.Length > 0)
            {
                string scenePath = EditorBuildSettings.scenes[0].path;
                SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);

                // ★ 이 설정이 핵심! 플레이 버튼 누를 때 시작할 씬을 고정함
                EditorSceneManager.playModeStartScene = sceneAsset;
            }
            else
            {
                Debug.LogWarning("Build Settings에 등록된 씬이 없습니다! (File -> Build Settings 확인)");
                EditorSceneManager.playModeStartScene = null;
            }
        }
        else
        {
            // null로 설정하면 "현재 열려있는 씬"에서 시작함.
            EditorSceneManager.playModeStartScene = null;
        }
    }
}
#endif
