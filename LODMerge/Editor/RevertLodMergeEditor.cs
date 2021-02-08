using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
[CustomEditor(typeof(RevertLodMerge))]
public class RevertLodMergeEditor : Editor {

    public override void OnInspectorGUI()
    {
        RevertLodMerge _target = (RevertLodMerge)target;

        if (_target.original == null)
        {
            try
            {
                  Undo.DestroyObjectImmediate(this);
            }
            catch
            {

            }
            return;
        }
        else
        {
            SerializedProperty original_s = serializedObject.FindProperty("original");
            SerializedProperty texutrePath_s = serializedObject.FindProperty("texutrePath");

            GUI.enabled = false;
            EditorGUILayout.PropertyField(original_s, new GUIContent("Original Grp Reference"));
            EditorGUILayout.PropertyField(texutrePath_s, new GUIContent("texutrePath Reference"));
            GUI.enabled = true;

            GUI.color = new Color(1, 0.3f, 0.3f);
            if (GUILayout.Button("Revert"))
            {
                LodMerge lodMerge = (LodMerge)EditorWindow.GetWindow(typeof(LodMerge), true);
                lodMerge.Revert(_target);
            }
        }

    }
}
