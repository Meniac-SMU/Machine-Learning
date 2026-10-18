using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace MachineLearning.Soccer.Manager5v5.Demo.Editor
{
    public static class FiveSurroundingsReview
    {
        [Serializable] sealed class Hit {public string path,asset;public Vector2 sample;public float height;public Bounds bounds;}
        [Serializable] sealed class Report {public List<Hit> surfaces=new List<Hit>();}
        public static void ReviewBatch()
        {
            var root=PrefabUtility.LoadPrefabContents("Assets/_Soccer/Manager5v5/Demo/FiveDemoArena.prefab");
            var report=new Report();
            try
            {
                foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    // Prefab contents live in a preview scene; activeInHierarchy can be false there.
                    if(filter.sharedMesh==null||!filter.GetComponent<Renderer>().enabled)continue;
                    using(var data=MeshUtility.AcquireReadOnlyMeshData(filter.sharedMesh))
                    using(var vertices=new NativeArray<Vector3>(data[0].vertexCount,Allocator.Temp))
                    {
                        data[0].GetVertices(vertices);
                        var world=new Vector3[vertices.Length];
                        for(int i=0;i<vertices.Length;i++)world[i]=filter.transform.TransformPoint(vertices[i]);
                        for(int sub=0;sub<data[0].subMeshCount;sub++)
                        {
                            var descriptor=data[0].GetSubMesh(sub);
                            if(descriptor.topology!=MeshTopology.Triangles)continue;
                            using(var indices=new NativeArray<int>(descriptor.indexCount,Allocator.Temp))
                            {
                                data[0].GetIndices(indices,sub);
                                foreach(var sample in new[]{new Vector2(0,0),new Vector2(20,20),new Vector2(-31,46),new Vector2(31,46),new Vector2(70,0),new Vector2(0,46)})
                                {
                                    var heights=new HashSet<int>();
                                    for(int i=0;i<indices.Length;i+=3)
                                    {
                                        var a=world[indices[i]];var b=world[indices[i+1]];var c=world[indices[i+2]];
                                        var ab=new Vector2(b.x-a.x,b.z-a.z);var ac=new Vector2(c.x-a.x,c.z-a.z);var ap=sample-new Vector2(a.x,a.z);
                                        float det=ab.x*ac.y-ab.y*ac.x;if(Mathf.Abs(det)<.00001f)continue;
                                        float u=(ap.x*ac.y-ap.y*ac.x)/det,v=(ab.x*ap.y-ab.y*ap.x)/det;
                                        float height=a.y+u*(b.y-a.y)+v*(c.y-a.y);
                                        if(u<0||v<0||u+v>1||!heights.Add(Mathf.RoundToInt(height*10000)))continue;
                                        string path=filter.name;for(var p=filter.transform.parent;p!=null;p=p.parent)path=p.name+"/"+path;
                                        report.surfaces.Add(new Hit{path=path,asset=AssetDatabase.GetAssetPath(filter.sharedMesh),sample=sample,height=height,bounds=filter.GetComponent<Renderer>().bounds});
                                    }
                                }
                            }
                        }
                    }
                }
                File.WriteAllText("Logs/Manager5v5/saiccer-independent-20261018/surroundings.json",JsonUtility.ToJson(report,true));
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            EditorApplication.Exit(0);
        }
    }
}
