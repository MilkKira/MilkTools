using System.IO;
using UnityEditor;
using UnityEngine;

namespace SimpleFolderIcon.Editor
{
    [InitializeOnLoad]
    public class CustomFolderLoader
    {
        //static CustomFolder()
        //{
        //    IconDictionaryCreator.BuildDictionary();
        //    EditorApplication.projectWindowItemOnGUI += DrawFolderIcon;
        //}
		static CustomFolderLoader()
        {	
			//Debug.Log("注册初始化事件");
            EditorApplication.delayCall += Initialize;
            
        }
        static void Initialize()
        {
			//Debug.Log("取消初始化事件并执行图标加载");
			Debug.Log("Loading Icons");
            EditorApplication.delayCall -= Initialize;
            IconDictionaryCreator.BuildDictionary();
            EditorApplication.projectWindowItemOnGUI += CustomFolderLoader.DrawFolderIcon;
        }
        static void DrawFolderIcon(string guid, Rect rect)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var iconDictionary = IconDictionaryCreator.IconDictionary;

            if (path == "" ||
                Event.current.type != EventType.Repaint ||
                !File.GetAttributes(path).HasFlag(FileAttributes.Directory) ||
                !iconDictionary.ContainsKey(Path.GetFileName(path)))
            {
                return;
            }

            Rect imageRect;

            if (rect.height > 20)
            {
                imageRect = new Rect(rect.x - 1, rect.y - 1, rect.width + 2, rect.width + 2);
            }
            else if (rect.x > 20)
            {
                imageRect = new Rect(rect.x - 1, rect.y - 1, rect.height + 2, rect.height + 2);
            }
            else
            {
                imageRect = new Rect(rect.x + 2, rect.y - 1, rect.height + 2, rect.height + 2);
            }

            var texture = IconDictionaryCreator.IconDictionary[Path.GetFileName(path)];
            if (texture == null)
            {
                return;
            }

            GUI.DrawTexture(imageRect, texture);
        }
    }
}
