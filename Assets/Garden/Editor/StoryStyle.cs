using System;
using System.IO;
using System.Linq;
using Garden;
using RealityPlayground;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Object = UnityEngine.Object;

namespace Garden.Editor
{
    public static class StoryStyle
    {
        const string Folder = "Assets/Garden/Art/Story/";
        static Loop loop;
        static Material dark, metal, paper, mint, amber, green;
        static TMP_FontAsset font;

        [MenuItem("Garden/Apply Story")]
        public static void ApplyOpen()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop play first.");
            loop = Object.FindFirstObjectByType<Loop>();
            if (!loop || loop.gameObject.scene.path != "Assets/Scenes/Garden.unity") throw new InvalidOperationException("Open Garden first.");
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            dark = Mat("Screen", new Color(.035f,.052f,.06f));
            metal = Mat("Case", new Color(.28f,.32f,.33f));
            paper = Mat("Label", new Color(.81f,.79f,.67f));
            mint = Mat("Mint", new Color(.2f,.72f,.63f));
            amber = Mat("Amber", new Color(.82f,.5f,.2f));
            green = Mat("Growth", new Color(.19f,.34f,.15f));
            Switch();
            Screens();
            Watch();
            SessionStyle.Apply(loop.gameObject);
            Wrapper();
            Security();
            Morning();
            Fracture();
            Questions();
            DinnerStyle.Apply(loop.gameObject);
            TransitStyle.Apply(loop.gameObject);
            SignalStyle.Apply(loop.gameObject);
            foreach(var label in loop.GetComponentsInChildren<TMP_Text>(true))
                if(label && label.text != null && label.text.Contains("Take / Fit / Choose Local")) label.text="FOREARM SERVICE\nLOCAL / NETWORK";
            var community = typeof(StoryStyle).Assembly.GetType("Garden.Editor.CommunityStyle");
            community?.GetMethod("Apply").Invoke(null,new object[]{loop.gameObject});
            Survey();
            GuideBuilder.Apply(loop.gameObject);
            CivicSpeech.Apply(loop.gameObject);
            Save();
        }

        static void Switch()
        {
            var bridge = loop.GetComponentInChildren<Bridge>(true);
            if (!bridge || !bridge.model) throw new InvalidOperationException("Missing bridge.");
            foreach (var grip in loop.GetComponentsInChildren<Grip>(true))
                if (grip.action == "OfferLocal" || Calls(grip, "OfferLocal")) Object.DestroyImmediate(grip.gameObject);
            if (loop.localChoice) Object.DestroyImmediate(loop.localChoice);
            if (loop.gardenChoice) Object.DestroyImmediate(loop.gardenChoice);
            loop.localChoice = loop.gardenChoice = null;
            var root = Reset(bridge.model.transform,"Switch");
            var scale = bridge.model.transform.lossyScale;
            root.localScale = new Vector3(1/scale.x,1/scale.y,1/scale.z);
            var rail = Box(root,"Rail",new Vector3(0,.033f,.035f),new Vector3(.11f,.008f,.025f),dark);
            var knob = Box(root,"Slide",new Vector3(-.035f,.044f,.035f),new Vector3(.025f,.018f,.029f),mint);
            var control = root.gameObject.AddComponent<BridgeSwitch>();
            control.loop = loop;
            control.bridge = bridge;
            control.knob = knob;
            control.display = Text(root,"Mode",new Vector3(0,.034f,-.043f),new Vector2(.146f,.05f),.083f,"NETWORK   LOCAL",paper, new Vector3(90,0,0));
            Wire(root.gameObject,new Vector3(0,.044f,.035f),new Vector3(.12f,.045f,.055f));
            var source = ObjectNames.Find(loop.overlays.transform, "Garden View");
            if (loop.gardenView) Object.DestroyImmediate(loop.gardenView);
            if (source)
            {
                loop.gardenView = Object.Instantiate(source.gameObject,loop.overlays.transform);
                loop.gardenView.name = "Horizon";
                loop.gardenView.AddComponent<GardenBait>();
                loop.gardenView.SetActive(false);
            }
        }

