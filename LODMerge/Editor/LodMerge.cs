//Written by: Eugene Chu
//Twitter @LenZ_Chu
//Free to use. Please mention or credit my name in projects if possible!

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

    public float lodDistance = 0.05f;

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
        public List<GameObject> meshGO = new List<GameObject>();
        public List<MeshFilter> meshR = new List<MeshFilter>();
        public List<int> lodMesh = new List<int>();
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
        EditorGUILayout.HelpBox("If using LOD 0-3 setup, name your child with the suffix: 'lod_0', 'lod_1', 'lod_2', respectfully.", MessageType.Info);
        sizeUnit = EditorGUILayout.IntField("sizeUnit", sizeUnit);
        GUILayout.Label( amountX + " x "+ amountY);
        if (_tr != null) lockSelection = EditorGUILayout.Toggle("lockSelection", lockSelection);
        //lodDistance = EditorGUILayout.Slider("lodDistance", lodDistance, 0, 1);

        EditorGUILayout.Space();

        if (Selection.activeTransform == null || Selection.activeTransform.transform.childCount < 2)
        {
            EditorGUILayout.HelpBox("Select 'Root GO' in scene to merge meshes. Requires an empty root GO container", MessageType.Info);
           // _tr = null;
        }
        else
        {
            if (!lockSelection)
            {
              _tr = Selection.activeTransform.transform;
            }
            if (_tr != null &&  GUILayout.Button("Merge within: " + _tr.name))
            {
                _tr.gameObject.SetActive(true);
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
        newMat.meshGO.Add(meshRenderer.gameObject);
        //newMat.lodMesh.Add(-1); //defualt is -1. ignores making lod

        bool NotFound = false;

        //find a similar mat with main texture and create it's own node
        for (int i = 0; i < gridNodes[gridNode].mat.Count; i++)
        {
            if (newMat.mat == gridNodes[gridNode].mat[i].mat && newMat.mat.mainTexture == gridNodes[gridNode].mat[i].mat.mainTexture)
            {
                //lod
                if (meshRenderer.gameObject.name.Contains("LOD0") || meshRenderer.gameObject.name.Contains("LOD_0") || meshRenderer.gameObject.name.Contains("Lod0") || meshRenderer.gameObject.name.Contains("Lod_0") || meshRenderer.gameObject.name.Contains("lod0") || meshRenderer.gameObject.name.Contains("lod_0"))
                {
                    Debug.Log("got 0");
                    gridNodes[gridNode].mat[i].lodMesh.Add(0);
                } else if (meshRenderer.gameObject.name.Contains("LOD1") || meshRenderer.gameObject.name.Contains("LOD_1") || meshRenderer.gameObject.name.Contains("Lod1") || meshRenderer.gameObject.name.Contains("Lod_1") || meshRenderer.gameObject.name.Contains("lod1") || meshRenderer.gameObject.name.Contains("lod_1"))
                {
                    Debug.Log("got 1");
                    gridNodes[gridNode].mat[i].lodMesh.Add(1);
                }
                else if (meshRenderer.gameObject.name.Contains("LOD2") || meshRenderer.gameObject.name.Contains("LOD_2") || meshRenderer.gameObject.name.Contains("Lod2") || meshRenderer.gameObject.name.Contains("Lod_2") || meshRenderer.gameObject.name.Contains("lod2") || meshRenderer.gameObject.name.Contains("lod_2"))
                {
                    Debug.Log("got 2");
                    gridNodes[gridNode].mat[i].lodMesh.Add(2);
                }
                else
                {
                    gridNodes[gridNode].mat[i].lodMesh.Add(-1);
                }

                gridNodes[gridNode].mat[i].meshR.Add(meshRenderer.GetComponent<MeshFilter>());
                gridNodes[gridNode].mat[i].meshGO.Add(meshRenderer.gameObject);
                NotFound = true;
            }
        
        }
        if (!NotFound)
        {
            //lod
            if (meshRenderer.gameObject.name.Contains("LOD0") || meshRenderer.gameObject.name.Contains("LOD_0") || meshRenderer.gameObject.name.Contains("Lod0") || meshRenderer.gameObject.name.Contains("Lod_0") || meshRenderer.gameObject.name.Contains("lod0") || meshRenderer.gameObject.name.Contains("lod_0"))
            {
                Debug.Log("got 0");
                newMat.lodMesh.Add(0);
            }
            else if (meshRenderer.gameObject.name.Contains("LOD1") || meshRenderer.gameObject.name.Contains("LOD_1") || meshRenderer.gameObject.name.Contains("Lod1") || meshRenderer.gameObject.name.Contains("Lod_1") || meshRenderer.gameObject.name.Contains("lod1") || meshRenderer.gameObject.name.Contains("lod_1"))
            {
                Debug.Log("got 1");
                newMat.lodMesh.Add(1);
            }
            else if (meshRenderer.gameObject.name.Contains("LOD2") || meshRenderer.gameObject.name.Contains("LOD_2") || meshRenderer.gameObject.name.Contains("Lod2") || meshRenderer.gameObject.name.Contains("Lod_2") || meshRenderer.gameObject.name.Contains("lod2") || meshRenderer.gameObject.name.Contains("lod_2"))
            {
                Debug.Log("got 2");
                newMat.lodMesh.Add(2);
            }
            else
            {
                newMat.lodMesh.Add(-1);
            }

            gridNodes[gridNode].mat.Add(newMat);
        }
     }

    //phase B===========================================
    void CombineThruList()
    {
        //find if exist and delete
        if (GameObject.Find("Combine_" + _tr.name) != null)
        {
            DestroyImmediate(GameObject.Find("Combine_" + _tr.name));
        }
        //=====================

        GameObject newRootGO;
        newRootGO = new GameObject("Combine_"+_tr.name);
        newRootGO.transform.position = _tr.position;


        for (int i = 0; i < gridNodes.Count; i++)
        {
            for (int ii = 0; ii < gridNodes[i].mat.Count; ii++)
            {


                List<CombineInstance> combine = new List<CombineInstance>(); // = new CombineInstance[gridNodes[i].mat[ii].meshR.Count];

                List<CombineInstance> combine0 = new List<CombineInstance>();
                List<CombineInstance> combine1 = new List<CombineInstance>();
                List<CombineInstance> combine2 = new List<CombineInstance>();

                int f = 0;
            while (f < gridNodes[i].mat[ii].meshR.Count)
            {
                    CombineInstance _combine =   new CombineInstance();
                    _combine.mesh = gridNodes[i].mat[ii].meshR[f].sharedMesh;
                     _combine.transform = gridNodes[i].mat[ii].meshR[f].transform.localToWorldMatrix;

                    Debug.Log(gridNodes[i].mat[ii].lodMesh[f] + " |||" + gridNodes[i].mat[ii].meshGO[f].name );
                    if (gridNodes[i].mat[ii].lodMesh[f] == 0)
                    {
                        combine0.Add(_combine);
                    } else if (gridNodes[i].mat[ii].lodMesh[f] == 1)
                    {
                        combine1.Add(_combine);
                    }
                    else if (gridNodes[i].mat[ii].lodMesh[f] == 2)
                    {
                        combine2.Add(_combine);
                    }
                    else
                    {
                        combine.Add(_combine);
                    }


                f++;
            }


            //create new gameobject
            GameObject newGO;
            newGO = new GameObject(gridNodes[i].mat[ii].meshR[0].name+"_"  +gridNodes[i].mat[ii].mat.name);
            newGO.AddComponent<MeshFilter>().mesh.CombineMeshes(combine.ToArray(),true,true);
            newGO.AddComponent<MeshRenderer>().material = gridNodes[i].mat[ii].mat;

            newGO.transform.SetParent(newRootGO.transform);

            //lod component
            Renderer[] render = new Renderer[1];
            render[0] = newGO.GetComponent<Renderer>();

            LOD[] lod = new LOD[1];
            lod[0].fadeTransitionWidth = lodDistance;
            lod[0].screenRelativeTransitionHeight = lodDistance;
            lod[0].renderers = render;

            newGO.AddComponent<LODGroup>().SetLODs(lod);



                //lod 0-2========================================================================================
                int _set = -1;
                if (combine0.Count > 0) _set = 1;
                if (combine1.Count > 0) _set = 2;
                if (combine2.Count > 0) _set = 3;

                Debug.Log("combine0.Count " + combine0.Count);
                Debug.Log("combine1.Count " + combine1.Count);
                Debug.Log("combine2.Count " + combine2.Count);
               // Debug.Log("set amount LOD" + _set);

                GameObject newGO_LOD0;
                GameObject newGO_LOD1;
                GameObject newGO_LOD2;
                Renderer[] render0 = new Renderer[0];
                Renderer[] render1 = new Renderer[0];
                Renderer[] render2 = new Renderer[0];

                if (_set > -1)
                {
                    //sub root for lod
                    newGO = new GameObject(gridNodes[i].mat[ii].meshR[0].name + "_" + gridNodes[i].mat[ii].mat.name + "_LOD");
                    newGO.transform.SetParent(newRootGO.transform);
                }

                if (combine0.Count > 0)
                {
                    newGO_LOD0 = new GameObject(gridNodes[i].mat[ii].meshR[0].name + "_" + gridNodes[i].mat[ii].mat.name + "_LOD0");

                    newGO_LOD0.AddComponent<MeshFilter>().mesh.CombineMeshes(combine0.ToArray(), true, true);
                    newGO_LOD0.AddComponent<MeshRenderer>().material = gridNodes[i].mat[ii].mat;
                    newGO_LOD0.transform.SetParent(newGO.transform);

                    //lod component
                    render0 = new Renderer[1];
                    render0[0] = newGO_LOD0.GetComponent<Renderer>(); 
                }
                if (combine1.Count > 0)
                {
                    newGO_LOD1 = new GameObject(gridNodes[i].mat[ii].meshR[0].name + "_" + gridNodes[i].mat[ii].mat.name + "_LOD1");

                    newGO_LOD1.AddComponent<MeshFilter>().mesh.CombineMeshes(combine1.ToArray(), true, true);
                    newGO_LOD1.AddComponent<MeshRenderer>().material = gridNodes[i].mat[ii].mat;
                    newGO_LOD1.transform.SetParent(newGO.transform);

                    //lod component
                    render1 = new Renderer[1];
                    render1[0] = newGO_LOD1.GetComponent<Renderer>();
                }
                if (combine2.Count > 0)
                {
                    newGO_LOD2 = new GameObject(gridNodes[i].mat[ii].meshR[0].name + "_" + gridNodes[i].mat[ii].mat.name + "_LOD2");

                    newGO_LOD2.AddComponent<MeshFilter>().mesh.CombineMeshes(combine2.ToArray(), true, true);
                    newGO_LOD2.AddComponent<MeshRenderer>().material = gridNodes[i].mat[ii].mat;
                    newGO_LOD2.transform.SetParent(newGO.transform);

                    //lod component
                    render2 = new Renderer[1];
                    render2[0] = newGO_LOD2.GetComponent<Renderer>();
                }

                if (_set > -1)
                {

                    LOD[] lods = new LOD[_set];
                    Renderer[] lod_renderers = new Renderer[_set];
                    for (int c = 0; c < _set; c++)
                    {
                        if (c == 0) lods[c] = new LOD(1.0F / (c + 2), render0);
                        else if (c == 1) lods[c] = new LOD(1.0F / (c + 2), render1);
                        else if (c == 2) lods[c] = new LOD(1.0F / (c + 2), render2);
                    }
                    newGO.AddComponent<LODGroup>().SetLODs(lods);
                    

                }

                //=============================================================================================

            }

 
        }

        //end==================
        _tr.gameObject.SetActive(false);
    }
}
