//Written by: Eugene Chu
//Free to use

//Todo List: Support LOD group

using System.Collections;
using System.Collections.Generic;
using System;
using UnityEditor;
using UnityEngine;

public class LodMerge : EditorWindow
{

    public int sizeUnit = 30;
    public Transform _tr;
    public bool lockSelection;


    //=========================bounds Preview======================//
    Vector3 minBound = Vector3.zero;
    Vector3 maxBound = Vector3.zero;

    Vector3 topLeft;
    Vector3 topRight;
    float betweenX;
    Vector3 bottomLeft;
    Vector3 bottomRight;
    float betweenY;

    //amount
    public int amountX;
    public int amountY;
    Vector3 mean;
    int childCountMean;

    //=========================logic======================//
    public List<Vector3> locationBox = new List<Vector3>();

    [System.Serializable]
    public class Mat
    {
        public Material mat;
        public List<MeshFilter> meshR = new List<MeshFilter>();
    }

    [System.Serializable]
    public class GridNodes
    {
        public List<Mat> mat = new List<Mat>();
    }
    public List<GridNodes> gridNodes = new List<GridNodes>();

    [MenuItem("Window/StoryBook/Tools/GOGridMerger")]
    static void Init()
    {
        // Get existing open window or if none, make a new one:
        LodMerge window = (LodMerge)EditorWindow.GetWindow(typeof(LodMerge));
        window.Show();
    }

    void OnGUI()
    {
        sizeUnit = EditorGUILayout.IntField("sizeUnit", sizeUnit);
        GUILayout.Label( amountX + " x "+ amountY);
        if (_tr != null) lockSelection = EditorGUILayout.Toggle("lockSelection", lockSelection);

        EditorGUILayout.Space();

        if (Selection.activeTransform == null || Selection.activeTransform.transform.childCount < 2)
        {
            EditorGUILayout.HelpBox("Select 'Root GO' in scene to merge meshes. Reccomend to have an empty root GO", MessageType.Info);
           // _tr = null;
        }
        else
        {
            if (!lockSelection)
            {
              _tr = Selection.activeTransform.transform;
            }
            if (_tr != null &&  GUILayout.Button("Merge within: " + _tr))
            {
                DoesMerge(_tr);
            }
        }

        Repaint();
    }

    // Window has been selected======================================================================================================
    void OnFocus()
    {
        // Remove delegate listener if it has previously
        // been assigned.
        SceneView.onSceneGUIDelegate -= this.OnSceneGUI;
        // Add (or re-add) the delegate.
        SceneView.onSceneGUIDelegate += this.OnSceneGUI;
    }

    void OnDestroy()
    {
        // When the window is destroyed, remove the delegate
        // so that it will no longer do any drawing.
        SceneView.onSceneGUIDelegate -= this.OnSceneGUI;
        SceneView.RepaintAll();
    }

    void OnSceneGUI(SceneView sceneView)
    {
        if (_tr == null) return;
        // Do your drawing here using Handles.
        DrawBoundGizmos();
        SceneView.RepaintAll();
    }

    void FindChildWithinChildMean(Transform t_tr)
    {
        for (int i = 0; i < t_tr.childCount; i++)
        {
            mean += t_tr.GetChild(i).position;
            childCountMean += 1;
            if (t_tr.GetChild(i).childCount > 0) FindChildWithinChildMean(t_tr.GetChild(i));
        }
    }
    void FindChildWithinChildBound(Transform t_tr)
    {
        for (int i = 0; i < t_tr.childCount; i++)
        {
            minBound = Vector3.Min(minBound, t_tr.GetChild(i).position);
            maxBound = Vector3.Max(maxBound, t_tr.GetChild(i).position);
            if (t_tr.GetChild(i).childCount > 0) FindChildWithinChildBound(t_tr.GetChild(i));
        }
        
    }

    void DrawBoundGizmos()
    {
        //mean ==========================================
        mean = Vector3.zero;
        childCountMean = 0;
        FindChildWithinChildMean(_tr);

        mean = mean / childCountMean;

        minBound = mean;
        maxBound = mean;
        FindChildWithinChildBound(_tr);
        // draw =======================================
        Handles.color = new Color(1,0,0,0.5f);
        //four corners

        //top left
        topLeft = new Vector3(minBound.x, 0, minBound.z);
        Handles.DrawWireCube(topLeft, Vector3.one);
        Handles.Label(topLeft, "TL"+ topLeft.ToString());
        //top right
        topRight = new Vector3(minBound.x, 0, maxBound.z);
        Handles.DrawWireCube(topRight, Vector3.one);
        Handles.Label(topRight, "TR" + topRight.ToString());

        betweenX = (topRight - topLeft).z;
        Handles.Label(new Vector3(topLeft.x - 20, 0, (topLeft.z + topRight.z) / 2), betweenX.ToString());

        //bottom left
        bottomLeft = new Vector3(maxBound.x, 0, minBound.z);
        Handles.DrawWireCube(bottomLeft, Vector3.one);
        Handles.Label(bottomLeft, "BL" + bottomLeft.ToString());
        //bottom right
        bottomRight = new Vector3(maxBound.x, 0, maxBound.z);
        Handles.DrawWireCube(bottomRight, Vector3.one);
        Handles.Label(bottomRight, "BR" + bottomRight.ToString());

        betweenY = (bottomLeft - topLeft).x;
        Handles.Label(new Vector3((bottomLeft.x + topLeft.x) / 2, 0, bottomLeft.z - 20), betweenY.ToString());

        //amount setup =======================================
        amountX = Mathf.RoundToInt(betweenX/ sizeUnit)+1;
        amountY = Mathf.RoundToInt(betweenY/ sizeUnit)+1;
        for (int y = 0; y < amountY; y++)
        {
            for (int x = 0; x < amountX; x++)
            {
                Handles.DrawWireCube(new Vector3(topLeft.x + (y * sizeUnit),  0, topLeft.z + (x*sizeUnit) ) , Vector3.one * sizeUnit);
            }
        }
    }
    //===================================================================================================================================================

