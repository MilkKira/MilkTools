using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class AllSkinnedMeshRendererSetter : EditorWindow
{
	//对象
	private static GameObject _parentObject;
	//根骨骼
	private static Transform _rootBone; 
	//锚点
	private static Transform _anchor;
	//边界
	private static Bounds _bounds = new Bounds(Vector3.zero, Vector3.one * 2);
	

	//Skinned Mesh Renderer
	private static List<GameObject> _skinnedMeshObjects;
	//Mesh Renderer
	//private static List<GameObject> _meshObjects;



	
	//模型右键菜单，取消菜单项，在菜单中的排序位置
	[MenuItem ("GameObject/AllSkinnedMeshRendererSetter", false, 20)]
	public static void ShowWindow () {
		// 右クリックした要素を設定
		_parentObject = Selection.activeGameObject;
		
		Animator anime = _parentObject.GetComponent<Animator>();
		if(anime)
		{
			_rootBone = anime.GetBoneTransform(HumanBodyBones.Hips);
			_anchor = anime.GetBoneTransform(HumanBodyBones.Chest);
		}
		EditorWindow.GetWindow (typeof (AllSkinnedMeshRendererSetter));
	}
	
	private void OnGUI()
	{
		EditorGUILayout.BeginVertical(GUI.skin.box);
		
		EditorGUILayout.BeginHorizontal();	//预制体
		_parentObject = (GameObject)EditorGUILayout.ObjectField("预制体", _parentObject, typeof(GameObject), true);
		EditorGUILayout.EndHorizontal();
		
		EditorGUILayout.BeginHorizontal();	// RootBone
		_rootBone = (Transform)EditorGUILayout.ObjectField("根骨骼(Hip)", _rootBone, typeof(Transform), true);
		EditorGUILayout.EndHorizontal();
		
		EditorGUILayout.BeginHorizontal();	// Prove Anchor
		_anchor = (Transform)EditorGUILayout.ObjectField("锚点覆盖(Chest)", _anchor, typeof(Transform), true);
		EditorGUILayout.EndHorizontal();
		
		EditorGUILayout.BeginHorizontal();	// Bounds
		_bounds = EditorGUILayout.BoundsField("渲染边界", _bounds);
		EditorGUILayout.EndHorizontal();

		EditorGUILayout.EndVertical();

		if (GUILayout.Button("开始设置!"))
		{
			//执行
			exec();
		}
	}

	private void exec()
	{
		// 参数检查
		if(_parentObject == null || _rootBone == null || _anchor == null)
		{
			EditorUtility.DisplayDialog("错误", "参数无效", "Error");
			return;
		}

		// 队伍初始化
		_skinnedMeshObjects = new List<GameObject>();
		//_meshObjects = new List<GameObject>();
		
		// 获取全部Skinned Mesh Renderer
		getAllSkinnedMeshes();
		
		// 设定
		foreach(GameObject obj in _skinnedMeshObjects)
		{
			var skinnedMesh = obj.GetComponent<SkinnedMeshRenderer>();
			var transform = obj.transform;
			
			// 允许撤销行为
			Undo.RecordObject(skinnedMesh, "SETTING " + skinnedMesh.name);
			Undo.RecordObject(transform, "SETTING " + transform.name);
			
			// 位置重置
			transform.localPosition = Vector3.zero;
			transform.localRotation = Quaternion.identity;
			transform.localScale    = Vector3.one;
			//设定锚点和根骨骼
			skinnedMesh.rootBone = _rootBone;
			skinnedMesh.localBounds = _bounds;
			skinnedMesh.probeAnchor = _anchor;

		}
		/*
		foreach(GameObject obj in _meshObjects)
		{
			var mesh = obj.GetComponent<MeshRenderer>();
			
			Undo.RecordObject(mesh, "SETTING " + mesh.name);
			
			mesh.probeAnchor = _anchor.transform;
		}
		*/
		
		EditorUtility.DisplayDialog("完成", "设定已经完成", "Finished");
		return;
	}

	
	//获取全部蒙皮网格渲染器
	private static void getAllSkinnedMeshes()
	{
		getChildren(_parentObject.transform);
	}
	
	private static void getChildren(Transform t)
	{
		Transform children = t.GetComponentInChildren<Transform>();

		foreach (Transform child in children)
		{
			// SkinnedMeshRendererを追加する
			if(child.GetComponent<SkinnedMeshRenderer>())
			{
				_skinnedMeshObjects.Add(child.gameObject);
			}
			
			/*
			if(child.GetComponent<MeshRenderer>())
			{
				_meshObjects.Add(child.gameObject);
			}
			*/
			getChildren(child);
		}
	}

}
