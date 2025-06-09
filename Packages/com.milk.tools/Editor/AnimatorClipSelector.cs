using UnityEditor;
using UnityEngine;
using UnityEditor.Animations;
using System.Collections.Generic;

public class AnimatorClipSelector : EditorWindow
{
    private AnimatorController animatorController;
    private int selectedLayerIndex = 0; // デフォルトで最初のレイヤーを選択
    private AnimatorStateMachine selectedStateMachine = null;
    private List<AnimatorState> animationStates = new List<AnimatorState>();

    private Vector2 layerScrollPos;
    private Vector2 stateScrollPos;

    [MenuItem("Tools/AnimatorClipSelector")]
    public static void ShowWindow()
    {
        GetWindow<AnimatorClipSelector>("AnimatorClipSelector");
    }

    private void OnGUI()
    {
        GUILayout.Label("AnimatorClipSelector", EditorStyles.boldLabel);

        // AnimatorControllerの選択
        AnimatorController newAnimatorController = (AnimatorController)EditorGUILayout.ObjectField("Animator Controller", animatorController, typeof(AnimatorController), false);

        if (newAnimatorController != animatorController)
        {
            animatorController = newAnimatorController;
            if (animatorController != null)
            {
                selectedLayerIndex = 0; // 最初のレイヤーをデフォルト選択
                LoadAnimatorLayerStates();
            }
        }

        if (animatorController == null)
        {
            GUILayout.Label("Please select an Animator Controller.", EditorStyles.helpBox);
            return;
        }

        // 左側にレイヤー一覧を縦表示
        EditorGUILayout.BeginHorizontal();

        // レイヤー一覧
        EditorGUILayout.BeginVertical("box", GUILayout.Width(200));
        GUILayout.Label("Layers", EditorStyles.boldLabel);

        layerScrollPos = EditorGUILayout.BeginScrollView(layerScrollPos, GUILayout.Width(200), GUILayout.ExpandHeight(true));

        string[] layerNames = GetLayerNames(animatorController);

        for (int i = 0; i < layerNames.Length; i++)
        {
            if (GUILayout.Button(layerNames[i], GUILayout.Width(180)))
            {
                selectedLayerIndex = i;
                LoadAnimatorLayerStates();
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        // 右側にStateの詳細表示
        EditorGUILayout.BeginVertical("box");

        stateScrollPos = EditorGUILayout.BeginScrollView(stateScrollPos, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        // State一覧表示
        if (selectedStateMachine != null && animationStates.Count > 0)
        {
            GUILayout.Label("States List", EditorStyles.boldLabel);

            foreach (AnimatorState state in animationStates)
            {
                GUILayout.BeginVertical("box");

                // State名と内容表示
                if (state.motion is AnimationClip clip)
                {
                    GUILayout.Label($"{state.name}: {clip.name}");
                }
                else if (state.motion is BlendTree blendTree)
                {
                    GUILayout.Label($"{state.name}: {blendTree.name}");

                    // Blend TreeのContentsをカンマ区切りで表示
                    string contents = string.Join(", ", GetBlendTreeContents(blendTree));
                    GUILayout.Label(contents);
                }

                // Editボタン
                if (GUILayout.Button("Edit", GUILayout.Width(150)))
                {
                    if (state.motion is BlendTree blendTree)
                    {
                        PingAndSelectObject(blendTree); // Blend Treeの場合はBlend Treeを開く
                    }
                    else
                    {
                        PingAndSelectObject(state); // 通常のStateを開く
                    }
                }

                GUILayout.EndVertical();
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }

    private string[] GetLayerNames(AnimatorController controller)
    {
        string[] names = new string[controller.layers.Length];
        for (int i = 0; i < controller.layers.Length; i++)
        {
            names[i] = controller.layers[i].name;
        }
        return names;
    }

    private void LoadAnimatorLayerStates()
    {
        animationStates.Clear();

        if (selectedLayerIndex < 0 || selectedLayerIndex >= animatorController.layers.Length)
            return;

        AnimatorControllerLayer layer = animatorController.layers[selectedLayerIndex];
        selectedStateMachine = layer.stateMachine;

        foreach (ChildAnimatorState childState in selectedStateMachine.states)
        {
            if (childState.state.motion is AnimationClip || childState.state.motion is BlendTree)
            {
                animationStates.Add(childState.state);
            }
        }

        Debug.Log($"Loaded {animationStates.Count} states from layer '{layer.name}'");
    }

    private List<string> GetBlendTreeContents(BlendTree blendTree)
    {
        List<string> contents = new List<string>();

        foreach (var child in blendTree.children)
        {
            contents.Add(child.motion != null ? child.motion.name : "None");
        }

        return contents;
    }

    private void PingAndSelectObject(Object obj)
    {
        if (obj == null)
        {
            Debug.LogWarning("Object is null. Cannot select.");
            return;
        }

        EditorGUIUtility.PingObject(obj);
        Selection.activeObject = obj;

        // 確実にInspectorを更新する
        EditorApplication.delayCall += () => EditorGUIUtility.PingObject(obj);
        EditorUtility.SetDirty(obj);
    }
}
