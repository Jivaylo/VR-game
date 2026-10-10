using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealityPlayground.Story
{

    public sealed class StoryWorldDetail : MonoBehaviour
    {
        [Serializable] sealed class Motion
        {
            public Transform target;
            public Vector3 position, scale;
            public Quaternion rotation;
            public int kind;
            public float phase, speed;
        }
        public StoryEnvironment environment;
        public StorySecret[] secrets;
        [SerializeField] Transform digitalRoot;
        [SerializeField] Motion[] motions = Array.Empty<Motion>();
        [SerializeField] Renderer[] digitalSurfaces = Array.Empty<Renderer>();
        readonly List<Motion> buildingMotions = new List<Motion>();
        readonly List<StorySecret> buildingSecrets = new List<StorySecret>();
        Material steel, concrete, porcelain, ochre, paper, green, cyan, pink, gold, violet;
        MaterialPropertyBlock properties;
        float lastReveal = -1;
        public int AnimatedObjectCount => motions.Length;

        void Awake()=>CaptureAuthoredPoses();
        public void CaptureAuthoredPoses()
        {
            foreach(var motion in motions)
                if(motion.target){motion.position=motion.target.localPosition;motion.rotation=motion.target.localRotation;motion.scale=motion.target.localScale;}
        }

        public int RefineMechanicalProps()
        {
            int count=0;
            foreach(var motion in motions)
            {
                if(motion.kind!=4 || !motion.target)continue;
                var mount=ObjectNames.StartsWith(motion.target.name, "Physical exhaust fan ")?motion.target:motion.target.parent;
                if(!mount || !ObjectNames.StartsWith(mount.name, "Physical exhaust fan "))continue;
                var rotor=ObjectNames.Find(mount, "Fan Blades");if(!rotor)continue;
                Transform guard=ObjectNames.Find(mount, "Fan safety hoop");
                if(!guard)
                {
                    float nearest=float.PositiveInfinity;
                    foreach(Transform sibling in mount.parent)
                    {
                        if(!ObjectNames.Matches(sibling.name, "Fan safety hoop"))continue;
                        float distance=(sibling.position-mount.position).sqrMagnitude;
                        if(distance<nearest){guard=sibling;nearest=distance;}
                    }
                }
                StoryVentGeometry.Refine(mount,rotor,guard);

                motion.target=rotor;motion.position=rotor.localPosition;motion.rotation=rotor.localRotation;motion.scale=rotor.localScale;
                count++;
            }
            var infrastructure=DirectChild(transform,"Infrastructure");
            if(infrastructure && !DirectChild(infrastructure,"Pipe Brackets"))
            {
                var supports=new MeshBatch();
                for(int side=-1;side<=1;side+=2)
                for(int bay=0;bay<4;bay++)
                {
                    float z=10+bay*7.3f;
                    supports.Box(new Vector3(side*4.248f,2.65f,z),new Vector3(.11f,.53f,.74f));
                    for(int tie=0;tie<3;tie++)
                        supports.Box(new Vector3(side*4.18f,1.3f+tie*2.1f,z+1.9f),new Vector3(.25f,.045f,.145f));
                    supports.Box(new Vector3(side*4.26f,.75f,z+1.3f),new Vector3(.09f,1.13f,.39f));
                }
                var reference=DirectChild(infrastructure,"Utility Pipes");
                supports.Finish("Pipe Brackets",infrastructure,reference.GetComponent<Renderer>().sharedMaterial);
            }
            return count;
        }
        static Transform DirectChild(Transform parent,string exactName)
        {foreach(Transform child in parent)if(ObjectNames.Matches(child.name, exactName))return child;return null;}

        public static StoryWorldDetail Install(StoryEnvironment city)
        {
            if (!city || !city.hyperrealityRoot) return null;
            var existing = city.GetComponentInChildren<StoryWorldDetail>(true);
            if (existing) { existing.environment = city; return existing; }
            var root = StoryVignetteGeometry.Group("Evidence", city.transform, Vector3.zero);
            var detail = root.gameObject.AddComponent<StoryWorldDetail>();
            detail.environment = city;
            detail.Build();
            city.digitalRenderers = city.hyperrealityRoot.GetComponentsInChildren<Renderer>(true);
            return detail;
        }

        void Build()
        {
            steel = GreyboxUtil.Material("Evidence worn graphite", new Color(.085f, .105f, .11f));
            concrete = GreyboxUtil.Material("Evidence chalk and concrete", new Color(.32f, .34f, .33f));
            porcelain = GreyboxUtil.Material("Evidence surviving ivory", new Color(.68f, .64f, .49f));
            ochre = GreyboxUtil.Material("Evidence oxidised ochre", new Color(.22f, .105f, .035f));
            paper = GreyboxUtil.Material("Evidence old paper", new Color(.63f, .57f, .41f));
            green = GreyboxUtil.Material("Plant Green", new Color(.13f, .49f, .11f), .22f);
            cyan = Holo("aquamarine", new Color(.015f, .84f, 1, .72f), 1.8f);
            pink = Holo("warm coral", new Color(1, .05f, .22f, .72f), 1.7f);
            gold = Holo("amber", new Color(1, .47f, .035f, .8f), 1.7f);
            violet = Holo("ultraviolet", new Color(.38f, .04f, 1, .7f), 1.8f);
            digitalRoot = Group("Ambient Holograms", environment.hyperrealityRoot.transform, Vector3.zero);
            ApartmentEvidence(); Infrastructure(); Secrets(); OrigamiMigration(); DeepSeaAdvertisement(); CuratedHomes();
            motions = buildingMotions.ToArray();
            secrets = buildingSecrets.ToArray();
            digitalSurfaces = digitalRoot.GetComponentsInChildren<Renderer>(true);
            foreach (var r in digitalSurfaces) { r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; }
            RefineMechanicalProps();CaptureAuthoredPoses();
        }

        Material Holo(string name, Color color, float glow)
        {
            var shader = Shader.Find("RealityPlayground/StoryLivingHologram");
            if (!shader) shader = Shader.Find("RealityPlayground/StoryARProjection");
            var material = new Material(shader) { name = "Spatial Evidence " + name };
            material.SetColor("_BaseColor", color); material.SetFloat("_Glow", glow);
            return material;
        }
        static Transform Group(string name, Transform parent, Vector3 position) => StoryVignetteGeometry.Group(name, parent, position);
        Transform Shape(string name, PrimitiveType type, Transform parent, Vector3 p, Vector3 scale, Material m)
            => GreyboxUtil.Primitive(name, type, parent, p, scale, m, false).transform;
        Transform Box(string name, Transform parent, Vector3 p, Vector3 size, Material m)
            => Shape(name, PrimitiveType.Cube, parent, p, size, m);
        TextMesh Caption(string text, Transform parent, Vector3 position, float size, Color color)
        {
            var label = GreyboxUtil.Label(text, parent, position, size, color);
            label.gameObject.AddComponent<StoryWorldText>();
            return label;
        }
        void Animate(Transform target, int kind, float speed, float phase = 0)
            => buildingMotions.Add(new Motion { target = target, kind = kind, speed = speed, phase = phase,
                position = target.localPosition, rotation = target.localRotation, scale = target.localScale });

        void ApartmentEvidence()
        {
            var room = Group("Apartment Evidence", transform, Vector3.zero);
            var metal = new MeshBatch(); var ceramic = new MeshBatch(); var pages = new MeshBatch();

            for (int i = 0; i < 2; i++)
            {
                var center = new Vector3(2.65f + i * .4f, 1.07f, 1.25f);
                ceramic.Tube(center, .095f, .087f, .21f, 16);
                ceramic.Tube(center + Vector3.up * .108f, .092f, .074f, .012f, 16);
                metal.Box(center + Vector3.up * .09f, new Vector3(.125f, .008f, .125f));
            }
            metal.Box(new Vector3(2.68f, .5f, 2.5f), new Vector3(.48f, .065f, .48f));
            metal.Box(new Vector3(2.68f, .78f, 2.71f), new Vector3(.48f, .57f, .065f));
            for (int x = -1; x <= 1; x += 2)
            for (int z = -1; z <= 1; z += 2)
                metal.Box(new Vector3(2.68f + x * .19f, .25f, 2.5f + z * .19f), new Vector3(.045f, .5f, .045f));
            for (int i = 0; i < 4; i++) pages.Box(new Vector3(3.22f, .96f + i * .015f, .68f), new Vector3(.34f, .012f, .44f), Quaternion.Euler(0, i * 7, 0));

            ceramic.Tube(new Vector3(2.57f, .962f, 1.93f), .11f, .086f, .025f, 20);
            metal.Box(new Vector3(3.94f, .45f, 1.8f), new Vector3(.025f, .24f, .14f));
            var wire = new Vector3[20];
            for (int i = 0; i < wire.Length; i++) { float t = i / 19f; wire[i] = new Vector3(Mathf.Lerp(3.9f, 2.88f, t), .19f + .12f * Mathf.Sin(t * Mathf.PI), 1.8f + .2f * Mathf.Sin(t * 5)); }
            GreyboxUtil.Line("Spare Headset Cable", room, wire, steel, .012f);
            metal.Finish("Desk and Chair", room, steel);
            ceramic.Finish("Personal Items", room, porcelain);
            pages.Finish("Letters", room, paper);
            var evidence = Caption("MARA\nRETURN TO SENDER", room, new Vector3(3.22f, 1.025f, .65f), .055f, new Color(.2f,.17f,.13f));
            evidence.transform.localRotation = Quaternion.Euler(90, 0, 0);
        }

        void Infrastructure()
        {
            var infrastructure = Group("Infrastructure", transform, Vector3.zero);
            var metal = new MeshBatch(); var plaster = new MeshBatch(); var rust = new MeshBatch();
            var projectedTrim = new MeshBatch();
            for (int side = -1; side <= 1; side += 2)
            for (int bay = 0; bay < 4; bay++)
            {
                float z = 10 + bay * 7.3f;

                plaster.Box(new Vector3(side * 4.04f, 2.65f, z), new Vector3(.31f, .62f, .84f));
                for (int slat = 0; slat < 6; slat++) metal.Box(new Vector3(side * 3.873f, 2.4f + slat * .095f, z), new Vector3(.023f, .035f, .72f));
                metal.Box(new Vector3(side * 4.065f, 4.4f, z + 1.9f), new Vector3(.1f, 7.2f, .1f));
                for (int tie = 0; tie < 3; tie++) rust.Box(new Vector3(side * 4.01f, 1.3f + tie * 2.1f, z + 1.9f), new Vector3(.14f, .07f, .18f));
                metal.Box(new Vector3(side * 4.08f, .75f, z + 1.3f), new Vector3(.16f, 1.2f, .48f));
                plaster.Box(new Vector3(side * 3.98f, .93f, z + 1.3f), new Vector3(.09f, .44f, .31f));
                rust.Box(new Vector3(side * 3.94f, .19f, z + 1.28f), new Vector3(.12f, .28f, .48f));
                projectedTrim.Box(new Vector3(side*3.847f,2.96f,z),new Vector3(.015f,.022f,.87f));
                projectedTrim.Box(new Vector3(side*3.847f,2.34f,z),new Vector3(.015f,.022f,.87f));
                projectedTrim.Box(new Vector3(side*3.847f,2.65f,z-.425f),new Vector3(.015f,.63f,.022f));
                projectedTrim.Box(new Vector3(side*3.847f,2.65f,z+.425f),new Vector3(.015f,.63f,.022f));
            }

            for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 8; i++)
            {
                float z = 8 + i * 3.3f;
                metal.Box(new Vector3(side * 3.4f, .013f, z), new Vector3(.36f, .018f, .85f));
                for (int n = 0; n < 5; n++) plaster.Box(new Vector3(side * 3.4f, .024f, z - .32f + n * .16f), new Vector3(.3f, .018f, .035f));
            }

            for (int i = 0; i < 3; i++)
            {
                var fan = Group("Physical exhaust fan " + i, infrastructure, new Vector3(3.78f, 3.7f, 11 + i * 10));
                fan.localRotation = Quaternion.Euler(0, 90, 0);
                var blades = new MeshBatch();
                for (int n = 0; n < 5; n++) blades.Box(new Vector3(0, 0, 0), new Vector3(.05f, .61f, .025f), Quaternion.Euler(0, 0, n * 72));
                blades.Finish("Fan Blades", fan, steel);
                Animate(fan, 4, 34 + i * 7);
                var ring = StoryVignetteGeometry.Ring("Fan safety hoop", infrastructure, .36f, .027f, steel, 28);
                ring.localPosition = fan.localPosition; ring.localRotation = fan.localRotation;
            }
            metal.Finish("Utility Pipes", infrastructure, steel);
            plaster.Finish("Vent Edges", infrastructure, concrete);
            rust.Finish("Rust", infrastructure, ochre);
            projectedTrim.Finish("Vent Overlays",digitalRoot,cyan);
            var stencil = Caption("6A / VACANT\nDO NOT REASSIGN", infrastructure, new Vector3(-4.001f, 1.35f, 8.9f), .09f, new Color(.53f,.48f,.38f));
            stencil.transform.localRotation = Quaternion.Euler(0, -90, 0);
        }

        StorySecret Secret(string name, Vector3 p, float yaw, Vector3 size, string instruction, string message, int sound, float minimumReveal)
        {
            var root = Group(name, transform, p); root.localRotation = Quaternion.Euler(0, yaw, 0);
            var secret = root.gameObject.AddComponent<StorySecret>();
            secret.environment = environment; secret.minimumReveal = minimumReveal; secret.soundVariation = sound;
            secret.revealedText = message;
            Box("Object casing", root, Vector3.zero, size, steel);
            Box("Ivory rim", root, new Vector3(0, size.y * .45f, 0), new Vector3(size.x * 1.03f, .035f, size.z * 1.03f), porcelain);
            secret.lid = Group("Hinged lid", root, new Vector3(0, size.y * .55f, size.z * .48f));
            Box("Closed lid", secret.lid, new Vector3(0, 0, -size.z * .48f), new Vector3(size.x * 1.06f, .045f, size.z), ochre);
            secret.discovery = Group("What was saved", root, new Vector3(0, size.y * .52f, 0));
            secret.instruction = Caption(instruction, root, new Vector3(0, -.015f, -size.z * .54f), .068f, new Color(.8f,.81f,.66f));
            var volume = root.gameObject.AddComponent<BoxCollider>();
            volume.isTrigger = true; volume.center = new Vector3(0, .04f, 0); volume.size = size + new Vector3(.06f,.12f,.06f);
            secret.Configure(volume);
            buildingSecrets.Add(secret);
            return secret;
        }

        void Secrets()
        {

            var pedestal = new MeshBatch();
            pedestal.Box(new Vector3(1.84f,.46f,-.43f), new Vector3(.46f,.92f,.44f));
            pedestal.Finish("Keepsake Stand", transform, concrete);
            var memory = Secret("Music Box", new Vector3(1.84f, 1.005f, -.43f), -15, new Vector3(.39f,.15f,.29f), "OPEN", "FOR MARA / KEEP THIS", 0, 0);
            var hills = new MeshBatch();
            hills.Box(new Vector3(0,.04f,0), new Vector3(.31f,.065f,.19f));
            hills.Finish("Landscape", memory.discovery, porcelain);
            var tree = Shape("Tiny memory tree", PrimitiveType.Capsule, memory.discovery, new Vector3(-.09f,.155f,.03f), new Vector3(.028f,.09f,.028f), ochre);
            Shape("The shape of a remembered tree", PrimitiveType.Sphere, memory.discovery, tree.localPosition + Vector3.up * .06f, new Vector3(.11f,.12f,.09f), green);
            for (int i = 0; i < 2; i++)
            {
                var ring = StoryVignetteGeometry.Ring("Two interlocked rings", memory.discovery, .048f, .007f, porcelain, 24);
                ring.localPosition = new Vector3(.045f+i*.06f,.11f,-.035f); ring.localRotation = Quaternion.Euler(0,i*65,15);
            }

            var ration = Secret("Ration Message", new Vector3(2.75f,1.05f,13.7f), 25, new Vector3(.58f,.28f,.38f), "SERVICE / OPEN", "STILL HERE. STILL HUMAN.", 1, 0);
            var housing = new MeshBatch(); housing.Box(new Vector3(2.75f,.48f,13.7f),new Vector3(.65f,.95f,.47f));
            housing.Finish("Ration Stand",transform,concrete);
            var packages = new MeshBatch();
            for (int i=0;i<3;i++) packages.Box(new Vector3((i-1)*.14f,.045f,.03f),new Vector3(.12f,.08f,.2f),Quaternion.Euler(0,(i-1)*8,0));
            packages.Finish("Nutrient Blocks",ration.discovery,paper);
            var token = StoryVignetteGeometry.Ring("The resistance left an eye-shaped token",ration.discovery,.075f,.01f,porcelain,24);
            token.localPosition = new Vector3(0,.12f,-.08f); token.localScale = new Vector3(1.7f,.65f,1);
            Shape("The token's pupil",PrimitiveType.Sphere,ration.discovery,new Vector3(0,.12f,-.08f),Vector3.one*.04f,porcelain);

            var seed = Secret("Hidden Plant", new Vector3(1.05f,1.01f,33.45f), -15, new Vector3(.52f,.22f,.36f), "LIFT", "IT WAS NEVER ALL CONCRETE.", 2, .97f);
            var planter = new MeshBatch(); planter.Box(new Vector3(1.05f,.45f,33.45f),new Vector3(.58f,.9f,.42f));
            planter.Finish("Irrigation Box",transform,concrete);
            Shape("Real soil",PrimitiveType.Sphere,seed.discovery,new Vector3(0,.025f,0),new Vector3(.36f,.05f,.25f),ochre);
            var stem=Shape("A real green shoot",PrimitiveType.Capsule,seed.discovery,new Vector3(0,.155f,0),new Vector3(.018f,.16f,.018f),green);
            for(int i=0;i<3;i++)
            {
                var leaf=Shape("Unlicensed life",PrimitiveType.Sphere,seed.discovery,new Vector3(i%2==0?.055f:-.055f,.14f+i*.07f,0),new Vector3(.15f,.035f,.065f),green);
                leaf.localRotation=Quaternion.Euler(0,i*35,i%2==0?25:-25);
            }

            GreyboxUtil.Line("Plant Root",transform,new[]{new Vector3(.86f,.91f,33.23f),new Vector3(.78f,.55f,33.22f),new Vector3(.91f,.2f,33.2f),new Vector3(.65f,.028f,33.01f)},ochre,.016f);
        }

        void OrigamiMigration()
        {
            var flock=Group("Bird Ad",digitalRoot,new Vector3(-.45f,3.35f,10.2f));
            Animate(flock,7,.27f);
            for(int i=0;i<7;i++)
            {
                var bird=Group("Origami migrant "+i,flock,new Vector3((i-3)*.54f,Mathf.Sin(i*1.8f)*.22f,Mathf.Abs(i-3)*.27f));
                bird.localRotation=Quaternion.Euler(0,20+i*9,0); Animate(bird,0,.9f+i*.04f,i*.75f);
                var body=StoryVignetteGeometry.Surface("Folded luminous body",bird,Vector3.zero,OrigamiBody(),i%2==0?cyan:gold);
                body.transform.localScale=Vector3.one*.37f;
                for(int side=-1;side<=1;side+=2)
                {
                    var wing=StoryVignetteGeometry.Surface("Faceted folding wing",bird,Vector3.zero,OrigamiWing(side),i%2==0?cyan:gold).transform;
                    wing.localScale=Vector3.one*.37f;Animate(wing,1,1.8f+i*.06f,(side>0?1:-1)*(i+.1f));
                }
            }
            var caption=Caption("MIGRATION™",flock,new Vector3(0,-.62f,.3f),.12f,new Color(.18f,.95f,1));
            Animate(caption.transform,0,.45f,0);
        }

        void DeepSeaAdvertisement()
        {

            var jelly=Group("Jellyfish Ad",digitalRoot,new Vector3(-1.55f,5.0f,22.8f));
            Animate(jelly,0,.65f,1.2f);
            var dome=StoryVignetteGeometry.Surface("Translucent jellyfish bell",jelly,Vector3.zero,JellyBell(),violet).transform;
            Animate(dome,2,.85f,0);
            for(int rib=0;rib<8;rib++)
            {
                float a=rib*Mathf.PI*.25f;
                var points=new Vector3[15];
                for(int i=0;i<points.Length;i++)
                {
                    float u=i/(float)(points.Length-1)*Mathf.PI*.5f;
                    points[i]=new Vector3(Mathf.Cos(a)*Mathf.Sin(u)*.76f,Mathf.Cos(u)*.48f,Mathf.Sin(a)*Mathf.Sin(u)*.76f);
                }
                GreyboxUtil.Line("Bioluminescent bell rib",dome,points,rib%2==0?cyan:pink,.012f);
            }
            for(int tentacle=0;tentacle<6;tentacle++)
            {
                float a=tentacle*Mathf.PI/3;
                var points=new Vector3[28];
                for(int i=0;i<points.Length;i++)
                {
                    float t=i/(float)(points.Length-1);
                    points[i]=new Vector3(Mathf.Cos(a)*.38f+Mathf.Sin(t*7+tentacle)*t*.16f,-t*(1.15f+tentacle%2*.38f),Mathf.Sin(a)*.38f+Mathf.Cos(t*6+tentacle)*t*.16f);
                }
                var tendril=GreyboxUtil.Line("Light Tendril",jelly,points,tentacle%2==0?cyan:pink,.02f).transform;
                Animate(tendril,3,.55f+tentacle*.03f,tentacle*.8f);
            }
            Caption("DEEP BREATH",jelly,new Vector3(0,-1.95f,0),.18f,new Color(.5f,.8f,1));
        }

        void CuratedHomes()
        {
            var home=Group("Housing Ad",digitalRoot,new Vector3(2.8f,3.55f,24.5f));
            home.localRotation=Quaternion.Euler(0,-20,0);Animate(home,5,.18f,0);
            var frame=new MeshBatch();
            for(int x=-1;x<=1;x+=2)
            for(int z=-1;z<=1;z+=2)
                frame.Box(new Vector3(x*.55f,0,z*.55f),new Vector3(.023f,1.6f,.023f));
            for(int floor=0;floor<3;floor++)
            {
                float y=(floor-1)*.8f;
                frame.Box(new Vector3(0,y,-.55f),new Vector3(1.12f,.025f,.025f));
                frame.Box(new Vector3(0,y,.55f),new Vector3(1.12f,.025f,.025f));
                frame.Box(new Vector3(-.55f,y,0),new Vector3(.025f,.025f,1.12f));
                frame.Box(new Vector3(.55f,y,0),new Vector3(.025f,.025f,1.12f));
            }
            frame.Finish("Floor Plan",home,cyan);
            for(int floor=0;floor<2;floor++)
            {
                var suite=Group("Floating Room",home,new Vector3(0,-.62f+floor*.8f,0));
                Animate(suite,6,.45f,floor*Mathf.PI);
                var furniture=new MeshBatch();
                furniture.Box(Vector3.zero,new Vector3(1.05f,.055f,1.05f));
                furniture.Box(new Vector3(-.24f,.14f,.12f),new Vector3(.3f,.19f,.53f));
                furniture.Box(new Vector3(.27f,.16f,-.2f),new Vector3(.25f,.035f,.26f));
                furniture.Box(new Vector3(.27f,.07f,-.2f),new Vector3(.03f,.18f,.03f));
                furniture.Finish("Perfect mini apartment",suite,floor==0?pink:gold);
                var human=Shape("Holographic Resident",PrimitiveType.Capsule,suite,new Vector3(.22f,.23f,.24f),new Vector3(.08f,.16f,.08f),cyan);
                Animate(human,0,.9f,floor);
            }
            Caption("YOUR SPACE. OUR VISION.",home,new Vector3(0,-1.04f,-.57f),.105f,new Color(.4f,.95f,1));

        }

        void Update() => Step(Time.time);
        public void Step(float time)
        {
            float reveal=environment?environment.RevealAmount:0;
            foreach(var m in motions)
            {
                if(!m.target)continue;
                float t=time*m.speed+m.phase;
                var p=m.position;var r=m.rotation;var s=m.scale;
                switch(m.kind)
                {
                    case 0:p+=new Vector3(Mathf.Sin(t*.63f)*.045f,Mathf.Sin(t)*.09f,Mathf.Cos(t*.77f)*.04f);break;
                    case 1:r*=Quaternion.Euler(0,0,Mathf.Sin(t*2.1f)*29*(m.phase>=0?1:-1));break;
                    case 2:s=Vector3.Scale(s,new Vector3(1+Mathf.Sin(t)*.075f,1-Mathf.Sin(t)*.1f,1+Mathf.Sin(t)*.075f));break;
                    case 3:r*=Quaternion.Euler(Mathf.Sin(t)*8,Mathf.Sin(t*.8f)*11,Mathf.Cos(t)*7);break;
                    case 4:r*=Quaternion.Euler(0,0,time*m.speed);break;
                    case 5:r*=Quaternion.Euler(0,Mathf.Sin(t)*28,0);break;
                    case 6:p+=Vector3.right*Mathf.Sin(t)*.23f;break;
                    case 7:p+=new Vector3(Mathf.Sin(t)*.7f,Mathf.Sin(t*.5f)*.18f,Mathf.Cos(t)*.85f);r*=Quaternion.Euler(0,Mathf.Sin(t)*22,0);break;
                }
                m.target.localPosition=p;m.target.localRotation=r;m.target.localScale=s;
            }
            if(Mathf.Abs(lastReveal-reveal)>.0001f)
            {
                lastReveal=reveal;if(properties==null)properties=new MaterialPropertyBlock();
                for(int i=0;i<digitalSurfaces.Length;i++)
                {
                    var surface=digitalSurfaces[i];
                    if(!surface)continue;surface.GetPropertyBlock(properties);properties.SetFloat("_Reveal",reveal);properties.SetFloat("_Seed",Mathf.Repeat(i*.61803399f,.99f));surface.SetPropertyBlock(properties);
                }
            }
        }

        static Mesh OrigamiBody()
        {
            return Facets("Faceted paper crane",new[]{new Vector3(0,.12f,-.48f),new Vector3(-.12f,0,0),new Vector3(.12f,0,0),new Vector3(0,-.11f,.17f),new Vector3(0,.32f,.44f),new Vector3(0,.18f,.65f)},new[]{0,1,2,1,3,2,1,4,3,2,3,4,3,4,5});
        }
        static Mesh OrigamiWing(int side)
        {
            return Facets("Folded triangular wing",new[]{Vector3.zero,new Vector3(side*.93f,.1f,-.16f),new Vector3(side*.3f,.18f,.25f),new Vector3(side*.12f,-.015f,.23f)},new[]{0,1,2,0,2,3});
        }
        static Mesh Facets(string name,Vector3[] points,int[] tris)
        {
            var v=new List<Vector3>();var indices=new List<int>();
            for(int i=0;i<tris.Length;i+=3)
            {
                int start=v.Count;v.Add(points[tris[i]]);v.Add(points[tris[i+1]]);v.Add(points[tris[i+2]]);
                indices.Add(start);indices.Add(start+1);indices.Add(start+2);
            }
            var uv=new List<Vector2>();foreach(var p in v)uv.Add(new Vector2(p.x+p.z*.37f,p.y+p.z*.53f));
            var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        static Mesh JellyBell()
        {
            const int sides=32,rows=10;var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(int row=0;row<=rows;row++)for(int col=0;col<=sides;col++)
            {
                float u=row/(float)rows*Mathf.PI*.5f,a=col/(float)sides*Mathf.PI*2;
                vertices.Add(new Vector3(Mathf.Cos(a)*Mathf.Sin(u)*.76f,Mathf.Cos(u)*.48f,Mathf.Sin(a)*Mathf.Sin(u)*.76f));uv.Add(new Vector2(col/(float)sides,row/(float)rows));
            }
            for(int row=0;row<rows;row++)for(int col=0;col<sides;col++)
            {
                int a=row*(sides+1)+col,b=a+sides+1;triangles.AddRange(new[]{a,b,a+1,a+1,b,b+1});
            }
            var mesh=new Mesh{name="Jellyfish Bell"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }

        sealed class MeshBatch
        {
            readonly List<Vector3> vertices=new List<Vector3>();
            readonly List<int> triangles=new List<int>();
            static readonly Vector3[] Cube={new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(-1,1,-1),new Vector3(-1,-1,1),new Vector3(1,-1,1),new Vector3(1,1,1),new Vector3(-1,1,1)};
            static readonly int[] Faces={0,3,2,1,5,6,7,4,4,7,3,0,1,2,6,5,3,7,6,2,4,0,1,5};
            public void Box(Vector3 center,Vector3 size,Quaternion rotation=default)
            {
                if(rotation==default)rotation=Quaternion.identity;
                for(int f=0;f<6;f++)
                {
                    int first=vertices.Count;for(int i=0;i<4;i++)vertices.Add(center+rotation*Vector3.Scale(Cube[Faces[f*4+i]],size*.5f));
                    triangles.AddRange(new[]{first,first+1,first+2,first,first+2,first+3});
                }
            }
            public void Tube(Vector3 center,float outer,float inner,float height,int sides)
            {
                for(int side=0;side<sides;side++)
                {
                    float a=side*Mathf.PI*2/sides,b=(side+1)*Mathf.PI*2/sides;
                    var da=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));var db=new Vector3(Mathf.Cos(b),0,Mathf.Sin(b));
                    var up=Vector3.up*height*.5f;
                    Quad(center+da*outer-up,center+da*outer+up,center+db*outer+up,center+db*outer-up);
                    Quad(center+db*inner-up,center+db*inner+up,center+da*inner+up,center+da*inner-up);
                    Quad(center+da*inner+up,center+db*inner+up,center+db*outer+up,center+da*outer+up);
                    Quad(center+da*outer-up,center+db*outer-up,center+db*inner-up,center+da*inner-up);
                }
            }
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {
                int i=vertices.Count;vertices.AddRange(new[]{a,b,c,d});triangles.AddRange(new[]{i,i+1,i+2,i,i+2,i+3});
            }
            public Transform Finish(string name,Transform parent,Material material)
            {
                var mesh=new Mesh{name=name};if(vertices.Count>65535)mesh.indexFormat=IndexFormat.UInt32;
                var uv=new List<Vector2>();foreach(var p in vertices)uv.Add(new Vector2(p.x+p.z*.37f,p.y+p.z*.53f));
                mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
                return StoryVignetteGeometry.Surface(name,parent,Vector3.zero,mesh,material).transform;
            }
        }
    }
}
