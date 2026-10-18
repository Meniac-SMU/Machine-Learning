using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace MachineLearning.Soccer.Manager5v5.Editor
{
 public static class FivePitchSetup
 {
  const string Root="Assets/_Soccer/Manager5v5/Art/Pitch";
  [Serializable] class Geometry { public Vector3[] grassVertices,lineVertices; public int[] grassTriangles,lineTriangles; }
  static Mesh MeshAsset(string name,Vector3[] vertices,int[] triangles)
  {
   var path=Root+"/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
   if(mesh==null){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
   mesh.name=name;mesh.vertices=vertices;mesh.triangles=triangles;
   mesh.uv=vertices.Select(v=>new Vector2(v.x/5f,v.z/5f)).ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);return mesh;
  }
  static Material MaterialAsset(string name,Texture texture,Material source)
  {
   string path=Root+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
   if(mat==null){mat=new Material(source);mat.name=name;mat.SetTexture("_BaseMap",texture);mat.SetTexture("_MainTex",texture);mat.SetColor("_BaseColor",Color.white);AssetDatabase.CreateAsset(mat,path);}return mat;
  }
  static string Colliders(GameObject root)=>string.Join("\n",root.GetComponentsInChildren<Collider>(true).Select(c=>c.GetType().Name+"|"+c.name+"|"+EditorJsonUtility.ToJson(c)+"|"+c.transform.localToWorldMatrix.ToString("R")));
  public static void Build()
  {
   AssetDatabase.Refresh();var geo=JsonUtility.FromJson<Geometry>(File.ReadAllText(Root+"/PitchGeometry.json"));
   var grass=MeshAsset("PitchGrass",geo.grassVertices,geo.grassTriangles);var lines=MeshAsset("PitchLinesHalfWidth",geo.lineVertices,geo.lineTriangles);
   string texPath=Root+"/GrassColor.png";
   if(!File.Exists(texPath)){
    var palette=new Texture2D(2,2);palette.LoadImage(File.ReadAllBytes("Assets/Hayq Art/GrantStadium/Textures/T_M_1.png"));
    var tex=new Texture2D(1,1,TextureFormat.RGBA32,false);tex.SetPixel(0,0,palette.GetPixelBilinear(.176f,.489f));tex.Apply();File.WriteAllBytes(texPath,tex.EncodeToPNG());Object.DestroyImmediate(tex);Object.DestroyImmediate(palette);AssetDatabase.ImportAsset(texPath);
   }
   var source=AssetDatabase.LoadAssetAtPath<Material>("Assets/Hayq Art/GrantStadium/Materials/M_1.mat");
   var gm=MaterialAsset("PitchGrass",AssetDatabase.LoadAssetAtPath<Texture2D>(texPath),source);var lm=MaterialAsset("PitchLines",null,source);
   string path="Assets/_Soccer/Manager5v5/Prefabs/FiveArena.prefab";var arena=PrefabUtility.LoadPrefabContents(path);
   try{
    string before=Colliders(arena);var match=arena.GetComponent<FiveMatch>();string geometry=EditorJsonUtility.ToJson(match.geometry);
    var original=arena.GetComponentsInChildren<MeshFilter>(true).Single(m=>m.name=="SM_Football_Field");
    if(Vector3.Distance(original.sharedMesh.bounds.min,grass.bounds.min)>.0001f||Vector3.Distance(original.sharedMesh.bounds.max,grass.bounds.max)>.0001f)throw new Exception("Pitch bounds changed: "+original.sharedMesh.bounds+" vs "+grass.bounds);
    original.GetComponent<MeshRenderer>().enabled=false;
    Attach(original.transform,"Pitch_Grass",grass,gm);Attach(original.transform,"Pitch_WhiteLines",lines,lm);
    if(before!=Colliders(arena)||geometry!=EditorJsonUtility.ToJson(match.geometry))throw new Exception("Pitch physics or geometry changed");
    PrefabUtility.SaveAsPrefabAsset(arena,path);
    Directory.CreateDirectory("Logs/Manager5v5/pitch-split-20261018");
    File.WriteAllText("Logs/Manager5v5/pitch-split-20261018/unity-validation.json",JsonUtility.ToJson(new Receipt{collidersPreserved=true,geometryPreserved=true,boundsPreserved=true,grassBounds=grass.bounds.ToString(),colliderCount=arena.GetComponentsInChildren<Collider>(true).Length},true));
   }finally{PrefabUtility.UnloadPrefabContents(arena);}
   AssetDatabase.SaveAssets();Debug.Log("FIVE_PITCH_SPLIT_PASS");
  }
  [Serializable] class Receipt {public bool collidersPreserved,geometryPreserved,boundsPreserved;public int colliderCount;public string grassBounds;}
  static void Attach(Transform parent,string name,Mesh mesh,Material material){var t=parent.Find(name);if(t==null){t=new GameObject(name).transform;t.SetParent(parent,false);}var mf=t.GetComponent<MeshFilter>();if(mf==null)mf=t.gameObject.AddComponent<MeshFilter>();var mr=t.GetComponent<MeshRenderer>();if(mr==null)mr=t.gameObject.AddComponent<MeshRenderer>();mf.sharedMesh=mesh;mr.sharedMaterial=material;}
 }
}

