using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "LODMergeData", menuName = "StoryBook/Tools/LODMergeData", order = 10)]
public class LodMergeSO : ScriptableObject
{
    public Material mat;
    public string mainTextureProperty = "_MainTex";
}
