using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RealityPlayground.Story;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Garden.Editor
{
    public static class GardenPolish
    {
        const string Assets = "Assets/Garden/Art/";
        const string Shared = "Assets/RealityPlayground/Materials and Meshes/";
        static Transform world, art, ar;
        static Loop loop;
        static TMP_FontAsset font;
        static Material stone, dark, cream, wood, sage, rust, glass, teal, amber, violet, skin, cloth, steel, leaf;
        static Material facade;
        static int meshId;
        public static string Report { get; private set; }

        public static void Apply(GameObject root)
        {
            if (!root || EditorApplication.isPlaying) throw new InvalidOperationException("Open Garden in edit mode.");
            loop = root.GetComponent<Loop>();
            if (!loop || !loop.overlays) throw new InvalidOperationException("Garden presentation is missing.");
            world = root.transform;
            FixFloor(root);
            meshId = 0;
            foreach (string name in new[]{"Home","Breakfast","Factory","Concourse","Outside"})
            {
                var module=ObjectNames.Find(world, name);
                var previous=module?ObjectNames.Find(module, "Art"):null;
                if(previous)Object.DestroyImmediate(previous.gameObject);
            }
            Directory.CreateDirectory(Assets);
            AssetDatabase.Refresh();
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            stone = Mat("Stone", new Color(.40f,.43f,.43f));
            dark = Mat("Graphite", new Color(.065f,.095f,.105f));
            cream = Mat("Linen", new Color(.74f,.70f,.59f));
            wood = Mat("Oak", new Color(.37f,.25f,.15f));
            sage = Mat("Sage", new Color(.28f,.43f,.36f));
            rust = Mat("Clay", new Color(.47f,.27f,.20f));
            glass = Mat("Glass", new Color(.10f,.19f,.22f));
            teal = Mat("Cyan", new Color(.13f,.76f,.77f), true);
            amber = Mat("Amber", new Color(.94f,.66f,.28f), true);
            violet = Mat("Violet", new Color(.51f,.39f,.80f), true);
            skin = Mat("Skin", new Color(.46f,.32f,.23f));
            cloth = Mat("Jacket", new Color(.24f,.34f,.38f));
            steel = Mat("Steel", new Color(.27f,.31f,.32f));
            leaf = Mat("Leaf", new Color(.23f,.39f,.17f));
            facade = AssetDatabase.LoadAssetAtPath<Material>(Shared + "FacadeSurfaces.mat");
            art = Reset(world, "Art");
            ar = Reset(loop.overlays.transform, "Art");
            foreach (Transform child in loop.overlays.transform.Cast<Transform>().ToArray())
                if (ObjectNames.Matches(child.name, "Display") || ObjectNames.Matches(child.name, "Tree")) Object.DestroyImmediate(child.gameObject);
            Home();
            Commons();
            Factory();
            Concourse();
            Stations();
            Buildings();
            People();
            Outside();
            Sensors();
            Angel();
            foreach (var target in ar.GetComponentsInChildren<Transform>(true)) target.gameObject.layer = 11;
            foreach (var renderer in art.GetComponentsInChildren<Renderer>(true).Concat(ar.GetComponentsInChildren<Renderer>(true)))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            int physicalCount=art.GetComponentsInChildren<Renderer>(true).Length;
            foreach(Transform group in art.Cast<Transform>().ToArray())
            {
                var module=ObjectNames.Find(world, group.name);
                if(!module || module==art)continue;
                group.SetParent(module,true);
                group.name="Art";
            }
            EditorUtility.SetDirty(root);
            AssetDatabase.SaveAssets();
            Report = "Refined home, commons, factory, concourse, metro, facades, people, outside and signal. "
                + physicalCount + " physical renderers, "
                + ar.GetComponentsInChildren<Renderer>(true).Length + " presentation renderers. Pad positions unchanged; concourse floor separated by 1 cm.";
        }

        public static void FixFloor(GameObject root)
        {
            if(!root)return;
            var threshold=ObjectNames.Find(root.transform, "Concourse/Threshold");
            if(!threshold)return;
            var size=threshold.localScale;
            size.y=Mathf.Max(.04f,size.y);
            threshold.localScale=size;
            var position=threshold.localPosition;
            position.y=.01f-size.y*.5f;
            threshold.localPosition=position;
            EditorUtility.SetDirty(threshold);
        }

        static Material Mat(string name, Color color, bool unlit = false)
        {
            string path = Assets + name + ".mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!result)
            {
                result = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(result, path);
            }
            result.SetColor("_BaseColor", color);
            result.enableInstancing = true;
            if (result.HasProperty("_Smoothness")) result.SetFloat("_Smoothness", .26f);
            EditorUtility.SetDirty(result);
            return result;
        }

        static Transform New(string name, Transform parent, Vector3 local = default)
        {
            var result = new GameObject(ObjectNames.Short(name)).transform;
            result.SetParent(parent, false);
            result.localPosition = local;
            return result;
        }

        static Transform Reset(Transform parent, string name)
        {
            var old = ObjectNames.Find(parent, name);
            if (old) Object.DestroyImmediate(old.gameObject);
            return New(name, parent);
        }

        static Transform Site(string name, Transform parent)
        {
            var source = ObjectNames.Find(world, name);
            if (!source) return null;
            var group = New(name, parent);
            group.SetPositionAndRotation(source.position, source.rotation);
            return group;
        }

        static Transform Shape(string name, PrimitiveType type, Transform parent, Vector3 local, Vector3 scale, Material material, Vector3 angles = default)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = ObjectNames.Short(name);
            Object.DestroyImmediate(obj.GetComponent<Collider>());
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = local;
            obj.transform.localScale = scale;
            obj.transform.localRotation = Quaternion.Euler(angles);
            var renderer = obj.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return obj.transform;
        }

        static Transform Box(Transform parent, Vector3 local, Vector3 size, Material material, string name = "Panel", Vector3 angles = default)
        {
            return Shape(name, PrimitiveType.Cube, parent, local, size, material, angles);
        }

        static Transform Sphere(Transform parent, Vector3 local, Vector3 size, Material material, string name = "Form")
        {
            return Shape(name, PrimitiveType.Sphere, parent, local, size, material);
        }

        static Transform Rod(Transform parent, Vector3 a, Vector3 b, float radius, Material material)
        {
            var result = Shape("Rail", PrimitiveType.Cylinder, parent, (a+b)*.5f, new Vector3(radius*2,(b-a).magnitude*.5f,radius*2), material);
            result.localRotation = Quaternion.FromToRotation(Vector3.up, b-a);
            return result;
        }

        static TMP_Text Label(Transform parent, string value, Vector3 local, Vector2 size, float textSize, Color? color = null)
        {
            var result = New("Text", parent, local).gameObject.AddComponent<TextMeshPro>();
            result.font = font;
            result.text = value;
            result.fontSize = textSize;
            result.rectTransform.sizeDelta = size;
            result.alignment = TextAlignmentOptions.Center;
            result.color = color ?? new Color(.89f,.91f,.83f);
            result.enableAutoSizing = false;
            result.textWrappingMode = TextWrappingModes.Normal;
            result.overflowMode = TextOverflowModes.Truncate;
            result.richText = false;
            return result;
        }

        static void Sign(Transform parent, string title, string subtitle, Vector3 local, float width, Material accent)
        {
            var group = New("Sign", parent, local);
            Box(group, new Vector3(0,0,.045f), new Vector3(width,.54f,.06f), dark);
            Box(group, new Vector3(0,.29f,0), new Vector3(width,.025f,.03f), accent, "Light");
            Label(group,title,new Vector3(0,.09f,-.001f),new Vector2(width-.14f,.26f),1.5f);
            Label(group,subtitle,new Vector3(0,-.145f,-.003f),new Vector2(width-.14f,.13f),.61f,new Color(.65f,.72f,.70f));
        }

        static void Batch(Transform root, string name)
        {
            var filters = root.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh && !f.GetComponent<TMP_Text>()).ToArray();
            var materials = filters.SelectMany(f => f.GetComponent<MeshRenderer>().sharedMaterials).Distinct().ToArray();
            foreach (var material in materials)
            {
                if (!material) continue;
                var combines = new List<CombineInstance>();
                foreach (var filter in filters)
                {
                    var sourceMaterials = filter.GetComponent<MeshRenderer>().sharedMaterials;
                    for (int sub = 0; sub < sourceMaterials.Length; sub++)
                        if (sourceMaterials[sub] == material && sub < filter.sharedMesh.subMeshCount)
                            combines.Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = sub, transform = root.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                }
                if (combines.Count == 0) continue;
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(combines.ToArray(), true, true);
                string path = Assets + "Mesh" + (++meshId).ToString("000") + ".asset";
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (saved)
                {
                    saved.Clear(false);
                    saved.indexFormat=mesh.indexFormat;
                    saved.SetVertices(mesh.vertices);
                    saved.SetNormals(mesh.normals);
                    if(mesh.tangents.Length>0)saved.SetTangents(mesh.tangents);
                    if(mesh.colors.Length>0)saved.SetColors(mesh.colors);
                    var uv=new List<Vector4>();
                    for(int channel=0;channel<8;channel++)
                    {
                        uv.Clear();
                        mesh.GetUVs(channel,uv);
                        if(uv.Count>0)saved.SetUVs(channel,uv);
                    }
                    saved.subMeshCount=mesh.subMeshCount;
                    for(int sub=0;sub<mesh.subMeshCount;sub++)saved.SetIndices(mesh.GetIndices(sub),mesh.GetTopology(sub),sub,false);
                    saved.bounds=mesh.bounds;
                    saved.name=name;
                    saved.UploadMeshData(false);
                    EditorUtility.SetDirty(saved);
                    Object.DestroyImmediate(mesh);
                    mesh=saved;
                }
                else AssetDatabase.CreateAsset(mesh,path);
                var target = New(name,root).gameObject;
                target.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = target.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            foreach (var filter in filters)
            {
                Object.DestroyImmediate(filter.GetComponent<MeshRenderer>());
                Object.DestroyImmediate(filter);
            }
            foreach (var child in root.GetComponentsInChildren<Transform>(true).Reverse())
                if (child != root && child.childCount == 0 && child.GetComponents<Component>().Length == 1) Object.DestroyImmediate(child.gameObject);
        }

        static void Animate(Transform group, params Holo.Part[] parts)
        {
            var motion = group.gameObject.AddComponent<Holo>();
            motion.parts = parts;
            motion.distance = 24;
        }

        static void Home()
        {
            var r = Site("Home",art);
            var p = Site("Home",ar);
            if (!r || !p) return;
            var plaster=Mat("Plaster",new Color(.63f,.58f,.47f));
            var boards=Mat("Boards",new Color(.40f,.32f,.23f));
            var joints=Mat("Joints",new Color(.22f,.19f,.16f));
            var shell=New("Interior",p);
            Box(shell,new Vector3(0,1.52f,2.393f),new Vector3(5.82f,3.0f,.018f),plaster,"Wall");
            Box(shell,new Vector3(-2.896f,1.52f,0),new Vector3(.018f,3.0f,4.80f),plaster,"Wall");
            Box(shell,new Vector3(2.896f,1.52f,0),new Vector3(.018f,3.0f,4.80f),plaster,"Wall");
            foreach(float x in new[]{-1.93f,1.93f})
                Box(shell,new Vector3(x,1.52f,-2.393f),new Vector3(1.97f,3.0f,.018f),plaster,"Wall");
            Box(shell,new Vector3(0,2.755f,-2.393f),new Vector3(1.84f,.49f,.018f),plaster,"Lintel");
            Box(shell,new Vector3(0,3.037f,0),new Vector3(5.82f,.018f,4.8f),cream,"Ceiling");
            Box(shell,new Vector3(0,.002f,0),new Vector3(5.81f,.004f,4.79f),boards,"Floor");
            for(int i=0;i<15;i++)Box(shell,new Vector3(-2.72f+i*.389f,.005f,0),new Vector3(.007f,.003f,4.76f),joints,"Joint");
            for(int row=0;row<3;row++)for(int col=0;col<15;col++)
                Box(shell,new Vector3(-2.535f+col*.389f,.005f,-1.48f+row*1.50f+(col%2)*.44f),new Vector3(.384f,.003f,.007f),joints,"Joint");
            foreach(float x in new[]{-2.85f,2.85f})
            {
                Box(shell,new Vector3(x,2.92f,0),new Vector3(.09f,.19f,4.75f),cream,"Cornice");
                Box(shell,new Vector3(x,2.817f,0),new Vector3(.07f,.014f,4.60f),amber,"Light");
                Box(shell,new Vector3(x,.12f,0),new Vector3(.07f,.24f,4.75f),wood,"Skirt");
            }
            Box(shell,new Vector3(0,2.92f,2.35f),new Vector3(5.60f,.19f,.09f),cream,"Cornice");
            Box(shell,new Vector3(0,2.817f,2.34f),new Vector3(5.53f,.014f,.06f),amber,"Light");
            Box(shell,new Vector3(0,.12f,2.36f),new Vector3(5.62f,.24f,.045f),wood,"Skirt");
            foreach(float x in new[]{1.025f,1.175f})
                Box(shell,new Vector3(x,1.21f,1.7f),new Vector3(.022f,2.41f,1.425f),wood,"Screen");
            Box(shell,new Vector3(1.1f,2.43f,1.7f),new Vector3(.19f,.065f,1.44f),cream,"Cap");
            for(int i=0;i<8;i++)Box(shell,new Vector3(1.009f,1.22f,1.07f+i*.18f),new Vector3(.013f,2.2f,.02f),cream,"Slat");
            Batch(shell,"Interior");
            Box(p,new Vector3(-2.875f,1.52f,.6f),new Vector3(.025f,2.65f,3.25f),cream);
            for (int i=0;i<11;i++) Box(p,new Vector3(-2.854f,1.45f,-.85f+i*.29f),new Vector3(.025f,2.45f,.052f),wood);
            Box(p,new Vector3(-1.93f,.728f,.11f),new Vector3(1.015f,.045f,1.32f),sage,"Duvet");
            Box(p,new Vector3(-1.93f,.755f,.14f),new Vector3(.86f,.012f,1.14f),cream,"Fold");
            Box(p,new Vector3(-1.93f,.737f,.66f),new Vector3(.98f,.024f,.075f),sage);
            Box(p,new Vector3(-2.845f,2.70f,.55f),new Vector3(.035f,.022f,3.25f),amber,"Light");
            var picture=New("Horizon",p,new Vector3(-2.81f,1.75f,-1.28f));
            picture.localRotation=Quaternion.Euler(0,270,0);
            Box(picture,Vector3.zero,new Vector3(.88f,.63f,.035f),dark);
            Box(picture,new Vector3(0,-.10f,-.025f),new Vector3(.80f,.33f,.012f),sage);
            Shape("Sun",PrimitiveType.Cylinder,picture,new Vector3(.2f,.13f,-.033f),new Vector3(.2f,.008f,.2f),amber,new Vector3(90,0,0));
            Box(r,new Vector3(2.05f,.768f,.32f),new Vector3(.20f,.035f,.27f),cream,"Book");
            Box(r,new Vector3(2.07f,.80f,.34f),new Vector3(.17f,.022f,.24f),rust,"Book",new Vector3(0,8,0));
            Box(r,new Vector3(2.84f,1.55f,.65f),new Vector3(.018f,.66f,.85f),dark,"Panel");
            for(int i=0;i<5;i++) Box(r,new Vector3(2.826f,1.30f+i*.12f,.65f),new Vector3(.018f,.012f,.68f),steel);
            var ornament=New("Ornament",p,new Vector3(2.06f,1.02f,.62f));
            Sphere(ornament,Vector3.zero,Vector3.one*.18f,teal);
            Ring(ornament,.15f,.008f,amber,Quaternion.Euler(60,0,20));
            Batch(ornament,"Glass");
            Animate(ornament,new Holo.Part{target=ornament,spin=new Vector3(7,18,0),lift=.023f,rate=1.7f});
            var physical=New("Trim",r);
            for(int i=0;i<4;i++) Box(physical,new Vector3(-2.6f+i*.2f,2.8f,2.35f),new Vector3(.09f,.12f,.028f),stone);
            Batch(physical,"Fittings");
        }

        static void Commons()
        {
            var r=Site("Breakfast",art);
            var p=Site("Breakfast",ar);
            if(!r || !p)return;
            var booth=New("Kiosk",p);
            Box(booth,new Vector3(0,.65f,.823f),new Vector3(2.28f,1.09f,.04f),sage);
            for(int i=0;i<15;i++)Box(booth,new Vector3(-1.03f+i*.147f,.65f,.79f),new Vector3(.019f,.91f,.016f),wood);
            Box(booth,new Vector3(0,2.95f,1.13f),new Vector3(3.15f,.12f,1.3f),cream,"Awning");
            Box(booth,new Vector3(0,2.86f,.53f),new Vector3(2.95f,.08f,.06f),sage);
            Box(booth,new Vector3(0,2.79f,.49f),new Vector3(2.55f,.017f,.023f),amber,"Light");
            foreach(float x in new[]{-1.34f,1.34f})Rod(booth,new Vector3(x,1.24f,1.51f),new Vector3(x,2.93f,1.51f),.035f,dark);
            Batch(booth,"Kiosk");
            var ad=New("Coffee",p,new Vector3(-1.78f,2.1f,1.05f));
            var cup=New("Cup",ad);
            Shape("Cup",PrimitiveType.Cylinder,cup,Vector3.zero,new Vector3(.39f,.24f,.39f),cream);
            Shape("Coffee",PrimitiveType.Cylinder,cup,new Vector3(0,.246f,0),new Vector3(.33f,.004f,.33f),wood);
            Shape("Saucer",PrimitiveType.Cylinder,cup,new Vector3(0,-.265f,0),new Vector3(.60f,.018f,.60f),amber);
            Ring(cup,.18f,.028f,cream,Quaternion.identity,new Vector3(.28f,0,0));
            Batch(cup,"Cup");
            var steam=New("Steam",ad);
            for(int n=0;n<3;n++)for(int i=0;i<7;i++)Sphere(steam,new Vector3((n-1)*.085f+Mathf.Sin(i*.8f+n)*.018f,.31f+i*.038f,0),Vector3.one*(.014f+i*.002f),teal);
            Batch(steam,"Steam");
            Animate(ad,new Holo.Part{target=cup,spin=new Vector3(0,13,0),lift=.025f,rate=1.3f},new Holo.Part{target=steam,lift=.035f,rate=1.3f,phase=.5f});
            Sign(p,"YOUR USUAL","Freshly presented",new Vector3(0,2.30f,1.42f),2.18f,amber);
            var machine=New("Dispenser",r);
            Box(machine,new Vector3(.83f,1.46f,1.4f),new Vector3(.44f,.48f,.34f),steel);
            Box(machine,new Vector3(.83f,1.45f,1.22f),new Vector3(.32f,.27f,.027f),dark);
            Rod(machine,new Vector3(.83f,1.43f,1.19f),new Vector3(.83f,1.28f,1.19f),.025f,steel);
            Batch(machine,"Dispenser");
            Tree(p,new Vector3(3.25f,0,1.65f),.85f,0);
        }

        static void Factory()
        {
            var r=Site("Factory",art);
            var p=Site("Factory",ar);
            if(!r || !p)return;
            var machine=New("Line",r);
            Box(machine,new Vector3(0,1.135f,2.5f),new Vector3(3.75f,.025f,1.04f),dark,"Belt");
            for(int i=0;i<17;i++)Box(machine,new Vector3(-1.78f+i*.222f,1.155f,2.5f),new Vector3(.012f,.016f,.98f),steel);
            foreach(float x in new[]{-1.88f,1.88f})
            {
                Rod(machine,new Vector3(x,1.22f,1.97f),new Vector3(x,1.22f,3.03f),.045f,steel);
                Box(machine,new Vector3(x,.75f,2.5f),new Vector3(.09f,.78f,.8f),stone);
            }
            for(int i=0;i<10;i++)Box(machine,new Vector3(-1.80f+i*.4f,.92f,1.125f),new Vector3(.19f,.045f,.026f),i%2==0?amber:dark,angles:new Vector3(0,0,-25));
            Box(machine,new Vector3(0,3.24f,2.8f),new Vector3(3.8f,.05f,.18f),steel);
            Box(machine,new Vector3(0,3.205f,2.71f),new Vector3(3.5f,.02f,.022f),amber);
            Batch(machine,"Machine");
            var source=ObjectNames.Find(world, "Factory").GetComponent<Assembly>();
            if(source)
            {
                Arm(source.acceptArm,teal);
                Arm(source.rejectArm,rust);
                Supports(r,source);
                if(source.samples!=null)foreach(var sample in source.samples)if(sample)Optic(sample.transform,.11f);
                if(source.gloveStand)
                {
                    var glove=Reset(source.gloveStand,"Art");
                    for(int i=0;i<4;i++)Box(glove,new Vector3(-.044f+i*.029f,.025f,.077f),new Vector3(.023f,.027f,.077f),steel,"Finger");
                    Box(glove,new Vector3(0,.044f,0),new Vector3(.09f,.019f,.1f),dark);
                    Box(glove,new Vector3(0,.055f,-.01f),new Vector3(.05f,.006f,.028f),teal);
                    Batch(glove,"Glove");
                }
            }
            Sign(p,"PRECISION IS CARE","Presentation modules   07",new Vector3(0,4.1f,2.7f),3.6f,teal);
            var symbol=New("Lens",p,new Vector3(2.8f,2.45f,2.55f));
            Ring(symbol,.42f,.028f,teal,Quaternion.identity);
            Ring(symbol,.31f,.012f,amber,Quaternion.Euler(35,0,0));
            Sphere(symbol,Vector3.zero,new Vector3(.25f,.25f,.08f),glass);
            Batch(symbol,"Lens");
            Animate(symbol,new Holo.Part{target=symbol,spin=new Vector3(0,18,0),lift=.035f,rate=1.2f});
        }

        static void Arm(Transform source,Material accent)
        {
            if(!source)return;
            var r=Reset(source,"Art");
            r.localScale=new Vector3(1/source.localScale.x,1/source.localScale.y,1/source.localScale.z);
            Shape("Joint",PrimitiveType.Cylinder,r,new Vector3(0,0,.32f),new Vector3(.28f,.14f,.28f),steel,new Vector3(90,0,0));
            Shape("Hub",PrimitiveType.Cylinder,r,new Vector3(0,0,.47f),new Vector3(.18f,.024f,.18f),dark,new Vector3(90,0,0));
            Shape("Pivot",PrimitiveType.Cylinder,r,new Vector3(0,-.035f,.10f),new Vector3(.25f,.14f,.25f),stone,new Vector3(0,0,90));
            Box(r,new Vector3(0,.11f,0),new Vector3(.12f,.03f,.78f),accent);
            for(int side=-1;side<=1;side+=2)
            {
                Rod(r,new Vector3(side*.10f,.035f,.19f),new Vector3(side*.10f,.025f,-.35f),.026f,steel);
                Rod(r,new Vector3(side*.10f,.03f,-.29f),new Vector3(side*.10f,-.025f,-.42f),.034f,dark);
            }
            Vector3 previous=new Vector3(-.12f,.01f,.31f);
            for(int i=1;i<10;i++)
            {
                float t=i/9f;
                var next=new Vector3(-.12f-Mathf.Sin(t*Mathf.PI)*.058f,.01f+Mathf.Sin(t*Mathf.PI)*.075f,.31f-t*.70f);
                Rod(r,previous,next,.013f,dark);
                previous=next;
            }
            for(int side=-1;side<=1;side+=2)
            {
                Box(r,new Vector3(side*.105f,-.06f,-.48f),new Vector3(.055f,.23f,.055f),steel,"Finger");
                Box(r,new Vector3(side*.075f,-.17f,-.48f),new Vector3(.10f,.04f,.08f),dark,"Jaw");
            }
            Batch(r,"Arm");
        }

        static void Supports(Transform root,Assembly source)
        {
            var posts=New("Posts",root);
            foreach(var arm in new[]{source.rejectArm,source.acceptArm})
            {
                if(!arm)continue;
                float side=arm==source.rejectArm?-1:1;
                var at=new Vector3(side*1.36f,1.65f,2.91f);
                Box(posts,new Vector3(at.x,1.16f,at.z),new Vector3(.35f,.10f,.32f),steel,"Foot");
                Rod(posts,new Vector3(at.x,1.16f,at.z),at,.082f,steel);
                Sphere(posts,at,new Vector3(.22f,.22f,.22f),dark,"Pivot");
                var mount=New("Link",root,at);
                var link=mount.gameObject.AddComponent<Strut>();
                link.end=arm;
                link.contact=new Vector3(0,.025f/arm.localScale.y,.34f/arm.localScale.z);
                link.shaft=Shape("Piston",PrimitiveType.Cylinder,mount,Vector3.zero,Vector3.one,steel);
                link.sleeve=Shape("Sleeve",PrimitiveType.Cylinder,mount,Vector3.zero,Vector3.one,dark);
                link.joint=Sphere(mount,Vector3.zero,Vector3.one*.17f,steel,"Joint");
                link.Refresh();
            }
            Batch(posts,"Posts");
        }

        static void Optic(Transform source,float size)
        {
            var r=Reset(source,"Art");
            r.localScale=new Vector3(1/source.localScale.x,1/source.localScale.y,1/source.localScale.z);
            Shape("Lens",PrimitiveType.Cylinder,r,new Vector3(0,0,-size),new Vector3(size,size*.11f,size),glass,new Vector3(90,0,0));
            Ring(r,size*.58f,size*.08f,steel,Quaternion.identity,new Vector3(0,0,-size*1.11f));
            Box(r,new Vector3(size*.75f,size*.5f,-size),Vector3.one*(size*.18f),amber,"Light");
            Batch(r,"Optic");
        }

        static void Concourse()
        {
            var r=Site("Concourse",art);
            var p=Site("Concourse",ar);
            if(!r || !p)return;
            var garden=New("Garden",p,new Vector3(.30f,0,0));
            garden.localRotation=Quaternion.Euler(0,90,0);
            Box(garden,new Vector3(0,2.58f,0),new Vector3(3.23f,.14f,.21f),cream,"Lintel");
            Box(garden,new Vector3(0,2.49f,-.11f),new Vector3(2.8f,.025f,.025f),amber,"Light");
            foreach(float side in new[]{-1.61f,1.61f})Box(garden,new Vector3(side,1.23f,0),new Vector3(.12f,2.6f,.16f),wood);
            Sign(garden,"A MOMENT OF STILLNESS","Garden experience   12",new Vector3(0,3.1f,0),2.8f,teal);
            Tree(p,new Vector3(-1.25f,0,2.48f),.83f,1);
            Tree(p,new Vector3(-1.25f,0,-2.48f),.83f,2);
            var utility=New("Service",r);
            for(int i=0;i<3;i++)Rod(utility,new Vector3(2+i*.18f,2.63f,-1.53f),new Vector3(4.55f,2.63f,-1.53f),.035f,steel);
            Box(utility,new Vector3(2.5f,2.66f,.1f),new Vector3(.15f,.026f,1.45f),cream,"Light");
            Batch(utility,"Service");
        }

        static void Stations()
        {
            var train=world.GetComponentInChildren<Train>(true);
            if(!train)return;
            foreach(var point in new[]{train.south,train.work})
            {
                if(!point)continue;
                var p=New("Metro",ar);
                p.SetPositionAndRotation(point.position,point.rotation);
                var frame=New("Frame",p);
                foreach(float side in new[]{-1.9f,1.9f})Box(frame,new Vector3(side,1.45f,1.7f),new Vector3(.1f,2.9f,.1f),cream);
                Box(frame,new Vector3(0,2.95f,1.7f),new Vector3(4,.14f,.8f),sage);
                Box(frame,new Vector3(0,2.86f,1.33f),new Vector3(3.5f,.024f,.02f),teal);
                Batch(frame,"Shelter");
                Sign(p,"M  GARDEN LINE","Residence     Commons     Assembly",new Vector3(0,2.4f,1.78f),3.2f,teal);
            }
        }

        static void Buildings()
        {
            var blocks=ObjectNames.Find(world, "City/Blocks");
            if(!blocks)return;
            var stops=new[]{"Home","Breakfast","Factory","Concourse","Poster"}.Select(n=>ObjectNames.Find(world, n)).Where(t=>t).ToArray();
            var pads=world.GetComponentsInChildren<NavPad>(true).Select(n=>n.transform.position).ToArray();
            var source=AssetDatabase.LoadAssetAtPath<Mesh>(Shared+"TownhouseFacade16.asset");
            int count=0;
            foreach(var block in blocks.GetComponentsInChildren<MeshRenderer>(true))
            {
                var bounds=block.bounds;
                if(bounds.size.y<7.4f || bounds.size.x<2 || bounds.size.z<2 || !stops.Any(t=>bounds.SqrDistance(t.position+Vector3.up*1.6f)<180))continue;
                foreach(var direction in new[]{Vector3.forward,Vector3.back,Vector3.left,Vector3.right})
                {
                    float width=direction.x==0?bounds.size.x:bounds.size.z;
                    if(width<4.8f)continue;
                    var point=bounds.center+Vector3.Scale(direction,bounds.extents);
                    if(!pads.Any(p=>Vector3.Dot(p-point,direction)>.7f && Vector3.ProjectOnPlane(p-point,Vector3.up).magnitude<9))continue;
                    if(count++>=24)break;
                    var group=New("Facade",ar);
                    group.position=point+direction*.03f;
                    group.rotation=Quaternion.LookRotation(-direction);
                    if(source && facade)
                    {
                        var shape=New("Skin",group);
                        var filter=shape.gameObject.AddComponent<MeshFilter>(); filter.sharedMesh=source;
                        shape.gameObject.AddComponent<MeshRenderer>().sharedMaterial=facade;
                        var size=source.bounds.size;
                        shape.localScale=new Vector3(Mathf.Min(width-.12f,8.1f)/size.x,bounds.size.y/size.y,.65f);
                        shape.localPosition=new Vector3(-source.bounds.center.x*shape.localScale.x,-source.bounds.center.y*shape.localScale.y,-source.bounds.max.z*shape.localScale.z);
                    }
                    else
                    {
                        Box(group,Vector3.zero,new Vector3(width-.12f,bounds.size.y-.1f,.025f),cream);
                        for(int row=0;row<2;row++)for(int col=-1;col<=1;col++)
                            Box(group,new Vector3(col*1.5f,-bounds.extents.y+4+row*2.1f,-.04f),new Vector3(.95f,1.1f,.025f),glass);
                        Batch(group,"Facade");
                    }
                    Animate(group);
                    group.GetComponent<Holo>().distance=34;
                }
                if(count>=24)break;
            }
        }

        static void Tree(Transform parent,Vector3 position,float scale,int variant)
        {
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Shared+"TreeCanopy"+(12+variant%6)+".asset");
            if(!mesh || !facade)return;
            var point=parent.TransformPoint(position);
            if(Physics.CheckCapsule(point+Vector3.up*.42f,point+Vector3.up*1.8f,.30f,1,QueryTriggerInteraction.Ignore))return;
            if(world.GetComponentsInChildren<NavPad>(true).Any(p=>Mathf.Abs(p.transform.position.y-point.y)<1 && Vector3.ProjectOnPlane(p.transform.position-point,Vector3.up).magnitude<.76f))return;
            if(!Physics.Raycast(point+Vector3.up*.15f,Vector3.down,.4f,1,QueryTriggerInteraction.Ignore))return;
            var tree=New("Tree",parent,position);
            tree.localScale=Vector3.one*scale;
            var model=New("Plant",tree);
            model.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            model.gameObject.AddComponent<MeshRenderer>().sharedMaterial=facade;
            Animate(tree);
        }

        static void People()
        {
            foreach(string name in new[]{"Mara","Noor","Repair","Passenger"})
            {
                var source=world.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>ObjectNames.Matches(t.name, name) && ObjectNames.Find(t, "Body"));
                if(!source)continue;
                foreach(var label in new[]{"Body","Head","Arm"})
                {
                    var old=ObjectNames.Find(source, label);
                    if(old && old.GetComponent<Renderer>())old.GetComponent<Renderer>().enabled=false;
                }
                var r=Reset(source,"Art");
                var jacket=name=="Noor"?sage:name=="Repair"?rust:cloth;
                Shape("Torso",PrimitiveType.Capsule,r,new Vector3(0,1.1f,0),new Vector3(.40f,.34f,.28f),jacket);
                Rod(r,new Vector3(-.18f,1.275f,0),new Vector3(.18f,1.275f,0),.098f,jacket);
                Rod(r,new Vector3(0,1.35f,0),new Vector3(0,1.485f,0),.066f,skin);
                Shape("Collar",PrimitiveType.Cylinder,r,new Vector3(0,1.378f,0),new Vector3(.18f,.035f,.18f),jacket);
                Shape("Hip",PrimitiveType.Capsule,r,new Vector3(0,.77f,0),new Vector3(.34f,.13f,.26f),dark);
                for(int side=-1;side<=1;side+=2)
                {
                    Rod(r,new Vector3(side*.12f,.79f,0),new Vector3(side*.15f,.16f,.025f),.082f,dark);
                    Box(r,new Vector3(side*.15f,.095f,-.055f),new Vector3(.15f,.13f,.29f),wood,"Shoe");
                    Rod(r,new Vector3(side*.215f,1.28f,0),new Vector3(side*.32f,.91f,-.05f),.071f,jacket);
                    Sphere(r,new Vector3(side*.205f,1.275f,0),new Vector3(.19f,.20f,.21f),jacket,"Shoulder");
                    Sphere(r,new Vector3(side*.32f,.89f,-.06f),new Vector3(.12f,.16f,.12f),skin,"Hand");
                }
                Box(r,new Vector3(.11f,1.17f,-.149f),new Vector3(.09f,.1f,.017f),stone,"Badge");
                Batch(r,"Body");
                var face=ObjectNames.Find(source, "Head");
                if(!face)face=New("Head",source,new Vector3(0,1.6f,0));
                var head=Reset(face,"Art");
                head.localScale=new Vector3(1/face.localScale.x,1/face.localScale.y,1/face.localScale.z);
                Sphere(head,Vector3.zero,new Vector3(.255f,.325f,.27f),skin,"Face");
                Sphere(head,new Vector3(0,.08f,.035f),new Vector3(.27f,.225f,.265f),dark,"Hair");
                Sphere(head,new Vector3(0,-.007f,-.135f),new Vector3(.049f,.06f,.059f),skin,"Nose");
                foreach(float side in new[]{-.055f,.055f})Sphere(head,new Vector3(side,.035f,-.12f),new Vector3(.032f,.014f,.018f),dark,"Eye");
                Batch(head,"Head");
            }
        }

        static void Outside()
        {
            var r=Site("Outside",art);
            if(!r)return;
            var shelter=New("Shelter",r);
            foreach(float x in new[]{5.13f,8.87f})foreach(float z in new[]{2.5f,4.6f})Rod(shelter,new Vector3(x,0,z),new Vector3(x,2.62f,z),.058f,wood);
            for(int i=0;i<11;i++)Box(shelter,new Vector3(5.23f+i*.352f,2.805f,3.6f),new Vector3(.335f,.025f,2.37f),i%3==0?rust:steel);
            foreach(float x in new[]{6.25f,7.75f})Box(shelter,new Vector3(x,.19f,-1f),new Vector3(.09f,.36f,.48f),wood);
            Box(shelter,new Vector3(7,.76f,-.73f),new Vector3(2,.4f,.055f),wood);
            foreach(float x in new[]{3.36f,4.64f})foreach(float z in new[]{.75f,1.25f})Rod(shelter,new Vector3(x,0,z),new Vector3(x,.64f,z),.034f,wood);
            Batch(shelter,"Shelter");
            var growth=New("Growth",r);
            for(int i=0;i<16;i++)
            {
                float x=1.9f+(i%4)*1.4f;
                float z=-2.9f-(i/4)*.68f;
                var basePoint=new Vector3(x,.19f,z);
                float height=.23f+(i%3)*.05f;
                Rod(growth,basePoint,basePoint+Vector3.up*height,.008f,leaf);
                for(int j=0;j<3;j++)
                {
                    float a=j*2.4f+i;
                    var joint=basePoint+Vector3.up*(.06f+j*.05f);
                    var end=joint+new Vector3(Mathf.Cos(a)*.14f,.045f,Mathf.Sin(a)*.14f);
                    Rod(growth,joint,end,.006f,leaf);
                    var blade=Sphere(growth,end,new Vector3(.17f,.023f,.06f),leaf,"Leaf");
                    blade.localRotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,15);
                }
            }
            Batch(growth,"Growth");
        }

        static void Ring(Transform root,float radius,float tube,Material material,Quaternion facing,Vector3 center=default)
        {
            const int count=40;
            for(int i=0;i<count;i++)
            {
                float a=i*Mathf.PI*2/count,b=(i+1)*Mathf.PI*2/count;
                Rod(root,center+facing*new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*radius,center+facing*new Vector3(Mathf.Cos(b),Mathf.Sin(b),0)*radius,tube,material);
            }
        }

        static void Sensors()
        {
            var delivery=world.GetComponentInChildren<Delivery>(true);
            if(delivery && delivery.drone)
            {
                var r=Reset(delivery.drone,"Art");
                Ring(r,.087f,.011f,steel,Quaternion.identity,new Vector3(0,0,-.18f));
                Shape("Lens",PrimitiveType.Cylinder,r,new Vector3(0,0,-.176f),new Vector3(.15f,.01f,.15f),glass,new Vector3(90,0,0));
                foreach(float side in new[]{-.27f,.27f})
                {
                    Box(r,new Vector3(side,0,0),new Vector3(.12f,.11f,.22f),stone,"Rotor");
                    Ring(r,.09f,.011f,steel,Quaternion.Euler(90,0,0),new Vector3(side,.065f,0));
                }
                Batch(r,"Drone");
            }
            foreach(var sensor in world.GetComponentsInChildren<Drone>(true))
            {
                if(!sensor.visual)continue;
                var r=Reset(sensor.visual,"Art");
                r.localScale=new Vector3(1/sensor.visual.localScale.x,1/sensor.visual.localScale.y,1/sensor.visual.localScale.z);
                Ring(r,.115f,.017f,steel,Quaternion.identity,new Vector3(0,0,.238f));
                Shape("Lens",PrimitiveType.Cylinder,r,new Vector3(0,0,.238f),new Vector3(.18f,.013f,.18f),glass,new Vector3(90,0,0));
                if(!sensor.cameraOnly)
                    foreach(float side in new[]{-.34f,.34f})
                    {
                        Rod(r,new Vector3(0,0,0),new Vector3(side,0,0),.031f,steel);
                        Ring(r,.14f,.017f,steel,Quaternion.Euler(90,0,0),new Vector3(side,0,0));
                        Box(r,new Vector3(side,0,0),new Vector3(.23f,.015f,.035f),dark,"Rotor");
                    }
                Batch(r,"Sensor");
            }
        }

        static void Angel()
        {
            var signal=ObjectNames.Find(world, "Signal");
            if(!signal)return;
            var original=ObjectNames.Find(signal, "Angel");
            if(original)original.gameObject.SetActive(false);
            var previous=ObjectNames.Find(signal, "Form");
            if(previous)Object.DestroyImmediate(previous.gameObject);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RealityPlayground/StoryPrefabs/RealityStory.prefab");
            var source=prefab?prefab.GetComponentInChildren<StoryAngel>(true):null;
            if(!source)return;
            var model=Object.Instantiate(source.gameObject,signal,false);
            model.name="Form";
            model.SetActive(true);
            model.transform.localPosition=new Vector3(0,.35f,1.5f);
            model.transform.localRotation=Quaternion.identity;
            model.transform.localScale=Vector3.one*.9f;
            var existing=model.GetComponent<StoryAngel>();
            var serialized=new SerializedObject(existing);
            var rings=ReadTransforms(serialized.FindProperty("orbits"));
            var wings=ReadTransforms(serialized.FindProperty("wings"));
            var core=serialized.FindProperty("core").objectReferenceValue as Transform;
            var field=serialized.FindProperty("coreRenderer").objectReferenceValue as Renderer;
            if(field && field.sharedMaterial)
            {
                string path=Assets+"Core.mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!material){material=new Material(field.sharedMaterial);AssetDatabase.CreateAsset(material,path);}
                if(material.HasProperty("_Intensity"))material.SetFloat("_Intensity",1.15f);
                field.sharedMaterial=material;
                EditorUtility.SetDirty(material);
            }
            foreach(var text in model.GetComponentsInChildren<TextMesh>(true))Object.DestroyImmediate(text.gameObject);
            foreach(var behavior in model.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(behavior);
            foreach(var sound in model.GetComponentsInChildren<AudioSource>(true))Object.DestroyImmediate(sound);
            foreach(var collider in model.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);
            foreach(var ring in rings)if(ring)Batch(ring,"Ring");
            foreach(var wing in wings)if(wing)Batch(wing,"Wing");
            var form=model.AddComponent<SignalForm>();
            form.voice=loop.talk?loop.talk.voice:null;
            form.rings=rings;
            form.wings=wings;
            form.core=core;
            form.field=field;
            foreach(var t in model.GetComponentsInChildren<Transform>(true))
            {
                if(t==model.transform)continue;
                if(ObjectNames.Contains(t.name, "ring") || ObjectNames.Contains(t.name, "orbit"))t.name="Ring";
                else if(ObjectNames.Contains(t.name, "wing") || ObjectNames.Contains(t.name, "Wing"))t.name="Wing";
                else if(ObjectNames.Contains(t.name, "eye") || ObjectNames.Contains(t.name, "pupil"))t.name="Eye";
                else if(ObjectNames.Contains(t.name, "Core"))t.name="Core";
                else if(ObjectNames.Contains(t.name, "anatomy"))t.name="Body";
                else if(ObjectNames.Contains(t.name, "feather"))t.name="Feather";
            }
            var space=Reset(signal,"Space");
            var grid=New("Grid",space);
            for(int i=-5;i<=5;i++)
            {
                Box(grid,new Vector3(i*.8f,.008f,0),new Vector3(.012f,.008f,8.0f),glass);
                Box(grid,new Vector3(0,.008f,i*.8f),new Vector3(8.0f,.008f,.012f),glass);
            }
            Batch(grid,"Grid");
            var halo=New("Orbit",space,new Vector3(0,2.6f,1.5f));
            Ring(halo,3.8f,.015f,teal,Quaternion.Euler(30,0,0));
            Ring(halo,4.3f,.012f,violet,Quaternion.Euler(0,30,60));
            Batch(halo,"Orbit");
            Animate(space,new Holo.Part{target=halo,spin=new Vector3(2,4,6)});
        }

        static Transform[] ReadTransforms(SerializedProperty property)
        {
            if(property==null || !property.isArray)return Array.Empty<Transform>();
            var result=new Transform[property.arraySize];
            for(int i=0;i<result.Length;i++)result[i]=property.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
            return result;
        }
    }
}