    void DoesMerge(Transform _Tr)
    {
        Debug.Log("Merge Start");

        //setup grid boxes
        gridNodes.Clear();
        locationBox.Clear();
        for (int y = 0; y < amountY; y++)
        {
            for (int x = 0; x < amountX; x++)
            {
                Vector3 gridCenter = new Vector3(topLeft.x + (y * sizeUnit), 0, topLeft.z + (x * sizeUnit));
                locationBox.Add (gridCenter);
                //Debug.Log(gridCenter);
            }
        }
        //presetup
        for (int i = 0; i < locationBox.Count; i++)
        {
            gridNodes.Add( new GridNodes());
        }

        //search for objs within the location box 1st
        for (int x = 0; x < locationBox.Count; x++)
        {
            FindChildWithinChild(_Tr, x);
        }
        //phase B======================================================
        CombineThruList();


    }

    void FindChildWithinChild(Transform _Tr, int x)
    {
        //go thru all the child count 2nd
        for (int i = 0; i < _Tr.childCount; i++)
        {
            if (_Tr.GetChild(i).GetComponent<MeshRenderer>() != null)
            {
                float distanceX = Mathf.Abs(_Tr.GetChild(i).position.x - locationBox[x].x);
                float distanceZ = Mathf.Abs(_Tr.GetChild(i).position.z - locationBox[x].z);
                if (distanceX <= sizeUnit / 2 && distanceZ <= sizeUnit / 2)
                {
                    //found obj within the grid
                    Debug.Log( "#" +x+ " | "+_Tr.GetChild(i).name + " @: " + locationBox[x] + " Distance: " + distanceX + "x" + distanceZ);
                    FilterOutMat(_Tr.GetChild(i).GetComponent<MeshRenderer>(), x   );
                }
            }

            //find child within child some more
            if (_Tr.GetChild(i).childCount > 0)
            {
                FindChildWithinChild(_Tr.GetChild(i), x);
            }
        }
    }


    void FilterOutMat(MeshRenderer meshRenderer, int gridNode)
    {
        Mat newMat = new Mat();
        newMat.mat = meshRenderer.sharedMaterial;
        newMat.meshR.Add (meshRenderer.GetComponent<MeshFilter>());

        bool NotFound = false;

        //find a similar mat with main texture and create it's own node
        for (int i = 0; i < gridNodes[gridNode].mat.Count; i++)
        {
            if (newMat.mat == gridNodes[gridNode].mat[i].mat &&  newMat.mat.mainTexture == gridNodes[gridNode].mat[i].mat.mainTexture)
            {
                gridNodes[gridNode].mat[i].meshR.Add(meshRenderer.GetComponent<MeshFilter>());
                NotFound = true;
            }
        }
        if (!NotFound)
        {
            gridNodes[gridNode].mat.Add(newMat);
        }
     }

    //phase B===========================================
    void CombineThruList()
    {
        GameObject newRootGO;
        newRootGO = new GameObject("Combine_"+_tr.name);
        newRootGO.transform.position = _tr.position;


        for (int i = 0; i < gridNodes.Count; i++)
        {
            for (int ii = 0; ii < gridNodes[i].mat.Count; ii++)
            {


            CombineInstance[] combine = new CombineInstance[gridNodes[i].mat[ii].meshR.Count];

            int f = 0;
            while (f < combine.Length)
            {
                combine[f].mesh = gridNodes[i].mat[ii].meshR[f].sharedMesh;
                combine[f].transform = gridNodes[i].mat[ii].meshR[f].transform.localToWorldMatrix;
                f++;
            }

            GameObject newGO;
            newGO = new GameObject(gridNodes[i].mat[ii].meshR[0].name+"_"  +gridNodes[i].mat[ii].mat.name);
            newGO.AddComponent<MeshFilter>().mesh.CombineMeshes(combine,true,true);
            newGO.AddComponent<MeshRenderer>().material = gridNodes[i].mat[ii].mat;

                newGO.transform.SetParent(newRootGO.transform);


            }

 
        }
    }
}
