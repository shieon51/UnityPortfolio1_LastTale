using UnityEngine;
using UnityEditor;

// Assets/Scripts/Editor/SceneBoundsEditor.cs (신규): 이벤트 마커처럼 핸들로 직접 끌 수 있게
[CustomEditor(typeof(SceneBounds))]
public class SceneBoundsEditor : Editor
{
    private void OnSceneGUI()
    {
        var bounds = (SceneBounds)target;

        EditorGUI.BeginChangeCheck();
        Vector3 newMin = Handles.PositionHandle(new Vector3(bounds.min.x, bounds.min.y, 0), Quaternion.identity);
        Vector3 newMax = Handles.PositionHandle(new Vector3(bounds.max.x, bounds.max.y, 0), Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(bounds, "Adjust Scene Bounds");
            bounds.min = new Vector2(newMin.x, newMin.y);
            bounds.max = new Vector2(newMax.x, newMax.y);
        }
    }
}