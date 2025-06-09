using UnityEngine;
using UnityEditor;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Collections.Generic;
using System;

public class BlendShapeImporter : EditorWindow
{
    private SkinnedMeshRenderer selectedRenderer;

    [MenuItem("MilkTools/BlendShapeTools/BlendShapeImporter/Import BlendShapes")]
    public static void ShowWindow()
    {
        GetWindow<BlendShapeImporter>("BlendShape Importer");
    }

    private void OnGUI()
    {
        selectedRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Target Skinned Mesh Renderer", selectedRenderer, typeof(SkinnedMeshRenderer), true);

        if (GUILayout.Button("Import BlendShapes (Optimized Binary)"))
        {
            ImportBlendShapes();
        }
    }

    private void ImportBlendShapes()
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

        Mesh originalMesh = selectedRenderer.sharedMesh;

        if (originalMesh == null)
        {
            Debug.LogError("The selected SkinnedMeshRenderer does not have a mesh assigned.");
            return;
        }

        string path = EditorUtility.OpenFilePanel("Load BlendShape Data", "", "dat");

        if (!string.IsNullOrEmpty(path))
        {
            using (FileStream file = File.Open(path, FileMode.Open))
            {
                BinaryFormatter bf = new BinaryFormatter();
                List<BlendShapeData> blendShapeDataList = (List<BlendShapeData>)bf.Deserialize(file);

                HashSet<string> existingBlendShapes = new HashSet<string>();
                for (int i = 0; i < originalMesh.blendShapeCount; i++)
                {
                    existingBlendShapes.Add(originalMesh.GetBlendShapeName(i));
                }

                List<string> addedBlendShapes = new List<string>();

                // 新しいメッシュを作成（元のメッシュの複製）
                Mesh newMesh = Instantiate(originalMesh);

                foreach (var blendShapeData in blendShapeDataList)
                {
                    // 既存のBlendShapeはスキップ
                    if (existingBlendShapes.Contains(blendShapeData.name))
                    {
                        continue;
                    }

                    // 新規にBlendShapeを追加
                    Vector3[] deltaVertices = new Vector3[newMesh.vertexCount];
                    Vector3[] deltaNormals = new Vector3[newMesh.vertexCount];
                    Vector3[] deltaTangents = new Vector3[newMesh.vertexCount];

                    // 非0の頂点に変位を適用
                    for (int j = 0; j < blendShapeData.nonZeroVertexIndices.Count; j++)
                    {
                        int index = blendShapeData.nonZeroVertexIndices[j];
                        deltaVertices[index] = blendShapeData.deltaVertices[j].ToVector3();
                        deltaNormals[index] = blendShapeData.deltaNormals[j].ToVector3();
                        deltaTangents[index] = blendShapeData.deltaTangents[j].ToVector3();
                    }

                    newMesh.AddBlendShapeFrame(blendShapeData.name, blendShapeData.weight, deltaVertices, deltaNormals, deltaTangents);

                    // 新規に追加したBlendShape名を保存
                    addedBlendShapes.Add(blendShapeData.name);
                }

                // 新しいメッシュの名前と保存パスの生成
                string rendererName = RemoveInvalidFileNameChars(selectedRenderer.name);
                string currentTime = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string newMeshName = $"{rendererName}_ImportBlendShape_{currentTime}";
                string newPath = $"Assets/{newMeshName}.asset";

                // 新しいメッシュを保存
                AssetDatabase.CreateAsset(newMesh, newPath);
                AssetDatabase.SaveAssets();

                // SkinnedMeshRendererに新しいメッシュを適用
                selectedRenderer.sharedMesh = newMesh;

                // メッセージの表示処理
                if (addedBlendShapes.Count > 0)
                {
                    // 新規に追加されたBlendShapeがある場合
                    string addedNames = string.Join(", ", addedBlendShapes);
                    string message = "BlendShapes imported successfully!\n" +
                                     "以下のBlendShapeがインポートされました。\n" +
                                     addedNames;

                    EditorUtility.DisplayDialog("Import Complete", message, "OK");
                }
                else
                {
                    // 新規に追加されたBlendShapeがない場合
                    EditorUtility.DisplayDialog(
                        "No BlendShapes Added", 
                        "No new BlendShapes were added.\n" +
                        "新しいBlendShapeは追加されませんでした。", 
                        "OK"
                    );
                }

                Debug.Log($"BlendShapes import process finished. New mesh saved as: {newPath}");
            }
        }
    }

    // 無効なファイル名の文字を削除する関数
    private string RemoveInvalidFileNameChars(string filename)
    {
        return string.Concat(filename.Split(Path.GetInvalidFileNameChars()));
    }
}
