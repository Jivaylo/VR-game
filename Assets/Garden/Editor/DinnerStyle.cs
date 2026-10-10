using System;
using System.IO;
using RealityPlayground;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Object = UnityEngine.Object;

namespace Garden.Editor
{
    public static class DinnerStyle
    {
        const string Folder = "Assets/Garden/Art/Dinner/";
        static Material metal, dark, paper, green, gold, teal;
        static TMP_FontAsset font;

        public static void Apply(GameObject root)
        {
            if (!root || EditorApplication.isPlaying) throw new InvalidOperationException("Open Garden in edit mode.");
            var loop = root.GetComponent<Loop>();
            var home = ObjectNames.Find(root.transform, "Home");
            var delivery = root.GetComponentInChildren<Delivery>(true);
            if (!loop || !home || !delivery || !delivery.package) throw new InvalidOperationException("Dinner receiver is missing.");
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            metal = Mat("Case", new Color(.23f,.28f,.29f), false);
            dark = Mat("Screen", new Color(.035f,.052f,.055f), true);
            paper = Mat("Label", new Color(.8f,.81f,.70f), true);
            green = Mat("Greens", new Color(.32f,.65f,.37f), true);
            gold = Mat("Crust", new Color(.77f,.51f,.21f), true);
            teal = Mat("Rim", new Color(.25f,.67f,.60f), true);
            var mount = Reset(home, "Menu");
            mount.localPosition = new Vector3(.89f,1.08f,.74f);
            mount.localRotation = Quaternion.Euler(65,0,0);
            var inspect = mount.gameObject.AddComponent<DinnerInspect>();
            inspect.loop = loop;
            inspect.delivery = delivery;
            inspect.menu = mount;
            Part(mount,"Case",PrimitiveType.Cube,new Vector3(0,0,.018f),new Vector3(.45f,.26f,.035f),metal);
            Part(mount,"Screen",PrimitiveType.Cube,new Vector3(0,0,-.003f),new Vector3(.43f,.24f,.009f),dark);
            inspect.title = Text(mount,"Title",new Vector3(0,.092f,-.011f),new Vector2(.4f,.04f),.10f,"GARDEN BOWL",paper);
            inspect.description = Text(mount,"Label",new Vector3(.084f,.027f,-.012f),new Vector2(.20f,.07f),.079f,"EVENING MENU\nTurn to browse",paper).gameObject;
            var raw = New(mount, "Production");
            Text(raw,"Batch",new Vector3(0,.005f,-.027f),new Vector2(.41f,.15f),.10f,"STANDARD BASE\nPRESENTATION VARIABLE\nBatch 07",paper);
            inspect.raw = raw.gameObject;
            var knob = New(mount,"Dial");
            knob.localPosition = new Vector3(.157f,-.078f,-.025f);
            var face = Part(knob,"Knob",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.07f,.013f,.07f),metal);
            face.localRotation = Quaternion.Euler(90,0,0);
            Part(face,"Notch",PrimitiveType.Cube,new Vector3(0,.6f,.27f),new Vector3(.10f,.16f,.34f),teal);
            var grip = knob.gameObject.AddComponent<Grip>();
            grip.mode = Grip.Mode.Turn;
            grip.axis = Vector3.back;
            grip.angle = 35;
            grip.model = face;
            UnityEventTools.AddPersistentListener(grip.used,inspect.Next);
            Wire(knob.gameObject,new Vector3(.095f,.095f,.07f));
            inspect.previews = new GameObject[3];
            inspect.meals = new GameObject[3];
            var dish = Reset(delivery.package.transform,"Dish");
            Vector3 size = delivery.package.transform.lossyScale;
            dish.localScale = new Vector3(1 / size.x,1 / size.y,1 / size.z);
            dish.localPosition = new Vector3(0,.54f,0);
            for (int i=0;i<3;i++)
            {
                var preview = New(mount,"Plate");
                preview.localPosition = new Vector3(-.10f,-.031f,-.054f);
                preview.localRotation = Quaternion.Euler(-65,0,0);
                Food(preview,i);
                inspect.previews[i] = preview.gameObject;
                var meal = Object.Instantiate(preview.gameObject,dish);
                meal.name = "Serving";
                meal.transform.localPosition = Vector3.zero;
                meal.transform.localRotation = Quaternion.identity;
                meal.transform.localScale = Vector3.one * 1.5f;
                inspect.meals[i] = meal;
            }
            var stand = New(home,"Menu Stand");
            stand.localPosition = new Vector3(.89f,.955f,.74f);
            Part(stand,"Top",PrimitiveType.Cube,Vector3.zero,new Vector3(.45f,.045f,.29f),metal);
            Part(stand,"Brace",PrimitiveType.Cube,new Vector3(0,.051f,.02f),new Vector3(.11f,.07f,.06f),metal);
            Part(stand,"Leg",PrimitiveType.Cube,new Vector3(0,-.46f,0),new Vector3(.05f,.9f,.05f),metal);
            Part(stand,"Foot",PrimitiveType.Cube,new Vector3(0,-.93f,0),new Vector3(.25f,.05f,.20f),metal);
            inspect.Refresh();
            EditorUtility.SetDirty(inspect);
            EditorUtility.SetDirty(loop);
        }

