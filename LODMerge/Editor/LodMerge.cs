//Written by: Eugene Chu
//Twitter @LenZ_Chu
//Free to use. Please mention or credit my name in projects if possible!

using System.Collections;
using System.Collections.Generic;
using System;
using UnityEditor;
using UnityEngine;
using System.IO;
using UnityEditor.SceneManagement;
public class LodMerge : EditorWindow
{
    public enum State
    {
        Merge=0, AtlasPack =1
    }
    public State state;

    public int sizeUnit = 30;
    public Transform _tr = null;
    public bool lockSelection;

    public float lodDistance = 0.02f;

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

    //======atlas state ==//
    public int atlasTextureSize = 1024;

    [MenuItem("Window/StoryBook/Tools/GOGridMerger")]
    static void Init()
    {
        // Get existing open window or if none, make a new one:
        LodMerge window = (LodMerge)EditorWindow.GetWindow(typeof(LodMerge));
        window.Show();
    }


    void OnGUI()
    {
        state= (State)EditorGUILayout.EnumPopup(state);

        //if (state == State.Merge)
        //{
            EditorGUILayout.HelpBox("If using LOD 0-3 setup, name your child with the suffix: 'LOD0', 'LOD1', 'LOD2' respectfully.", MessageType.Info);
            GUILayout.BeginHorizontal();
            sizeUnit = EditorGUILayout.IntField("sizeUnit", sizeUnit);

            if (GUILayout.Button("10")) sizeUnit = 10;
            if (GUILayout.Button("50")) sizeUnit = 50;
            if (GUILayout.Button("100")) sizeUnit = 100;
            if (GUILayout.Button("500")) sizeUnit = 500;
            GUILayout.EndHorizontal();
            if (sizeUnit < 5) sizeUnit = 5;
            GUILayout.Label(amountX + " x " + amountY);

            //spacer
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            //
        //}
        if (_tr != null) lockSelection = EditorGUILayout.Toggle("lockSelection", lockSelection);

        if (_tr == null || (Selection.activeTransform == null || Selection.activeTransform.transform.childCount < 2))
        {
            EditorGUILayout.HelpBox("Select 'Root GO' in scene to merge meshes. Requires an empty root GO container", MessageType.Info);
        }
        else
        {

            if (GUILayout.Button("Merge within: " + _tr.name))
            {
                Undo.RecordObject(_tr, "setActive");
                _tr.gameObject.SetActive(true);
                _tr.tag = "EditorOnly";
              
                DoesMerge(_tr);

            }
        }

        if (Selection.activeTransform != null && Selection.activeTransform.GetComponent<RevertLodMerge>() != null)
        {
            RevertLodMerge _revert = Selection.activeTransform.GetComponent<RevertLodMerge>();
            if (_revert.original != null)
            {
                GUI.color = new Color(1, 0.3f, 0.3f);
                if (GUILayout.Button("Revert"))
                {
                    Revert(_revert);
                }
            }
            else
            {
                Undo.DestroyObjectImmediate(_revert);
            }
        }
        GUI.color = Color.white;

        //atlas ui
        if (state == State.AtlasPack)
        {
            //sizeUnit = 9999;
            //spacer
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.HelpBox("Name the GameObject unique names to prevent texture overwrites!", MessageType.Info);
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.BeginHorizontal();
            atlasTextureSize = EditorGUILayout.IntField("atlasTextureSize", atlasTextureSize);

            if (GUILayout.Button("128")) atlasTextureSize = 128;
            if (GUILayout.Button("256")) atlasTextureSize = 256;
            if (GUILayout.Button("512")) atlasTextureSize = 512;
            if (GUILayout.Button("1024")) atlasTextureSize = 1024;
            GUILayout.EndHorizontal();
            GUILayout.Box(atlas, GUILayout.Width(Screen.width/2), GUILayout.Height(Screen.height/2), GUILayout.MaxWidth(atlasTextureSize), GUILayout.MaxHeight(atlasTextureSize));

            GUILayout.EndVertical();
        }

    }

