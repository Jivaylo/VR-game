using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Garden;
using RealityPlayground;
using RealityPlayground.Story;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Object = UnityEngine.Object;

namespace Garden.Editor
{
    public static partial class GardenBuilder
    {
        static Material concrete, road, blue, red, metal, glass, glow, green;
        static Transform world;
        static Loop loop;
        static TMP_FontAsset font;
        static int materialId, meshId;
        static readonly List<GameObject> modules = new List<GameObject>();
        const string Root = "Assets/Garden/";
        [MenuItem("Garden/Build Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop play mode before building.");
            var current=EditorSceneManager.GetActiveScene();
            if (current.isDirty && !string.IsNullOrEmpty(current.path)) EditorSceneManager.SaveScene(current);
            Directory.CreateDirectory(Root + "Materials"); Directory.CreateDirectory(Root + "Meshes"); Directory.CreateDirectory(Root + "Prefabs");
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            modules.Clear(); materialId=0; meshId=0;
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            concrete = Mat("Concrete",new Color(.42f,.44f,.44f));
            road = Mat("Street",new Color(.29f,.31f,.32f));
            metal = Mat("Steel",new Color(.18f,.22f,.24f));
            blue = Mat("Route",new Color(.13f,.48f,.56f));
            red = Mat("Factory",new Color(.5f,.20f,.17f));
            green = Mat("Leaf",new Color(.2f,.38f,.17f));
            glow = Mat("Light",new Color(.51f,.83f,.81f),true);
            glass = Mat("Glass",new Color(.22f,.47f,.52f));
            world = Empty("Garden",null,Vector3.zero);
            loop = world.gameObject.AddComponent<Loop>();
            var sun = Empty("Sun",world,new Vector3(0,50,0)).gameObject.AddComponent<Light>();
            sun.type=LightType.Directional; sun.intensity=.9f; sun.shadows=LightShadows.None; sun.transform.rotation=Quaternion.Euler(48,-25,0);
            RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.65f,.68f,.7f); RenderSettings.skybox=null; RenderSettings.fog=true; RenderSettings.fogColor=new Color(.36f,.4f,.42f); RenderSettings.fogMode=FogMode.Linear; RenderSettings.fogStartDistance=38; RenderSettings.fogEndDistance=90;
            BuildMap(world);
            foreach(var renderer in world.GetComponentsInChildren<Renderer>()) if(!renderer.GetComponentInParent<NavPad>()) GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,StaticEditorFlags.BatchingStatic);
            loop.player=RigBuilder.Build(world);
            loop.player.stationViewpoints=Array.Empty<Transform>();
            loop.transition=StoryTransition.Create(world);
            loop.home=Empty("Home Point",world,Home);
            loop.checkpoint=Empty("Cover Point",world,Yard+new Vector3(-5,0,0));
            loop.outside=Empty("Outside Point",world,Outside);
            loop.overlays=Empty("Presentation",world,Vector3.zero).gameObject;
            BuildSound();
            BuildHome(); BuildBreakfast(); BuildFactory(); BuildConcourse(); BuildYard(); BuildOutside(); BuildTrain(); BuildChoices();
            BuildPresentation();
            BuildDetails();
            var mapSign=Text("MOVE\nAim at a blue pad\nTrigger or grip\n\nOBJECTS\nPoint and trigger\nGrip handles",world,Home+new Vector3(-2.83f,1.5f,-1.5f),.10f);
            mapSign.rectTransform.sizeDelta=new Vector2(1.15f,1.25f); mapSign.transform.rotation=Quaternion.Euler(0,270,0);
            Act("Replay voice",world,Home+new Vector3(-2.8f,.75f,-1.5f),loop.Replay).transform.rotation=Quaternion.Euler(0,270,0);
            Persist(world.gameObject);
            foreach(var module in modules) { if(!module.GetComponent<Link>()) module.AddComponent<Link>(); PrefabUtility.SaveAsPrefabAsset(module,Root+"Prefabs/"+module.name+".prefab"); }
            PrefabUtility.SaveAsPrefabAsset(world.gameObject,Root+"Prefabs/Garden.prefab");
            EditorSceneManager.SaveScene(scene,"Assets/Scenes/Garden.unity");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/Scenes/Garden.unity",true)}.Concat(EditorBuildSettings.scenes.Where(x=>x.path!="Assets/Scenes/Garden.unity").Select(x=>new EditorBuildSettingsScene(x.path,false))).ToArray();
            AssetDatabase.SaveAssets();
            Selection.activeGameObject=world.gameObject;
            if(SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.LookAt(Home+Vector3.up,Quaternion.Euler(35,0,0),14);
            Improve();
        }
        static Transform Module(string name,Vector3 point)
        {
            var root=Empty(name,world,point); modules.Add(root.gameObject); return root;
        }
        static Material Mat(string name,Color color,bool unlit=false)
        {
            string path=Root+"Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material) { material=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material,path); }
            material.name=name; material.enableInstancing=true; material.SetColor("_BaseColor",color); if(material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness",.15f);
            return material;
        }
        static Transform Empty(string name,Transform parent,Vector3 pos)
        {
            var go=new GameObject(ObjectNames.Short(name)); go.transform.SetParent(parent,false); go.transform.position=pos; return go.transform;
        }
        static GameObject Cube(string name,Transform parent,Vector3 pos,Vector3 size,Material mat,bool collider=true)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=ObjectNames.Short(name); go.transform.SetParent(parent,false); go.transform.position=pos; go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=mat; go.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            if(!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        static TMP_Text Text(string label,Transform parent,Vector3 pos,float size)
        {
            var t=Empty("Text",parent,pos).gameObject.AddComponent<TextMeshPro>();
            t.font=font; t.text=label; t.fontSize=size*10; t.color=new Color(.89f,.95f,.95f); t.alignment=TextAlignmentOptions.Center;
            t.rectTransform.sizeDelta=new Vector2(2.4f,1.2f); t.textWrappingMode=TextWrappingModes.Normal; t.richText=false; t.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off; return t;
        }
        static Button Act(string label,Transform parent,Vector3 pos,UnityAction action)
        {
            var root=Empty(label.Split('\n')[0],parent,pos); root.gameObject.layer=8;
            var b=root.gameObject.AddComponent<Button>(); b.visual=Cube("Model",root,pos,new Vector3(.45f,.21f,.10f),blue,false).transform;
            var box=root.gameObject.AddComponent<BoxCollider>(); box.size=new Vector3(.5f,.25f,.13f); box.isTrigger=true;
            var txt=Text(label,root,pos+new Vector3(0,0,-.057f),.10f); txt.rectTransform.sizeDelta=new Vector2(.44f,.20f);
            Wire(root.gameObject,box);
            if(action.Target is Loop) b.action=action.Method.Name; else UnityEventTools.AddPersistentListener(b.pressed,action);
            return b;
        }
        static void Wire(GameObject obj,Collider collider=null)
        {
            obj.layer=8;
            var i=obj.GetComponent<XRSimpleInteractable>(); if(!i) i=obj.AddComponent<XRSimpleInteractable>();
            if(collider) { i.colliders.Clear(); i.colliders.Add(collider); }
            if(!obj.GetComponent<XRExperimentBridge>()) obj.AddComponent<XRExperimentBridge>();
        }
        static NavPad Pad(string name,Transform parent,Vector3 floor)
        {
            var go=Empty(name,parent,floor).gameObject; go.layer=9;
            var marker=Cylinder("Model",go.transform,floor+Vector3.up*.018f,new Vector3(.72f,.018f,.72f),blue,false);
            var box=go.AddComponent<BoxCollider>(); box.size=new Vector3(.85f,.07f,.85f); box.center=Vector3.up*.035f; box.isTrigger=true;
            var p=go.AddComponent<NavPad>(); p.marker=marker.GetComponent<Renderer>(); p.destination=go.transform;
            var a=go.GetComponent<PadAnchor>(); a.colliders.Clear(); a.colliders.Add(box);
            return p;
        }
        static GameObject Cylinder(string name,Transform parent,Vector3 pos,Vector3 size,Material mat,bool collider=true)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name=ObjectNames.Short(name); go.transform.SetParent(parent,false); go.transform.position=pos; go.transform.localScale=size; go.GetComponent<Renderer>().sharedMaterial=mat; go.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            if(!collider) Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
        }
        static Vector3 MapPoint(float px,float py,float y=0) { return new Vector3((px/2480f-.5f)*100,y,(.5f-py/3508f)*141.451613f); }
        static void Room(Transform root,Vector3 p,float width,float depth,bool roof=true)
        {
            Cube("Floor",root,p+Vector3.down*.12f,new Vector3(width,.24f,depth),concrete);
            Cube("Back",root,p+new Vector3(0,1.5f,depth*.5f),new Vector3(width,3,.18f),concrete);
            Cube("Side",root,p+new Vector3(-width*.5f,1.5f,0),new Vector3(.18f,3,depth),concrete);
            Cube("Side",root,p+new Vector3(width*.5f,1.5f,0),new Vector3(.18f,3,depth),concrete);
            Cube("Front",root,p+new Vector3(-(width+1.8f)*.25f,1.5f,-depth*.5f),new Vector3((width-1.8f)*.5f,3,.18f),concrete);
            Cube("Front",root,p+new Vector3((width+1.8f)*.25f,1.5f,-depth*.5f),new Vector3((width-1.8f)*.5f,3,.18f),concrete);
            if(roof) Cube("Roof",root,p+Vector3.up*3.15f,new Vector3(width,.2f,depth),concrete);
        }
        static void BuildSound()
        {
            var t=Empty("Receiver",world,Vector3.zero); loop.talk=t.gameObject.AddComponent<Talk>();
            var board=Empty("Caption",t,Vector3.zero); loop.talk.board=board;
            Cube("Back",board,new Vector3(0,0,.03f),new Vector3(1.5f,.74f,.025f),metal,false);
            loop.talk.caption=Text("",board,new Vector3(0,0,0),.075f); loop.talk.caption.rectTransform.sizeDelta=new Vector2(1.42f,.68f);
            loop.talk.voice=t.gameObject.AddComponent<AudioSource>(); loop.talk.voice.spatialBlend=0; loop.talk.voice.playOnAwake=false; loop.talk.voice.volume=.72f;
            loop.cue=Empty("Cues",world,Vector3.zero).gameObject.AddComponent<AudioSource>(); loop.cue.playOnAwake=false;
            loop.ambience=Empty("Ambience",world,Vector3.zero).gameObject.AddComponent<AudioSource>(); loop.ambience.loop=true; loop.ambience.spatialBlend=0; loop.ambience.volume=.2f;
            loop.clickSound=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"Audio/Click.wav"); loop.city=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"Audio/City.wav"); loop.wind=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"Audio/Wind.wav");
            string path=Root+"Audio/Voices.json";
            if(File.Exists(path)) { var data=JsonUtility.FromJson<VoiceList>(File.ReadAllText(path)); if(data!=null && data.lines!=null) loop.talk.lines=data.lines.Select(x=>new Talk.Line{text=x.text,clip=AssetDatabase.LoadAssetAtPath<AudioClip>(x.path.Replace('\\','/'))}).ToArray(); }
        }
        [Serializable] sealed class VoiceItem { public string text; public string path; }
        [Serializable] sealed class VoiceList { public VoiceItem[] lines; }
        static void BuildHome()
        {
            var r=Module("Home",Home); Room(r,Home,6,5);
            Cube("Bed",r,Home+new Vector3(-2,.3f,.4f),new Vector3(1.15f,.6f,2.1f),metal);
            Cube("Mattress",r,Home+new Vector3(-2,.66f,.4f),new Vector3(1.08f,.12f,2.0f),concrete);
            Cube("Table",r,Home+new Vector3(1.8f,.65f,.4f),new Vector3(1.25f,.12f,.9f),metal);
            Cube("Chair",r,Home+new Vector3(1.7f,.25f,-.45f),new Vector3(.5f,.5f,.5f),concrete);
            Cube("Sink",r,Home+new Vector3(2.62f,.55f,-1.45f),new Vector3(.35f,.75f,1.05f),concrete);
            Cube("Partition",r,Home+new Vector3(1.1f,1.2f,1.7f),new Vector3(.12f,2.4f,1.4f),metal);
            Cylinder("Toilet",r,Home+new Vector3(2.2f,.28f,1.9f),new Vector3(.5f,.28f,.6f),concrete);
            Cube("Shower",r,Home+new Vector3(1.65f,2.3f,2.2f),new Vector3(.4f,.12f,.4f),metal,false);
            var door=r.gameObject.AddComponent<Door>(); door.leaf=Cube("Door",r,Home+new Vector3(0,1.2f,-2.5f),new Vector3(1.7f,2.4f,.12f),metal).transform; door.barrier=door.leaf.GetComponent<Collider>();
            Act("Door",r,Home+new Vector3(.65f,1.05f,-2.37f),door.Toggle).transform.rotation=Quaternion.Euler(0,180,0);
            Act("Door",r,Home+new Vector3(.65f,1.05f,-2.65f),door.Toggle);
            Pad("Bed Pad",r,Home); Pad("Door Pad",r,Home+new Vector3(0,0,-1.65f)); Pad("Landing",r,Home+new Vector3(0,0,-3.4f));
            Act("Sleep",r,Home+new Vector3(-1.5f,.95f,1.3f),loop.Sleep);
            Act("Wash",r,Home+new Vector3(2.36f,.98f,-1.7f),loop.Wash).transform.rotation=Quaternion.Euler(0,90,0);
            Act("Dinner",r,Home+new Vector3(1.6f,.98f,.85f),loop.Dinner);
            loop.receiver=Text("Food receiver\nEvening delivery",r,Home+new Vector3(1.8f,1.4f,1.25f),.12f);
            loop.clock=Text("",r,Home+new Vector3(0,1.8f,2.35f),.15f);
            Act("News",r,Home+new Vector3(.7f,.95f,1.9f),loop.News);
            loop.morning=Empty("Morning",loop.overlays.transform,Home+new Vector3(-2.1f,1.8f,2.2f)).gameObject;
            var greeting=Text("GOOD MORNING\n07:00",loop.morning.transform,loop.morning.transform.position,.12f); greeting.rectTransform.sizeDelta=new Vector2(1.1f,.6f);
            loop.alarmPoint=Empty("Alarm Point",r,Home+new Vector3(-1.0f,.7f,1.1f));
            var rooster=StoryAlarm.Create(null); rooster.GetComponent<StoryAlarm>().interactionEnabled=true; rooster.GetComponent<StoryAlarm>().ConfigureCrow(AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"Audio/Rooster.wav")); Wire(rooster,rooster.GetComponent<Collider>());
            var alarmLabel=rooster.GetComponentInChildren<TextMesh>(); if(alarmLabel) alarmLabel.transform.localScale*=.5f;
            Persist(rooster); loop.alarmPrefab=PrefabUtility.SaveAsPrefabAsset(rooster,Root+"Prefabs/Rooster.prefab"); Object.DestroyImmediate(rooster);
            Act("Snooze",r,Home+new Vector3(-.85f,1.0f,1.1f),loop.Swat);
            MakeMirror(r,Home+new Vector3(2.77f,1.55f,-1.45f),90);
            Pad("Sink Pad",r,Home+new Vector3(2.08f,0,-1.45f));
            Act("Local",r,Home+new Vector3(1.1f,.95f,-1.5f),loop.OfferLocal);
            var mug=Empty("Mug",r,Home+new Vector3(1.8f,.85f,.4f));
            var model=Cylinder("Model",mug,mug.position,new Vector3(.12f,.09f,.12f),blue,false); var carry=mug.gameObject.AddComponent<Carry>(); carry.model=model.transform; var bc=mug.gameObject.AddComponent<BoxCollider>(); bc.size=Vector3.one*.18f; Wire(mug.gameObject,bc);
        }
        static void BuildBreakfast()
        {
            var r=Module("Breakfast",Breakfast); var p=Breakfast;
            Cube("Counter",r,p+new Vector3(0,.6f,1.2f),new Vector3(2.3f,1.2f,.7f),concrete);
            Cube("Meal",r,p+new Vector3(0,1.25f,1.2f),new Vector3(.28f,.10f,.2f),road,false);
            Act("Meal",r,p+new Vector3(-.6f,1.1f,.77f),loop.Meal); Act("Peel wrapper",r,p+new Vector3(.25f,1.1f,.77f),loop.Peel);
            Text("NUTRITION\n07:30\nStandard base / Presentation variable",r,p+new Vector3(0,1.9f,1.3f),.16f);
            Pad("Counter Pad",r,p);
            Person("Mara",r,p+new Vector3(2,0,0),blue);
            Act("Talk",r,p+new Vector3(1.55f,1.05f,-.2f),loop.Mara);
            Cube("Table",r,p+new Vector3(2,.7f,-1.1f),new Vector3(1.2f,.1f,.7f),metal);
            var cup=Empty("Cup",r,p+new Vector3(1.7f,.86f,-1.1f)); var c=cup.gameObject.AddComponent<Carry>(); c.cup=true; c.model=Cylinder("Model",cup,cup.position,new Vector3(.13f,.09f,.13f),blue,false).transform;
            var box=cup.gameObject.AddComponent<BoxCollider>(); box.size=Vector3.one*.22f; Wire(cup.gameObject,box);
            Act("Take cup",r,p+new Vector3(2.1f,.92f,-1.5f),loop.Cup);
        }
        static void Person(string name,Transform r,Vector3 p,Material m)
        {
            var root=Empty(name,r,p); Cylinder("Body",root,p+Vector3.up*.9f,new Vector3(.42f,.55f,.35f),m,false);
            Cylinder("Head",root,p+Vector3.up*1.6f,new Vector3(.28f,.16f,.28f),concrete,false);
            var arm=Cube("Arm",root,p+new Vector3(.3f,1.0f,0),new Vector3(.12f,.6f,.12f),m,false).transform;
            arm.rotation=Quaternion.Euler(15,0,-15);
        }
        static void BuildFactory()
        {
            var r=Module("Factory",Factory); var p=Factory;
            Cube("Platform",r,p+Vector3.down*.1f,new Vector3(7,.2f,6),road);
            Cube("Line",r,p+new Vector3(0,.55f,2.5f),new Vector3(4,1.1f,1.3f),metal);
            Cube("Guard",r,p+new Vector3(0,.5f,1.2f),new Vector3(4,1,.12f),metal);
            var a=r.gameObject.AddComponent<Assembly>(); a.loop=loop;
            a.rejectArm=Cube("Reject Arm",r,p+new Vector3(-.55f,1.6f,2.4f),new Vector3(.2f,.2f,1.1f),red,false).transform;
            a.acceptArm=Cube("Accept Arm",r,p+new Vector3(.55f,1.6f,2.4f),new Vector3(.2f,.2f,1.1f),blue,false).transform;
            a.panel=Cube("Panel",r,p+new Vector3(0,1.4f,2.7f),new Vector3(1.6f,.7f,.12f),concrete,false).transform;
            a.panelDrop=new Vector3(0,-.5f,-.4f);
            a.cabinet=Empty("Cabinet",r,p+new Vector3(-2.5f,.85f,.4f)).gameObject; Cube("Base",a.cabinet.transform,a.cabinet.transform.position,new Vector3(.7f,.25f,.5f),metal);
            a.gloveStand=Empty("Glove Stand",a.cabinet.transform,p+new Vector3(-2.5f,1.0f,.4f));
            Cube("Glove",a.gloveStand,a.gloveStand.position,new Vector3(.12f,.06f,.2f),blue,false);
            Act("Equip glove",a.cabinet.transform,p+new Vector3(-2.5f,.9f,.05f),loop.Glove);
            a.rawSample=Cube("Sample",a.cabinet.transform,p+new Vector3(-2.5f,1.4f,.5f),new Vector3(.65f,.4f,.04f),green,false);
            Act("Raw sample",a.cabinet.transform,p+new Vector3(-2.5f,1.4f,.2f),a.ViewSample);
            a.display=Text("",r,p+new Vector3(0,2.2f,2.2f),.15f); a.display.rectTransform.sizeDelta=new Vector2(3.5f,1.2f);
            a.samples=new GameObject[3]; for(int i=0;i<3;i++) { a.samples[i]=Cube("Housing "+(i+1),r,p+new Vector3(0,1.35f,1.6f),new Vector3(.4f,.2f,.3f),i==2?green:i==1?red:blue,false); if(i==1) a.samples[i].transform.rotation=Quaternion.Euler(0,0,25); }
            a.acceptGuide=Cube("Accept Path",r,p+new Vector3(.5f,1.25f,1.4f),new Vector3(.9f,.015f,.08f),glow,false); a.rejectGuide=Cube("Reject Path",r,p+new Vector3(-.5f,1.25f,1.4f),new Vector3(.9f,.015f,.08f),red,false);
            a.acceptGuide.SetActive(false); a.rejectGuide.SetActive(false);
            var reject=Act("X\nIncorrect",r,p+new Vector3(-.6f,1.0f,.65f),a.Reject).GetComponent<XRSimpleInteractable>();
            var accept=Act("CHECK\nCorrect",r,p+new Vector3(.6f,1.0f,.65f),a.Accept).GetComponent<XRSimpleInteractable>();
            UnityEventTools.AddVoidPersistentListener(reject.hoverEntered,a.HoverReject); UnityEventTools.AddVoidPersistentListener(reject.hoverExited,a.ClearPreview);
            UnityEventTools.AddVoidPersistentListener(accept.hoverEntered,a.HoverAccept); UnityEventTools.AddVoidPersistentListener(accept.hoverExited,a.ClearPreview);
            Act("Review sample",r,p+new Vector3(1.5f,1.0f,.65f),a.Review);
            Text("ASSEMBLY 07\nClassify the housing\nDiagnostic trace: X / CHECK / X",r,p+new Vector3(0,2.7f,2.7f),.16f);
            Pad("Station",r,p); Pad("Cabinet Pad",r,p+new Vector3(-2.1f,0,-.5f));
        }
        static void MakeMirror(Transform parent,Vector3 pos,float yaw=0)
        {
            var facing=Quaternion.Euler(0,yaw,0);
            var root=BleakMirror.Create(parent); root.name="Mirror"; root.transform.SetPositionAndRotation(pos-Vector3.up*1.0075f,facing); root.transform.localScale=Vector3.one*.65f;
            foreach(var label in root.GetComponentsInChildren<TextMesh>(true)) Object.DestroyImmediate(label.gameObject);
            var serialized=new SerializedObject(root.GetComponent<BleakMirror>()); serialized.FindProperty("reflectionResolution").intValue=384; serialized.FindProperty("hiddenLayers").intValue=1<<11; serialized.ApplyModifiedPropertiesWithoutUndo();
            var range=root.AddComponent<MirrorRange>(); range.view=root.GetComponent<BleakMirror>();
            var m=root.AddComponent<Garden.Mirror>(); m.loop=loop; var liquid=root.GetComponentInChildren<LiquidMirror>(); m.surface=liquid.SurfaceTransform; m.size=liquid.SurfaceDimensions;
            var diagram=Text("CONCOURSE HATCH\nTrace the frame / Release socket",parent,pos+facing*new Vector3(0,1.03f,-.14f),.10f); diagram.rectTransform.sizeDelta=new Vector2(1.65f,.4f); diagram.transform.rotation=facing; m.diagram=diagram.gameObject;
            Wire(liquid.gameObject,m.surface.GetComponent<Collider>());
            Act("Calibrate",parent,pos+facing*new Vector3(-.6f,-.45f,-.2f),m.Calibrate).transform.rotation=facing;
        }
        static void BuildConcourse()
        {
            var r=Module("Concourse",Hatch); var p=Hatch;
            Cube("Threshold",r,p+new Vector3(2,-.1f,0),new Vector3(7,.2f,3.2f),road);
            Cube("Wall",r,p+new Vector3(.5f,2.3f,4.7f),new Vector3(.25f,4.6f,6),concrete);
            Cube("Wall",r,p+new Vector3(.5f,2.3f,-4.7f),new Vector3(.25f,4.6f,6),concrete);
            var hroot=Empty("Hatch",r,p+new Vector3(.5f,1.2f,0)); hroot.rotation=Quaternion.Euler(0,90,0);
            var h=hroot.gameObject.AddComponent<Garden.Hatch>(); h.loop=loop;
            h.door=Cube("Door",hroot,hroot.position,new Vector3(1.8f,2.4f,.16f),metal).transform; h.door.localRotation=Quaternion.identity; h.door.localScale=new Vector3(3.0f,2.4f,.16f);
            h.barrier=h.door.GetComponent<Collider>(); h.size=new Vector2(3,2.4f); h.travel=new Vector3(3.1f,0,0);
            h.overlay=Cube("Overlay",hroot,hroot.position+Vector3.left*.12f,new Vector3(3,2.4f,.05f),blue,false); h.overlay.transform.localRotation=Quaternion.identity; h.overlay.layer=11;
            h.display=Text("",r,p+new Vector3(-.1f,1.9f,-1.2f),.1f); h.display.transform.rotation=Quaternion.Euler(0,90,0);
            var trace=Act("Trace",r,p+new Vector3(-.25f,1.1f,-1.1f),h.Trace); trace.transform.rotation=Quaternion.Euler(0,90,0);
            h.socket=Act("Socket",r,p+new Vector3(-.25f,1.1f,1.1f),h.Unlock).gameObject; h.socket.transform.rotation=Quaternion.Euler(0,90,0);
            var hb=hroot.gameObject.AddComponent<BoxCollider>(); hb.isTrigger=true; hb.size=new Vector3(3,2.4f,.08f); Wire(hroot.gameObject,hb);
            Pad("Hatch Pad",r,p+new Vector3(-1.3f,0,0)); Pad("Seam Pad",r,p+new Vector3(-.18f,0,0)); Pad("Socket Pad",r,p+new Vector3(-.6f,0,.95f)); Pad("Service Pad",r,p+new Vector3(2.5f,0,0));
            MakeMirror(r,p+new Vector3(-2.8f,1.5f,0),270);
            Pad("Mirror Pad",r,p+new Vector3(-2.15f,0,0));
            var br=Empty("Bridge",r,p+new Vector3(2.5f,1.05f,1.05f)); var b=br.gameObject.AddComponent<Bridge>(); b.loop=loop;
            Cube("Shelf",r,p+new Vector3(2.5f,.75f,1.05f),new Vector3(1.4f,.12f,.6f),metal);
            b.model=Cube("Model",br,br.position,new Vector3(.16f,.06f,.22f),glow); b.model.layer=8;
            b.port=Cube("Port",world,Vector3.zero,new Vector3(.19f,.025f,.25f),blue,false).transform;
            Wire(br.gameObject,b.model.GetComponent<Collider>());
            b.display=Text("FOREARM SERVICE\nLOCAL / NETWORK",r,p+new Vector3(2.5f,1.65f,1.2f),.12f);
            Act("Take bridge",r,p+new Vector3(1.85f,.98f,.7f),b.Take); Act("Fit bridge",r,p+new Vector3(2.5f,.98f,.7f),b.Install); Act("Local",r,p+new Vector3(3.15f,.98f,.7f),b.OfferLocal);
            Pad("Bridge Pad",r,p+new Vector3(2.5f,0,.35f));
            var inner=r.gameObject.AddComponent<Door>(); inner.localOnly=true; inner.travel=new Vector3(0,3,0); inner.leaf=Cube("Inner Door",r,p+new Vector3(4.8f,1.3f,0),new Vector3(.18f,2.6f,3.2f),metal).transform; inner.barrier=inner.leaf.GetComponent<Collider>();
            Cube("Roof",r,p+new Vector3(2.65f,2.8f,0),new Vector3(4.5f,.2f,3.4f),concrete);
            Cube("Side",r,p+new Vector3(2.65f,1.5f,1.7f),new Vector3(4.5f,3,.2f),concrete); Cube("Side",r,p+new Vector3(2.65f,1.5f,-1.7f),new Vector3(4.5f,3,.2f),concrete);
            Act("Inner door",r,p+new Vector3(4.5f,1,-1),inner.Toggle).transform.rotation=Quaternion.Euler(0,90,0);
            Act("Inner door",r,p+new Vector3(5.1f,1,-1),inner.Toggle).transform.rotation=Quaternion.Euler(0,270,0);
            BuildPoster();
            var glove=Empty("Glove",world,Vector3.zero); loop.gloveModel=glove; Cube("Model",glove,Vector3.zero,new Vector3(.09f,.035f,.17f),blue,false);
            loop.evidence=Text("",glove,new Vector3(0,.04f,0),.025f); loop.evidence.transform.localRotation=Quaternion.Euler(90,0,0); loop.evidence.rectTransform.sizeDelta=new Vector2(.3f,.25f);
            var pulse=glove.gameObject.AddComponent<Pulse>(); pulse.loop=loop; pulse.emitter=glove;
            var pulsePanel=Empty("Pulse Display",glove,new Vector3(0,.045f,-.17f)); pulsePanel.localRotation=Quaternion.Euler(90,0,0);
            Cube("Back",pulsePanel,pulsePanel.position+pulsePanel.forward*.003f,new Vector3(.15f,.05f,.004f),metal,false).transform.localRotation=Quaternion.identity;
            pulse.status=Text("PULSE\nLOCAL REQUIRED",pulsePanel,pulsePanel.position,.015f); pulse.status.transform.localRotation=Quaternion.identity; pulse.status.rectTransform.sizeDelta=new Vector2(.145f,.047f);
            Act("Pulse",glove,new Vector3(-.08f,.04f,-.08f),pulse.Fire).transform.localScale=Vector3.one*.22f;
            Act("Evidence",glove,new Vector3(.08f,.04f,-.08f),loop.Evidence).transform.localScale=Vector3.one*.22f;
        }
        static void BuildPoster()
        {
            var r=Module("Poster",Poster); var p=Poster;
            Cube("Board",r,p+new Vector3(0,1.4f,1.2f),new Vector3(2.2f,2.8f,.2f),concrete);
            var po=r.gameObject.AddComponent<Garden.Poster>(); po.loop=loop;
            po.paper=Empty("Paper",r,p+new Vector3(0,1.45f,1.05f));
            var paperMesh=new Mesh{name="Paper"}; var vertices=new Vector3[117]; var uv=new Vector2[117]; var triangles=new List<int>();
            for(int y=0;y<13;y++) for(int x=0;x<9;x++) { int n=y*9+x; vertices[n]=new Vector3((x/8f-.5f)*1.15f,(y/12f-.5f)*1.55f,0); uv[n]=new Vector2(x/8f,y/12f); if(x<8 && y<12) triangles.AddRange(new[]{n,n+9,n+1,n+1,n+9,n+10}); }
            paperMesh.vertices=vertices; paperMesh.uv=uv; paperMesh.triangles=triangles.ToArray(); paperMesh.RecalculateNormals(); paperMesh.RecalculateBounds();
            po.paper.gameObject.AddComponent<MeshFilter>().sharedMesh=paperMesh; po.paper.gameObject.AddComponent<MeshRenderer>().sharedMaterial=blue;
            Text("A BETTER TOMORROW\n\nDo you remember\nunfiltered sky?",po.paper,p+new Vector3(0,1.45f,1.025f),.13f).rectTransform.sizeDelta=new Vector2(1.1f,1.4f);
            var pc=po.paper.gameObject.AddComponent<BoxCollider>(); pc.size=new Vector3(1.15f,1.55f,.025f); Wire(r.gameObject,pc);
            po.rift=Empty("Rift",r,p+new Vector3(0,1.4f,1.05f)).gameObject;
            Cylinder("Ring",po.rift.transform,po.rift.transform.position,new Vector3(1.4f,.025f,1.4f),glow,false).transform.rotation=Quaternion.Euler(90,0,0);
            Act("Enter signal",po.rift.transform,p+new Vector3(0,1.1f,.72f),loop.EnterAngel);
            Act("Peel poster",r,p+new Vector3(-.8f,.95f,.7f),po.Tear);
            po.panels=new GameObject[3];
            for(int i=0;i<3;i++) { var at=p+new Vector3((i-1)*.7f,2.5f,.5f-i*.12f); var panel=Empty("Trace",r,at); Cube("Back",panel,at+Vector3.forward*.025f,new Vector3(.65f,.5f,.035f),metal,false); var line=Text(i==0?"CARRIER FOUND\n07 / 011 / 104":i==1?"PRESENTATION\nChecking local state":"CHANNEL OPEN\nAwaiting contact",panel,at,.075f); line.rectTransform.sizeDelta=new Vector2(.62f,.45f); po.panels[i]=panel.gameObject; }
            po.status=Text("",r,p+new Vector3(0,2.1f,.8f),.11f);
            Pad("Alcove Pad",r,p); loop.alcove=Empty("Alcove",r,p);
            var a=Module("Signal",new Vector3(180,0,0)); var q=a.position;
            Cube("Floor",a,q+Vector3.down*.15f,new Vector3(10,.3f,10),metal);
            loop.angelRoom=Empty("Arrival",a,q+new Vector3(0,0,-2));
            var core=Empty("Angel",a,q+new Vector3(0,2.7f,1.5f));
            for(int ring=0;ring<3;ring++)
            {
                var orbit=Empty("Ring",core,core.position); orbit.rotation=Quaternion.Euler(45*ring,30*ring,25*ring); var move=orbit.gameObject.AddComponent<Motion>(); move.spin=new Vector3(17+ring*11,27+ring*13,31-ring*8); move.voice=loop.talk.voice;
                for(int i=0;i<18;i++) { float angle=i*Mathf.PI*2/18; var bead=Cube("Segment",orbit,orbit.TransformPoint(new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*(1.0f+ring*.2f)),new Vector3(.12f,.12f,.24f),ring==1?blue:glow,false); bead.transform.LookAt(core.position); }
            }
            var eye=Cube("Eye",core,core.position,new Vector3(.5f,.5f,.5f),glow,false); var eyeMotion=eye.AddComponent<Motion>(); eyeMotion.spin=new Vector3(30,70,45); eyeMotion.voice=loop.talk.voice; eyeMotion.response=.8f;
            for(int i=0;i<18;i++) { float ang=i*2.39996f; Cube("Fragment",a,q+new Vector3(Mathf.Cos(ang)*4,1+(i%5)*.65f,Mathf.Sin(ang)*4),new Vector3(.12f,.12f,.8f),blue,false).AddComponent<Motion>().spin=new Vector3(0,12,6); }
            Pad("Signal Pad",a,loop.angelRoom.position);
            Act("Ask",a,q+new Vector3(-.65f,1.1f,-.85f),loop.AskAngel); Act("Return",a,q+new Vector3(.65f,1.1f,-.85f),loop.LeaveAngel);
        }
        static void BuildYard()
        {
            var r=Module("Yard",Yard); var p=Yard; float west=Hatch.x+.5f, east=Gate.x;
            Cube("Floor",r,new Vector3((west+east)*.5f,-.08f,p.z),new Vector3(east-west,.24f,14),road);
            Cube("Wall",r,new Vector3((west+east)*.5f,2.4f,p.z+7),new Vector3(east-west,4.8f,.25f),concrete);
            Cube("Wall",r,new Vector3((west+east)*.5f,2.4f,p.z-4.8f),new Vector3(east-west,4.8f,.25f),concrete);
            Cube("Wall",r,new Vector3(west,2.4f,p.z+3.9f),new Vector3(.3f,4.8f,6.2f),concrete);
            Cube("Wall",r,new Vector3(west,2.4f,p.z-4.0f),new Vector3(.3f,4.8f,1.6f),concrete);
            Cube("Wall",r,new Vector3(east,2.4f,p.z+4.4f),new Vector3(.3f,4.8f,5.2f),concrete);
            Cube("Wall",r,new Vector3(east,2.4f,p.z-3.3f),new Vector3(.3f,4.8f,3.0f),concrete);
            var p0=loop.checkpoint.position;
            Cube("Cover",r,p0+new Vector3(1.25f,1.25f,0),new Vector3(.35f,2.5f,3.2f),metal);
            Cube("Cover",r,p+new Vector3(0,1.25f,3.0f),new Vector3(3,2.5f,.35f),metal);
            Cube("Cover",r,p+new Vector3(0,1.25f,-3.0f),new Vector3(3,2.5f,.35f),metal);
            Cube("Cover",r,Gate+new Vector3(-2.8f,1.25f,0),new Vector3(.35f,2.5f,6f),metal);
            Pad("P0",r,p0); Pad("P1",r,p+new Vector3(-.2f,0,4)); Pad("P2",r,p+new Vector3(-.2f,0,-4)); Pad("P3",r,Gate+new Vector3(-1.3f,0,0));
            Pad("Entry",r,new Vector3(Hatch.x+6.2f,0,Hatch.z)); Pad("Turn",r,p0+new Vector3(0,0,-2.7f));
            Pad("Left",r,Gate+new Vector3(-1.3f,0,3.7f)); Pad("Right",r,Gate+new Vector3(-1.3f,0,-3.7f));
            MakeDrone(r,p+new Vector3(0,2.0f,-1.9f),false,p+new Vector3(0,2.0f,1.9f));
            MakeDrone(r,p+new Vector3(2.6f,2.3f,6.3f),true,p+new Vector3(2.6f,2.3f,6.3f));
            Text("SERVICE YARD\nPulse: short range / line of sight\nCover blocks recovery optics\nThe gate has a local release",r,p0+new Vector3(-.3f,1.6f,1.4f),.13f);
            var g=Empty("Gate",r,Gate).gameObject.AddComponent<Garden.Gate>(); g.loop=loop;
            g.door=Cube("Door",g.transform,Gate+new Vector3(0,1.5f,0),new Vector3(.28f,3,3.2f),metal).transform; g.barrier=g.door.GetComponent<Collider>();
            var panel=Gate+new Vector3(-1.1f,1.1f,1.1f);
            g.selector=Cube("Selector",g.transform,panel+new Vector3(-.4f,0,0),new Vector3(.08f,.24f,.1f),blue,false).transform;
            g.lever=Cube("Lever",g.transform,panel+new Vector3(0,0,0),new Vector3(.08f,.3f,.1f),red,false).transform;
            g.wheel=Cylinder("Wheel",g.transform,panel+new Vector3(.4f,0,0),new Vector3(.3f,.04f,.3f),blue,false).transform; g.wheel.rotation=Quaternion.Euler(90,0,0);
            g.bolt=Cube("Bolt",g.transform,Gate+new Vector3(-.2f,1.7f,1.25f),new Vector3(.2f,.1f,.6f),red,false).transform;
            foreach(var data in new[]{(g.selector,Handle.Kind.Selector),(g.lever,Handle.Kind.Lever),(g.wheel,Handle.Kind.Wheel)}) { var handle=data.Item1.gameObject.AddComponent<Handle>(); handle.gate=g; handle.kind=data.Item2; var col=data.Item1.gameObject.AddComponent<BoxCollider>(); Wire(handle.gameObject,col); }
            g.display=Text("",r,panel+new Vector3(0,.65f,.1f),.12f); g.gauge=Text("",r,panel+new Vector3(0,.37f,.1f),.1f);
            Act("1 Isolate",r,panel+new Vector3(-.6f,-.32f,-.2f),g.Isolate); Act("2 Equalize",r,panel+new Vector3(0,-.32f,-.2f),g.Equalize); Act("3 Release",r,panel+new Vector3(.6f,-.32f,-.2f),g.Release);
            Act("Request departure",r,Gate+new Vector3(-.4f,1,-1.25f),loop.RequestDeparture).transform.rotation=Quaternion.Euler(0,90,0);
            for(float x=Gate.x+1.5f;x<Outside.x;x+=4.5f) { Cube("Passage",r,new Vector3(x,-.1f,Gate.z),new Vector3(4.6f,.2f,3.2f),road); Pad("Path",r,new Vector3(x,0,Gate.z)); }
            Cube("Wall",r,new Vector3((Gate.x+Outside.x)*.5f,2.6f,Gate.z-1.7f),new Vector3(Outside.x-Gate.x,5.2f,.2f),concrete);
            Cube("Wall",r,new Vector3((Gate.x+Outside.x)*.5f,2.6f,Gate.z+1.7f),new Vector3(Outside.x-Gate.x,5.2f,.2f),concrete);
            var exit=Pad("Threshold",r,new Vector3(Gate.x+2.2f,0,Gate.z)); exit.action="ReachOutside";
        }
        static void MakeDrone(Transform r,Vector3 pos,bool camera,Vector3 end)
        {
            var root=Empty(camera?"Camera":"Drone",r,pos); var d=root.gameObject.AddComponent<Drone>(); d.loop=loop; d.cameraOnly=camera;
            d.pointA=Empty("Patrol A",r,pos); d.pointB=Empty("Patrol B",r,end);
            d.visual=Cube("Model",root,pos,new Vector3(.65f,.3f,.4f),metal,false).transform;
            d.lamp=Cube("Optic",root,pos+new Vector3(0,0,.23f),new Vector3(.2f,.12f,.06f),glow,false).GetComponent<Renderer>();
            d.status=Text("",root,pos+new Vector3(0,.45f,0),.11f);
            if(camera) { root.rotation=Quaternion.Euler(0,180,0); d.viewAngle=70; }
        }
        static void BuildOutside()
        {
            var r=Module("Outside",Outside); var p=Outside;
            Cube("Soil",r,p+new Vector3(4,-.18f,0),new Vector3(13,.35f,12),road);
            Cube("Wall",r,p+new Vector3(0,2.5f,3.7f),new Vector3(.3f,5,4),concrete);
            Pad("Arrival",r,p); Pad("Garden Pad",r,p+new Vector3(4,0,2)); Pad("Bench Pad",r,p+new Vector3(7,0,-2));
            Person("Noor",r,p+new Vector3(4,0,3),green); Person("Repair",r,p+new Vector3(8,0,3),concrete);
            Cube("Table",r,p+new Vector3(4,.7f,1),new Vector3(1.6f,.12f,.7f),metal);
            Cylinder("Water",r,p+new Vector3(4,.95f,1),new Vector3(.18f,.18f,.18f),blue,false);
            Act("Talk",r,p+new Vector3(3.4f,1.05f,1.0f),loop.Noor); Act("Water plant",r,p+new Vector3(4.4f,1.05f,1.0f),loop.Water);
            Cube("Bench",r,p+new Vector3(7,.4f,-1),new Vector3(2,.15f,.6f),metal); Act("Rest",r,p+new Vector3(7,.95f,-.75f),loop.Rest);
            Cube("Roof",r,p+new Vector3(7,2.7f,3.6f),new Vector3(4,.15f,2.5f),concrete); Cube("Solar",r,p+new Vector3(7,2.82f,3.6f),new Vector3(1.6f,.08f,1.2f),blue,false).transform.rotation=Quaternion.Euler(12,0,0);
            for(int i=0;i<12;i++) { var at=p+new Vector3(2+(i%4)*1.4f,0,-3-(i/4)*.7f); Cube("Bed",r,at+Vector3.up*.1f,new Vector3(.75f,.2f,.5f),metal); Cylinder("Stem",r,at+Vector3.up*.4f,new Vector3(.03f,.24f,.03f),green,false); Cube("Leaf",r,at+Vector3.up*.6f,new Vector3(.3f,.1f,.24f),green,false).transform.rotation=Quaternion.Euler(12,i*51,18); }
            var bird=Cube("Bird",r,p+new Vector3(4,4,0),new Vector3(.4f,.08f,.17f),concrete,false); bird.AddComponent<Motion>().spin=new Vector3(0,30,0);
            loop.endCard=Text("GARDEN\nYou can stay. You can leave.",r,p+new Vector3(7,1.4f,.4f),.2f).gameObject;
        }
        static void BuildChoices()
        {
            loop.localChoice=Empty("Local Choice",world,Vector3.zero).gameObject;
            Text("Local control\nEnd presentation and metro service?",loop.localChoice.transform,new Vector3(0,.4f,.05f),.13f);
            Act("Keep network",loop.localChoice.transform,new Vector3(-.4f,0,0),loop.KeepNetwork); Act("Continue",loop.localChoice.transform,new Vector3(.4f,0,0),loop.ConfirmLocal);
            loop.gardenChoice=Empty("Garden Choice",world,Vector3.zero).gameObject;
            Text("A garden view is available\nPresentation or local control?",loop.gardenChoice.transform,new Vector3(0,.4f,.05f),.13f);
            Act("Keep garden",loop.gardenChoice.transform,new Vector3(-.4f,0,0),loop.KeepNetwork); Act("Go local",loop.gardenChoice.transform,new Vector3(.4f,0,0),loop.Disconnect);
        }
        static void BuildPresentation()
        {
            var r=loop.overlays.transform;
            foreach(var p in new[]{Breakfast,SouthMetro,WorkMetro,Factory,Arcade})
            {
                for(int i=0;i<3;i++)
                {
                    var root=Empty("Display",r,p+new Vector3((i-1)*1.4f,3.6f,3.7f));
                    Cube("Frame",root,root.position,new Vector3(1.0f,.6f,.035f),blue,false);
                    Text(i==0?"GARDEN 07":i==1?"A BETTER TOMORROW":"YOUR DAY IS READY",root,root.position+new Vector3(0,0,-.04f),.1f).rectTransform.sizeDelta=new Vector2(.95f,.5f);
                    var ornament=Cube("Light",root,root.position+Vector3.up*.6f,Vector3.one*.2f,glow,false); ornament.AddComponent<Motion>().spin=new Vector3(15,35,12);
                }
            }
            foreach(var p in new[]{Breakfast,Arcade,WorkMetro}) for(int i=0;i<3;i++) { var root=Empty("Tree",r,p+new Vector3(3+i*.8f,0,2.3f)); Cylinder("Stem",root,root.position+Vector3.up*.8f,new Vector3(.06f,.8f,.06f),blue,false); var crown=Cube("Crown",root,root.position+Vector3.up*1.8f,Vector3.one*.65f,glow,false); crown.AddComponent<Motion>().spin=new Vector3(0,12,0); }
            foreach(var t in r.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=11;
        }
        static void Persist(GameObject root)
        {
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                foreach(var material in renderer.sharedMaterials) if(material && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(material))) { material.name="Surface "+(++materialId); AssetDatabase.CreateAsset(material,AssetDatabase.GenerateUniqueAssetPath(Root+"Materials/"+material.name+".mat")); }
            }
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true)) if(!filter.GetComponent<TMP_Text>() && filter.sharedMesh && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(filter.sharedMesh))) { filter.sharedMesh.name="Shape "+(++meshId); AssetDatabase.CreateAsset(filter.sharedMesh,AssetDatabase.GenerateUniqueAssetPath(Root+"Meshes/"+filter.sharedMesh.name+".asset")); }
        }
    }
}
