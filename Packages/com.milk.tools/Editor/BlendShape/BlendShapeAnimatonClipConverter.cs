using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using System;

public class BlendShapeAnimatonClipConverter : EditorWindow
{
    // 対象のSkinnedMeshRendererとAnimationClip
    private SkinnedMeshRenderer targetSMR;
    private AnimationClip targetClip;

    // 新規作成するBlendShape名（デフォルトは AnimationClip 名）
    private string blendShapeName = "";

    [MenuItem("MilkTools/BlendShapeTools/BlendShapeAnimatonClipConverter")]
    public static void ShowWindow()
    {
        GetWindow<BlendShapeAnimatonClipConverter>("BlendShapeAnimatonClipConverter");
    }

    private void OnGUI()
    {
        // 簡単なタイトルのみ表示
        EditorGUILayout.LabelField("Convert entire AnimationClip to a BlendShape", EditorStyles.boldLabel);

        targetSMR = EditorGUILayout.ObjectField("Skinned Mesh Renderer", targetSMR, typeof(SkinnedMeshRenderer), true) as SkinnedMeshRenderer;
        var newClip = EditorGUILayout.ObjectField("Animation Clip", targetClip, typeof(AnimationClip), false) as AnimationClip;
        if (newClip != targetClip)
        {
            targetClip = newClip;
            if (targetClip != null)
            {
                blendShapeName = targetClip.name;
            }
        }
        blendShapeName = EditorGUILayout.TextField("New BlendShape Name", blendShapeName);

        EditorGUILayout.Space();
        if (GUILayout.Button("Bake BlendShape"))
        {
            if (targetSMR == null || targetClip == null)
            {
                EditorUtility.DisplayDialog("Error", "Please specify both a SkinnedMeshRenderer and an AnimationClip.", "OK");
                return;
            }
            BakeBlendShapeFromClip();
        }
    }

    /// <summary>
    /// AnimationClip 内の blendShape.xxx カーブを解析し、AnimationClip の最終時刻の状態で
    /// SkinnedMeshRenderer の BlendShape ウェイトを適用、その状態との差分を新規BlendShapeとして追加。
    /// </summary>
    private void BakeBlendShapeFromClip()
    {
        // 1. 元メッシュを取得
        Mesh baseMesh = targetSMR.sharedMesh;
        if (baseMesh == null)
        {
            EditorUtility.DisplayDialog("Error", "The specified SkinnedMeshRenderer has no Mesh.", "OK");
            return;
        }

        // 2. 中立状態を取得するため、全BlendShapeを一時的に0に
        int blendShapeCount = baseMesh.blendShapeCount;
        float[] originalWeights = new float[blendShapeCount];
        for (int i = 0; i < blendShapeCount; i++)
        {
            originalWeights[i] = targetSMR.GetBlendShapeWeight(i);
            targetSMR.SetBlendShapeWeight(i, 0f);
        }
        Mesh neutralMesh = new Mesh();
        targetSMR.BakeMesh(neutralMesh);

        // 3. AnimationClip の全カーブを走査し、"blendShape.xxx" のカーブがあれば評価して適用
        var bindings = AnimationUtility.GetCurveBindings(targetClip);
        // AnimationClip の最終時刻を使用
        float actualTime = targetClip.length;
        for (int i = 0; i < blendShapeCount; i++)
        {
            targetSMR.SetBlendShapeWeight(i, 0f);
        }
        foreach (var binding in bindings)
        {
            if (binding.propertyName.StartsWith("blendShape."))
            {
                string shapeName = binding.propertyName.Substring("blendShape.".Length);
                AnimationCurve curve = AnimationUtility.GetEditorCurve(targetClip, binding);
                if (curve == null) continue;
                float weight = curve.Evaluate(actualTime);
                int shapeIndex = FindBlendShapeIndexByName(targetSMR, shapeName);
                if (shapeIndex >= 0)
                {
                    targetSMR.SetBlendShapeWeight(shapeIndex, weight);
                }
            }
        }

        // 4. BakeMesh により変形状態を取得
        Mesh deformedMesh = new Mesh();
        targetSMR.BakeMesh(deformedMesh);

        // 5. 元の BlendShape ウェイトを復元
        for (int i = 0; i < blendShapeCount; i++)
        {
            targetSMR.SetBlendShapeWeight(i, originalWeights[i]);
        }

        // 6. 中立状態と変形状態との差分を計算
        if (neutralMesh.vertexCount != deformedMesh.vertexCount)
        {
            EditorUtility.DisplayDialog("Error", "Vertex count mismatch between neutral and deformed meshes.", "OK");
            return;
        }
        int vertexCount = neutralMesh.vertexCount;
        Vector3[] neutralVertices = neutralMesh.vertices;
        Vector3[] deformedVertices = deformedMesh.vertices;
        Vector3[] deltaVertices = new Vector3[vertexCount];
        for (int i = 0; i < vertexCount; i++)
        {
            deltaVertices[i] = deformedVertices[i] - neutralVertices[i];
        }

        Vector3[] deltaNormals = new Vector3[vertexCount];
        if (neutralMesh.normals != null && neutralMesh.normals.Length == vertexCount &&
            deformedMesh.normals != null && deformedMesh.normals.Length == vertexCount)
        {
            Vector3[] neutralNormals = neutralMesh.normals;
            Vector3[] deformedNormals = deformedMesh.normals;
            for (int i = 0; i < vertexCount; i++)
            {
                deltaNormals[i] = deformedNormals[i] - neutralNormals[i];
            }
        }

        Vector3[] deltaTangents = new Vector3[vertexCount];
        if (neutralMesh.tangents != null && neutralMesh.tangents.Length == vertexCount &&
            deformedMesh.tangents != null && deformedMesh.tangents.Length == vertexCount)
        {
            Vector4[] neutralTans = neutralMesh.tangents;
            Vector4[] deformedTans = deformedMesh.tangents;
            for (int i = 0; i < vertexCount; i++)
            {
                Vector3 nTan = new Vector3(neutralTans[i].x, neutralTans[i].y, neutralTans[i].z);
                Vector3 dTan = new Vector3(deformedTans[i].x, deformedTans[i].y, deformedTans[i].z);
                deltaTangents[i] = dTan - nTan;
            }
        }

        // 7. 既存のBlendShapeも引き継いだ新規 Mesh を生成し、新BlendShapeを追加
        Mesh newMesh = CopyMeshWithBlendShapes(baseMesh);
        if (string.IsNullOrEmpty(blendShapeName) && targetClip != null)
        {
            blendShapeName = targetClip.name;
        }
        string uniqueName = GetUniqueBlendShapeName(newMesh, blendShapeName);
        newMesh.AddBlendShapeFrame(uniqueName, 100f, deltaVertices, deltaNormals, deltaTangents);

        // 8. 新Mesh を日付時刻名で保存（例：20230218104530.asset）
        string folderPath = "Assets/BlendShapeAnimatonClipConverter/Mesh";
        if (!AssetDatabase.IsValidFolder("Assets/BlendShapeAnimatonClipConverter"))
        {
            AssetDatabase.CreateFolder("Assets", "BlendShapeAnimatonClipConverter");
        }
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder("Assets/BlendShapeAnimatonClipConverter", "Mesh");
        }
        string dateTimeStr = DateTime.Now.ToString("yyyyMMddHHmmss");
        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{dateTimeStr}.asset");
        AssetDatabase.CreateAsset(newMesh, assetPath);
        AssetDatabase.SaveAssets();

