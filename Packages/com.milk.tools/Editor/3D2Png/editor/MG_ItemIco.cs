using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

public partial class MG_ItemIco: EditorWindow
{
    [MenuItem("图标生成/3DObj to Icon")]
    private static void ShowWindow()
    {
        var window = GetWindow<MG_ItemIco>("UIElements");
        window.titleContent = new GUIContent("3DObj to Icon");
        window.Show();
    }

    [SerializeField] private VisualTreeAsset _rootVisualTreeAsset;
    [SerializeField] private StyleSheet _rootStyleSheet;
    private ObjectField objectField;
    private Button runButton;
    private IntegerField iconSizeField;
    private Toggle activeObjToggle;
    private List<GameObject> foundComponents = new List<GameObject>();

    string dirPath = "Assets/MilkTools/Icons/IconOutput/";
    bool activeParentElement = false;

    // GUI生成
    private void CreateGUI()
    {
    _rootVisualTreeAsset.CloneTree(rootVisualElement);
    if (_rootStyleSheet != null)
    {
        rootVisualElement.styleSheets.Add(_rootStyleSheet);
    }
    objectField = rootVisualElement.Q<ObjectField>("MainObj");
    objectField.objectType = typeof(GameObject);
    
    runButton = rootVisualElement.Q<Button>("RunButton");
    if (runButton != null)
    {
        runButton.clicked += FindAndCaptureComponents;
    }
    iconSizeField = rootVisualElement.Q<IntegerField>("iconSize");
    activeObjToggle = rootVisualElement.Q<Toggle>("ActiveObjToggle");
    }
    private void FindAndCaptureComponents()
    {
    GameObject parentObject = objectField.value as GameObject;
    if (parentObject == null)
    {
        Debug.LogWarning("No object selected.");
        return;
    }
    foundComponents.Clear();
    if (activeObjToggle != null && activeObjToggle.value)
    {
        FindComponentsInChildrenActive(parentObject.transform, true);
    }
    else
    {
        FindComponentsInChildren(parentObject.transform, true);
    }

    CaptureAndSaveRenderTextures();

    if (activeParentElement)
    {
        // 親要素戻す
        parentObject.SetActive(false);
        activeParentElement = false;
    }
    Debug.Log("Completed.");
    }

    // 親要素と子要素にSkinnedMeshRendererまたはMeshRendererがあるか確認
    private void FindComponentsInChildren(Transform parent, bool includeParent = false)
    {
        if (includeParent && (parent.GetComponent<SkinnedMeshRenderer>() != null || parent.GetComponent<MeshRenderer>() != null)) {
            foundComponents.Add(parent.gameObject);
        }
        foreach(Transform child in parent)
        {
            if ((child.GetComponent<SkinnedMeshRenderer>() != null) || (child.GetComponent<MeshRenderer>() != null))
            {
                foundComponents.Add(child.gameObject);
            }
            FindComponentsInChildren(child, false);
        }
    }
    // 親要素が非表示な場合その子要素は除外する処理追加した方
    private void FindComponentsInChildrenActive(Transform parent, bool includeParent = false)
    {
    bool wasActive = parent.gameObject.activeSelf;
    if (!parent.gameObject.activeInHierarchy && includeParent)
    {
        parent.gameObject.SetActive(true);
        activeParentElement = true;
    }
    if (includeParent && (parent.GetComponent<SkinnedMeshRenderer>() != null || parent.GetComponent<MeshRenderer>() != null)) {
        foundComponents.Add(parent.gameObject);
    }
    foreach(Transform child in parent)
    {
        if (child.gameObject.activeInHierarchy && 
            (child.GetComponent<SkinnedMeshRenderer>() != null || child.GetComponent<MeshRenderer>() != null))
        {
            foundComponents.Add(child.gameObject);
        }
        FindComponentsInChildrenActive(child, false);
    }
    }



    // カメラ設定、画面キャプチャ、画像生成
    private void CaptureAndSaveRenderTextures()
    {
        // Path先がなかったら生成
        if (!Directory.Exists(dirPath)) {
            Directory.CreateDirectory(dirPath);
        }
        // レイヤー、映ってしまったら変えてください。
        int captureLayer = LayerMask.NameToLayer("PlayerLocal");

        // アイコン
        int iconSize = iconSizeField != null ? iconSizeField.value : 256;
        RenderTexture renderTexture = new RenderTexture(iconSize, iconSize, 24, RenderTextureFormat.ARGB32);
        
        // カメラ
        Camera captureCamera = new GameObject("MG_CaptureCamera").AddComponent<Camera>();
        captureCamera.cullingMask = 1 << captureLayer;
        captureCamera.targetTexture = renderTexture;
        captureCamera.backgroundColor = Color.clear;
        captureCamera.clearFlags = CameraClearFlags.SolidColor;
        captureCamera.nearClipPlane = 0.01f;
        captureCamera.farClipPlane = 10000f;

        RenderTexture.active = renderTexture;

        // シーン
        if (SceneView.lastActiveSceneView != null)
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            captureCamera.transform.position = sceneView.camera.transform.position;
            captureCamera.transform.rotation = sceneView.camera.transform.rotation;

            // 平行投影と透視投影で調整
            if (sceneView.camera.orthographic)
            {
                captureCamera.orthographic = true;
                captureCamera.orthographicSize = sceneView.camera.orthographicSize;
            }
            else
            {
                captureCamera.orthographic = false;
                captureCamera.fieldOfView = sceneView.camera.fieldOfView;
            }
        }


        // 対象を複製
        var copiedComponents = new List<GameObject>();
        foreach(var obj in foundComponents)
        {
            // 非表示を除外
            GameObject objCopy = Instantiate(obj);
            objCopy.layer = captureLayer;
            if (obj.activeSelf)
            {
                obj.SetActive(true);
                copiedComponents.Add(objCopy);
            }
            else
            {
                DestroyImmediate(objCopy);
            }
        }
        // ない場合終了
        if (copiedComponents.Count == 0)
        {
            Debug.LogWarning("No components.");
            return;
        }

        // テクスチャ生成
        captureCamera.Render();
        Texture2D screenshot = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.ARGB32, false);
        screenshot.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        screenshot.Apply();
        captureCamera.targetTexture = null;
        RenderTexture.active = null;
        byte[] imageBytes = screenshot.EncodeToPNG();
        File.WriteAllBytes(dirPath + foundComponents[0].name + "_icon.png", imageBytes);

        // 画像のAlphaIsTransparencyをTrueに
        string filePath = dirPath + foundComponents[0].name + "_icon.png";
        File.WriteAllBytes(filePath, imageBytes);
        AssetDatabase.ImportAsset(filePath);
        TextureImporter importer = AssetImporter.GetAtPath(filePath) as TextureImporter;
        if (importer != null) {
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        // コピー削除
        foreach(var copiedObj in copiedComponents) {
            DestroyImmediate(copiedObj);
        }
        Object.DestroyImmediate(renderTexture);
        DestroyImmediate(captureCamera.gameObject);

        // カメラ削除
        foreach(GameObject obj in GameObject.FindObjectsOfType<GameObject>()) {
            if (obj.name == "MG_CaptureCamera") {
                DestroyImmediate(obj);
            }
        }
    }
}