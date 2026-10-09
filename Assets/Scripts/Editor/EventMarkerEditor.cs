using UnityEngine;
using UnityEditor;
using System.IO;

[CustomEditor(typeof(EventMarker))]
public class EventMarkerEditor : Editor
{
    private DialogueGraphData _targetGraph;

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

        // ★ 경로 금지 문자(" < > | 탭·줄바꿈 등)가 있으면 Path.Combine이 예외를 던져 인스펙터 아랫부분이 그려지지 않았다
        //    → 경로를 조립하기 전에 검사하고, 걸리면 안내만 띄우고 Ink 버튼을 끈다
        string invalidChars = FindInvalidPathChars(rawNodeName);
        bool pathValid = invalidChars.Length == 0;
        if (!pathValid)
            EditorGUILayout.HelpBox($"Ink Node Name에 파일 이름으로 쓸 수 없는 문자가 있습니다: {invalidChars}\n붙여넣기로 공백·줄바꿈이 섞이지 않았는지 확인하세요.", MessageType.Error);

        // 경로 설정
        string baseDir = Path.Combine(Application.dataPath, "Datas");
        string targetDir = pathValid ? Path.Combine(baseDir, folderName) : "";            // ★ 금지 문자가 있으면 조립하지 않는다
        string fullPath = pathValid ? Path.Combine(targetDir, $"{fileName}.ink") : "";    // ★
        string assetPath = $"Assets/Datas/{folderName}/{fileName}.ink";

        EditorGUI.BeginDisabledGroup(!pathValid); // ★ 경로가 잘못되면 Ink 버튼 비활성화
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
        EditorGUI.EndDisabledGroup(); // ★

        GUILayout.Space(5);
        GUIStyle style = new GUIStyle(EditorStyles.helpBox);
        style.fontSize = 10;
        GUILayout.Label($"Target: {fileName}.ink\nFolder: {folderName}", style);

        // -------------- 자동 이벤트 발동 영역 설정
        GUILayout.Space(10);
        GUILayout.Label("발동 영역", EditorStyles.boldLabel);
        if (marker.triggerZoneSize == Vector2.zero)
        {
            if (GUILayout.Button("사각 발동 영역 만들기", GUILayout.Height(25)))
            {
                Undo.RecordObject(marker, "Create Trigger Zone");
                marker.triggerZoneSize = new Vector2(6f, 4f);
                EditorUtility.SetDirty(marker);
            }
        }
        else if (GUILayout.Button("사각 영역 제거 (원형 반경으로)", GUILayout.Height(25)))
        {
            Undo.RecordObject(marker, "Remove Trigger Zone");
            marker.triggerZoneSize = Vector2.zero;
            marker.triggerZoneOffset = Vector2.zero;
            EditorUtility.SetDirty(marker);
        }

        // --------------- 연출 등장 지점
        GUILayout.Space(10);
        GUILayout.Label("연출 등장 지점", EditorStyles.boldLabel);
        if (GUILayout.Button("+ 등장 지점 추가", GUILayout.Height(25)))
        {
            var go = new GameObject("SummonPoint");
            go.transform.SetParent(marker.transform);
            go.transform.position = marker.transform.position + Vector3.left * 8f; // 기본값: 화면 밖 왼쪽
            go.AddComponent<SummonPointMarker>();
            Undo.RegisterCreatedObjectUndo(go, "Add Summon Point");
            Selection.activeGameObject = go;
        }

        var points = marker.GetComponentsInChildren<SummonPointMarker>();
        if (points.Length > 0)
        {
            EditorGUILayout.HelpBox($"등장 지점 {points.Length}개 — 씬에서 드래그해 위치를 조절하세요.\n저장 시 자동으로 CSV에 기록됩니다.", MessageType.Info);
            foreach (var p in points)
                EditorGUILayout.LabelField($"· {p.npcName} @ {(Vector2)(p.transform.position - marker.transform.position)}");
        }


        // 그래프 대화 에디터 연동
        GUILayout.Space(10);
        GUILayout.Label("대화 그래프 연동", EditorStyles.boldLabel);

        _targetGraph = (DialogueGraphData)EditorGUILayout.ObjectField("대상 그래프", _targetGraph, typeof(DialogueGraphData), false);

        var knots = GraphKeySource.GetKnotNames();
        bool exists = knots.Contains(marker.InkNodeName);

