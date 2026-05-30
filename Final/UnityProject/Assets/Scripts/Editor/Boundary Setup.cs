// 放在 Assets/Scripts/Editor/，執行一次即可，不需要留在專案裡
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public static class BoundaryWallCreator
{
    [MenuItem("Tools/Phase2/Create Boundary Walls Around Selected Plane")]
    static void Create()
    {
        GameObject plane = Selection.activeGameObject;
        if (plane == null) { Debug.LogError("Select the Plane first."); return; }

        Bounds b = plane.GetComponent<Renderer>().bounds;
        float wallH = 3f, wallT = 0.2f;
        float cx = b.center.x, cz = b.center.z;
        float ex = b.extents.x, ez = b.extents.z;

        // (center, scaleX, scaleZ, offsetX, offsetZ)
        (Vector3 pos, Vector3 scale)[] walls = {
            (new Vector3(cx,          wallH/2, cz + ez), new Vector3(b.size.x + wallT*2, wallH, wallT)),
            (new Vector3(cx,          wallH/2, cz - ez), new Vector3(b.size.x + wallT*2, wallH, wallT)),
            (new Vector3(cx + ex,     wallH/2, cz),      new Vector3(wallT, wallH, b.size.z)),
            (new Vector3(cx - ex,     wallH/2, cz),      new Vector3(wallT, wallH, b.size.z)),
        };

        string tag = "RVOObstacle";
        // Ensure tag exists
        foreach (var (pos, scale) in walls)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "BoundaryWall";
            go.transform.position = pos;
            go.transform.localScale = scale;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.NavigationStatic);
            try { go.tag = tag; } catch { Debug.LogWarning($"Tag '{tag}' not found — create it first."); }
        }
        Debug.Log("Boundary walls created. Remember to re-bake NavMesh.");
    }
}
#endif