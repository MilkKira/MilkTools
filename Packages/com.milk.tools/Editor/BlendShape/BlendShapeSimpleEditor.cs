using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System.IO;

public class BlendShapeSimpleEditor : EditorWindow
{
    private SkinnedMeshRenderer skinnedMeshRenderer;
    private Dictionary<int, float> blendShapeValues = new Dictionary<int, float>();
    private Vector2 leftScrollPosition;
    private Vector2 rightScrollPosition;
    private int activeBlendShapeIndex = -1;

    [MenuItem("MilkTools/BlendShapeTools/BlendShapeSimpleEditor")]
    public static void ShowWindow()
    {
        GetWindow<BlendShapeSimpleEditor>("BlendShapeSimpleEditor");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("BlendShapeSimpleEditor", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        // Left Panel: Skinned Mesh Renderer and Active BlendShape Selection
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.4f));

        skinnedMeshRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("SkinnedMeshRenderer", skinnedMeshRenderer, typeof(SkinnedMeshRenderer), true);

        if (skinnedMeshRenderer == null || skinnedMeshRenderer.sharedMesh == null)
        {
            EditorGUILayout.HelpBox("Please assign a valid SkinnedMeshRenderer with a Mesh.", MessageType.Warning);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            return;
        }

        Mesh mesh = skinnedMeshRenderer.sharedMesh;

        EditorGUILayout.LabelField("Please select the BlendShape", EditorStyles.boldLabel);
        leftScrollPosition = EditorGUILayout.BeginScrollView(leftScrollPosition);

        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(mesh.GetBlendShapeName(i), GUILayout.Width(200)))
            {
                SetActiveBlendShape(i, mesh);
            }

            if (activeBlendShapeIndex == i)
            {
                GUILayout.Label("Active", GUILayout.Width(45));
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        // Right Panel: BlendShape Adjustments
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.6f));

        if (activeBlendShapeIndex != -1)
        {
            EditorGUILayout.LabelField("BlendShape Adjustments", EditorStyles.boldLabel);
            rightScrollPosition = EditorGUILayout.BeginScrollView(rightScrollPosition);

            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                if (!blendShapeValues.ContainsKey(i))
                {
                    blendShapeValues[i] = skinnedMeshRenderer.GetBlendShapeWeight(i);
                }

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(mesh.GetBlendShapeName(i), GUILayout.Width(200));
                float newValue = EditorGUILayout.Slider(blendShapeValues[i], 0f, 100f);
                if (!Mathf.Approximately(newValue, blendShapeValues[i]))
                {
                    blendShapeValues[i] = newValue;
                    skinnedMeshRenderer.SetBlendShapeWeight(i, newValue);
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Apply to Active BlendShape"))
            {
                ApplyActiveBlendShape(mesh);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Select an Active BlendShape to edit.", MessageType.Info);
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    private void SetActiveBlendShape(int index, Mesh mesh)
    {
        activeBlendShapeIndex = index;
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            float weight = (i == index) ? 100f : 0f;
            blendShapeValues[i] = weight;
            skinnedMeshRenderer.SetBlendShapeWeight(i, weight);
        }
    }

    private void ApplyActiveBlendShape(Mesh mesh)
    {
        if (activeBlendShapeIndex == -1 || skinnedMeshRenderer == null)
        {
            Debug.LogError("No active BlendShape selected or SkinnedMeshRenderer is null.");
            return;
        }

        Vector3[] deltaVertices = new Vector3[mesh.vertexCount];
        Vector3[] deltaNormals = new Vector3[mesh.vertexCount];
        Vector3[] deltaTangents = new Vector3[mesh.vertexCount];

        // Reset deltas for the target BlendShape
        for (int i = 0; i < deltaVertices.Length; i++)
        {
            deltaVertices[i] = Vector3.zero;
            deltaNormals[i] = Vector3.zero;
            deltaTangents[i] = Vector3.zero;
        }

        // Blend each selected BlendShape
        foreach (var kvp in blendShapeValues)
        {
            int blendShapeIndex = kvp.Key;
            float weight = kvp.Value / 100f;

            Vector3[] tempDeltaVertices = new Vector3[mesh.vertexCount];
            Vector3[] tempDeltaNormals = new Vector3[mesh.vertexCount];
            Vector3[] tempDeltaTangents = new Vector3[mesh.vertexCount];

            mesh.GetBlendShapeFrameVertices(blendShapeIndex, 0, tempDeltaVertices, tempDeltaNormals, tempDeltaTangents);

            for (int i = 0; i < deltaVertices.Length; i++)
            {
                deltaVertices[i] += tempDeltaVertices[i] * weight;
                deltaNormals[i] += tempDeltaNormals[i] * weight;
                deltaTangents[i] += tempDeltaTangents[i] * weight;
            }
        }

        // Create a new mesh to modify the BlendShape
        Mesh newMesh = new Mesh();
        newMesh.name = mesh.name;
        newMesh.vertices = mesh.vertices;
        newMesh.normals = mesh.normals;
        newMesh.tangents = mesh.tangents;
        newMesh.uv = mesh.uv;
        newMesh.triangles = mesh.triangles;

        // Copy subMesh information
        newMesh.subMeshCount = mesh.subMeshCount;
        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            newMesh.SetTriangles(mesh.GetTriangles(i), i);
        }

        // Copy skinning information
        newMesh.bindposes = mesh.bindposes;
        newMesh.boneWeights = mesh.boneWeights;

        // Add all BlendShapes, preserving their original order
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            if (i == activeBlendShapeIndex)
            {
                // Overwrite the active BlendShape with the new deltas
                newMesh.AddBlendShapeFrame(mesh.GetBlendShapeName(i), 100f, deltaVertices, deltaNormals, deltaTangents);
            }
            else
            {
                // Copy existing BlendShapes
                Vector3[] tempDeltaVertices = new Vector3[mesh.vertexCount];
                Vector3[] tempDeltaNormals = new Vector3[mesh.vertexCount];
                Vector3[] tempDeltaTangents = new Vector3[mesh.vertexCount];
                mesh.GetBlendShapeFrameVertices(i, 0, tempDeltaVertices, tempDeltaNormals, tempDeltaTangents);
                newMesh.AddBlendShapeFrame(mesh.GetBlendShapeName(i), 100f, tempDeltaVertices, tempDeltaNormals, tempDeltaTangents);
            }
        }

        // Save the new mesh to Assets/BlendShapeSimpleEditor/Mesh folder
        string folderPath = "Assets/BlendShapeSimpleEditor/Mesh";
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string filePath = Path.Combine(folderPath, System.DateTime.Now.ToString("yyyyMMddHHmmss") + ".mesh");
        AssetDatabase.CreateAsset(newMesh, filePath);
        AssetDatabase.SaveAssets();

        // Assign the new mesh back to the SkinnedMeshRenderer
        skinnedMeshRenderer.sharedMesh = newMesh;

        // Set active BlendShape to 100% and others to 0%
        for (int i = 0; i < newMesh.blendShapeCount; i++)
        {
            float weight = (i == activeBlendShapeIndex) ? 100f : 0f;
            blendShapeValues[i] = weight;
            skinnedMeshRenderer.SetBlendShapeWeight(i, weight);
        }

        Debug.Log($"BlendShape '{mesh.GetBlendShapeName(activeBlendShapeIndex)}' updated and saved to {filePath}.");
    }
}

