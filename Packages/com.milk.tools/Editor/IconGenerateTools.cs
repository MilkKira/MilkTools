
//2024.1.26实现汉化

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Hai.IconGen.Scripts.Editor
{
    public class IconGenThumbnailEditorWindow : EditorWindow
    {
        public float fieldOfView = 60;
        public float cameraRoll = 0f;
        public float farClipPlane = 15;
        public bool useSceneFov = false;
        public string lastSave = "";
        public bool transparent = true;
        public Color background = new Color(1, 1, 1, 1);
        public bool captureSky = false;
        public bool avoidEmulatorConflict = true;
        public bool usePostProcessing;

        public Vector3 capture_position;
        public Vector3 capture_rotation;
        public float capture_fieldOfView;
        private RenderTexture _renderTextureNullable;
        private Texture2D _textureNullable;
        
        private bool _savedRecently = false;
        private bool _isCapturing = false;
        private Vector2 _scrollPos;
        private Texture2D _transparentBackground;

        private static readonly Vector2Int iconSize = new Vector2Int(120, 90);

        internal static Type PplType;
        internal static FieldInfo PplVolumeLayerField;
        internal static FieldInfo PplVolumeTriggerField;

        public IconGenThumbnailEditorWindow()
        {
            titleContent = new GUIContent("场景相机");

            PplType = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .FirstOrDefault(type => type.Name == "PostProcessLayer");
            if (PplType != null)
            {
                PplVolumeLayerField = PplType.GetField("volumeLayer", BindingFlags.Instance | BindingFlags.Public);
                PplVolumeTriggerField = PplType.GetField("volumeTrigger", BindingFlags.Instance | BindingFlags.Public);
            }
        }

        private void Update()
        {
            if (_isCapturing)
            {
                RegisterSceneCameraSettings();
                DoCapture();

                if (_transparentBackground == null)
                {
                    var grayish = new Color(0.75f, 0.75f, 0.75f);
                    var whitey = new Color(1, 1, 1);

                    var xx = iconSize.x;
                    var yy = iconSize.y;
                    _transparentBackground = new Texture2D(xx, yy);
                    for (var i = 0; i < xx; i++)
                    {
                        var ii = i * 20 / xx;
                        for (var j = 0; j < yy; j++)
                        {
                            var jj = j * 20 / xx;
                            var kk = (ii + jj) % 2 == 0;
                            _transparentBackground.SetPixel(i, yy - j - 1, kk ? grayish : whitey);
                        }
                    }
                    _transparentBackground.Apply();
                }
            }
        }

        private void OnGUI()
        {
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(position.height - EditorGUIUtility.singleLineHeight));
            
            var serializedObject = new SerializedObject(this);
            
            if (ColoredBgButton(_isCapturing, Color.red, () => GUILayout.Button(!_isCapturing ? "开始截取" : "停止截取")))
            {
                _isCapturing = !_isCapturing;
            }

            if (_textureNullable != null)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                
                var bypassPlaymodeTintOldColor = GUI.color;
                GUI.color = Color.white;
                var positionWidth = Mathf.Min(position.width - 25, iconSize.x);
                GUILayout.Box(_textureNullable, new GUIStyle("box")
                    {
                        padding = new RectOffset(0, 0, 0, 0),
                        normal = new GUIStyleState { background = _transparentBackground }
                    },
                    GUILayout.Width(positionWidth), GUILayout.Height(positionWidth * ((1f * iconSize.y) / iconSize.x))
                );
                GUI.color = bypassPlaymodeTintOldColor;
                
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUI.BeginDisabledGroup(_textureNullable == null);
            if (GUILayout.Button("作为PNG导出..."))
            {
                TrySave(serializedObject);
            }
            EditorGUI.BeginDisabledGroup(!_savedRecently || lastSave == "");
            if (GUILayout.Button("覆盖保存"))
            {
                TrySaveAtLocation(lastSave);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.Separator();
            
            GUILayout.BeginVertical("GroupBox");
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(useSceneFov)), new GUIContent("使用场景摄像机FOV"));
            
            EditorGUI.BeginDisabledGroup(useSceneFov);
            EditorGUILayout.Slider(serializedObject.FindProperty(nameof(fieldOfView)), 0.1f, 179,new GUIContent("摄像机视野"));
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.Slider(serializedObject.FindProperty(nameof(farClipPlane)), 0.5f, 15, new GUIContent("远切面"));
            EditorGUILayout.Slider(serializedObject.FindProperty(nameof(farClipPlane)), 0.5f, 10000, new GUIContent("(拓展)"));
            
            EditorGUILayout.BeginHorizontal();
            var rollProp = serializedObject.FindProperty(nameof(cameraRoll));
            EditorGUILayout.Slider(rollProp, -180, 180, new GUIContent("远切面"));
            if (GUILayout.Button("重置Roll", GUILayout.Width(80)))
            {
                rollProp.floatValue = 0f;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(transparent)),new GUIContent("背景透明"));
            
            EditorGUI.BeginDisabledGroup(transparent);
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(background)),new GUIContent("纯色背景"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(captureSky)),new GUIContent("捕获天空盒"));
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(avoidEmulatorConflict)),new GUIContent("避免调制器冲突"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(nameof(usePostProcessing)),new GUIContent("后期处理"));
            
            GUILayout.EndVertical();

            GUILayout.EndScrollView();
            
            serializedObject.ApplyModifiedProperties();
        }

        private static bool ColoredBgButton(bool isActive, Color bgColor, Func<bool> inside)
        {
            var col = GUI.backgroundColor;
            try
            {
                if (isActive) GUI.backgroundColor = bgColor;
                return inside();
            }
            finally
            {
                GUI.backgroundColor = col;
            }
        }

        private bool TrySave(SerializedObject serializedObject)
        {
            var savePath = EditorUtility.SaveFilePanel("Save image as...", lastSave == "" ? Application.dataPath : lastSave, "", "png");
            if (savePath == null || savePath.Trim() == "") return false;
            
            serializedObject.FindProperty(nameof(lastSave)).stringValue = savePath;

            TrySaveAtLocation(savePath);
            
            return true;
        }

        private void TrySaveAtLocation(string savePath)
        {
            var bytes = _textureNullable.EncodeToPNG();
            File.WriteAllBytes(savePath, bytes);

            var isAnAsset = savePath.StartsWith(Application.dataPath);
            if (isAnAsset)
            {
                AssetDatabase.Refresh();
                var assetPath = "Assets" + savePath.Substring(Application.dataPath.Length);
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath));
            }
            
            _savedRecently = true;
        }

        private void RegisterSceneCameraSettings()
        {
            var sceneCamera = SceneView.lastActiveSceneView.camera;
            capture_position = sceneCamera.transform.position;
            capture_rotation = sceneCamera.transform.rotation.eulerAngles;
            capture_rotation.z += cameraRoll;
            capture_fieldOfView = useSceneFov ? sceneCamera.fieldOfView : fieldOfView;
        }

        private void DoCapture()
        {
            if (_renderTextureNullable != null)
            {
                RenderTexture.ReleaseTemporary(_renderTextureNullable);
                _renderTextureNullable = null;
            }

            _renderTextureNullable = RenderTexture.GetTemporary(iconSize.x, iconSize.y, 24);
            
            var _camera = new GameObject().AddComponent<Camera>();
            _camera.transform.position = capture_position;
            _camera.transform.rotation = Quaternion.Euler(capture_rotation);
            _camera.fieldOfView = capture_fieldOfView;
            _camera.orthographic = false;
            _camera.nearClipPlane = 0.00001f;
            _camera.farClipPlane = farClipPlane;
            _camera.clearFlags = transparent || !captureSky ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
            _camera.backgroundColor = transparent ? new Color(0, 0, 0, 0) : background;
            _camera.targetTexture = _renderTextureNullable;
            _camera.aspect = (float) _renderTextureNullable.width / _renderTextureNullable.height;
            if (avoidEmulatorConflict)
            {
                _camera.cullingMask = ~(1 << 9 | 1 << 18);
            }
            if (usePostProcessing && PplType != null)
            {
                var ppl = _camera.gameObject.AddComponent(PplType);
                Debug.Log("PP");
                PplVolumeLayerField.SetValue(ppl, new LayerMask { value = -1 });
                PplVolumeTriggerField.SetValue(ppl, _camera.transform);
            }
            _camera.Render();
            
            RenderTexture.active = _renderTextureNullable;
            if (_textureNullable != null)
            {
                // FIXME: Review the texture management
                DestroyImmediate(_textureNullable);
                _textureNullable = null;
            }
            var texture2D = !transparent ? new Texture2D(iconSize.x, iconSize.y, TextureFormat.RGB24, false) : new Texture2D(iconSize.x, iconSize.y);
            texture2D.ReadPixels(new Rect(0, 0, _renderTextureNullable.width, _renderTextureNullable.height), 0, 0);
            texture2D.Apply();
            _textureNullable = texture2D;
            RenderTexture.active = null;
            
            RenderTexture.ReleaseTemporary(_renderTextureNullable);
            
            _renderTextureNullable = null;
            DestroyImmediate(_camera.gameObject);
            
            Repaint();
        }

        [MenuItem("图标生成/场景相机")]
        public static void ShowWindow()
        {
            Obtain().Show();
        }

        private static IconGenThumbnailEditorWindow Obtain()
        {
            var editor = GetWindow<IconGenThumbnailEditorWindow>(false, null, false);
            editor.titleContent = new GUIContent("场景相机");
            return editor;
        }

    }
}