        static void Screens()
        {
            if (!loop.gloveModel) return;
            var root = Reset(loop.gloveModel,"Records");
            root.localPosition = new Vector3(0,.043f,-.30f);
            root.localRotation = Quaternion.Euler(90,0,0);
            Box(root,"Case",Vector3.zero,new Vector3(.19f,.13f,.014f),metal);
            Box(root,"Strap",new Vector3(0,0,.028f),new Vector3(.205f,.04f,.065f),dark);
            Box(root,"Screen",new Vector3(0,0,-.009f),new Vector3(.176f,.116f,.004f),dark);
            var evidence = root.gameObject.AddComponent<Evidence>();
            evidence.loop = loop;
            evidence.title = Text(root,"Title",new Vector3(0,.046f,-.013f),new Vector2(.167f,.018f),.07f,"1/3  ARM PATH",paper);
            var slots = new GameObject[3];
            for (int i=0;i<3;i++) slots[i]=New(root,"Record").gameObject;
            Text(slots[0].transform,"Path",new Vector3(0,.009f,-.013f),new Vector2(.16f,.028f),.115f,"X   >   +   >   X",mint);
            for(int i=0;i<3;i++)
            {
                float x=-.058f+i*.058f;
                Box(slots[0].transform,"Arm",new Vector3(x,-.033f,-.014f),new Vector3(.042f,.009f,.002f),paper).localRotation=Quaternion.Euler(0,0,i==1?-30:25);
                Box(slots[0].transform,"Joint",new Vector3(x-.019f,-.021f,-.014f),new Vector3(.009f,.024f,.002f),amber);
            }
            for(int i=0;i<2;i++) Box(slots[1].transform,"Frame",new Vector3(i==0?-.036f:.036f,-.009f,-.014f),new Vector3(.007f,.06f,.002f),paper);
            Box(slots[1].transform,"Lintel",new Vector3(0,.022f,-.014f),new Vector3(.08f,.008f,.002f),paper);
            Box(slots[1].transform,"Socket",new Vector3(-.052f,-.011f,-.014f),new Vector3(.014f,.014f,.002f),amber);
            Text(slots[1].transform,"Site",new Vector3(0,-.049f,-.014f),new Vector2(.166f,.018f),.065f,"CONCOURSE / CONTACT",paper);
            Text(slots[2].transform,"Date",new Vector3(0,-.049f,-.014f),new Vector2(.166f,.018f),.058f,"07 OCT 2056 / RAW",paper);
            var survey = New(slots[2].transform,"Photo");
            survey.localPosition = new Vector3(0,-.006f,-.014f);
            Box(survey,"Wall",new Vector3(0,.004f,0),new Vector3(.135f,.055f,.002f),metal);
            Box(survey,"Opening",new Vector3(.026f,-.005f,-.002f),new Vector3(.033f,.039f,.002f),dark);
            for(int i=0;i<6;i++) Box(survey,"Plant",new Vector3(-.064f+i*.014f,-.022f,-.003f),new Vector3(.009f,.018f+i%2*.01f,.002f),green);
            evidence.slots=slots;
            Wire(root.gameObject,Vector3.zero,new Vector3(.19f,.13f,.034f));
            foreach(var button in loop.gloveModel.GetComponentsInChildren<Button>(true))
                if(button.action=="Evidence") { button.action=""; button.pressed=new UnityEngine.Events.UnityEvent(); UnityEventTools.AddPersistentListener(button.pressed,evidence.Next); }
            var station=loop.GetComponentInChildren<Assembly>(true);
            if(station && station.samples.Length>2 && station.samples[2])
            {
                var view=Reset(station.samples[2].transform,"Raw");
                Vector3 s=station.samples[2].transform.lossyScale;
                view.localScale=new Vector3(1/s.x,1/s.y,1/s.z);
                view.localPosition=new Vector3(0,.85f,0);
                var copy=Object.Instantiate(survey.gameObject,view,false);
                copy.transform.localPosition=new Vector3(0,0,-.11f);
                copy.transform.localRotation=Quaternion.Euler(55,0,0);
                copy.transform.localScale=Vector3.one*2.1f;
            }
        }

