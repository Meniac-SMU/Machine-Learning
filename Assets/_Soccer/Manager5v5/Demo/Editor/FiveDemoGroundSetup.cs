using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace MachineLearning.Soccer.Manager5v5.Demo.Editor
{
    public static class FiveDemoGroundSetup
    {
        const string Art="Assets/_Soccer/Manager5v5/Demo/Art";
        struct Vertex {public Vector3 position;public Vector2 uv;}
        [Serializable] sealed class GroundReceipt {public int removedTriangles,groundTriangles;public float surfaceY;public float[] benchFootHeights;public bool physicsUnchanged;}
        public static void Build(GameObject root,string evidence)
        {
            string PhysicsState()=>string.Join("\n",root.GetComponentsInChildren<Collider>(true).Where(c=>c.enabled).Select(c=>EditorJsonUtility.ToJson(c)+c.transform.localToWorldMatrix.ToString("R")));
            string physics=PhysicsState();
            var match=root.GetComponent<FiveMatch>();
            var filter=root.GetComponentsInChildren<MeshFilter>(true).Single(m=>m.name=="SM_Stadium");
            var source=filter.sharedMesh;
            var groundVertices=new List<Vector3>();var groundUv=new List<Vector2>();var groundTriangles=new List<int>();
            int removed=0;
            using(var data=MeshUtility.AcquireReadOnlyMeshData(source))
            using(var positions=new NativeArray<Vector3>(data[0].vertexCount,Allocator.Temp))
            using(var normals=new NativeArray<Vector3>(data[0].vertexCount,Allocator.Temp))
            using(var uv=new NativeArray<Vector2>(data[0].vertexCount,Allocator.Temp))
            {
                data[0].GetVertices(positions);data[0].GetNormals(normals);data[0].GetUVs(0,uv);
                var copy=new Mesh{name="StadiumWithoutSurroundingFloor",indexFormat=source.indexFormat};
                copy.vertices=positions.ToArray();copy.normals=normals.ToArray();copy.uv=uv.ToArray();copy.subMeshCount=data[0].subMeshCount;
                if(data[0].HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Tangent))
                using(var tangents=new NativeArray<Vector4>(data[0].vertexCount,Allocator.Temp))
                {data[0].GetTangents(tangents);copy.tangents=tangents.ToArray();}
                if(data[0].HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Color))
                using(var colors=new NativeArray<Color>(data[0].vertexCount,Allocator.Temp))
                {data[0].GetColors(colors);copy.colors=colors.ToArray();}
                for(int sub=0;sub<data[0].subMeshCount;sub++)
                using(var indices=new NativeArray<int>(data[0].GetSubMesh(sub).indexCount,Allocator.Temp))
                {
                    data[0].GetIndices(indices,sub);var kept=new List<int>();
                    for(int i=0;i<indices.Length;i+=3)
                    {
                        var triangle=new List<Vertex>();
                        for(int j=0;j<3;j++){int index=indices[i+j];triangle.Add(new Vertex{position=filter.transform.TransformPoint(positions[index]),uv=uv[index]});}
                        bool floor=triangle.All(v=>v.position.y>-.7f&&v.position.y<-.6f&&Mathf.Abs(v.position.x)<80&&Mathf.Abs(v.position.z)<60);
                        if(!floor){for(int j=0;j<3;j++)kept.Add(indices[i+j]);continue;}
                        removed++;
                        float x=match.geometry.HalfLength,z=match.geometry.HalfWidth;
                        // Partition the floor around the pitch: raised faces must never cover grass.
                        Add(Clip(triangle,0,-x,false));Add(Clip(triangle,0,x,true));
                        var middle=Clip(Clip(triangle,0,-x,true),0,x,false);
                        Add(Clip(middle,2,-z,false));Add(Clip(middle,2,z,true));
                    }
                    copy.SetTriangles(kept,sub);
                }
                if(removed!=32)throw new InvalidOperationException("Unexpected surrounding floor topology: "+removed);
                copy.RecalculateBounds();filter.sharedMesh=Save(copy,Art+"/StadiumWithoutSurroundingFloor.asset");
            }
            var ground=new Mesh{name="RaisedSurroundingFloor"};ground.SetVertices(groundVertices);ground.SetUVs(0,groundUv);ground.SetTriangles(groundTriangles,0);ground.RecalculateNormals();ground.RecalculateBounds();
            var go=new GameObject("Raised surrounding floor — visual only");go.transform.SetParent(root.transform,false);
            go.AddComponent<MeshFilter>().sharedMesh=Save(ground,Art+"/RaisedSurroundingFloor.asset");
            go.AddComponent<MeshRenderer>().sharedMaterial=filter.GetComponent<MeshRenderer>().sharedMaterial;
            var benches=root.GetComponentsInChildren<MeshFilter>(true).Where(m=>m.name.StartsWith("SM_Chair_Canopy",StringComparison.Ordinal)).ToArray();
            foreach(var bench in benches)
            {
                if(bench.GetComponents<Collider>().Any(c=>c.enabled))throw new InvalidOperationException("Decorative bench must not participate in gameplay physics.");
            }
            if(physics!=PhysicsState())
            {
                File.WriteAllText("Logs/Manager5v5/saiccer-ground-20261018/physics-before.txt",physics);
                File.WriteAllText("Logs/Manager5v5/saiccer-ground-20261018/physics-after.txt",PhysicsState());
                throw new InvalidOperationException("Ground presentation changed gameplay colliders.");
            }
            if(evidence!=null)File.WriteAllText(evidence,JsonUtility.ToJson(new GroundReceipt{removedTriangles=removed,groundTriangles=groundTriangles.Count/3,surfaceY=FiveDemoAssets.SurroundingFloorY,benchFootHeights=benches.Select(b=>b.GetComponent<Renderer>().bounds.min.y).ToArray(),physicsUnchanged=true},true));
            void Add(List<Vertex> polygon)
            {
                if(polygon.Count<3)return;
                int start=groundVertices.Count;
                foreach(var v in polygon){var p=v.position;p.y=FiveDemoAssets.SurroundingFloorY;groundVertices.Add(root.transform.InverseTransformPoint(p));groundUv.Add(v.uv);}
                for(int i=1;i<polygon.Count-1;i++){groundTriangles.Add(start);groundTriangles.Add(start+i);groundTriangles.Add(start+i+1);}
            }
        }
        static Mesh Save(Mesh mesh,string path)
        {
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            EditorUtility.CopySerialized(mesh,existing);UnityEngine.Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);return existing;
        }
        static List<Vertex> Clip(List<Vertex> polygon,int axis,float edge,bool greater)
        {
            var output=new List<Vertex>();if(polygon.Count==0)return output;
            var previous=polygon[polygon.Count-1];bool before=greater?previous.position[axis]>=edge:previous.position[axis]<=edge;
            foreach(var current in polygon)
            {
                bool inside=greater?current.position[axis]>=edge:current.position[axis]<=edge;
                if(inside!=before)
                {
                    float t=(edge-previous.position[axis])/(current.position[axis]-previous.position[axis]);
                    output.Add(new Vertex{position=Vector3.Lerp(previous.position,current.position,t),uv=Vector2.Lerp(previous.uv,current.uv,t)});
                }
                if(inside)output.Add(current);previous=current;before=inside;
            }
            return output;
        }
        [Serializable] sealed class MeshDump {public Vector3[] vertices;public int[] triangles;}
        public static void InspectBatch()
        {
            var root=PrefabUtility.LoadPrefabContents("Assets/_Soccer/Manager5v5/Demo/FiveDemoArena.prefab");
            try
            {
                var filter=root.GetComponentsInChildren<MeshFilter>(true).Single(m=>m.name=="SM_Stadium");
                using(var data=MeshUtility.AcquireReadOnlyMeshData(filter.sharedMesh))
                using(var vertices=new NativeArray<Vector3>(data[0].vertexCount,Allocator.Temp))
                {
                    data[0].GetVertices(vertices);var world=vertices.ToArray().Select(v=>filter.transform.TransformPoint(v)).ToArray();
                    var triangles=new System.Collections.Generic.List<int>();
                    for(int sub=0;sub<data[0].subMeshCount;sub++)
                    using(var indices=new NativeArray<int>(data[0].GetSubMesh(sub).indexCount,Allocator.Temp))
                    {data[0].GetIndices(indices,sub);triangles.AddRange(indices.ToArray());}
                    File.WriteAllText("Logs/Manager5v5/saiccer-ground-20261018/stadium-mesh.json",JsonUtility.ToJson(new MeshDump{vertices=world,triangles=triangles.ToArray()}));
                }
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            EditorApplication.Exit(0);
        }
    }
}