        // 9. 新Mesh を SkinnedMeshRenderer に差し替え
        targetSMR.sharedMesh = newMesh;

        EditorUtility.DisplayDialog("Success",
            $"process is complete.","OK");
    }

    /// <summary>
    /// SkinnedMeshRenderer 内の指定名のブレンドシェイプインデックスを返す
    /// </summary>
    private int FindBlendShapeIndexByName(SkinnedMeshRenderer smr, string shapeName)
    {
        Mesh mesh = smr.sharedMesh;
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            if (mesh.GetBlendShapeName(i) == shapeName)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// 既存のMeshをコピーし、頂点、UV、SubMesh、BoneWeights、BlendShapeなどをすべて引き継ぐ
    /// </summary>
    private Mesh CopyMeshWithBlendShapes(Mesh original)
    {
        Mesh mesh = new Mesh();
        mesh.indexFormat = original.indexFormat;
        mesh.vertices = original.vertices;
        mesh.uv = original.uv;
        mesh.uv2 = original.uv2;
        mesh.uv3 = original.uv3;
        mesh.uv4 = original.uv4;
        mesh.uv5 = original.uv5;
        mesh.uv6 = original.uv6;
        mesh.uv7 = original.uv7;
        mesh.uv8 = original.uv8;
        mesh.colors = original.colors;
        mesh.colors32 = original.colors32;
        mesh.normals = original.normals;
        mesh.tangents = original.tangents;
        mesh.bindposes = original.bindposes;
        mesh.boneWeights = original.boneWeights;
        mesh.subMeshCount = original.subMeshCount;

        for (int i = 0; i < original.subMeshCount; i++)
        {
            mesh.SetTriangles(original.GetTriangles(i), i);
            mesh.SetSubMesh(i, original.GetSubMesh(i), MeshUpdateFlags.DontRecalculateBounds);
        }

        for (int shapeIndex = 0; shapeIndex < original.blendShapeCount; shapeIndex++)
        {
            string shapeName = original.GetBlendShapeName(shapeIndex);
            int frameCount = original.GetBlendShapeFrameCount(shapeIndex);
            for (int frame = 0; frame < frameCount; frame++)
            {
                float frameWeight = original.GetBlendShapeFrameWeight(shapeIndex, frame);
                Vector3[] deltaVertices = new Vector3[original.vertexCount];
                Vector3[] deltaNormals = new Vector3[original.vertexCount];
                Vector3[] deltaTangents = new Vector3[original.vertexCount];
                original.GetBlendShapeFrameVertices(shapeIndex, frame, deltaVertices, deltaNormals, deltaTangents);
                mesh.AddBlendShapeFrame(shapeName, frameWeight, deltaVertices, deltaNormals, deltaTangents);
            }
        }

        return mesh;
    }

    /// <summary>
    /// 指定のMesh内に同名のBlendShapeが存在しないユニークな名前を返す
    /// </summary>
    private string GetUniqueBlendShapeName(Mesh mesh, string baseName)
    {
        if (string.IsNullOrEmpty(baseName)) baseName = "BakedBlendShape";
        string newName = baseName;
        int counter = 1;
        while (BlendShapeExists(mesh, newName))
        {
            newName = baseName + "_" + counter;
            counter++;
        }
        return newName;
    }

    private bool BlendShapeExists(Mesh mesh, string name)
    {
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            if (mesh.GetBlendShapeName(i) == name)
                return true;
        }
        return false;
    }
}