        static void Security()
        {
            var drones=loop.GetComponentsInChildren<Drone>(true);
            foreach(var drone in drones)
            {
                if(drone.practice || !drone.visual || ObjectNames.Matches(drone.visual.name, "Unit")) continue;
                var unit=New(drone.transform,"Unit");
                unit.position=drone.visual.position;
                unit.rotation=drone.visual.rotation;
                var source=drone.visual;
                foreach(var child in drone.transform.Cast<Transform>().ToArray())
                {
                    if(child==unit || child==drone.pointA || child==drone.pointB) continue;
                    if(child==source || child.GetComponent<Renderer>() || drone.status && child==drone.status.transform) child.SetParent(unit,true);
                }
                drone.visual=unit;
            }
            var yard=ObjectNames.Find(loop.transform, "Yard");
            if(yard && loop.checkpoint)
            {
                var cradle=Reset(yard,"Cradle");
                cradle.position=loop.checkpoint.position+new Vector3(-.85f,0,.7f);
                cradle.rotation=Quaternion.Euler(0,-50,0);
                Box(cradle,"Stand",new Vector3(0,.46f,0),new Vector3(.32f,.92f,.28f),metal);
                Box(cradle,"Deck",new Vector3(0,.94f,0),new Vector3(.58f,.06f,.4f),dark);
                var sensor=New(cradle,"Sensor");
                sensor.localPosition=new Vector3(0,1.15f,0);
                var sample=sensor.gameObject.AddComponent<Drone>();
                sample.loop=loop; sample.practice=true;
                sample.visual=New(sensor,"Unit");
                Box(sample.visual,"Case",Vector3.zero,new Vector3(.26f,.17f,.18f),metal);
                sample.lamp=Box(sample.visual,"Lens",new Vector3(0,0,-.095f),new Vector3(.11f,.065f,.012f),mint).GetComponent<Renderer>();
                sample.status=Text(cradle,"Status",new Vector3(0,1.48f,-.02f),new Vector2(.62f,.18f),.2f,"SERVICE TEST",paper);
                Text(cradle,"Instructions",new Vector3(0,.8f,-.155f),new Vector2(.53f,.2f),.16f,"PALM TO SENSOR\nLOCK / PULSE\n5s RECOVERY",paper);
                var note=New(cradle,"Terminal");
                note.localPosition=new Vector3(-.58f,1.24f,0);
                Box(note,"Case",Vector3.zero,new Vector3(.68f,.52f,.06f),metal);
                Box(note,"Screen",new Vector3(0,0,-.033f),new Vector3(.63f,.47f,.012f),dark);
                Text(note,"Signal",new Vector3(0,0,-.043f),new Vector2(.59f,.43f),.16f,"Your overlay is off.\nTheir cameras aren't.\n\nI can show you the service route.\nI can't see every part of it.",amber);
                var sign=New(cradle,"Route");
                sign.localPosition=new Vector3(0,.5f,-.15f);
                Text(sign,"Map",Vector3.zero,new Vector2(.48f,.22f),.125f,"ENTRY > COVER > GATE\nLOCAL / BYPASS / WHEEL",paper);
            }
            var gate=loop.GetComponentInChildren<Gate>(true);
            if(gate)
            {
                var fittings=Reset(gate.transform,"Linkage");
                var growth=New(fittings,"Growth");
                growth.position=gate.transform.position+new Vector3(.62f,0,1.38f);
                for(int i=0;i<6;i++)
                {
                    var leaf=Box(growth,"Leaf",new Vector3(i*.08f,.10f+i%3*.04f,0),new Vector3(.12f,.023f,.15f),green);
                    leaf.localRotation=Quaternion.Euler(10+i*7,i*39,18);
                    Box(growth,"Stem",new Vector3(i*.08f,.052f,0),new Vector3(.012f,.10f,.012f),green);
                }
                if(gate.selector)
                {
                    var link=New(fittings,"Actuator");
                    link.position=gate.selector.position+new Vector3(0,.35f,.08f);
                    Box(link,"Rod",Vector3.zero,new Vector3(.065f,.35f,.06f),paper);
                    gate.actuator=link;
                }
                if(gate.lever)
                {
                    var dial=New(fittings,"Gauge");
                    dial.position=gate.lever.position+new Vector3(0,.52f,.03f);
                    Box(dial,"Case",Vector3.zero,new Vector3(.32f,.24f,.06f),metal);
                    Box(dial,"Face",new Vector3(0,0,-.035f),new Vector3(.29f,.2f,.01f),paper);
                    Box(dial,"Safe",new Vector3(.092f,.055f,-.043f),new Vector3(.07f,.05f,.005f),green);
                    var needle=New(dial,"Needle"); needle.localPosition=new Vector3(0,-.08f,-.048f);
                    Box(needle,"Pointer",new Vector3(0,.075f,0),new Vector3(.012f,.15f,.005f),dark);
                    gate.needle=needle;
                }
            }
        }

