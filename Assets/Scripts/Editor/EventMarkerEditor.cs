using UnityEngine;
using UnityEditor;
using System.IO;

[CustomEditor(typeof(EventMarker))]
public class EventMarkerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EventMarker marker = (EventMarker)target;
        base.OnInspectorGUI();

        GUILayout.Space(20);
        GUILayout.Label("Ink Script Manager", EditorStyles.boldLabel);

        // -----------------------------------------------------------------------
        // [경로 파싱 로직]
        // -----------------------------------------------------------------------
        string rawNodeName = string.IsNullOrEmpty(marker.InkNodeName) ? "New_Story_01" : marker.InkNodeName;
        string fileName = rawNodeName;

        // 1. 파일 이름 추출 (마지막 '_' 뒤의 숫자 제거)
        int lastUnderscoreIndex = rawNodeName.LastIndexOf('_');
        if (lastUnderscoreIndex > 0)
        {
            fileName = rawNodeName.Substring(0, lastUnderscoreIndex);
        }

        // 2. 폴더 이름 추출
        string[] parts = fileName.Split('_');
        string folderName = (parts.Length > 0) ? parts[0] : "etc";

        // -----------------------------------------------------------------------

        // 경로 설정
        string baseDir = Path.Combine(Application.dataPath, "Datas");
        string targetDir = Path.Combine(baseDir, folderName);
        string fullPath = Path.Combine(targetDir, $"{fileName}.ink");
        string assetPath = $"Assets/Datas/{folderName}/{fileName}.ink";

        GUILayout.BeginHorizontal();

        // 1. Ink 파일 열기
        if (GUILayout.Button("Open Ink File", GUILayout.Height(30)))
        {
            if (File.Exists(fullPath))
            {
                UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                AssetDatabase.OpenAsset(obj);
            }
            else
            {
                Debug.LogWarning($"파일을 찾을 수 없습니다: {assetPath}");
            }
        }

        // 2. Ink 파일 생성 및 main.ink 등록
        if (GUILayout.Button("Create / Reset Ink", GUILayout.Height(30)))
        {
            // 폴더 생성
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            // 파일 생성 (덮어쓰기 질문 포함)
            bool proceed = true;
            if (File.Exists(fullPath))
            {
                proceed = EditorUtility.DisplayDialog("경고",
                    $"'{fileName}.ink' 파일이 이미 존재합니다.\n덮어쓰시겠습니까? (내용 초기화됨)", "네", "아니오");
            }

            if (proceed)
            {
                // A. 파일 내용 작성
                string content = $"=== {rawNodeName} ===\n\nTODO: Write dialogue for {rawNodeName} here.\n\n-> END";
                File.WriteAllText(fullPath, content);

                // B. main.ink에 INCLUDE 자동 추가 (★ 추가된 핵심 기능)
                AddToMainInk(folderName, fileName);

                // C. 갱신 및 열기
                AssetDatabase.Refresh();
                Debug.Log($"Ink 파일 생성 및 등록 완료: {fileName}.ink");

                UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                AssetDatabase.OpenAsset(obj);
            }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(5);
        GUIStyle style = new GUIStyle(EditorStyles.helpBox);
        style.fontSize = 10;
        GUILayout.Label($"Target: {fileName}.ink\nFolder: {folderName}", style);
    }

    // ★★★ [기능 추가] main.ink에 INCLUDE 구문 추가하는 함수 ★★★
    private void AddToMainInk(string folderName, string fileName)
    {
        // main.ink 경로 (Assets/Datas/main.ink)
        string mainInkPath = Path.Combine(Application.dataPath, "Datas", "main.ink");

        if (!File.Exists(mainInkPath))
        {
            Debug.LogError($"main.ink 파일을 찾을 수 없습니다! 경로를 확인해주세요: {mainInkPath}");
            return;
        }

        // 추가할 구문 만들기 (예: INCLUDE NPC1\NPC1_Day1.ink)
        // 윈도우 스타일(\)을 원하셔서 백슬래시를 사용합니다.
        string includeLine = $"INCLUDE {folderName}\\{fileName}.ink";

        // 기존 내용을 읽어서 이미 있는지 확인
        string allText = File.ReadAllText(mainInkPath);

        // 이미 해당 INCLUDE가 있다면 추가하지 않음
        if (allText.Contains(includeLine))
        {
            Debug.Log("main.ink에 이미 등록되어 있습니다.");
            return;
        }

        // 파일 맨 끝에 추가
        // 파일 끝이 줄바꿈으로 안 끝나있을 수도 있으니 \n을 앞에 붙여서 안전하게 추가
        File.AppendAllText(mainInkPath, "\n" + includeLine);

        Debug.Log($"main.ink에 '{includeLine}' 구문이 추가되었습니다.");
    }
}