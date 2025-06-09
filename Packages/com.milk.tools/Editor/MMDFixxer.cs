using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class BlendShapeFixxer : EditorWindow
{
    private SkinnedMeshRenderer skinnedMeshRenderer;
    private List<string> matchedShapes = new List<string>();
    private ShapeKeyDataList savedData = new ShapeKeyDataList();

    private string selectedShapeKey = "";
    private float selectedShapeValue = 0f;
    private float originalShapeValue = 0f;

    private bool enableAdditionalApply = false;
    private List<AdditionalShapeKey> additionalShapeKeys = new List<AdditionalShapeKey>();

    private bool showAllShapeKeys = false;
    private readonly HashSet<string> predefinedShapeKeysSet;

    [System.Serializable]
    public class ShapeKeyDataList
    {
        public List<ShapeKeyData> items = new List<ShapeKeyData>();
    }

    [System.Serializable]
    public class ShapeKeyData
    {
        public string guid;
        public List<BlendShapeValue> blendShapes = new List<BlendShapeValue>();
    }

    [System.Serializable]
    public class BlendShapeValue
    {
        public string shapeName;
        public float value;
        public List<AdditionalShapeKeyData> additionalKeys = new List<AdditionalShapeKeyData>(); 
    }

    [System.Serializable]
    public class AdditionalShapeKeyData
    {
        public string shapeName;
        public float value;
    }

    [System.Serializable]
    public class AdditionalShapeKey
    {
        public string shapeName = "";
        public float value = 0f;
        public float originalValue = 0f;
    }

    private List<string> predefinedShapeKeys = new List<string>
    {
        "あ","い","う","え","お","にやり","∧","ワ","ω","▲","口角上げ","口角下げ","口横広げ",
        "Ah","Ch","U","E","Oh","Grin","Wa","Mouth Horn Raise","Mouth Horn Lower","Mouth Side Widen",
        "まばたき","笑い","はぅ","瞳小","ｳｨﾝｸ２右","ウィンク２","ウィンク","ウィンク右","なごみ","じと目","びっくり","ｷﾘｯ","はぁと","星目",
        "Blink","Blink Happy","Close><","Pupil","Wink 2 Right","Wink 2","Wink","Wink Right","Calm","Stare","Surprised","Slant","Heart","Star Eye",
        "にこり","上","下","真面目","困る","怒り","前",
        "Cheerful","Upper","Lower","Serious","Sadness","Anger","Front",
        "照れ","にやり２","ん","あ2","恐ろしい子！","歯無し下","涙","Blush"
    };

    private const string SavePath = "Assets/!Ein/VEUT MMDFixer.json";
    private Vector2 scrollPos;

    public BlendShapeFixxer()
    {
        predefinedShapeKeysSet = new HashSet<string>(predefinedShapeKeys);
    }

    private List<string> GetAllBlendShapeNames()
    {
        List<string> allBlendShapeNames = new List<string>();
        if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null)
        {
            Mesh mesh = skinnedMeshRenderer.sharedMesh;
            for (int i = 0; i < mesh.blendShapeCount; i++)
                allBlendShapeNames.Add(mesh.GetBlendShapeName(i));
        }
        return allBlendShapeNames;
    }

    private void UpdateSelectedBlendShapeWeight()
    {
        if (skinnedMeshRenderer != null && !string.IsNullOrEmpty(selectedShapeKey))
        {
            int index = GetShapeIndex(selectedShapeKey);
            if (index >= 0)
                skinnedMeshRenderer.SetBlendShapeWeight(index, selectedShapeValue);
        }
    }

    private void UpdateAdditionalBlendShapeWeights()
    {
        if (skinnedMeshRenderer != null)
        {
            foreach (var ask in additionalShapeKeys)
            {
                if (!string.IsNullOrEmpty(ask.shapeName))
                {
                    int index = GetShapeIndex(ask.shapeName);
                    if (index >= 0)
                        skinnedMeshRenderer.SetBlendShapeWeight(index, ask.value);
                }
            }
        }
    }

    [MenuItem("MilkTools/MMD BlendShape Fixxer")]
    public static void ShowWindow()
    {
        BlendShapeFixxer window = GetWindow<BlendShapeFixxer>("MMD BlendShape Fixxer");
        window.minSize = new Vector2(550, 550);
    }

    private void OnEnable()
    {
        LoadData();
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("MMD BlendShape Fixxer 1.2", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        GUILayout.Label("ALL Shapekey Mode");
        EditorGUI.BeginChangeCheck();
        showAllShapeKeys = EditorGUILayout.Toggle(showAllShapeKeys, GUILayout.Width(20));
        if (EditorGUI.EndChangeCheck())
        {
            CheckBlendShapes();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Skinned Mesh Renderer (Face)", EditorStyles.label, GUILayout.Width(180));
        skinnedMeshRenderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField(skinnedMeshRenderer, typeof(SkinnedMeshRenderer), true);
        EditorGUILayout.EndHorizontal();
        if (EditorGUI.EndChangeCheck())
        {
            CheckBlendShapes();
        }

        if (matchedShapes.Count > 0)
        {
            GUILayout.Label("Matching MMD ShapeKey", EditorStyles.boldLabel);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(150));
            foreach (var shape in matchedShapes)
            {
                if (GUILayout.Button(shape))
                {
                    selectedShapeKey = shape;
                    originalShapeValue = skinnedMeshRenderer.GetBlendShapeWeight(GetShapeIndex(shape));
                    selectedShapeValue = originalShapeValue;
                    additionalShapeKeys.Clear();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        if (!string.IsNullOrEmpty(selectedShapeKey))
        {
            GUILayout.Space(10);
            GUILayout.Label($"Select Shapekey: {selectedShapeKey}", EditorStyles.boldLabel);
            float newValue = EditorGUILayout.Slider("Shapekey Value", selectedShapeValue, 0f, 100f);
            if (Mathf.Abs(newValue - selectedShapeValue) > 0.01f)
            {
                selectedShapeValue = newValue;
                UpdateSelectedBlendShapeWeight();
            }

            enableAdditionalApply = EditorGUILayout.Toggle("Additional modification", enableAdditionalApply);
            if (enableAdditionalApply)
            {
                if (GUILayout.Button("+"))
                {
                    AdditionalShapeKey newAsk = new AdditionalShapeKey();
                    List<string> allBlendShapeNames = GetAllBlendShapeNames();
                    if (allBlendShapeNames.Count > 0)
                    {
                        newAsk.shapeName = allBlendShapeNames[0];
                        int index = GetShapeIndex(newAsk.shapeName);
                        if (index >= 0)
                        {
                            newAsk.originalValue = skinnedMeshRenderer.GetBlendShapeWeight(index);
                            newAsk.value = newAsk.originalValue;
                        }
                        additionalShapeKeys.Add(newAsk);
                    }
                }

                for (int i = 0; i < additionalShapeKeys.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    List<string> allBlendShapeNames = GetAllBlendShapeNames();
                    int currentIndex = allBlendShapeNames.IndexOf(additionalShapeKeys[i].shapeName);
                    if (currentIndex < 0 && allBlendShapeNames.Count > 0)
                    {
                        additionalShapeKeys[i].shapeName = allBlendShapeNames[0];
                        currentIndex = 0;
                        int index = GetShapeIndex(additionalShapeKeys[i].shapeName);
                        if (index >= 0)
                        {
                            additionalShapeKeys[i].originalValue = skinnedMeshRenderer.GetBlendShapeWeight(index);
                            additionalShapeKeys[i].value = additionalShapeKeys[i].originalValue;
                        }
                    }

                    int newIndex = EditorGUILayout.Popup(currentIndex, allBlendShapeNames.ToArray());
                    if (newIndex >= 0 && newIndex != currentIndex)
                    {
                        int prevIndex = GetShapeIndex(additionalShapeKeys[i].shapeName);
                        if (prevIndex >= 0)
                            skinnedMeshRenderer.SetBlendShapeWeight(prevIndex, additionalShapeKeys[i].originalValue);

                        additionalShapeKeys[i].shapeName = allBlendShapeNames[newIndex];
                        int newShapeIndex = GetShapeIndex(additionalShapeKeys[i].shapeName);
                        if (newShapeIndex >= 0)
                        {
                            additionalShapeKeys[i].originalValue = skinnedMeshRenderer.GetBlendShapeWeight(newShapeIndex);
                            additionalShapeKeys[i].value = additionalShapeKeys[i].originalValue;
                        }
                    }

                    float newValueAdd = EditorGUILayout.Slider(additionalShapeKeys[i].value, 0f, 100f);
                    if (Mathf.Abs(newValueAdd - additionalShapeKeys[i].value) > 0.01f)
                    {
                        additionalShapeKeys[i].value = newValueAdd;
                        UpdateAdditionalBlendShapeWeights();
                    }

                    if (GUILayout.Button("X", GUILayout.Width(20)))
                    {
                        int index = GetShapeIndex(additionalShapeKeys[i].shapeName);
                        if (index >= 0)
                            skinnedMeshRenderer.SetBlendShapeWeight(index, additionalShapeKeys[i].originalValue);
                        additionalShapeKeys.RemoveAt(i);
                        i--;
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            if (GUILayout.Button("Add Data"))
            {
                SaveBlendShapeValue();
                selectedShapeValue = originalShapeValue;
                foreach (var ask in additionalShapeKeys)
                    ask.value = ask.originalValue;
                UpdateSelectedBlendShapeWeight();
                UpdateAdditionalBlendShapeWeights();
            }
        }

        GUILayout.Space(10);
        GUILayout.Label("Saved Data", EditorStyles.boldLabel);
        if (skinnedMeshRenderer != null)
        {
            string guid = GetMeshGUID();
            ShapeKeyData data = savedData.items.Find(item => item.guid == guid);
            if (data != null)
            {
                List<BlendShapeValue> keysToRemove = new List<BlendShapeValue>();
                foreach (var shape in data.blendShapes)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Label($"{shape.shapeName}: {shape.value}", GUILayout.Width(200));
                    if (shape.additionalKeys.Count > 0)
                    {
                        string additionalText = string.Join(", ", shape.additionalKeys.Select(k => $"{k.shapeName}: {k.value}"));
                        GUILayout.Label($"[추가: {additionalText}]");
                    }
                    if (GUILayout.Button("X", GUILayout.Width(20)))
                        keysToRemove.Add(shape);
                    EditorGUILayout.EndHorizontal();
                }

                if (keysToRemove.Count > 0)
                {
                    foreach (var key in keysToRemove)
                        data.blendShapes.Remove(key);
                    SaveData();
                }
            }
        }

        GUILayout.Space(20);
        if (GUILayout.Button("Fixxer!", GUILayout.Height(30)))
            ApplyFixxer(skinnedMeshRenderer);
    }

    private Dictionary<string, float> SaveCurrentWeights()
    {
        Dictionary<string, float> currentWeights = new Dictionary<string, float>();
        if (skinnedMeshRenderer != null)
        {
            Mesh mesh = skinnedMeshRenderer.sharedMesh;
            for (int i = 0; i < mesh.blendShapeCount; i++)
                currentWeights[mesh.GetBlendShapeName(i)] = skinnedMeshRenderer.GetBlendShapeWeight(i);
        }
        return currentWeights;
    }

    private void RestoreWeights(Dictionary<string, float> currentWeights)
    {
        if (skinnedMeshRenderer != null)
        {
            Mesh mesh = skinnedMeshRenderer.sharedMesh;
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                string shapeName = mesh.GetBlendShapeName(i);
                if (currentWeights.TryGetValue(shapeName, out float weight))
                    skinnedMeshRenderer.SetBlendShapeWeight(i, weight);
            }
        }
    }

    private void CheckBlendShapes()
    {
        matchedShapes.Clear();
        if (skinnedMeshRenderer == null || skinnedMeshRenderer.sharedMesh == null) return;

        List<string> allShapes = GetAllBlendShapeNames();
        if (showAllShapeKeys)
        {
            matchedShapes = allShapes;
        }
        else
        {
            matchedShapes = allShapes.Where(key => predefinedShapeKeysSet.Contains(key)).ToList();
        }
    }

    private int GetShapeIndex(string shapeName)
    {
        if (skinnedMeshRenderer == null || skinnedMeshRenderer.sharedMesh == null) return -1;
        return skinnedMeshRenderer.sharedMesh.GetBlendShapeIndex(shapeName);
    }

    private void SaveBlendShapeValue()
    {
        if (skinnedMeshRenderer == null || string.IsNullOrEmpty(selectedShapeKey)) return;

        string guid = GetMeshGUID();
        if (string.IsNullOrEmpty(guid)) return;

        ShapeKeyData data = savedData.items.Find(item => item.guid == guid);
        if (data == null)
        {
            data = new ShapeKeyData { guid = guid };
            savedData.items.Add(data);
        }

        BlendShapeValue existingShape = data.blendShapes.Find(s => s.shapeName == selectedShapeKey);
        if (existingShape != null)
        {
            existingShape.value = selectedShapeValue;
            existingShape.additionalKeys.Clear();
            foreach (var ask in additionalShapeKeys)
                existingShape.additionalKeys.Add(new AdditionalShapeKeyData { shapeName = ask.shapeName, value = ask.value });
        }
        else
        {
            var newShape = new BlendShapeValue { shapeName = selectedShapeKey, value = selectedShapeValue };
            foreach (var ask in additionalShapeKeys)
                newShape.additionalKeys.Add(new AdditionalShapeKeyData { shapeName = ask.shapeName, value = ask.value });
            data.blendShapes.Add(newShape);
        }

        SaveData();
    }

    private void SaveData()
    {
        string json = JsonUtility.ToJson(savedData, true);
        File.WriteAllText(SavePath, json);
        AssetDatabase.Refresh();
    }

    private void LoadData()
    {
        if (File.Exists(SavePath))
        {
            string json = File.ReadAllText(SavePath);
            savedData = JsonUtility.FromJson<ShapeKeyDataList>(json) ?? new ShapeKeyDataList();
        }
    }

    private string GetMeshGUID()
    {
        if (skinnedMeshRenderer == null || skinnedMeshRenderer.sharedMesh == null) return "";
        string path = AssetDatabase.GetAssetPath(skinnedMeshRenderer.sharedMesh);
        return string.IsNullOrEmpty(path) ? "" : AssetDatabase.AssetPathToGUID(path);
    }

    void ApplyFixxer(SkinnedMeshRenderer skinnedMeshRenderer)
    {
        Mesh originalMesh = skinnedMeshRenderer.sharedMesh;
        string guid = GetMeshGUID();
        ShapeKeyData data = savedData.items.Find(item => item.guid == guid);
        if (data == null) return;

        Dictionary<string, float> originalWeights = SaveCurrentWeights();
        Mesh modifiedMesh = Instantiate(originalMesh);
        modifiedMesh.ClearBlendShapes();
        Dictionary<string, BlendShapeValue> savedShapes = data.blendShapes.ToDictionary(s => s.shapeName, s => s);
        HashSet<string> processedShapes = new HashSet<string>();

        for (int i = 0; i < originalMesh.blendShapeCount; i++)
        {
            string shapeName = originalMesh.GetBlendShapeName(i);
            if (processedShapes.Contains(shapeName)) continue;

            Vector3[] vertices = new Vector3[originalMesh.vertexCount];
            Vector3[] normals = new Vector3[originalMesh.vertexCount];
            Vector3[] tangents = new Vector3[originalMesh.vertexCount];
            originalMesh.GetBlendShapeFrameVertices(i, 0, vertices, normals, tangents);
            float originalWeight = originalMesh.GetBlendShapeFrameWeight(i, 0);

            if (savedShapes.TryGetValue(shapeName, out BlendShapeValue savedShape))
            {
                Vector3[] combinedVertices = (Vector3[])vertices.Clone();
                Vector3[] combinedNormals = (Vector3[])normals.Clone();
                Vector3[] combinedTangents = (Vector3[])tangents.Clone();

                foreach (var addKey in savedShape.additionalKeys)
                {
                    int addIndex = originalMesh.GetBlendShapeIndex(addKey.shapeName);
                    if (addIndex >= 0)
                    {
                        Vector3[] addVertices = new Vector3[originalMesh.vertexCount];
                        Vector3[] addNormals = new Vector3[originalMesh.vertexCount];
                        Vector3[] addTangents = new Vector3[originalMesh.vertexCount];
                        originalMesh.GetBlendShapeFrameVertices(addIndex, 0, addVertices, addNormals, addTangents);
                        
                        float defaultWeight = 30f / 100f; 
                        for (int j = 0; j < combinedVertices.Length; j++)
                        {
                            combinedVertices[j] -= addVertices[j] * defaultWeight;
                            combinedNormals[j] -= addNormals[j] * defaultWeight;
                            combinedTangents[j] -= addTangents[j] * defaultWeight;
                        }

                        float weight = addKey.value / savedShape.value; 
                        for (int j = 0; j < combinedVertices.Length; j++)
                        {
                            combinedVertices[j] += addVertices[j] * weight;
                            combinedNormals[j] += addNormals[j] * weight;
                            combinedTangents[j] += addTangents[j] * weight;
                        }
                    }
                }

                float scaleFactor = savedShape.value / 100f; 
                for (int j = 0; j < combinedVertices.Length; j++)
                {
                    combinedVertices[j] *= scaleFactor;
                    combinedNormals[j] *= scaleFactor;
                    combinedTangents[j] *= scaleFactor;
                }
                modifiedMesh.AddBlendShapeFrame(shapeName, 100f, combinedVertices, combinedNormals, combinedTangents);
                processedShapes.Add(shapeName);
            }
            else
            {
                modifiedMesh.AddBlendShapeFrame(shapeName, Mathf.Min(originalWeight, 100f), vertices, normals, tangents);
                processedShapes.Add(shapeName);
            }
        }

        Undo.RecordObject(skinnedMeshRenderer, "Apply BlendShape Fixxer");
        skinnedMeshRenderer.sharedMesh = modifiedMesh;
        string path = Path.GetDirectoryName(AssetDatabase.GetAssetPath(originalMesh)) + "/" + originalMesh.name + "_fixed.asset";
        AssetDatabase.CreateAsset(modifiedMesh, AssetDatabase.GenerateUniqueAssetPath(path));
        AssetDatabase.SaveAssets();

        RestoreWeights(originalWeights);
        selectedShapeValue = originalWeights.GetValueOrDefault(selectedShapeKey, 0f);
        foreach (var ask in additionalShapeKeys)
            ask.value = ask.originalValue;

        EditorUtility.DisplayDialog("BlendShape Fixxer", "Mesh blend shape modified successfully.", "Okey");
    }
}