        static void Watch()
        {
            var wrist=loop.GetComponentInChildren<Wrist>(true);
            if(!wrist || !wrist.housing) return;
            wrist.screenOffset=new Vector3(0,.058f,.008f);
            var frame=Reset(wrist.housing,"Display");
            Box(frame,"Case",new Vector3(0,.048f,.008f),new Vector3(.15f,.012f,.073f),metal);
            Box(frame,"Glass",new Vector3(0,.055f,.008f),new Vector3(.14f,.002f,.061f),dark);
        }

        static void Morning()
        {
            var home=ObjectNames.Find(loop.transform, "Home");
            if(!home || !loop.overlays) return;
            var root=Reset(loop.overlays.transform,"Broadcast");
            root.SetPositionAndRotation(home.TransformPoint(new Vector3(-.25f,1.85f,2.16f)),home.rotation);
            var feed=root.gameObject.AddComponent<NewsFeed>(); feed.loop=loop;
            var screen=New(root,"Picture"); feed.screen=screen.gameObject;
            Box(screen,"Glass",Vector3.zero,new Vector3(1.32f,.75f,.03f),dark);
            feed.headline=Text(screen,"Headline",new Vector3(.2f,-.1f,-.07f),new Vector2(.78f,.34f),.22f,"12 NEW HORIZONS",paper);
            Text(screen,"Time",new Vector3(.29f,-.3f,-.07f),new Vector2(.74f,.08f),.09f,"SERVICE RECORD  06 OCT 2056",mint);
            var presenter=New(screen,"Presenter"); presenter.localPosition=new Vector3(-.43f,-.08f,-.085f); feed.presenter=presenter;
            Shape(presenter,"Jacket",PrimitiveType.Capsule,Vector3.zero,new Vector3(.18f,.14f,.05f),metal);
            Shape(presenter,"Head",PrimitiveType.Sphere,new Vector3(0,.205f,0),new Vector3(.115f,.145f,.055f),paper);
            Box(presenter,"Smile",new Vector3(0,.176f,-.029f),new Vector3(.042f,.008f,.002f),dark);
            for(int i=0;i<2;i++) Box(presenter,"Eye",new Vector3(i==0?-.025f:.025f,.218f,-.029f),new Vector3(.012f,.009f,.002f),dark);
            var arm=New(presenter,"Arm"); arm.localPosition=new Vector3(.1f,.04f,0); feed.arm=arm;
            Box(arm,"Sleeve",new Vector3(.035f,-.064f,0),new Vector3(.06f,.16f,.05f),metal).localRotation=Quaternion.Euler(0,0,25);
            screen.gameObject.SetActive(false);
        }

