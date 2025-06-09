using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Linq;

public class BlendShapeInserter : EditorWindow
{
    private SkinnedMeshRenderer selectedRenderer;
    private string blendShapeNames = "Sample1\nSample2\nSample3"; // BlendShape Namesの初期値

    [MenuItem("MilkTools/BlendShapeTools/BlendShapeInserter")]
    public static void ShowWindow()
    {
        GetWindow<BlendShapeInserter>("BlendShapeInserter");
    }

    private void OnGUI()
    {
        selectedRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Skinned Mesh Renderer", selectedRenderer, typeof(SkinnedMeshRenderer), true);

        GUILayout.Label("BlendShape Names (one per line):", EditorStyles.boldLabel);
        blendShapeNames = EditorGUILayout.TextArea(blendShapeNames, GUILayout.Height(100)); // 改行可能なテキストエリア

        if (GUILayout.Button("Apply Add BlendShapes"))
        {
            AddMultipleBlendShapes();
        }
    }

    private void AddMultipleBlendShapes()
    {
        if (selectedRenderer == null)
        {
            EditorUtility.DisplayDialog("Error", "No Skinned Mesh Renderer selected. Please select a target Skinned Mesh Renderer.\nスキンメッシュレンダラーが選択されていません。ターゲットのスキンメッシュレンダラーを選択してください。", "OK");
            return;
        }

        Mesh selectedMesh = selectedRenderer.sharedMesh;

        if (selectedMesh == null)
        {
            Debug.LogError("The selected SkinnedMeshRenderer does not have a mesh assigned.");
            return;
        }

        // 入力テキストからBlendShape名リストを作成
        string[] blendShapeList = blendShapeNames
            .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
            .Select(n => n.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();

        // 既存のBlendShape名を取得
        string[] existingBlendShapes = new string[selectedMesh.blendShapeCount];
        for (int i = 0; i < selectedMesh.blendShapeCount; i++)
        {
            existingBlendShapes[i] = selectedMesh.GetBlendShapeName(i);
        }

        // 追加対象：既存に含まれないBlendShapeのみ追加
        var blendShapesToAdd = blendShapeList.Except(existingBlendShapes).ToArray();
        // スキップ対象：既に存在しているBlendShape名
        var duplicateNames = blendShapeList.Intersect(existingBlendShapes).ToArray();

        // 新しいメッシュを複製し、既存のBlendShapeはコピー
        Mesh newMesh = Instantiate(selectedMesh);
        newMesh.ClearBlendShapes();
        CopyExistingBlendShapes(selectedMesh, newMesh);

        // 重複していないBlendShapeのみ追加
        foreach (var name in blendShapesToAdd)
        {
            AddNewBlendShapeWithoutDeformation(newMesh, name);
        }

        selectedRenderer.sharedMesh = newMesh;

        // アセットとして保存
        string assetsFolder = "Assets/BlendShapeInserter/Mesh";
        if (!Directory.Exists(assetsFolder))
        {
            Directory.CreateDirectory(assetsFolder);
        }

        string currentTime = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string newPath = $"{assetsFolder}/{currentTime}.asset";

        AssetDatabase.CreateAsset(newMesh, newPath);
        AssetDatabase.SaveAssets();

        Debug.Log($"Multiple BlendShapes added to mesh for renderer '{selectedRenderer.name}' and saved as '{newPath}'.");

        // 処理完了後、スキップされたBlendShapeがあればポップアップで表示
        if (duplicateNames.Length > 0)
        {
            string duplicates = string.Join(", ", duplicateNames);
            EditorUtility.DisplayDialog("BlendShapes Skipped", $"The following BlendShapes were skipped because they already exist: {duplicates}\n上記のBlendShapeは既に存在しているためスキップされました", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("BlendShapes Added", "BlendShapes were added successfully.", "OK");
        }
    }

    private void CopyExistingBlendShapes(Mesh originalMesh, Mesh newMesh)
    {
        try
        {
            for (int i = 0; i < originalMesh.blendShapeCount; i++)
            {
                string shapeName = originalMesh.GetBlendShapeName(i);
                int frameCount = originalMesh.GetBlendShapeFrameCount(i);

                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    float weight = originalMesh.GetBlendShapeFrameWeight(i, frameIndex);
                    Vector3[] deltaVertices = new Vector3[originalMesh.vertexCount];
                    Vector3[] deltaNormals = new Vector3[originalMesh.vertexCount];
                    Vector3[] deltaTangents = new Vector3[originalMesh.vertexCount];

                    originalMesh.GetBlendShapeFrameVertices(i, frameIndex, deltaVertices, deltaNormals, deltaTangents);
                    newMesh.AddBlendShapeFrame(shapeName, weight, deltaVertices, deltaNormals, deltaTangents);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error copying BlendShape: {e.Message}");
        }
    }

    private void AddNewBlendShapeWithoutDeformation(Mesh mesh, string blendShapeName)
    {
        Vector3[] vertices = new Vector3[mesh.vertexCount];
        Vector3[] normals = new Vector3[mesh.vertexCount];
        Vector3[] tangents = new Vector3[mesh.vertexCount];

        // 各頂点に対して、変形が発生しないようにゼロベクトルを設定
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = Vector3.zero;
            normals[i] = Vector3.zero;
            tangents[i] = Vector3.zero;
        }

        mesh.AddBlendShapeFrame(blendShapeName, 100.0f, vertices, normals, tangents);
    }
}
