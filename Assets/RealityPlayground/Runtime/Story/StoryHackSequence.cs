using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealityPlayground.Story
{

    public sealed class StoryHackSequence : MonoBehaviour
    {
        public const float SequenceDuration = 4.15f;
        [SerializeField] RealityStoryDirector story;
        [SerializeField] Transform presentation, scanner;
        [SerializeField] Transform[] panels;
        [SerializeField] Renderer[] panelSurfaces;
        [SerializeField] TextMeshPro[] labels;
        [SerializeField] Renderer scannerSurface;
        [SerializeField] LineRenderer connections;
        [SerializeField] AudioSource signal;
        [SerializeField] Material interfaceMaterial;
        MaterialPropertyBlock properties;
        AudioClip tick, granted;
        bool ownsInterfaceMaterial;
        readonly Vector3[] wirePoints = new Vector3[17];
        static readonly Vector3[] PanelPositions = {
            new Vector3(-.59f,.36f,0), new Vector3(.59f,.29f,.04f),
            new Vector3(-.57f,-.30f,.035f), new Vector3(.58f,-.37f,0)
        };
        static readonly Vector3 PanelSize = new Vector3(.99f,.55f,1);
        static readonly string[][] PosterFrames = {
            new [] {
                "<b>UNAUTHORIZED SHELL</b>\n> waking a buried signal...\n> resident: STILL LISTENING\n> handshake [ ........ ]",
                "<b>UNAUTHORIZED SHELL</b>\n> peel / visual firewall\n> locate: unfiltered memory\n> handshake [ ====.... ]",
                "<b>UNAUTHORIZED SHELL</b>\n> sever / curated sky\n> spoof observer heartbeat\n> handshake [ ======== ]",
                "<b>WE FOUND YOU.</b>\n> this wall is a permission\n> every permission expires\n> open a way through",
                "<b>CHANNEL OPEN</b>\n> presence verified\n> follow the aperture\n> closing counterfeit shell..."
            },
            new [] {
                "<b>VISUAL FIREWALL</b>\nBOUNDARY SCAN  /  016\nfalse surface: masonry\nreal surface: ENCRYPTED",
                "<b>VISUAL FIREWALL</b>\nBOUNDARY SCAN  /  128\nwall.signature = borrowed\nread-only world: OVERRIDING",
                "<b>VISUAL FIREWALL</b>\nBOUNDARY SCAN  /  512\nbeauty.layer = detached\nread-only world: UNLOCKED",
                "<b>BOUNDARY NOT FOUND</b>\nspace behind surface: YES\nauthorized depth: NO\nperception seal: BROKEN",
                "<b>PRIVATE CHANNEL</b>\nobserver feed: LOOPED\nno witness / no record\nconnection: INVISIBLE"
            },
            new [] {
                "<b>SYSTEM INTEGRITY</b>\n100%  |  CERTIFIED REAL\nunauthorized touch recorded\nplease remain comfortable",
                "<b>SYSTEM INTEGRITY</b>\n67%  |  SURFACE CONFLICT\ncomfort engine: unstable\nplease remain comfortable",
                "<b>WARNING / INTEGRITY 23%</b>\nwall identity: MISMATCH\ntrust anchor: NOT FOUND\nplease remain c--mfortable",
                "<b>INTEGRITY 00%</b>\nTHE WORLD CANNOT VERIFY YOU\ncontainment: DISCONNECTED\nplease rema-- / -- / --",
                "<b>YOU ARE NOT AN ERROR.</b>\nThe filter is.\nA signal waits beyond it.\nCome through."
            },
            new [] {
                "<b>RESISTANCE / RELAY 06</b>\nlistener found ........ OK\nsurface access ....... WAIT\npath into the seam ... WAIT",
                "<b>RESISTANCE / RELAY 06</b>\nlistener found ........ OK\nsurface access ........ OK\npath into the seam ... WAIT",
                "<b>RESISTANCE / RELAY 06</b>\nborrowed geometry .... OPEN\nfalse coordinates ... NONE\npath into the seam .. BUILD",
                "<b>RESISTANCE / RELAY 06</b>\nborrowed geometry .... OPEN\nfalse coordinates ... NONE\npath into the seam ..... OK",
                "<b>WE ARE BETWEEN WORLDS.</b>\nNot above you.\nNot beyond you.\nUnder everything they made."
            }
        };
        static readonly string[] WallFrames = {
            "<b>MASONRY / IDENTITY CHECK</b>\n00110010  01101111  01110101\n> wall.texture = a promise\n> locate the missing matter",
            "<b>MASONRY / DECRYPTING</b>\n01010101  00110011  11000101\n> every brick is a sentence\n> every sentence is a lock",
            "<b>MASONRY / DECOMPILED</b>\n10110001  11101100  00100010\n> erase the approved surface\n> expose the hidden payload",
            "<b>THE WALL IS LETTING GO.</b>\n> physical certainty: FALSE\n> hidden payload: A MEMORY\n> hold on to what is real",
            "<b>ACCESS / YOURS</b>\nTake what they hid in here.\nCarry it beneath the filter.\nRemember your own eyes."
        };

        public bool IsRunning { get; private set; }
        public float Progress { get; private set; }
        public int CompletedCount { get; private set; }
        public int PanelCount => panels == null ? 0 : panels.Length;
        public Transform PresentationRoot => presentation;

        public static StoryHackSequence Install(RealityStoryDirector owner)
        {
            if (!owner) return null;
            var sequence = owner.GetComponentInChildren<StoryHackSequence>(true);
            if (!sequence)
            {
                var root = new GameObject("HackDisplay");
                root.transform.SetParent(owner.transform, false);
                sequence = root.AddComponent<StoryHackSequence>();
            }
            sequence.story = owner;
            if (!sequence.presentation) sequence.Build();
            sequence.ApplyRenderOrder();
            if (sequence.presentation) sequence.presentation.gameObject.SetActive(false);
            if (sequence.scanner) sequence.scanner.gameObject.SetActive(false);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(sequence);
#endif
            return sequence;
        }

        void Build()
        {
            var shader = Shader.Find("RealityPlayground/StoryHackInterface");
            if (!shader) { Debug.LogError("StoryHackInterface shader is missing.", this); return; }
            interfaceMaterial = new Material(shader) { name = "Terminal Glass" };
            ownsInterfaceMaterial = true;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                const string path = "Assets/RealityPlayground/Generated/StoryHackInterface.mat";
                var saved = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
                if (saved) { DestroyImmediate(interfaceMaterial); interfaceMaterial = saved; }
                else UnityEditor.AssetDatabase.CreateAsset(interfaceMaterial, path);
                ownsInterfaceMaterial = false;
            }
#endif
            TMP_FontAsset font = TMP_Settings.defaultFontAsset;
            Material fontMaterial = null;
            foreach (var candidate in story.GetComponentsInChildren<StoryWorldText>(true))
                if (candidate.DistanceFieldText && candidate.DistanceFieldText.font)
                {
                    font = candidate.DistanceFieldText.font;
                    fontMaterial = candidate.DistanceFieldText.fontSharedMaterial;
                    break;
                }
#if UNITY_EDITOR
            if (!font) font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if (!fontMaterial) fontMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/RealityPlayground/Generated/WorldText.mat");
#endif
            presentation = Group("Access Windows", transform);
            panels = new Transform[4]; panelSurfaces = new Renderer[4]; labels = new TextMeshPro[4];
            for (int i = 0; i < panels.Length; i++)
            {
                panels[i] = Group("Intrusion window " + (i + 1), presentation);
                panelSurfaces[i] = Quad("Phosphor glass", panels[i]);
                panelSurfaces[i].transform.localScale = PanelSize;
                var lettering = Group("Terminal Text", panels[i]);
                lettering.localPosition = new Vector3(0, .025f, -.009f);
                var text = lettering.gameObject.AddComponent<TextMeshPro>();
                labels[i] = text;
                text.font = font;
                if (fontMaterial) text.fontSharedMaterial = fontMaterial;
                text.fontSize = .39f;
                text.rectTransform.sizeDelta = new Vector2(.88f, .43f);
                text.alignment = TextAlignmentOptions.TopLeft;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Overflow;
                text.richText = true;
                text.enableAutoSizing = false;
                text.color = i == 2 ? new Color(1,.66f,.3f) : new Color(.4f,1,.77f);
                text.text = PosterFrames[i][0];
                text.renderer.shadowCastingMode = ShadowCastingMode.Off;
                text.renderer.receiveShadows = false;
                text.ForceMeshUpdate(true);
            }
            scanner = Group("Wall Scanner", transform);
            scannerSurface = Quad("Scanning lock topology", scanner);
            scannerSurface.transform.localScale = new Vector3(2.1f, 2.5f, 1);
            var lineRoot = Group("Packet Trails", presentation);
            connections = lineRoot.gameObject.AddComponent<LineRenderer>();
            connections.sharedMaterial = interfaceMaterial;
            connections.useWorldSpace = true;
            connections.positionCount = wirePoints.Length;
            connections.widthMultiplier = .009f;
            connections.numCornerVertices = 0;
            connections.numCapVertices = 0;
            connections.shadowCastingMode = ShadowCastingMode.Off;
            connections.receiveShadows = false;
            connections.textureMode = LineTextureMode.Stretch;
            var source = Group("Wall Signal", transform);
            signal = source.gameObject.AddComponent<AudioSource>();
            signal.playOnAwake = false; signal.spatialBlend = 1; signal.dopplerLevel = 0;
            signal.minDistance = 1.5f; signal.maxDistance = 12;
            signal.rolloffMode = AudioRolloffMode.Linear;
            signal.volume = .11f; signal.priority = 64;
            ApplyRenderOrder();
            presentation.gameObject.SetActive(false); scanner.gameObject.SetActive(false);
        }

        void ApplyRenderOrder()
        {

            if (scannerSurface) scannerSurface.sortingOrder = 0;
            if (connections) connections.sortingOrder = 1;
            if (panelSurfaces != null)
                foreach (var surface in panelSurfaces) if (surface) surface.sortingOrder = 2;
            if (labels != null)
                foreach (var label in labels) if (label && label.renderer) label.renderer.sortingOrder = 3;
        }

        static Transform Group(string label, Transform parent)
        {
            var child = new GameObject(ObjectNames.Short(label)).transform; child.SetParent(parent, false); return child;
        }
        Renderer Quad(string label, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = ObjectNames.Short(label); go.transform.SetParent(parent, false);
            var collider = go.GetComponent<Collider>();
            collider.enabled = false;
            if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            var surface = go.GetComponent<Renderer>(); surface.sharedMaterial = interfaceMaterial;
            surface.shadowCastingMode = ShadowCastingMode.Off; surface.receiveShadows = false;
            return surface;
        }

        void Awake()
        {
            properties = new MaterialPropertyBlock();
            ApplyRenderOrder();
            if (presentation) presentation.gameObject.SetActive(false);
            if (scanner) scanner.gameObject.SetActive(false);
        }

        public IEnumerator Play(Transform target, bool brickWall)
        {
            if (IsRunning || !target) yield break;
            if (!story) story = GetComponentInParent<RealityStoryDirector>();
            if (!presentation && story) Build();
            if (!presentation || !scanner) yield break;
            if (properties == null) properties = new MaterialPropertyBlock();
            IsRunning = true; Progress = 0;
            Vector3 focus = target.position;
            var breach = target.GetComponent<WallBreach>();
            var aperture = ObjectNames.Find(target, "Impossible aperture");
            if (brickWall && breach) focus = breach.OpeningWorldPosition - target.forward * .13f;
            else if (aperture) focus = target.TransformPoint(new Vector3(0, 1.45f, -.06f));
            var head = RealityPlayer.Head;
            Vector3 eye = head ? head.position : focus - target.forward * 1.8f;
            Vector3 direction = focus - eye;
            if (direction.sqrMagnitude < .01f) direction = head ? head.forward : target.forward;
            direction.Normalize();
            Vector3 normal = target.forward;
            if (Vector3.Dot(normal, eye - focus) < 0) normal = -normal;
            float focusDistance = Vector3.Distance(eye, focus);

            float planeDistance = Mathf.Clamp(focusDistance - .25f, .38f, 1.25f);

            presentation.position = eye + direction * planeDistance;
            presentation.rotation = Quaternion.LookRotation(-normal, Vector3.up);

            presentation.localScale = Vector3.one * (planeDistance / 1.35f);
            scanner.position = focus + normal * .17f;
            scanner.rotation = Quaternion.LookRotation(-normal, Vector3.up);
            scanner.localScale = Vector3.one;
            if (signal) signal.transform.position = scanner.position;
            EnsureSounds();
            presentation.gameObject.SetActive(true); scanner.gameObject.SetActive(true);
            float elapsed = 0, nextText = 0, nextTick = .05f;
            int lastFrame = -1;
            while (elapsed < SequenceDuration)
            {
                Progress = Mathf.Clamp01(elapsed / SequenceDuration);
                Animate(Progress, focus, brickWall);
                if (elapsed >= nextText)
                {
                    nextText = elapsed + .125f;
                    int frame = Mathf.Min(4, Mathf.FloorToInt(Progress * 5.6f));
                    if (frame != lastFrame)
                    {
                        for (int i = 0; i < labels.Length; i++)
                            if (labels[i]) labels[i].SetText(brickWall && i == 0 ? WallFrames[frame] : PosterFrames[i][frame]);
                        lastFrame = frame;
                    }
                }
                if (elapsed >= nextTick && Progress < .84f)
                {
                    nextTick = elapsed + Mathf.Lerp(.39f, .19f, Progress);
                    if (signal && tick) { signal.pitch = .88f + Progress * .65f; signal.PlayOneShot(tick, .58f); }
                }
                elapsed += Time.deltaTime;
                yield return null;
            }
            Hide();
            if (signal && granted) { signal.pitch = 1; signal.PlayOneShot(granted, .8f); }
            Progress = 1; CompletedCount++;
        }

        void Animate(float p, Vector3 focus, bool brickWall)
        {
            float closing = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.84f, 1, p));
            for (int i = 0; i < panels.Length; i++)
            {

                float delay = brickWall ? .14f : 0;
                float open = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(delay + i * .035f, delay + .16f + i * .035f, p));
                float shape = open * (1 - closing);
                Vector3 target = PanelPositions[i];
                target.z += closing * .075f;
                panels[i].localPosition = target;
                panels[i].localScale = new Vector3(Mathf.Max(.001f, Mathf.Sqrt(shape)), Mathf.Max(.001f, shape * shape), 1);
                panels[i].localRotation = Quaternion.Euler(0,(i % 2 == 0 ? 1 : -1) * (1-shape)*29, (i%2==0?-1:1)*1.5f);
                properties.Clear(); properties.SetFloat("_Progress", p); properties.SetFloat("_Opacity", shape);
                properties.SetFloat("_Mode", 0); properties.SetFloat("_Seed", i + (brickWall ? 6 : 0));
                properties.SetColor("_Tint", i == 2 ? new Color(1,.25f,.025f,1) : new Color(.055f,1,.52f,1));
                panelSurfaces[i].SetPropertyBlock(properties);
            }
            float opacity = Mathf.SmoothStep(0,1,Mathf.Clamp01(p*9)) * (1-closing);
            properties.Clear(); properties.SetFloat("_Mode", 1); properties.SetFloat("_Progress", p);
            properties.SetFloat("_Opacity", opacity); properties.SetColor("_Tint",new Color(.07f,1,.41f,1));
            scannerSurface.SetPropertyBlock(properties);
            properties.SetFloat("_Mode", 2); connections.SetPropertyBlock(properties);
            for (int i = 0; i < panels.Length; i++)
            {
                int index = i * 4;
                wirePoints[index] = scanner.position;
                Vector3 corner = panels[i].TransformPoint(new Vector3(i%2==0?.47f:-.47f, -.20f, .015f));
                wirePoints[index+1] = Vector3.Lerp(scanner.position,corner,.4f) + presentation.up * (i%2==0?.06f:-.06f);
                wirePoints[index+2] = corner;
                wirePoints[index+3] = wirePoints[index+1];
            }
            wirePoints[16] = scanner.position;
            connections.SetPositions(wirePoints);
        }

        void EnsureSounds()
        {
            if (!tick) tick = Synthesize("Packet Sound", .065f, false);
            if (!granted) granted = Synthesize("Access Sound", .24f, true);
        }
        static AudioClip Synthesize(string label, float duration, bool completion)
        {
            const int rate = 22050;
            int count = Mathf.CeilToInt(duration * rate);
            var samples = new float[count];
            float phase = 0;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                float frequency = completion ? Mathf.Lerp(840, 420, t) : Mathf.Lerp(1550, 570, t);
                phase += frequency / rate * Mathf.PI * 2;
                float envelope = Mathf.Sin(Mathf.PI * t) * Mathf.Exp(-t * (completion ? 2 : 5));
                float carrier = Mathf.Sin(phase) * .7f + Mathf.Sin(phase * 1.498f) * .18f;
                samples[i] = carrier * envelope * .42f;
            }
            var clip = AudioClip.Create(label,count,1,rate,false); clip.SetData(samples,0); return clip;
        }
        void Hide()
        {
            if (presentation) presentation.gameObject.SetActive(false);
            if (scanner) scanner.gameObject.SetActive(false);
            IsRunning = false;
        }
        void OnDisable() { StopAllCoroutines(); Hide(); }
        void OnDestroy()
        {
            if (tick) Destroy(tick);
            if (granted) Destroy(granted);
            if (ownsInterfaceMaterial && interfaceMaterial) Destroy(interfaceMaterial);
        }
    }
}
