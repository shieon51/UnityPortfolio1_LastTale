using System.Collections.Generic;
using UnityEngine;
using System;

// ³ëµå: ¾À ÀÌ¸§
public class SceneNode
{
    public string SceneName;
    public Dictionary<int, PortalEdge> Edges = new Dictionary<int, PortalEdge>();
}

public class PortalEdge
{
    public int PortalID;             // ÇöÀç ¾À Æ÷Å» ID
    public string TargetSceneName;   // ¸ñÇ¥ ¾À
    public int TargetPortalID;       // ¸ñÇ¥ ¾À Æ÷Å» ID
}

// ÀüÃ¼ ±×·¡ÇÁ
public class PortalGraph
{
    public Dictionary<string, SceneNode> Nodes = new Dictionary<string, SceneNode>();

    public void AddEdge(string fromScene, int fromPortal, string toScene, int toPortal)
    {
        if (!Nodes.ContainsKey(fromScene))
            Nodes[fromScene] = new SceneNode { SceneName = fromScene };
        if (!Nodes.ContainsKey(toScene))
            Nodes[toScene] = new SceneNode { SceneName = toScene };

        Nodes[fromScene].Edges[fromPortal] = new PortalEdge
        {
            PortalID = fromPortal,
            TargetSceneName = toScene,
            TargetPortalID = toPortal
        };
    }

    public PortalEdge GetTarget(string fromScene, int portalID)
    {
        return Nodes[fromScene].Edges[portalID];
    }
}

public class PortalManager : Singleton<PortalManager>
{
    private Dictionary<int, SceneNode> sceneGraph = new Dictionary<int, SceneNode>();


    private void Start()
    {
        
    }

    public void LoadPortalData(string csvPath)
    {
        // CSV ÆÄ½Ì ¡æ SceneNode¿Í PortalEdge ±¸¼º
        // ¿¹: sceneGraph[1] = BeginnerTown
        // sceneGraph[1].Edges.Add(new PortalEdge { ... });



    }


}
