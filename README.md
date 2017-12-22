# Unity-GameObject-GridBaseMerge
 - Merges meshes based on custom unit space in a boundary grid. Prevents large vert counts and less manuelly labor.
Based on Material name and Material _MainTex name to prevent conflict.
 - Supports LOD naming convention. Name ur lod child with a suffix of 'lod_0', 'lod_1', 'lod_3' respectfully. Lod0 or LOD0 is fine too!
 - Messy child structures are supported too ;3

![alt text](https://pbs.twimg.com/media/DRSO4rVVoAAVa_a.jpg:large)

  Notices:
  - When merged, it disactive the original root GO and tags it with "EditorOnly" to avoid it to be in build.
  - Only supports 0-2 layers of LOD.

Free to use. Please mention my name "Eugene Chu" twitter: @LenZ_Chu if you can ;3 https://twitter.com/LenZ_Chu
