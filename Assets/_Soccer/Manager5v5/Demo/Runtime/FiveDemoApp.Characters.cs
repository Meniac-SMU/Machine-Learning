using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MachineLearning.Soccer.Manager5v5.Demo
{
    public sealed partial class FiveDemoApp
    {
        readonly Dictionary<GameObject,Texture2D> characterPreviews=new();

        // The selector currently offers one set per team; previews and substitutes share this source.
        FiveDemoAssets.CharacterSet CharacterSetForTeam(int team)=>(team==0?assets.redCharacters:assets.navyCharacters)[0];

        void DrawCharacterSelector(VisualElement card,int team)
        {
            var set=CharacterSetForTeam(team);
            var preview=new Image{image=CharacterPreview(set.fieldPlayer),scaleMode=ScaleMode.ScaleToFit,name="character-preview-"+team};
            Place(preview,227,60,310,294);card.Add(preview);
            foreach(int direction in new[]{-1,1})
            {
                var arrow=Button(card,direction<0?"‹":"›",direction<0?105:603,172,56,64,null,"character-arrow");
                arrow.name="character-"+team+(direction<0?"-previous":"-next");
                arrow.tooltip=T("현재 모델링 세트","Current character set");arrow.SetEnabled(false);
            }
            Text(card,set.displayName,46,356,672,45,28,4,C(team==0?"#591824":"#111B3D"),true).name="character-name-"+team;
        }

        Texture2D CharacterPreview(GameObject prefab)
        {
            if(characterPreviews.TryGetValue(prefab,out var cached))return cached;
            var root=new GameObject("SAICCER character preview");
            root.transform.position=new Vector3(1000+characterPreviews.Count*20,1000,1000);
            var model=Instantiate(prefab,root.transform,false);
            foreach(var t in model.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
            var renderers=model.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
            foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            var cameraObject=new GameObject("Preview camera");cameraObject.transform.SetParent(root.transform,false);
            var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;
            camera.orthographic=true;camera.orthographicSize=Mathf.Max(bounds.extents.y,bounds.extents.x)*1.28f;
            camera.aspect=1;camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;
            camera.nearClipPlane=.01f;camera.farClipPlane=10;camera.allowHDR=false;
            camera.transform.position=bounds.center+Quaternion.Euler(0,5,0)*new Vector3(.65f,.35f,3);
            camera.transform.LookAt(bounds.center);
            var lightObject=new GameObject("Preview light");lightObject.transform.SetParent(root.transform,false);
            var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.4f;light.cullingMask=1<<31;
            light.transform.rotation=Quaternion.Euler(35,-155,0);
            var rt=RenderTexture.GetTemporary(512,512,24,RenderTextureFormat.ARGB32);
            var previous=RenderTexture.active;
            try
            {
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                cached=new Texture2D(512,512,TextureFormat.RGBA32,false){name=prefab.name+" preview"};
                cached.ReadPixels(new Rect(0,0,512,512),0,0);cached.Apply();
                characterPreviews.Add(prefab,cached);return cached;
            }
            finally
            {
                camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);
                root.SetActive(false);Destroy(root);
            }
        }

        void ReleaseCharacterPreviews()
        {
            foreach(var preview in characterPreviews.Values)Destroy(preview);
            characterPreviews.Clear();
        }
    }
}
