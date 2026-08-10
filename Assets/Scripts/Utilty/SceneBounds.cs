using UnityEngine;

// SceneBounds.cs (신규) — 씬마다 빈 오브젝트 하나에 부착
public class SceneBounds : MonoBehaviour
{
    public Vector2 min = new Vector2(-20, -10);
    public Vector2 max = new Vector2(20, 10);
    public float wallThickness = 1f;

    private void Awake()
    {
        CreateWall("Wall_Left", new Vector2(min.x - wallThickness / 2f, (min.y + max.y) / 2f), new Vector2(wallThickness, max.y - min.y + wallThickness * 2));
        CreateWall("Wall_Right", new Vector2(max.x + wallThickness / 2f, (min.y + max.y) / 2f), new Vector2(wallThickness, max.y - min.y + wallThickness * 2));
        CreateWall("Wall_Bottom", new Vector2((min.x + max.x) / 2f, min.y - wallThickness / 2f), new Vector2(max.x - min.x + wallThickness * 2, wallThickness));
        CreateWall("Wall_Top", new Vector2((min.x + max.x) / 2f, max.y + wallThickness / 2f), new Vector2(max.x - min.x + wallThickness * 2, wallThickness));
    }

    private void CreateWall(string name, Vector2 center, Vector2 size)
    {
        GameObject wall = new GameObject(name);
        wall.transform.SetParent(transform);
        wall.transform.position = center;
        wall.layer = gameObject.layer; // Ground와 같은 레이어로 두시면 별도 설정 없이 바로 막힙니다
        wall.AddComponent<BoxCollider2D>().size = size;
    }

    private void Start() => SceneBoundsManager.Instance?.RegisterBounds(this);

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Vector3 center = new Vector3((min.x + max.x) / 2f, (min.y + max.y) / 2f, 0);
        Vector3 size = new Vector3(max.x - min.x, max.y - min.y, 0);
        Gizmos.DrawWireCube(center, size);
    }
}