        static void Fracture()
        {
            var reveal=loop.GetComponent<Reveal>(); if(!reveal) reveal=loop.gameObject.AddComponent<Reveal>();
            reveal.loop=loop;
            string path=Folder+"Break.mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material) { material=new Material(Shader.Find("Garden/Break")); AssetDatabase.CreateAsset(material,path); }
            reveal.material=material;
            var sound=ObjectNames.Find(loop.transform, "Rupture");
            if(!sound) sound=New(loop.transform,"Rupture");
            var audio=sound.GetComponent<AudioSource>(); if(!audio) audio=sound.gameObject.AddComponent<AudioSource>();
            audio.clip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/RealityPlayground/StoryAudio/cue-unfilter.wav");
            audio.playOnAwake=false; audio.spatialBlend=0; audio.volume=.28f; reveal.rupture=audio;
        }

        static void Wrapper()
        {
            var food=loop.GetComponentsInChildren<Carry>(true).FirstOrDefault(x=>x.meal && !x.dinner);
            var wrapper=loop.GetComponentsInChildren<Peel>(true).FirstOrDefault(x=>x.kind==Peel.Kind.Wrapper);
            if(food && wrapper && !wrapper.transform.IsChildOf(food.transform)) wrapper.transform.SetParent(food.transform,true);
        }

        static void Questions()
        {
            var grip=loop.GetComponentsInChildren<Grip>(true).FirstOrDefault(x=>x.action=="AskAngel");
            if(grip)
            {
                foreach(var label in grip.GetComponentsInChildren<TMP_Text>(true)) label.text="ASK";
                var display=Reset(grip.transform,"Questions");
                Text(display,"Prompt",new Vector3(0,.28f,0),new Vector2(1.2f,.2f),.22f,"Who are you?",paper);
                var question=display.gameObject.AddComponent<Question>(); question.loop=loop; question.label=display.GetComponentInChildren<TMP_Text>();
            }
        }

