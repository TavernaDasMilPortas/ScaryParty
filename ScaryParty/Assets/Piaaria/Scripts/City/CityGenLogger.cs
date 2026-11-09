using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class CityGenLog
{
    public List<BlockLog> blocks = new List<BlockLog>();
}

[System.Serializable]
public class BlockLog
{
    public int blockIndex;
    public float area;
    public int vertexCount;
    public List<EdgeLog> edges = new List<EdgeLog>();
}

[System.Serializable]
public class EdgeLog
{
    public int edgeIndex;
    public float length;
    public float margin1;
    public float margin2;
    public List<AttemptLog> attempts = new List<AttemptLog>();
}

[System.Serializable]
public class AttemptLog
{
    public int pass;
    public float distanceAlongEdge;
    public float requestedWidth;
    public float requestedDepth;
    public string result;
    public float finalWidth;
    public float finalDepth;
    public string failReason;
}

public static class CityGenLogger
{
    private static CityGenLog currentLog;
    private static BlockLog currentBlock;
    private static EdgeLog currentEdge;

    public static void StartLog()
    {
        currentLog = new CityGenLog();
    }

    public static void StartBlock(int index, float area, int vertexCount)
    {
        if (currentLog == null) return;
        currentBlock = new BlockLog { blockIndex = index, area = area, vertexCount = vertexCount };
        currentLog.blocks.Add(currentBlock);
    }

    public static void StartEdge(int index, float length, float m1, float m2)
    {
        if (currentBlock == null) return;
        currentEdge = new EdgeLog { edgeIndex = index, length = length, margin1 = m1, margin2 = m2 };
        currentBlock.edges.Add(currentEdge);
    }

    public static void LogAttempt(int pass, float dist, float reqW, float reqD, bool success, float finW, float finD, string reason = "")
    {
        if (currentEdge == null) return;
        currentEdge.attempts.Add(new AttemptLog
        {
            pass = pass,
            distanceAlongEdge = dist,
            requestedWidth = reqW,
            requestedDepth = reqD,
            result = success ? "Success" : "Failed",
            finalWidth = finW,
            finalDepth = finD,
            failReason = reason
        });
    }

    public static void SaveLog()
    {
        if (currentLog == null) return;
        string json = JsonUtility.ToJson(currentLog, true);
        string path = Path.Combine(Application.dataPath, "../CityGenerationLog.json");
        File.WriteAllText(path, json);
        Debug.Log($"City Generation Log saved to: {path}");
        currentLog = null;
    }
}