        if (exists)
        {
            EditorGUILayout.HelpBox($"그래프에 '{marker.InkNodeName}' 노드가 있습니다.", MessageType.Info);
        }
        else if (_targetGraph != null && !string.IsNullOrWhiteSpace(marker.InkNodeName))
        {
            EditorGUILayout.HelpBox($"그래프에 '{marker.InkNodeName}' 노드가 없습니다.", MessageType.Warning);
            if (GUILayout.Button("그래프에 시작 노드 생성", GUILayout.Height(28)))
            {
                Undo.RecordObject(_targetGraph, "Create Start Node");
                _targetGraph.CreateStartNodeFrom(marker);
                EditorUtility.SetDirty(_targetGraph);
                AssetDatabase.SaveAssets();
                GraphKeySource.InvalidateCache();
                Debug.Log($"[EventMarker] 그래프에 시작 노드 생성: {marker.InkNodeName} (그래프 창에서 '불러오기'를 눌러 확인)");
            }
        }

        // 기존 knot에서 고르기 (그래프 먼저 만든 경우)
        GUILayout.Space(5);
        // ★ 예전에는 목록에 없는 이름이면 IndexOf가 -1 → 팝업이 0번을 보여주고, 변경 확인 없이 대입해
        //    매 repaint마다 이름이 0번 knot으로 덮어써졌다(직접 입력 불가). 리엘 Day 1 마커가 Day 5 노드가 된 원인
        //    → 목록에 없는 이름은 맨 앞에 "(직접 입력: 이름)"으로 보여주고, 사용자가 팝업을 바꿨을 때만 반영한다
        int current = knots.IndexOf(marker.InkNodeName);
        var options = new System.Collections.Generic.List<string>(knots);
        int offset = 0;
        if (current < 0)
        {
            string shown = string.IsNullOrWhiteSpace(marker.InkNodeName) ? "(비어 있음)" : $"(직접 입력: {marker.InkNodeName})";
            options.Insert(0, shown);
            offset = 1;
        }

        EditorGUI.BeginChangeCheck();
        int picked = EditorGUILayout.Popup("기존 knot에서 선택", current < 0 ? 0 : current + offset, options.ToArray());
        if (EditorGUI.EndChangeCheck())
        {
            int knotIndex = picked - offset;
            if (knotIndex >= 0 && knotIndex < knots.Count && knots[knotIndex] != marker.InkNodeName && !knots[knotIndex].StartsWith("("))
            {
                Undo.RecordObject(marker, "Set Ink Node");
                marker.InkNodeName = knots[knotIndex];
                EditorUtility.SetDirty(marker);
            }
        }
    }

    // ★ 파일 이름에 쓸 수 없는 문자를 보이는 형태로 모아 돌려준다 (없으면 빈 문자열)
    private static string FindInvalidPathChars(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var found = new System.Collections.Generic.List<string>();
        foreach (char c in name)
        {
            if (System.Array.IndexOf(invalid, c) < 0) continue;
            string shown = c switch
            {
                '\t' => "\\t",
                '\n' => "\\n",
                '\r' => "\\r",
                _ => char.IsControl(c) ? $"\\u{(int)c:X4}" : c.ToString(),
            };
            if (!found.Contains(shown)) found.Add(shown);
        }
        return string.Join(" ", found);
    }

    // main.ink에 INCLUDE 구문 추가하는 함수
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

    // EventMarkerEditor.cs — OnSceneGUI 추가 (씬 뷰에서 드래그로 크기 조절)
    private void OnSceneGUI()
    {
        EventMarker marker = (EventMarker)target;
        if (marker.triggerZoneSize.x <= 0f || marker.triggerZoneSize.y <= 0f) return;

        Vector3 center = marker.transform.position + (Vector3)marker.triggerZoneOffset;
        Vector2 half = marker.triggerZoneSize * 0.5f;

        EditorGUI.BeginChangeCheck();
        Vector3 right = Handles.FreeMoveHandle(center + Vector3.right * half.x, 0.25f, Vector3.zero, Handles.DotHandleCap);
        Vector3 up = Handles.FreeMoveHandle(center + Vector3.up * half.y, 0.25f, Vector3.zero, Handles.DotHandleCap);
        Vector3 centerHandle = Handles.FreeMoveHandle(center, 0.3f, Vector3.zero, Handles.RectangleHandleCap);

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(marker, "Edit Trigger Zone");
            marker.triggerZoneOffset = (Vector2)(centerHandle - marker.transform.position);
            Vector3 newCenter = marker.transform.position + (Vector3)marker.triggerZoneOffset;
            marker.triggerZoneSize = new Vector2(
                Mathf.Max(0.5f, Mathf.Abs(right.x - newCenter.x) * 2f),
                Mathf.Max(0.5f, Mathf.Abs(up.y - newCenter.y) * 2f));
            EditorUtility.SetDirty(marker);
        }

        Handles.Label(center + Vector3.up * (half.y + 0.5f), $"발동 영역 {marker.triggerZoneSize.x:F1} x {marker.triggerZoneSize.y:F1}");
    }
}