    public void Revert(RevertLodMerge _revert)
    {
        Undo.RecordObject(_revert.original, "setActive");
        _revert.original.SetActive(true);
        Selection.activeGameObject = _revert.original;
        Undo.DestroyObjectImmediate(_revert.gameObject);

        if (_revert.texutrePath != string.Empty)  AssetDatabase.DeleteAsset(_revert.texutrePath);
        if (_revert.materialPath != string.Empty) AssetDatabase.DeleteAsset(_revert.materialPath);

    }
    void OnSelectionChange()
    {
        if ( (Selection.activeTransform != null && Selection.activeTransform.transform.childCount > 1))
        {
            if (!lockSelection)
            {
                _tr = Selection.activeTransform.transform;
            }
        }
        Repaint();
        SceneView.RepaintAll();
    }
    // Window has been selected======================================================================================================
    void OnFocus()
    {
        OnSelectionChange();
        // Remove delegate listener if it has previously
        // been assigned.
        SceneView.duringSceneGui -= this.OnSceneGUI;
        // Add (or re-add) the delegate.
        SceneView.duringSceneGui += this.OnSceneGUI;
    }

    void OnDestroy()
    {
        // When the window is destroyed, remove the delegate
        // so that it will no longer do any drawing.
        SceneView.duringSceneGui -= this.OnSceneGUI;
    }

    void OnSceneGUI(SceneView sceneView)
    {
        if (_tr == null) return;
        // Do your drawing here using Handles.
        DrawBoundGizmos();
    }

