using System;
using System.Linq;
using Garden;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Garden.Editor
{
    public static partial class GardenBuilder
    {
        static Material ink, paper, accent;

        [MenuItem("Garden/Improve Scene")]
        public static void Improve()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop play mode first.");
            loop = Object.FindFirstObjectByType<Loop>(FindObjectsInactive.Include);
            if (!loop || loop.gameObject.scene.path != "Assets/Scenes/Garden.unity") throw new InvalidOperationException("Open Garden first.");
            world = loop.transform;
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            concrete = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Concrete.mat");
            road = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Street.mat");
            metal = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Steel.mat");
            blue = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Route.mat");
            red = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Factory.mat");
            green = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Leaf.mat");
            glow = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Light.mat");
            ink = Mat("Ink", new Color(.045f,.067f,.075f), true);
            paper = Mat("Ivory", new Color(.68f,.7f,.65f));
            accent = Mat("Mint", new Color(.24f,.58f,.55f), true);
            ImproveControls();
            ImprovePanels();
            ImproveProps();
            ImproveDisplays();
            foreach (var name in new[]{"Home","Breakfast","Factory"})
            {
                var old = ObjectNames.Find(world, name + "/Fittings"); if (old) Object.DestroyImmediate(old.gameObject);
            }
            ImproveRooms();
            var fittings = ObjectNames.Find(world, "Fittings");
            if (fittings)
            {
                foreach (Transform item in fittings.Cast<Transform>().ToArray())
                {
                    var owner = ObjectNames.Find(world, item.name);
                    if (owner) { item.SetParent(owner,true); item.name = "Fittings"; }
                }
                if (fittings.childCount == 0) Object.DestroyImmediate(fittings.gameObject);
            }
            GardenStreets.Apply(world.gameObject);
            StyleGrips();
            StylePads();
            GardenFlow.Apply(world.gameObject);
            GardenPolish.Apply(world.gameObject);
            ModelStyle.Apply(world.gameObject);
            DistrictStyle.Apply(world.gameObject);
            GuideBuilder.Apply(world.gameObject);
            CivicSpeech.Apply(world.gameObject);
            foreach (var text in world.GetComponentsInChildren<TMP_Text>(true))
            {
                text.richText = false;
                text.enableAutoSizing = false;
                text.overflowMode = TextOverflowModes.Truncate;
            }
            Persist(world.gameObject);
            foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[]{Root + "Prefabs"}).Select(AssetDatabase.GUIDToAssetPath))
            {
                var name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (name == "Garden" || name == "Rooster") continue;
                var module = world.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => ObjectNames.Matches(x.name, name) && x.GetComponent<Link>());
                if (module) PrefabUtility.SaveAsPrefabAsset(module.gameObject, path);
            }
            PrefabUtility.SaveAsPrefabAsset(world.gameObject, Root + "Prefabs/Garden.prefab");
            EditorSceneManager.MarkSceneDirty(world.gameObject.scene);
            EditorSceneManager.SaveScene(world.gameObject.scene);
            AssetDatabase.SaveAssets();
        }

        static string ActionOf(Button button)
        {
            if (!string.IsNullOrEmpty(button.action)) return button.action;
            return button.pressed.GetPersistentEventCount() > 0 ? button.pressed.GetPersistentMethodName(0) : "";
        }

        static void ImproveControls()
        {
            foreach (var button in world.GetComponentsInChildren<Button>(true))
            {
                string action = ActionOf(button);
                if (new[]{"Swat","Meal","Peel","Cup","Mara","Noor","Calibrate","Trace","Take","Install","Tear","Isolate","Equalize","Release"}.Contains(action) || ObjectNames.Matches(button.name, "Lift ad"))
                {
                    Object.DestroyImmediate(button.gameObject);
                    continue;
                }
                if (action == "BoardSouth" || action == "BoardWork" || action == "Leave")
                {
                    var train = world.GetComponentInChildren<Train>(true);
                    var parent = button.transform.parent;
                    Vector3 at = action == "Leave" ? train.cabin.parent.position + new Vector3(0,0,-3.15f) : action == "BoardSouth" ? train.south.position : train.work.position;
                    var pad = Pad(action == "Leave" ? "Exit" : "Board", parent, at);
                    pad.arrived = button.pressed;
                    if (action == "Leave") Cube("Step", parent, at + Vector3.down * .1f, new Vector3(1.6f,.2f,1.4f), road);
                    Object.DestroyImmediate(button.gameObject);
                    continue;
                }
                if (action == "OpenDoor") { Object.DestroyImmediate(button.gameObject); continue; }
                if (action == "Accept" || action == "Reject" || action == "Fire" || action == "Evidence" || action == "Replay")
                {
                    StyleButton(button, action == "Accept" ? "ACCEPT" : action == "Reject" ? "REJECT" : action == "Fire" ? "PULSE" : action.ToUpperInvariant());
                    if (action == "Fire" || action == "Evidence") button.transform.localRotation = Quaternion.Euler(90,0,0);
                    continue;
                }
                var mode = Grip.Mode.Pull;
                string label = "PULL";
                if (new[]{"Wash","Ride","ViewSample","Review","Water","KeepNetwork"}.Contains(action)) { mode = Grip.Mode.Turn; label = "TURN"; }
                if (new[]{"Sleep","Rest","Unlock","EnterAngel"}.Contains(action)) { mode = Grip.Mode.Hold; label = "HOLD"; }
                if (new[]{"News","AskAngel","RequestDeparture"}.Contains(action)) { mode = Grip.Mode.Touch; label = "TOUCH"; }
                if (action == "KeepNetwork") label = "NETWORK";
                if (action == "ConfirmLocal" || action == "Disconnect" || action == "OfferLocal") label = "LOCAL";
                if (action == "Glove") label = "GRIP";
                var grip = Convert(button, mode, label);
                if (action == "Sleep") { grip.action = "RestNight"; grip.holdTime = 2; }
                if (action == "Rest") grip.holdTime = 1.5f;
                if (action == "Unlock") { grip.leftOnly = true; grip.holdTime = .5f; }
                if (action == "CloseDoor")
                {
                    var train = world.GetComponentInChildren<Train>(true);
                    grip.used = new UnityEngine.Events.UnityEvent();
                    UnityEventTools.AddPersistentListener(grip.used, train.ToggleDoor);
                    grip.transform.position = train.cabin.parent.position + new Vector3(.6f,1.05f,-2.3f);
                    grip.transform.rotation = Quaternion.Euler(0,180,0);
                }
                if (action == "Ride") grip.transform.position = grip.transform.parent.position + new Vector3(.45f,1.05f,1.3f);
                if (action == "Glove")
                {
                    var assembly = world.GetComponentInChildren<Assembly>(true);
                    grip.transform.SetParent(assembly.gloveStand, true);
                    grip.transform.position = assembly.gloveStand.position + new Vector3(0,0,-.06f);
                }
                if (action == "Toggle" && ObjectNames.Matches(grip.transform.parent.name, "Home")) grip.transform.localPosition += Vector3.left * .22f;
            }
        }

        static void StyleButton(Button button, string label)
        {
            foreach (var t in button.GetComponentsInChildren<TMP_Text>(true)) Object.DestroyImmediate(t.gameObject);
            button.visual.localScale = new Vector3(.2f,.15f,.045f);
            button.visual.GetComponent<Renderer>().sharedMaterial = label == "REJECT" ? red : accent;
            var box = button.GetComponent<BoxCollider>();
            if (box) { box.size = new Vector3(.23f,.18f,.07f); box.center = Vector3.zero; }
            var text = Text(label, button.transform, button.transform.position, .03f);
            text.transform.localPosition = new Vector3(0,0,-.025f);
            text.rectTransform.sizeDelta = new Vector2(.19f,.08f);
            text.color = new Color(.94f,.95f,.88f);
            text.fontStyle = FontStyles.Bold;
        }

        static Grip Convert(Button button, Grip.Mode mode, string label)
        {
            var root = button.transform;
            var calls = button.pressed;
            string action = button.action;
            foreach (Transform child in root.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            Object.DestroyImmediate(button);
            var grip = root.gameObject.AddComponent<Grip>();
            grip.mode = mode; grip.used = calls; grip.action = action;
            grip.axis = Vector3.back;
            var back = Cube("Mount", root, root.position, new Vector3(.22f,.28f,.03f), ink, false);
            back.transform.localPosition = new Vector3(0,0,.015f); back.transform.localRotation = Quaternion.identity;
            var shape = mode == Grip.Mode.Turn
                ? Cylinder("Grip", root, root.position, new Vector3(.14f,.025f,.14f), accent, false)
                : Cube("Grip", root, root.position, mode == Grip.Mode.Pull ? new Vector3(.035f,.17f,.055f) : new Vector3(.15f,.15f,.025f), accent, false);
            shape.transform.localPosition = new Vector3(0,.025f,-.025f);
            shape.transform.localRotation = mode == Grip.Mode.Turn ? Quaternion.Euler(90,0,0) : Quaternion.identity;
            if (mode == Grip.Mode.Turn)
            {
                var pointer = Cube("Mark", shape.transform, shape.transform.position, new Vector3(.08f,.02f,.018f), paper, false);
                pointer.transform.SetParent(root, true);
                pointer.transform.localPosition = new Vector3(.035f,.025f,-.054f);
                pointer.transform.localRotation = Quaternion.identity;
                pointer.transform.SetParent(shape.transform, true);
            }
            grip.model = shape.transform;
            var text = Text(label, root, root.position, .027f);
            text.transform.localPosition = new Vector3(0,-.103f,-.007f);
            text.rectTransform.sizeDelta = new Vector2(.20f,.05f);
            var box = root.GetComponent<BoxCollider>();
            if (!box) box = root.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0,.025f,-.025f); box.size = new Vector3(.19f,.2f,.09f); box.isTrigger = true;
            Wire(root.gameObject, box);
            return grip;
        }

        static void ImprovePanels()
        {
            var board = loop.talk.board;
            foreach (Transform child in board.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            board.localScale = Vector3.one;
            var back = Cube("Back", board, board.position, new Vector3(1,.42f,.018f), ink, false);
            back.transform.localPosition = new Vector3(0,0,.02f); back.transform.localRotation = Quaternion.identity;
            var line = Cube("Edge", board, board.position, new Vector3(.012f,.36f,.006f), accent, false);
            line.transform.localPosition = new Vector3(-.48f,0,0); line.transform.localRotation = Quaternion.identity;
            loop.talk.speakerLabel = Text("", board, board.position, .037f);
            loop.talk.speakerLabel.transform.localPosition = new Vector3(.01f,.162f,0);
            loop.talk.speakerLabel.rectTransform.sizeDelta = new Vector2(.9f,.05f);
            loop.talk.speakerLabel.alignment = TextAlignmentOptions.Left;
            loop.talk.speakerLabel.color = new Color(.44f,.76f,.71f);
            loop.talk.caption = Text("", board, board.position, .060f);
            loop.talk.caption.transform.localPosition = new Vector3(.01f,-.028f,0);
            loop.talk.caption.rectTransform.sizeDelta = new Vector2(.9f,.30f);
            loop.talk.caption.alignment = TextAlignmentOptions.TopLeft;
            loop.talk.hold = 3;
            var panel = board.GetComponent<Panel>(); if (!panel) panel = board.gameObject.AddComponent<Panel>();
            panel.size = new Vector2(1,.42f); panel.center = Vector3.zero; panel.height = .43f; panel.distance = 1.15f; panel.fitChildren = false;
            StyleChoice(loop.localChoice, false);
            StyleChoice(loop.gardenChoice, true);
        }

        static void StyleChoice(GameObject choice, bool garden)
        {
            if (!choice) return;
            var root = choice.transform;
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true).Where(x => !x.GetComponentInParent<Grip>()).ToArray()) Object.DestroyImmediate(text.gameObject);
            var oldBack = ObjectNames.Find(root, "Back"); if (oldBack) Object.DestroyImmediate(oldBack.gameObject);
            var back = Cube("Back", root, root.position, new Vector3(.76f,.65f,.025f), ink, false);
            back.transform.localPosition = new Vector3(0,.05f,.075f); back.transform.localRotation = Quaternion.identity;
            var header = Text(garden ? "YOUR GARDEN" : "CONNECTION", root, root.position,.052f);
            header.transform.localPosition = new Vector3(0,.30f,0); header.rectTransform.sizeDelta = new Vector2(.7f,.07f);
            var detail = Text(garden ? "Keep the view, or go outside." : "Local ends civic guidance.", root, root.position,.038f);
            detail.transform.localPosition = new Vector3(0,.19f,0); detail.rectTransform.sizeDelta = new Vector2(.69f,.10f);
            foreach (var grip in root.GetComponentsInChildren<Grip>(true))
            {
                bool network = grip.action == "KeepNetwork";
                grip.transform.localPosition = new Vector3(network ? -.22f : .22f,-.03f,0);
                grip.transform.localRotation = Quaternion.identity;
            }
            var view = ObjectNames.Find(root, "Garden View");
            if (view) { view.localScale = Vector3.one * .16f; view.localPosition = new Vector3(0,.035f,-.02f); }
            var panel = choice.GetComponent<Panel>(); if (!panel) panel = choice.AddComponent<Panel>();
            panel.size = new Vector2(.8f,.68f); panel.center = new Vector3(0,.05f,.015f); panel.depth = .19f; panel.height = -.3f; panel.distance = .7f; panel.fitChildren = false;
        }

        static void ImproveProps()
        {
            var home = ObjectNames.Find(world, "Home");
            var breakfast = ObjectNames.Find(world, "Breakfast");
            var factory = ObjectNames.Find(world, "Factory");
            if (home)
            {
                var sleep = home.GetComponentsInChildren<Grip>(true).FirstOrDefault(x => x.action == "RestNight");
                if (sleep)
                {
                    sleep.transform.position = home.position + new Vector3(-1.43f,.81f,.8f);
                    sleep.transform.rotation = Quaternion.Euler(90,0,0);
                }
                AddPad(home, "Bedside", home.position + new Vector3(-.8f,0,.45f));
                var wash = home.GetComponentsInChildren<Grip>(true).FirstOrDefault(x => x.action == "Wash");
                if (wash && !wash.GetComponent<Tap>())
                {
                    var tap = wash.gameObject.AddComponent<Tap>(); tap.loop = loop;
                    Cube("Spout", home, home.position + new Vector3(2.52f,1.16f,-1.45f), new Vector3(.23f,.045f,.05f), metal, false);
                    Cube("Pipe", home, home.position + new Vector3(2.62f,1.04f,-1.45f), new Vector3(.04f,.26f,.04f), metal, false);
                    tap.stream = Cube("Water", home, home.position + new Vector3(2.41f,1.01f,-1.45f), new Vector3(.014f,.26f,.014f), accent, false);
                }
                var news = home.GetComponentsInChildren<Grip>(true).FirstOrDefault(x => x.action == "News");
                if (news) news.transform.localPosition = new Vector3(.4f,1.0f,1.17f);
                var dinner = home.GetComponentsInChildren<Grip>(true).FirstOrDefault(x => x.action == "Dinner");
                if (dinner) dinner.transform.localPosition = new Vector3(1.48f,.98f,.62f);
                var local = home.GetComponentsInChildren<Grip>(true).FirstOrDefault(x => x.action == "OfferLocal");
                if (local) { local.transform.localPosition = new Vector3(2.36f,.85f,-.85f); local.transform.localRotation = Quaternion.Euler(0,90,0); }
                AddPad(home, "Desk", home.position + new Vector3(.3f,0,.55f));
                AddPad(home, "Table Side", home.position + new Vector3(.87f,0,.35f));
            }
            if (breakfast)
            {
                AddPad(breakfast,"Cup Side",breakfast.position + new Vector3(.95f,0,-.9f));
                ObjectNames.Find(breakfast, "Cup Side").position = breakfast.position + new Vector3(.95f,0,-.9f);
                var meal = ObjectNames.Find(breakfast, "Meal");
                if (meal && !meal.GetComponent<Carry>())
                {
                    meal.name = "Food";
                    var root = Empty("Meal", breakfast, meal.position + Vector3.back * .20f);
                    meal.SetParent(root, true); meal.position = root.position;
                    var carry = root.gameObject.AddComponent<Carry>(); carry.meal = true; carry.model = meal;
                    var box = root.gameObject.AddComponent<BoxCollider>(); box.size = new Vector3(.28f,.1f,.2f);
                    Wire(root.gameObject, box);
                }
            }
            var wrapper = ObjectNames.Find(world, "Wrapper");
            if (wrapper)
            {
                var hint = wrapper.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(x => x.text == "Grip sleeve. Pull down." || x.text == "PEEL");
                var peel = wrapper.GetComponent<Peel>();
                if (hint && peel && peel.sheet)
                {
                    hint.transform.SetParent(wrapper,true);
                    hint.transform.SetPositionAndRotation(wrapper.position + new Vector3(0,.045f,-.145f),Quaternion.Euler(70,0,0));
                    hint.transform.SetParent(peel.sheet,true);
                    hint.text="PEEL"; hint.fontSize=.30f; hint.color=new Color(.12f,.2f,.22f); hint.rectTransform.sizeDelta=new Vector2(.25f,.06f);
                }
            }
            foreach (var name in new[]{"Mara","Noor"})
            {
                var npc = world.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => ObjectNames.Matches(x.name, name));
                if (!npc) continue;
                var meet = npc.GetComponent<Meet>(); if (!meet) meet = npc.gameObject.AddComponent<Meet>();
                meet.loop = loop; meet.action = name; meet.face = ObjectNames.Find(npc, "Head");
            }
            if (factory)
            {
                AddPad(factory, "Tool", factory.position + new Vector3(-2.5f,0,-.26f));
                AddPad(factory, "Review", factory.position + new Vector3(1.35f,0,0));
                AddPad(factory, "Inspect", factory.position + new Vector3(0,0,.35f));
            }
            var concourse = ObjectNames.Find(world, "Concourse");
            if (concourse)
            {
                AddPad(concourse,"Inner",concourse.position + new Vector3(4,0,-.7f));
                AddPad(concourse,"Outer",concourse.position + new Vector3(5.8f,0,-.7f));
            }
            var signal = ObjectNames.Find(world, "Signal");
            if (signal)
                foreach (var grip in signal.GetComponentsInChildren<Grip>(true))
                    if (grip.action == "AskAngel" || grip.action == "LeaveAngel") grip.transform.localPosition = new Vector3(grip.action == "AskAngel" ? -.4f : .4f,1.1f,-1.5f);
            var outside = ObjectNames.Find(world, "Outside");
            if (outside)
                foreach (var grip in outside.GetComponentsInChildren<Grip>(true))
                {
                    if (grip.action == "Water") { grip.transform.localPosition = new Vector3(4.25f,1.02f,1.42f); grip.transform.localRotation = Quaternion.Euler(0,180,0); }
                    if (grip.action == "Rest") { grip.transform.localPosition = new Vector3(7,.67f,-1.37f); grip.transform.localRotation = Quaternion.Euler(50,0,0); }
                }
            var train = world.GetComponentInChildren<Train>(true);
            if (train && train.cabin) AddPad(train.cabin.parent, "Doorway", train.cabin.parent.position + new Vector3(0,0,-1.7f));
            var gate = world.GetComponentInChildren<Gate>(true);
            if (gate)
            {
                AddPad(gate.transform,"Controls",gate.transform.position + new Vector3(-1.1f,0,.5f));
                AddPad(gate.transform,"Intercom",gate.transform.position + new Vector3(-.85f,0,-.95f));
            }
            var oldGuide = world.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(x => x.text.StartsWith("MOVE\n"));
            if (oldGuide)
            {
                oldGuide.text = "Aim at a blue pad to travel.\nGrip nearby handles and objects.\nTouch surfaces with your hand.";
                oldGuide.fontSize = .65f;
                oldGuide.rectTransform.sizeDelta = new Vector2(1.1f,.5f);
            }
        }

        static void AddPad(Transform parent, string name, Vector3 point)
        {
            if (ObjectNames.Find(parent, name)) return;
            Pad(name,parent,point);
        }

        static void ImproveDisplays()
        {
            foreach (var hatch in world.GetComponentsInChildren<Hatch>(true)) Frame(hatch.display,new Vector2(1.35f,.43f),.65f);
            foreach (var bridge in world.GetComponentsInChildren<Bridge>(true)) Frame(bridge.display,new Vector2(1.1f,.28f),.7f);
            foreach (var train in world.GetComponentsInChildren<Train>(true)) Frame(train.display,new Vector2(1.2f,.4f),.7f);
            foreach (var gate in world.GetComponentsInChildren<Gate>(true))
            {
                gate.display.transform.position=gate.transform.position+new Vector3(-1.1f,1.95f,1.18f);
                gate.gauge.transform.position=gate.transform.position+new Vector3(-1.1f,1.50f,1.18f);
                Frame(gate.display,new Vector2(1.3f,.43f),.65f);
                Frame(gate.gauge,new Vector2(.65f,.25f),.65f);
            }
            foreach (var text in world.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.text.StartsWith("SERVICE YARD\n") || text.text.StartsWith("MAINTENANCE\n"))
                {
                    text.text="MAINTENANCE\nLocal control required";
                    Frame(text,new Vector2(1.3f,.38f),.7f);
                }
                if (text.text.StartsWith("CONCOURSE HATCH\n"))
                {
                    text.text="SERVICE HATCH\nTrace with the glove";
                    Frame(text,new Vector2(1.45f,.3f),.65f);
                }
            }
        }

        static void Frame(TMP_Text text, Vector2 size, float fontSize)
        {
            if (!text) return;
            text.fontSize=fontSize; text.rectTransform.sizeDelta=size;
            text.alignment=TextAlignmentOptions.Center;
            text.margin=new Vector4(.04f,.025f,.04f,.025f);
            var old=ObjectNames.Find(text.transform, "Back"); if(old) Object.DestroyImmediate(old.gameObject);
            var back=Cube("Back",text.transform,text.transform.position,new Vector3(size.x+.05f,size.y+.04f,.018f),ink,false);
            back.transform.localPosition=new Vector3(0,0,.022f); back.transform.localRotation=Quaternion.identity;
        }

        static void StyleGrips()
        {
            foreach (var grip in world.GetComponentsInChildren<Grip>(true))
            {
                string action = !string.IsNullOrEmpty(grip.action) ? grip.action : grip.used.GetPersistentEventCount() > 0 ? grip.used.GetPersistentMethodName(0) : "";
                string label = action == "News" ? "NEWS" : action == "Dinner" ? "DELIVERY" : action == "Wash" ? "WATER" : action == "Review" ? "REVIEW" : action == "ViewSample" ? "SURVEY" : action == "Ride" ? "DEPART" : action == "RestNight" ? "SLEEP" : action == "Rest" ? "REST" : action == "Water" ? "WATER" : action == "AskAngel" ? "LISTEN" : action == "LeaveAngel" ? "RETURN" : action == "EnterAngel" ? "CONTACT" : action == "RequestDeparture" ? "INTERCOM" : action == "Unlock" ? "GLOVE" : "";
                if (!string.IsNullOrEmpty(label))
                    foreach (var text in grip.GetComponentsInChildren<TMP_Text>(true)) text.text = label;
                foreach (var text in grip.GetComponentsInChildren<TMP_Text>(true)) text.transform.localPosition = new Vector3(0,-.108f,-.063f);
                if (grip.mode == Grip.Mode.Pull && grip.model) grip.model.localScale = new Vector3(.035f,.14f,.055f);
                if (grip.mode == Grip.Mode.Turn && grip.model)
                {
                    var old = ObjectNames.Find(grip.model, "Mark"); if (old) Object.DestroyImmediate(old.gameObject);
                    var mark=Cube("Mark",grip.transform,grip.transform.position,new Vector3(.045f,.012f,.008f),paper,false);
                    mark.transform.localPosition=new Vector3(.024f,.025f,-.056f); mark.transform.localRotation=Quaternion.identity;
                    mark.transform.SetParent(grip.model,true);
                }
            }
        }

        static void StylePads()
        {
            PadStyle.Apply(world.gameObject);
        }
    }
}
