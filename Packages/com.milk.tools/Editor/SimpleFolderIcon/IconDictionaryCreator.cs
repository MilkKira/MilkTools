using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SimpleFolderIcon.Editor
{
    public class IconDictionaryCreator : AssetPostprocessor
    {
        private const string PackagePath = "Packages/com.milk.tools/Editor/SimpleFolderIcon/Icons";
        internal static Dictionary<string, Texture> IconDictionary;

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (!ContainsIconAsset(importedAssets) &&
                !ContainsIconAsset(deletedAssets) &&
                !ContainsIconAsset(movedAssets) &&
                !ContainsIconAsset(movedFromAssetPaths))
            {
                return;
            }

            BuildDictionary();
        }

        private static bool ContainsIconAsset(string[] assets)
        {
            foreach (string str in assets)
            {
                if (ReplaceSeparatorChar(Path.GetDirectoryName(str)) == PackagePath)
                {
                    return true;
                }
            }
            return false;
        }

        private static string ReplaceSeparatorChar(string path)
        {
            return path?.Replace("\\", "/");
        }

        internal static void BuildDictionary()
        {
            var dictionary = new Dictionary<string, Texture>();

            // 获取包内图标的完整路径
            string fullPath = Path.GetFullPath(PackagePath);
            var dir = new DirectoryInfo(fullPath);
            
            // 加载PNG图标
            FileInfo[] info = dir.GetFiles("*.png");
            foreach(FileInfo f in info)
            {
                string assetPath = $"{PackagePath}/{f.Name}";
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (texture != null)
                {
                    dictionary.Add(Path.GetFileNameWithoutExtension(f.Name), texture);
                }
            }

            // 加载.asset文件中的图标配置
            FileInfo[] infoSO = dir.GetFiles("*.asset");
            foreach (FileInfo f in infoSO) 
            {
                string assetPath = $"{PackagePath}/{f.Name}";
                var folderIconSO = AssetDatabase.LoadAssetAtPath<FolderIconSO>(assetPath);

                if (folderIconSO != null && folderIconSO.icon != null) 
                {
                    foreach (string folderName in folderIconSO.folderNames) 
                    {
                        if (!string.IsNullOrEmpty(folderName)) 
                        {
                            dictionary.TryAdd(folderName, folderIconSO.icon);
                        }
                    }
                }
            }
            
            IconDictionary = dictionary;
        }
    }
}