        static void Survey()
        {
            var gate=loop.GetComponentInChildren<Gate>(true);
            if(!gate || !loop.outside) return;
            string path=Folder+"Survey.png";
            var cameraObject=new GameObject("Survey");
            var camera=cameraObject.AddComponent<Camera>();
            var texture=new RenderTexture(640,360,24,RenderTextureFormat.ARGB32);
            var pixels=new Texture2D(640,360,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            bool fog=RenderSettings.fog;
            bool presentation=loop.overlays && loop.overlays.activeSelf;
            Vector3 door=gate.door ? gate.door.position : Vector3.zero;
            var hidden=loop.GetComponentsInChildren<PadGlow>(true).SelectMany(x=>x.parts ?? Array.Empty<Renderer>()).Where(x=>x && x.enabled).Distinct().ToArray();
            try
            {
                if(loop.overlays) loop.overlays.SetActive(false);
                if(gate.door) gate.door.position+=Vector3.up*3.5f;
                foreach(var renderer in hidden) renderer.enabled=false;
                camera.transform.position=gate.transform.position+new Vector3(2.2f,1.55f,-.2f);
                camera.transform.LookAt(gate.transform.position+new Vector3(0,1.15f,.35f));
                camera.fieldOfView=74;
                camera.aspect=640f/360;
                camera.nearClipPlane=.08f; camera.farClipPlane=100;
                camera.cullingMask=~((1<<8)|(1<<9)|(1<<11));
                camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=new Color(.28f,.34f,.37f);
                camera.targetTexture=texture;
                RenderSettings.fog=false;
                camera.Render();
                RenderTexture.active=texture;
                pixels.ReadPixels(new Rect(0,0,640,360),0,0); pixels.Apply();
                File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally
            {
                if(loop.overlays) loop.overlays.SetActive(presentation);
                if(gate.door) gate.door.position=door;
                foreach(var renderer in hidden) if(renderer) renderer.enabled=true;
                RenderSettings.fog=fog; RenderTexture.active=previous;
                camera.targetTexture=null; texture.Release();
                Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(texture); Object.DestroyImmediate(pixels);
            }
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.mipmapEnabled=true; importer.textureCompression=TextureImporterCompression.Compressed; importer.maxTextureSize=1024; importer.SaveAndReimport();
            var material=Mat("Survey",Color.white); material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            foreach(var source in loop.GetComponentsInChildren<Transform>(true).Where(x=>ObjectNames.Matches(x.name, "Photo")).ToArray())
            {
                foreach(Transform child in source.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                var quad=Shape(source,"Image",PrimitiveType.Quad,Vector3.zero,new Vector3(.15f,.084f,1),material);
            }
        }

        static void Save()
        {
            foreach(var path in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Garden/Prefabs"}).Select(AssetDatabase.GUIDToAssetPath))
            {
                string name=Path.GetFileNameWithoutExtension(path);
                if(name=="Garden" || name=="Rooster") continue;
                var module=loop.GetComponentsInChildren<Transform>(true).FirstOrDefault(x=>ObjectNames.Matches(x.name, name) && x.GetComponent<Link>());
                if(module) PrefabUtility.SaveAsPrefabAsset(module.gameObject,path);
            }
            PrefabUtility.SaveAsPrefabAsset(loop.gameObject,"Assets/Garden/Prefabs/Garden.prefab");
            EditorSceneManager.MarkSceneDirty(loop.gameObject.scene);
            EditorSceneManager.SaveScene(loop.gameObject.scene);
            AssetDatabase.SaveAssets();
        }

        static bool Calls(Grip grip,string action) { for(int i=0;i<grip.used.GetPersistentEventCount();i++) if(grip.used.GetPersistentMethodName(i)==action) return true; return false; }
        static Transform New(Transform parent,string name) { var obj=new GameObject(ObjectNames.Short(name)); obj.transform.SetParent(parent,false); return obj.transform; }
        static Transform Reset(Transform parent,string name) { var old=ObjectNames.Find(parent, name); if(old) Object.DestroyImmediate(old.gameObject); return New(parent,name); }
        static Material Mat(string name,Color color)
        {
            string path=Folder+name+".mat"; var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m) { m=new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m,path); }
            m.SetColor("_BaseColor",color); m.enableInstancing=true; return m;
        }
        static Transform Shape(Transform parent,string name,PrimitiveType type,Vector3 point,Vector3 size,Material material)
        {
            var obj=GameObject.CreatePrimitive(type); obj.name=ObjectNames.Short(name); obj.transform.SetParent(parent,false); obj.transform.localPosition=point; obj.transform.localScale=size;
            Object.DestroyImmediate(obj.GetComponent<Collider>()); var renderer=obj.GetComponent<Renderer>(); renderer.sharedMaterial=material; renderer.shadowCastingMode=ShadowCastingMode.Off; return obj.transform;
        }
        static Transform Box(Transform parent,string name,Vector3 point,Vector3 size,Material material) => Shape(parent,name,PrimitiveType.Cube,point,size,material);
        static TMP_Text Text(Transform parent,string name,Vector3 point,Vector2 size,float fontSize,string value,Material material,Vector3 angles=default)
        {
            var text=New(parent,name).gameObject.AddComponent<TextMeshPro>(); text.font=font; text.text=value; text.richText=false; text.fontSize=fontSize;
            text.alignment=TextAlignmentOptions.Center; text.color=material.GetColor("_BaseColor"); text.rectTransform.sizeDelta=size;
            text.transform.localPosition=point; text.transform.localRotation=Quaternion.Euler(angles); text.textWrappingMode=TextWrappingModes.Normal; return text;
        }
        static void Wire(GameObject obj,Vector3 center,Vector3 size)
        {
            obj.layer=8; var collider=obj.AddComponent<BoxCollider>(); collider.isTrigger=true; collider.center=center; collider.size=size;
            var interactable=obj.AddComponent<XRSimpleInteractable>(); interactable.colliders.Add(collider); obj.AddComponent<XRExperimentBridge>();
        }
    }
}
