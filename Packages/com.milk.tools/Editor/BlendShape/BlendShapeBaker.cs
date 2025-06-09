using UnityEngine;
using UnityEditor;
using System;
using System.IO;

public class BlendShapeBakerWindow : EditorWindow
{
    // 対象のSkinnedMeshRendererと新規ブレンドシェイプ名を指定
    private SkinnedMeshRenderer targetRenderer;
    private string newBlendShapeName = "BakedBlendShape";

    [MenuItem("MilkTools/BlendShapeTools/BlendShapeBaker")]
    public static void ShowWindow()
    {
        GetWindow<BlendShapeBakerWindow>("BlendShapeBaker");
    }

    private void OnGUI()
    {
        GUILayout.Label("BlendShapeBaker", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        targetRenderer = EditorGUILayout.ObjectField("SkinnedMeshRenderer", targetRenderer, typeof(SkinnedMeshRenderer), true) as SkinnedMeshRenderer;
        newBlendShapeName = EditorGUILayout.TextField("New BlendShapeName", newBlendShapeName);

        EditorGUILayout.Space();
        if (GUILayout.Button("Bake"))
        {
            if (targetRenderer == null)
            {
                EditorUtility.DisplayDialog("Error", "Skinned Mesh Rendererが設定されていません。", "OK");
                return;
            }
            if (string.IsNullOrEmpty(newBlendShapeName))
            {
                EditorUtility.DisplayDialog("Error", "新規BlendShape名を入力してください。", "OK");
                return;
            }
            BakeBlendShape();
        }
    }

    /// <summary>
    /// 現在の各ブレンドシェイプのウェイトによる変位を合算し、
    /// 新規ブレンドシェイプとして追加した新規メッシュを作成する
    /// </summary>
    private void BakeBlendShape()
    {
        Mesh originalMesh = targetRenderer.sharedMesh;
        if (originalMesh == null)
        {
            EditorUtility.DisplayDialog("Error", "対象のSkinned Mesh Rendererにメッシュが設定されていません。", "OK");
            return;
        }

        // オリジナルメッシュを複製してバックアップ用の新規メッシュを作成する
        Mesh newMesh = Instantiate(originalMesh);

        // 日時スタンプ（yyyyMMddHHmmss形式）
        string timeStamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        // 新規メッシュの名前をタイムスタンプのみとする
        newMesh.name = timeStamp;

        int vertexCount = originalMesh.vertexCount;

        // 各頂点ごとの変位（初期値はゼロ）
        Vector3[] deltaVertices = new Vector3[vertexCount];
        Vector3[] deltaNormals = new Vector3[vertexCount];
        Vector3[] deltaTangents = new Vector3[vertexCount];

        for (int i = 0; i < vertexCount; i++)
        {
            deltaVertices[i] = Vector3.zero;
            deltaNormals[i] = Vector3.zero;
            deltaTangents[i] = Vector3.zero;
        }

        // オリジナルメッシュの全ブレンドシェイプチャンネルを走査
        int blendShapeCount = originalMesh.blendShapeCount;
        for (int shapeIndex = 0; shapeIndex < blendShapeCount; shapeIndex++)
        {
            // 現在のレンダラー上のウェイトを取得
            float currentWeight = targetRenderer.GetBlendShapeWeight(shapeIndex);
            if (Mathf.Approximately(currentWeight, 0f))
                continue;

            int frameCount = originalMesh.GetBlendShapeFrameCount(shapeIndex);
            if (frameCount <= 0)
                continue;

            // 通常は各ブレンドシェイプは1フレームで定義されているので、0番目のフレームを使用
            float frameWeight = originalMesh.GetBlendShapeFrameWeight(shapeIndex, 0);
            Vector3[] shapeDeltaVertices = new Vector3[vertexCount];
            Vector3[] shapeDeltaNormals = new Vector3[vertexCount];
            Vector3[] shapeDeltaTangents = new Vector3[vertexCount];

            originalMesh.GetBlendShapeFrameVertices(shapeIndex, 0, shapeDeltaVertices, shapeDeltaNormals, shapeDeltaTangents);

            // 現在のウェイトとフレームのウェイトとの比率で変位をスケール
            float factor = currentWeight / frameWeight;

            for (int i = 0; i < vertexCount; i++)
            {
                deltaVertices[i] += shapeDeltaVertices[i] * factor;
                deltaNormals[i] += shapeDeltaNormals[i] * factor;
                deltaTangents[i] += shapeDeltaTangents[i] * factor;
            }
        }

        // 新規ブレンドシェイプとして追加（フレームウェイトは100とする）
        newMesh.AddBlendShapeFrame(newBlendShapeName, 100f, deltaVertices, deltaNormals, deltaTangents);

        // Assets/BlendShapeBaker/Mesh フォルダに保存
        string baseFolder = "Assets/BlendShapeBaker";
        string meshFolder = baseFolder + "/Mesh";
        if (!AssetDatabase.IsValidFolder(baseFolder))
        {
            AssetDatabase.CreateFolder("Assets", "BlendShapeBaker");
        }
        if (!AssetDatabase.IsValidFolder(meshFolder))
        {
            AssetDatabase.CreateFolder(baseFolder, "Mesh");
        }

        // ファイルパスをタイムスタンプのみで構築
        string assetPath = Path.Combine(meshFolder, timeStamp + ".asset");

        AssetDatabase.CreateAsset(newMesh, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Skinned Mesh Rendererに新しいメッシュを割り当てる
        targetRenderer.sharedMesh = newMesh;

        // メッシュ生成完了後のダイアログ (英語 → 日本語)
        bool resetSlider = EditorUtility.DisplayDialog(
            "Mesh creation completed",
            "Mesh creation completed.\nA new mesh has been created and a new BlendShape has been added.\n" +
            "Save Path: " + assetPath + "\n\n" +
            "Would you like to reset all blend shape sliders to 0 and set only the new blend shape to 100%?\n\n" +
            "ブレンドシェイプスライダーをリセットして、新規ブレンドシェイプのみを100%に設定しますか？",
            "Yes",  // ボタン1
            "No"    // ボタン2
        );

        if (resetSlider)
        {
            // 新規メッシュに含まれるブレンドシェイプをリセットし、
            // 新規ブレンドシェイプのみ100%に設定する
            int newBlendShapeCount = targetRenderer.sharedMesh.blendShapeCount;
            for (int i = 0; i < newBlendShapeCount; i++)
            {
                string bsName = targetRenderer.sharedMesh.GetBlendShapeName(i);
                if (bsName == newBlendShapeName)
                {
                    targetRenderer.SetBlendShapeWeight(i, 100f);
                }
                else
                {
                    targetRenderer.SetBlendShapeWeight(i, 0f);
                }
            }
        }
    }
}