        static void Food(Transform root,int kind)
        {
            Part(root,"Plate",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.14f,.009f,.14f),teal);
            if (kind == 2)
            {
                for (int i=0;i<2;i++) Part(root,"Toast",PrimitiveType.Cube,new Vector3(i==0?-.029f:.029f,.017f,0),new Vector3(.05f,.021f,.095f),gold);
            }
            else Part(root,"Meal",PrimitiveType.Cylinder,new Vector3(0,.011f,0),new Vector3(.118f,.012f,.118f),kind==0?paper:gold);
            for (int i=0;i<5;i++)
            {
                float angle = i*Mathf.PI*2/5;
                var leaf = Part(root,"Leaf",PrimitiveType.Sphere,new Vector3(Mathf.Cos(angle)*.037f,.032f,Mathf.Sin(angle)*.037f),new Vector3(.025f,.012f,.041f),green);
                leaf.localRotation = Quaternion.Euler(0,i*72,18);
            }
        }

        static Transform New(Transform parent,string name)
        {
            var value = new GameObject(ObjectNames.Short(name)).transform;
            value.SetParent(parent,false);
            return value;
        }

        static Transform Reset(Transform parent,string name)
        {
            var old = ObjectNames.Find(parent, name);
            if (old) Object.DestroyImmediate(old.gameObject);
            if (name == "Menu")
            {
                var stand = ObjectNames.Find(parent, "Menu Stand");
                if (stand) Object.DestroyImmediate(stand.gameObject);
            }
            return New(parent,name);
        }

        static Material Mat(string name,Color color,bool unlit)
        {
            string path = Folder+name+".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material,path); }
            material.SetColor("_BaseColor",color);
            material.enableInstancing = true;
            return material;
        }

        static Transform Part(Transform parent,string name,PrimitiveType type,Vector3 point,Vector3 size,Material material)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = ObjectNames.Short(name);
            obj.transform.SetParent(parent,false);
            obj.transform.localPosition = point;
            obj.transform.localScale = size;
            Object.DestroyImmediate(obj.GetComponent<Collider>());
            var renderer = obj.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return obj.transform;
        }

        static TMP_Text Text(Transform parent,string name,Vector3 point,Vector2 size,float height,string value,Material material)
        {
            var text = New(parent,name).gameObject.AddComponent<TextMeshPro>();
            text.font = font;
            text.text = value;
            text.fontSize = height;
            text.alignment = TextAlignmentOptions.Center;
            text.color = material.GetColor("_BaseColor");
            text.rectTransform.sizeDelta = size;
            text.transform.localPosition = point;
            return text;
        }

        static void Wire(GameObject root,Vector3 size)
        {
            root.layer = 8;
            var collider = root.AddComponent<BoxCollider>();
            collider.size = size;
            collider.isTrigger = true;
            var target = root.AddComponent<XRSimpleInteractable>();
            target.colliders.Add(collider);
            root.AddComponent<XRExperimentBridge>();
        }
    }
}
