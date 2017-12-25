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
                DestroyImmediate(this);
            }
            catch
            {

            }
            return;
        }
        else
        {
            GUI.color = new Color(1, 0.3f, 0.3f);
            if (GUILayout.Button("Revert"))
            {
                _target.original.SetActive(true);
                _target.original.tag = "Untagged";
                Selection.activeGameObject = _target.original;
                DestroyImmediate(_target.gameObject);
            }
        }

    }
}
