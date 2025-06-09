using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ab.SVSS
{
    [InitializeOnLoad]
    public class SceneViewScreenShot
    {
        static SceneViewScreenShot()
        {
            SceneView.duringSceneGui += OnGUI;
        }

        private static void OnGUI(SceneView sceneView)
        {
            Handles.BeginGUI();

            GUILayout.FlexibleSpace();
            
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("撮影", GUILayout.Width(40)))
                {
                    Capture(sceneView, (int) sceneView.position.width, (int) sceneView.position.height, 1, 1);
                }

                if (GUILayout.Button("高", GUILayout.Width(25)))
                {
                    Capture(sceneView, (int) sceneView.position.width, (int) sceneView.position.height, 1, 2);
                }

                if (GUILayout.Button("超", GUILayout.Width(25)))
                {
                    Capture(sceneView, (int) sceneView.position.width, (int) sceneView.position.height, 1, 4);
                }

                GUILayout.Space(10);
            }
            
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("２倍", GUILayout.Width(40)))
                {
                    Capture(sceneView, (int) sceneView.position.width, (int) sceneView.position.height, 2, 1);
                }

                if (GUILayout.Button("高", GUILayout.Width(25)))
                {
                    Capture(sceneView, (int) sceneView.position.width, (int) sceneView.position.height, 2, 2);
                }

                if (GUILayout.Button("超", GUILayout.Width(25)))
                {
                    Capture(sceneView, (int) sceneView.position.width, (int) sceneView.position.height, 2, 4);
                }

                GUILayout.Space(10);
            }

            GUILayout.Space(30);

            Handles.EndGUI();
        }

        private static void Capture(SceneView sceneView, int width, int height, int size, int scale)
        {
            width *= size;
            height *= size;
            var path = EditorUtility.SaveFilePanel("Save Image", "", "image.png", "png");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var texture = new Texture2D(width * scale, height * scale, TextureFormat.RGB24, false);
            var texture2 = new Texture2D(width, height, TextureFormat.RGB24, false);
            var rt = RenderTexture.GetTemporary(width * scale, height * scale, 32, RenderTextureFormat.Default);
            var camera = sceneView.camera;
            camera.targetTexture = rt;
            camera.Render();
            camera.targetTexture = null;

            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            texture.Apply();

            for (var i = 0; i < width; i++)
            {
                for (var j = 0; j < height; j++)
                {
                    for (var ii = 0; ii < scale; ii++)
                    {
                        var r = 0f;
                        var g = 0f;
                        var b = 0f;
                        for (var jj = 0; jj < scale; jj++)
                        {
                            var c = texture.GetPixel(i * scale + ii, j * scale + jj);
                            r += c.r;
                            g += c.g;
                            b += c.b;
                        }
                        texture2.SetPixel(i, j, new Color(r / scale,g / scale,b / scale));
                    }  
                }
            }

            byte[] bytes = texture2.EncodeToPNG();
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(texture2);

            File.WriteAllBytes(path, bytes);
            RenderTexture.active = null;
        }
    }
}