    void FindChildWithinChildMean(Transform t_tr)
    {
        Transform[] AllChild = t_tr.GetComponentsInChildren<Transform>();
        for (int i = 0; i < AllChild.Length; i++)
        {
            mean += AllChild[i].position;
            childCountMean += 1;
        }
    }
    void FindChildWithinChildBound(Transform t_tr)
    {
        Transform[] AllChild = t_tr.GetComponentsInChildren<Transform>();
        for (int i = 0; i < AllChild.Length; i++)
        {
            minBound = Vector3.Min(minBound, AllChild[i].position);
            maxBound = Vector3.Max(maxBound, AllChild[i].position);
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
        float minYPos = minBound.y;
        FindChildWithinChildBound(_tr);
        // draw =======================================
        Handles.color = new Color(1,0,0,0.5f);
        //four corners

        //top left
        topLeft = new Vector3(minBound.x, minYPos, minBound.z);
        Handles.DrawWireCube(topLeft, Vector3.one);
        Handles.Label(topLeft, "TL"+ topLeft.ToString());
        //top right
        topRight = new Vector3(minBound.x, minYPos, maxBound.z);
        Handles.DrawWireCube(topRight, Vector3.one);
        Handles.Label(topRight, "TR" + topRight.ToString());

        betweenX = (topRight - topLeft).z;
        Handles.Label(new Vector3(topLeft.x - 20, minYPos, (topLeft.z + topRight.z) / 2), betweenX.ToString());

        //bottom left
        bottomLeft = new Vector3(maxBound.x, minYPos, minBound.z);
        Handles.DrawWireCube(bottomLeft, Vector3.one);
        Handles.Label(bottomLeft, "BL" + bottomLeft.ToString());
        //bottom right
        bottomRight = new Vector3(maxBound.x, minYPos, maxBound.z);
        Handles.DrawWireCube(bottomRight, Vector3.one);
        Handles.Label(bottomRight, "BR" + bottomRight.ToString());

        betweenY = (bottomLeft - topLeft).x;
        Handles.Label(new Vector3((bottomLeft.x + topLeft.x) / 2, minYPos, bottomLeft.z - 20), betweenY.ToString());

        //amount setup =======================================
        amountX = Mathf.RoundToInt(betweenX/ sizeUnit)+1;
        amountY = Mathf.RoundToInt(betweenY/ sizeUnit)+1;
        for (int y = 0; y < amountY; y++)
        {
            for (int x = 0; x < amountX; x++)
            {
                Handles.DrawWireCube(new Vector3(topLeft.x + (y * sizeUnit), minYPos, topLeft.z + (x*sizeUnit) ) , Vector3.one * sizeUnit);
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
        if (state == State.Merge) CombineThruList();
        else if (state == State.AtlasPack) AtlasCombine();

    }

    void FindChildWithinChild(Transform _Tr, int x)
    {
        MeshRenderer[] MRs = _Tr.GetComponentsInChildren<MeshRenderer>();
        //go thru all the child count 2nd
        for (int i = 0; i < MRs.Length; i++)
        {

            float distanceX = Mathf.Abs(MRs[i].transform.position.x - locationBox[x].x);
            float distanceZ = Mathf.Abs(MRs[i].transform.position.z - locationBox[x].z);
            if (distanceX <= sizeUnit / 2 && distanceZ <= sizeUnit / 2)
            {
                //found obj within the grid
                Debug.Log( "#" +x+ " | "+ MRs[i].transform.name + " @: " + locationBox[x] + " Distance: " + distanceX + "x" + distanceZ);
                FilterOutMat(MRs[i], x   );
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

    //atlas texture
    List <Texture2D> atlasTextures = new List<Texture2D>();
    Rect[] rects;
    Texture2D atlas;

    //phase B Merge===========================================
    void AtlasCombine()
    {
        //atlas setup

        atlasTextures.Clear();
        //make all textures found readable
        for (int i = 0; i < gridNodes.Count; i++)
        {
            for (int ii = 0; ii < gridNodes[i].mat.Count; ii++)
            {
                Texture2D _mainTex = (Texture2D)gridNodes[i].mat[ii].mat.mainTexture; //get the main texture
                if (_mainTex != null)
                {
                    string path = AssetDatabase.GetAssetPath(_mainTex);

                    TextureImporter importer = (TextureImporter)TextureImporter.GetAtPath(path);
                    importer.isReadable = true;
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                    atlasTextures.Add(_mainTex);
                }

            }
        }

        //make texture ------------------------------------------------------------------------------------------
        Texture2D preBakeAtlas = new Texture2D(atlasTextureSize, atlasTextureSize);
        rects = preBakeAtlas.PackTextures(atlasTextures.ToArray(), 0, atlasTextureSize, false);

        //TO DO please make a list of texture name to reference back what each rect i are so I can reference to my grid support. Avoid unknow material uv set//////////////////////////////////////!!!

        //Encode the packed texture to PNG
        byte[] bytes = preBakeAtlas.EncodeToTGA();
        //Save the packed texture to the datapath of your choice
        string SelectedName = _tr.name;

        string SetFolderPath = Path.GetDirectoryName(EditorSceneManager.GetActiveScene().path) + "/AtlasCombine";
        var folder = Directory.CreateDirectory(SetFolderPath);
        string SetTexturePath = SetFolderPath +"/"+ SelectedName + ".tga";
        string SetMathPath = SetFolderPath + "/" + SelectedName +"_AtlasMat"+ ".mat";
        //texture
        File.WriteAllBytes(SetTexturePath, bytes);
        AssetDatabase.ImportAsset(SetTexturePath, ImportAssetOptions.ForceUpdate);
        atlas = (Texture2D)AssetDatabase.LoadAssetAtPath(SetTexturePath, typeof(Texture2D));
        //mat
        Material atlasMat = new Material(Shader.Find("Standard"));
        atlasMat.mainTexture = atlas;
        AssetDatabase.CreateAsset(atlasMat, SetMathPath);
        AssetDatabase.ImportAsset(SetMathPath, ImportAssetOptions.ForceUpdate);

        //destroy prebake
        DestroyImmediate(preBakeAtlas);

        //cleanup atlas. revert all textures found to unreadable
        for (int i = 0; i < gridNodes.Count; i++)
        {
            for (int ii = 0; ii < gridNodes[i].mat.Count; ii++)
            {
                Texture2D _mainTex = (Texture2D)gridNodes[i].mat[ii].mat.mainTexture;
                string path = AssetDatabase.GetAssetPath(_mainTex);

                TextureImporter importer = (TextureImporter)TextureImporter.GetAtPath(path);
                importer.isReadable = false;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }
        


        //setup
        GameObject newRootGO;
        newRootGO = new GameObject("Combine_" + _tr.name);
        Undo.RegisterCreatedObjectUndo(newRootGO, "created GO");
        Undo.RecordObject(newRootGO.transform, "Move GO");
        newRootGO.transform.position = _tr.position;

        RevertLodMerge _revertLODMerge = newRootGO.gameObject.AddComponent<RevertLodMerge>();
        _revertLODMerge.original = _tr.gameObject;
        _revertLODMerge.texutrePath = SetTexturePath;
        _revertLODMerge.materialPath = SetMathPath;
        //organizing
        if (_tr.parent != null) newRootGO.transform.SetParent(_tr.parent);
        newRootGO.transform.SetSiblingIndex(_tr.GetSiblingIndex());
        //----------------------------
 
        for (int i = 0; i < gridNodes.Count; i++)
        {
            for (int ii = 0; ii < gridNodes[i].mat.Count; ii++)
            {
                List<CombineInstance> combine = new List<CombineInstance>();

                int meshR_i = 0;
                int TotalMeshR_Count = gridNodes[i].mat[ii].meshR.Count;

                int uv_x = 0;
                int uv_y = 0;
                int OptimizedRowColumn = (int)Math.Ceiling(Mathf.Sqrt(TotalMeshR_Count));
                Debug.Log("OptimizedRowColumn" + OptimizedRowColumn);
                float perPerc = ((float)1 / (float)OptimizedRowColumn);


                while (meshR_i < TotalMeshR_Count)
                {
                    CombineInstance _combine = new CombineInstance();

                    Mesh newMesh = Instantiate(gridNodes[i].mat[ii].meshR[meshR_i].sharedMesh);
                    newMesh.name = "_processingMesh_";

                    _combine.mesh = newMesh;
                    _combine.transform = gridNodes[i].mat[ii].meshR[meshR_i].transform.localToWorldMatrix;

                    int ThisRectID = -1;
                    for (int x = 0; x < atlasTextures.Count; x++)
                    {
                        if (atlasTextures[x] == gridNodes[i].mat[ii].mat.mainTexture) ThisRectID = x;
                    }

                    ///uv0
                    if (ThisRectID > -1)
                    {
                        Vector2[] lightMapUV0 = newMesh.uv;
                        int thisUV0_i = 0;
                        while (thisUV0_i < lightMapUV0.Length)
                        {
                            Debug.Log("rect + " + ThisRectID + "  " + rects[ThisRectID]);
                            lightMapUV0[thisUV0_i] = (lightMapUV0[thisUV0_i] * new Vector2(rects[ThisRectID].width, rects[ThisRectID].height)) + (new Vector2(rects[ThisRectID].x, rects[ThisRectID].y));


                            thisUV0_i += 1;
                        }
                        newMesh.SetUVs(0, lightMapUV0);
                    }

                    //uv1 lightmap atlasing
                    Vector2[] lightMapUV = newMesh.uv2;

                    int thisUV_i = 0;
                    while (thisUV_i < lightMapUV.Length)
                    {
                        float uv_x_currentPerc = (uv_x * perPerc);
                        float uv_y_currentPerc = (uv_y * perPerc);

                        lightMapUV[thisUV_i] = (lightMapUV[thisUV_i] * perPerc) +
                           new Vector2(uv_x_currentPerc, uv_y_currentPerc);


                        thisUV_i += 1;
                    }
                    uv_x += 1;
                    if (uv_x >= OptimizedRowColumn)
                    {
                        uv_x = 0;
                        uv_y += 1;
                    }

                    newMesh.SetUVs(1, lightMapUV);





                    combine.Add(_combine);

                    meshR_i += 1;
                }




                GameObject newGO;
                newGO = new GameObject("_Combine_Atlas_");
                if (combine.Count > 0)
                {
                    Mesh s = new Mesh();
                    s.name = "_Combine_Atlas_Mesh";
                    MeshFilter mf = newGO.AddComponent<MeshFilter>();
                    s.CombineMeshes(combine.ToArray(), true, true);
                    mf.sharedMesh = s;

                    MeshRenderer thisMr = newGO.AddComponent<MeshRenderer>();
                    thisMr.sharedMaterial = atlasMat;
                    newGO.transform.SetParent(newRootGO.transform);

                }
                else
                {
                    if (newGO != null)
                    {
                        Undo.DestroyObjectImmediate(newGO);
                        //DestroyImmediate(newGO);
                    }
                }


            }
        }

 


        //------------------------- end
        Undo.RecordObject(_tr.gameObject, "setActive");
        _tr.gameObject.SetActive(false);
        Selection.activeGameObject = newRootGO;

    }
    void CombineThruList()
    {
        //setup
        GameObject newRootGO;
        newRootGO = new GameObject("Combine_"+_tr.name);
        Undo.RegisterCreatedObjectUndo(newRootGO, "created GO");
        Undo.RecordObject(newRootGO.transform, "Move GO");
        newRootGO.transform.position = _tr.position;

        newRootGO.gameObject.AddComponent<RevertLodMerge>().original = _tr.gameObject;
        //organizing
        if (_tr.parent != null) newRootGO.transform.SetParent(_tr.parent);
         newRootGO.transform.SetSiblingIndex(_tr.GetSiblingIndex());


        for (int i = 0; i < gridNodes.Count; i++)
        {
            for (int ii = 0; ii < gridNodes[i].mat.Count; ii++)
            {
                List<CombineInstance> combine = new List<CombineInstance>(); // = new CombineInstance[gridNodes[i].mat[ii].meshR.Count];

                List<CombineInstance> combine0 = new List<CombineInstance>();
                List<CombineInstance> combine1 = new List<CombineInstance>();
                List<CombineInstance> combine2 = new List<CombineInstance>();

                int meshR_i = 0;
                int TotalMeshR_Count = gridNodes[i].mat[ii].meshR.Count;

                int uv_x = 0;
                int uv_y = 0;
                int OptimizedRowColumn =(int) Math.Ceiling (Mathf.Sqrt(TotalMeshR_Count));
                Debug.Log("OptimizedRowColumn" + OptimizedRowColumn);
                float perPerc = ((float)1 / (float)OptimizedRowColumn);


                while (meshR_i < TotalMeshR_Count)
                {
                    CombineInstance _combine =   new CombineInstance();
                    Mesh newMesh = Instantiate(gridNodes[i].mat[ii].meshR[meshR_i].sharedMesh);
                    newMesh.name = "_processingMesh_";

                    _combine.mesh = newMesh;
                    _combine.transform = gridNodes[i].mat[ii].meshR[meshR_i].transform.localToWorldMatrix;

                    //uv1 lightmap atlasing
                    Vector2[] lightMapUV = newMesh.uv2;

                    int thisUV_i = 0;
                    while (thisUV_i < lightMapUV.Length)
                    {
                        float uv_x_currentPerc = (uv_x * perPerc);
                        float uv_y_currentPerc = (uv_y * perPerc);

                         lightMapUV[thisUV_i] = (lightMapUV[thisUV_i] * perPerc) +
                            new Vector2(uv_x_currentPerc, uv_y_currentPerc);


                        thisUV_i+=1;
                    }
                    uv_x += 1;
                    if (uv_x >= OptimizedRowColumn)
                    {
                        uv_x = 0;
                        uv_y += 1;
                    }

                    newMesh.SetUVs(1, lightMapUV);

                    //-------------------------

                    Debug.Log(gridNodes[i].mat[ii].lodMesh[meshR_i] + " |||" + gridNodes[i].mat[ii].meshGO[meshR_i].name );
                    if (gridNodes[i].mat[ii].lodMesh[meshR_i] == 0)
                    {
                        combine0.Add(_combine);
                    } else if (gridNodes[i].mat[ii].lodMesh[meshR_i] == 1)
                    {
                        combine1.Add(_combine);
                    }
                    else if (gridNodes[i].mat[ii].lodMesh[meshR_i] == 2)
                    {
                        combine2.Add(_combine);
                    }
                    else
                    {
                        combine.Add(_combine);
                    }


                    meshR_i+=1;
                }

                //create new gameobject

                GameObject newGO;
                newGO = new GameObject(gridNodes[i].mat[ii].meshR[0].name + "_" + gridNodes[i].mat[ii].mat.name);
                Undo.RegisterCreatedObjectUndo(newGO, "created GO");
                if (combine.Count > 0)
                {
                    Mesh s = new Mesh();
                    s.name = "_gridCombineMesh_"+ newGO.name;
                    MeshFilter mf = newGO.AddComponent<MeshFilter>();
                    s.CombineMeshes(combine.ToArray(), true, true);
                    mf.sharedMesh = s;


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
                }
                else
                {
                    if (newGO != null)
                    {
                        Undo.DestroyObjectImmediate(newGO);
                        //DestroyImmediate(newGO);
                    }
                }


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
                    Undo.RegisterCreatedObjectUndo(newGO, "created GO");
                    newGO.transform.SetParent(newRootGO.transform);
                }

                if (combine0.Count > 0)
                {
                    newGO_LOD0 = new GameObject(gridNodes[i].mat[ii].meshR[0].name + "_" + gridNodes[i].mat[ii].mat.name + "_LOD0");
                    Undo.RegisterCreatedObjectUndo(newGO_LOD0, "created GO");

                    Mesh s0 = new Mesh();
                    MeshFilter mf0 = newGO_LOD0.AddComponent<MeshFilter>();
                    s0.CombineMeshes(combine0.ToArray(), true, true);
                    mf0.sharedMesh = s0;

                    newGO_LOD0.AddComponent<MeshRenderer>().material = gridNodes[i].mat[ii].mat;
                    newGO_LOD0.transform.SetParent(newGO.transform);

                    //lod component
                    render0 = new Renderer[1];
                    render0[0] = newGO_LOD0.GetComponent<Renderer>(); 
                }
                if (combine1.Count > 0)
                {
                    newGO_LOD1 = new GameObject(gridNodes[i].mat[ii].meshR[0].name + "_" + gridNodes[i].mat[ii].mat.name + "_LOD1");
                    Undo.RegisterCreatedObjectUndo(newGO_LOD1, "created GO");

                    Mesh s1 = new Mesh();
                    MeshFilter mf1 = newGO_LOD1.AddComponent<MeshFilter>();
                    s1.CombineMeshes(combine1.ToArray(), true, true);
                    mf1.sharedMesh = s1;

                    newGO_LOD1.AddComponent<MeshRenderer>().material = gridNodes[i].mat[ii].mat;
                    newGO_LOD1.transform.SetParent(newGO.transform);

                    //lod component
                    render1 = new Renderer[1];
                    render1[0] = newGO_LOD1.GetComponent<Renderer>();
                }
                if (combine2.Count > 0)
                {
                    newGO_LOD2 = new GameObject(gridNodes[i].mat[ii].meshR[0].name + "_" + gridNodes[i].mat[ii].mat.name + "_LOD2");
                    Undo.RegisterCreatedObjectUndo(newGO_LOD2, "created GO");

                    Mesh s2 = new Mesh();
                    MeshFilter mf2 = newGO_LOD2.AddComponent<MeshFilter>();
                    s2.CombineMeshes(combine2.ToArray(), true, true);
                    mf2.sharedMesh = s2;

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
        Undo.RecordObject(_tr.gameObject, "setActive");
        _tr.gameObject.SetActive(false);
        Selection.activeGameObject = newRootGO;
    }




}
