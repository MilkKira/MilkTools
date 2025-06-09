using System;
using System.Collections.Generic;

[Serializable]
public class BlendShapeData
{
    public string name;
    public int frameIndex;
    public float weight;

    public List<int> nonZeroVertexIndices; // 非0の頂点インデックス
    public List<SerializableVector3> deltaVertices;
    public List<SerializableVector3> deltaNormals;
    public List<SerializableVector3> deltaTangents;

    public BlendShapeData(string name, int frameIndex, float weight, UnityEngine.Vector3[] vertices, UnityEngine.Vector3[] normals, UnityEngine.Vector3[] tangents)
    {
        this.name = name;
        this.frameIndex = frameIndex;
        this.weight = weight;

        nonZeroVertexIndices = new List<int>();
        deltaVertices = new List<SerializableVector3>();
        deltaNormals = new List<SerializableVector3>();
        deltaTangents = new List<SerializableVector3>();

        // 非0の頂点のみ保存
        for (int i = 0; i < vertices.Length; i++)
        {
            if (vertices[i] != UnityEngine.Vector3.zero || normals[i] != UnityEngine.Vector3.zero || tangents[i] != UnityEngine.Vector3.zero)
            {
                nonZeroVertexIndices.Add(i); // 非0の頂点インデックスを保存
                deltaVertices.Add(new SerializableVector3(vertices[i]));
                deltaNormals.Add(new SerializableVector3(normals[i]));
                deltaTangents.Add(new SerializableVector3(tangents[i]));
            }
        }
    }
}
