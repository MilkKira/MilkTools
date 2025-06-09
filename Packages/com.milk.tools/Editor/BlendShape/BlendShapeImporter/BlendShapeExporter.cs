using UnityEngine;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections.Generic;

public class BlendShapeExporter : EditorWindow
{
    private SkinnedMeshRenderer selectedRenderer;
    private bool[] blendShapeSelections; // 各BlendShapeの選択状態を保持する配列
    private Vector2 scrollPosition; // スクロールの位置を保持

    [MenuItem("MilkTools/BlendShapeTools/BlendShapeImporter/Export BlendShapes")]
    public static void ShowWindow()
    {
        GetWindow<BlendShapeExporter>("BlendShape Exporter");
    }

    private void OnGUI()
    {
        // SkinnedMeshRendererの選択
        selectedRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Target Skinned Mesh Renderer", selectedRenderer, typeof(SkinnedMeshRenderer), true);

        // SkinnedMeshRendererが選択されている場合にBlendShapeのリストを表示
        if (selectedRenderer != null && selectedRenderer.sharedMesh != null)
        {
            Mesh selectedMesh = selectedRenderer.sharedMesh;

            if (blendShapeSelections == null || blendShapeSelections.Length != selectedMesh.blendShapeCount)
            {
                // BlendShapeの数に合わせて配列を初期化
                blendShapeSelections = new bool[selectedMesh.blendShapeCount];
            }

            EditorGUILayout.LabelField("Select BlendShapes to export:");
            EditorGUILayout.Space();

            // スクロールビューを開始
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            // 各BlendShapeの名前をリスト表示し、チェックボックスで選択できるようにする
            for (int i = 0; i < selectedMesh.blendShapeCount; i++)
            {
                string blendShapeName = selectedMesh.GetBlendShapeName(i);
                blendShapeSelections[i] = EditorGUILayout.Toggle(blendShapeName, blendShapeSelections[i]);
            }

            // スクロールビューを終了
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();

            if (GUILayout.Button("Export Selected BlendShapes"))
            {
                ExportSelectedBlendShapes();
            }
        }
        else
        {
            EditorGUILayout.LabelField("Please select a Skinned Mesh Renderer.");
        }
    }

    private void ExportSelectedBlendShapes()
    {
        // SkinnedMeshRendererが選択されていない場合、エラーメッセージを表示
        if (selectedRenderer == null)
        {
            EditorUtility.DisplayDialog(
                "Error", 
                "No Skinned Mesh Renderer selected. Please select a target Skinned Mesh Renderer.\n" +
                "スキンメッシュレンダラーが選択されていません。ターゲットのスキンメッシュレンダラーを選択してください。", 
                "OK"
            );
            return;
        }

        Mesh selectedMesh = selectedRenderer.sharedMesh;

        if (selectedMesh == null)
        {
            Debug.LogError("The selected SkinnedMeshRenderer does not have a mesh assigned.");
            return;
        }

        List<BlendShapeData> blendShapeDataList = new List<BlendShapeData>();

        for (int i = 0; i < selectedMesh.blendShapeCount; i++)
        {
            // チェックボックスで選択されているBlendShapeのみをエクスポート
            if (blendShapeSelections[i])
            {
                string blendShapeName = selectedMesh.GetBlendShapeName(i);
                int frameCount = selectedMesh.GetBlendShapeFrameCount(i);

                for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
                {
                    float weight = selectedMesh.GetBlendShapeFrameWeight(i, frameIndex);
                    Vector3[] deltaVertices = new Vector3[selectedMesh.vertexCount];
                    Vector3[] deltaNormals = new Vector3[selectedMesh.vertexCount];
                    Vector3[] deltaTangents = new Vector3[selectedMesh.vertexCount];

                    selectedMesh.GetBlendShapeFrameVertices(i, frameIndex, deltaVertices, deltaNormals, deltaTangents);

                    BlendShapeData blendShapeData = new BlendShapeData(blendShapeName, frameIndex, weight, deltaVertices, deltaNormals, deltaTangents);

                    blendShapeDataList.Add(blendShapeData);
                }
            }
        }

        if (blendShapeDataList.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "No BlendShapes Selected", 
                "No BlendShapes were selected for export.\n" +
                "エクスポートするBlendShapeが選択されていません。", 
                "OK"
            );
            return;
        }

        string path = EditorUtility.SaveFilePanel("Save BlendShape Data", "", "blendshape_data_selected.dat", "dat");

        if (!string.IsNullOrEmpty(path))
        {
            using (FileStream file = File.Create(path))
            {
                BinaryFormatter bf = new BinaryFormatter();
                bf.Serialize(file, blendShapeDataList);
            }
            EditorUtility.DisplayDialog(
                "Export Complete", 
                "Selected BlendShapes exported successfully!\n" +
                "選択されたBlendShapeが正常にエクスポートされました。", 
                "OK"
            );
            Debug.Log("Selected BlendShapes exported to: " + path);
        }
    }
}
