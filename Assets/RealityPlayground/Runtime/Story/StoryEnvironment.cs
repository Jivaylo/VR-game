using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealityPlayground.Story
{

    public sealed class StoryEnvironment : MonoBehaviour
    {
        public Transform bedroomSpawn, bedsideSpot, landingSpot, streetSpot, posterSpot;
        public Transform returnSpot, alleySpot, chipSpot, endSpot, liminalSpot;
        public Transform doorMount, alarmMount, posterMount, angelMount, mirrorMount, breachMount;
        public GameObject hyperrealityRoot, liminalRoot;
        public Renderer[] digitalRenderers;
        [SerializeField] Transform[] digitalUnits;
        [SerializeField] Vector3[] unitPositions, unitScales;
        [SerializeField] Quaternion[] unitRotations;
        [SerializeField] float revealAmount;
        MaterialPropertyBlock revealProperties;
        readonly List<Transform> creatingUnits = new List<Transform>();
        Material concrete, darkConcrete, paleConcrete, steel, glass, bedFabric, cameraRed;
        Material cyan, pink, amber, violet, blue, white, softCyan, softPink;
        public float RevealAmount => revealAmount;

        void Awake()=>CaptureAuthoredPoses();
        public void CaptureAuthoredPoses()
        {
            if(digitalUnits==null)return;
            for(int i=0;i<digitalUnits.Length;i++)
            {
                if(!digitalUnits[i])continue;
                unitPositions[i]=digitalUnits[i].localPosition;
                unitRotations[i]=digitalUnits[i].localRotation;
                unitScales[i]=digitalUnits[i].localScale;
            }
        }

        public static StoryEnvironment Create(Transform parent)
        {
            var root = new GameObject("City");
            root.transform.SetParent(parent, false);
            var environment = root.AddComponent<StoryEnvironment>();
            environment.Build();
            return environment;
        }

        void Build()
        {
            concrete = GreyboxUtil.Material("Raw Concrete", new Color(.25f,.27f,.28f));
            darkConcrete = GreyboxUtil.Material("Stained Concrete", new Color(.10f,.12f,.13f));
            paleConcrete = GreyboxUtil.Material("Plaster", new Color(.42f,.44f,.44f));
            steel = GreyboxUtil.Material("Story blackened steel", new Color(.055f,.067f,.07f));
            glass = GreyboxUtil.Material("Story unlit windows", new Color(.055f,.095f,.12f));
            bedFabric = GreyboxUtil.Material("Story grey bedding", new Color(.38f,.4f,.41f));
            cameraRed = GreyboxUtil.Material("Camera Indicator",new Color(.6f,.015f,.005f),.6f);
            cyan = Projection("ion cyan",new Color(.025f,.95f,1,.95f),2.7f);
            pink = Projection("hot coral",new Color(1,.06f,.39f,.95f),2.4f);
            amber = Projection("sunrise amber",new Color(1,.62f,.06f,.98f),2.3f);
            violet = Projection("electric violet",new Color(.42f,.08f,1,.95f),2.4f);
            blue = Projection("cobalt",new Color(.02f,.14f,.52f,.98f),1.3f);
            white = Projection("clean white",new Color(.78f,.97f,1,.93f),2.1f);
            softCyan = Projection("cyan glass",new Color(.02f,.29f,.39f,.72f),1.7f);
            softPink = Projection("rose glass",new Color(.44f,.035f,.22f,.8f),1.65f);
            hyperrealityRoot = new GameObject("Overlays");
            hyperrealityRoot.transform.SetParent(transform,false);
            liminalRoot = new GameObject("Cyberspace");
            liminalRoot.transform.SetParent(transform,false);

            bedroomSpawn = Point("Wake in bed",new Vector3(0,0,-2));
            bedsideSpot = Point("Bedside navigation",new Vector3(1.3f,0,-.65f));
            landingSpot = Point("Apartment landing",new Vector3(0,0,7));
            streetSpot = Point("Street navigation",new Vector3(0,0,13));
            posterSpot = Point("Poster Approach",new Vector3(-3.35f,0,19));
            returnSpot = Point("Return Point",new Vector3(0,0,19));
            alleySpot = Point("Mirror alley",new Vector3(0,0,27.4f));
            chipSpot = Point("Touch the other side",new Vector3(2.85f,0,29));
            endSpot = Point("Raw City",new Vector3(0,0,32.8f));
            liminalSpot = Point("Arrival between channels",new Vector3(0,0,80));
            doorMount = Point("Bedroom door mount",new Vector3(0,0,4));
            alarmMount = Point("Bedside rooster mount",new Vector3(-.45f,.85f,-1.65f));
            posterMount = Point("Poster mount",new Vector3(-4.02f,0,19),-90);
            angelMount = Point("Resistance signal mount",new Vector3(0,0,86));
            mirrorMount = Point("Liquid mirror mount",new Vector3(-3.3f,0,29),-90);
            breachMount = Point("Rupture wall mount",new Vector3(3.3f,0,29),90);
            BuildBedroom();
            BuildExterior();
            BuildLiminal();
            digitalUnits = creatingUnits.ToArray();
            unitPositions = new Vector3[digitalUnits.Length];
            unitScales = new Vector3[digitalUnits.Length];
            unitRotations = new Quaternion[digitalUnits.Length];
            for(int i=0;i<digitalUnits.Length;i++)
            {
                unitPositions[i]=digitalUnits[i].localPosition;
                unitScales[i]=digitalUnits[i].localScale;
                unitRotations[i]=digitalUnits[i].localRotation;
            }
            digitalRenderers = hyperrealityRoot.GetComponentsInChildren<Renderer>(true);
            liminalRoot.SetActive(false);
            SetReveal(0);
        }

        Material Projection(string name,Color tint,float glow)
        {
            var shader=Shader.Find("RealityPlayground/StoryARProjection");
            if(!shader) return GreyboxUtil.Material("Story AR "+name,tint,glow,true);
            var material=new Material(shader){name="Story AR "+name};
            material.SetColor("_BaseColor",tint);
            material.SetFloat("_Glow",glow);
            return material;
        }

        Transform Point(string name,Vector3 position,float yaw=0)
        {
            var point=new GameObject(ObjectNames.Short(name)).transform;
            point.SetParent(transform,false);
            point.localPosition=position;
            point.localRotation=Quaternion.Euler(0,yaw,0);
            return point;
        }

        Transform Digital(string name,Vector3 position,float yaw=0)
        {
            var unit=new GameObject(ObjectNames.Short(name)).transform;
            unit.SetParent(hyperrealityRoot.transform,false);
            unit.localPosition=position;
            unit.localRotation=Quaternion.Euler(0,yaw,0);
            creatingUnits.Add(unit);
            return unit;
        }

        GameObject Box(string name,Transform parent,Vector3 p,Vector3 size,Material material,bool solid=false)
            => GreyboxUtil.Primitive(name,PrimitiveType.Cube,parent,p,size,material,solid);

        TextMesh Text(string text,Transform parent,Vector3 p,float size,Color color)
            => GreyboxUtil.Label(text,parent,p,size,color);

        void Frame(Transform parent,float width,float height,Material material)
        {
            Box("Upper luminous edge",parent,new Vector3(0,height*.5f,0),new Vector3(width,.025f,.025f),material);
            Box("Lower luminous edge",parent,new Vector3(0,-height*.5f,0),new Vector3(width,.025f,.025f),material);
            Box("Left luminous edge",parent,new Vector3(-width*.5f,0,0),new Vector3(.025f,height,.025f),material);
            Box("Right luminous edge",parent,new Vector3(width*.5f,0,0),new Vector3(.025f,height,.025f),material);
        }

        void Ring(Transform parent,string name,Vector3 position,float radius,Material material,float thickness=.025f,int segments=48)
        {
            var points=new Vector3[segments];
            for(int i=0;i<segments;i++)
            {
                float a=i*Mathf.PI*2/segments;
                points[i]=position+new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0);
            }
            GreyboxUtil.Line(name,parent,points,material,thickness,true);
        }

        void Panel(string name,Vector3 position,float yaw,Vector2 size,string title,string subtitle,Material accent)
        {
            var panel=Digital(name,position,yaw);
            Box("Glass projection",panel,Vector3.zero,new Vector3(size.x,size.y,.018f),accent==pink?softPink:softCyan);
            Frame(panel,size.x,size.y,accent);
            Text(title,panel,new Vector3(0,size.y*.18f,-.03f),Mathf.Min(.29f,size.x*.11f),Color.white);
            if(!string.IsNullOrEmpty(subtitle))
                Text(subtitle,panel,new Vector3(0,-size.y*.23f,-.035f),Mathf.Min(.105f,size.x*.04f),new Color(.58f,1,1));
            for(int i=0;i<3;i++) Box("Live data",panel,new Vector3(-size.x*.37f+i*.13f,-size.y*.39f,-.03f),new Vector3(.07f,.015f,.013f),accent);
        }

        void BuildBedroom()
        {
            var room=new GameObject("Apartment").transform;
            room.SetParent(transform,false);
            Box("Concrete floor",room,new Vector3(0,-.13f,0),new Vector3(8,.26f,8),concrete,true);
            Box("Back wall",room,new Vector3(0,2,-4.1f),new Vector3(8.2f,4,.22f),paleConcrete,true);
            Box("Left wall",room,new Vector3(-4.1f,2,0),new Vector3(.22f,4,8),paleConcrete,true);
            Box("Right wall",room,new Vector3(4.1f,2,0),new Vector3(.22f,4,8),paleConcrete,true);
            Box("Front left",room,new Vector3(-2.57f,2,4.1f),new Vector3(3.05f,4,.22f),paleConcrete,true);
            Box("Front right",room,new Vector3(2.57f,2,4.1f),new Vector3(3.05f,4,.22f),paleConcrete,true);
            Box("Door lintel",room,new Vector3(0,3.55f,4.1f),new Vector3(2.1f,.9f,.22f),paleConcrete,true);
            Box("Bed frame",room,new Vector3(0,.26f,-2.25f),new Vector3(1.55f,.5f,2.25f),steel);
            Box("Mattress",room,new Vector3(0,.56f,-2.2f),new Vector3(1.48f,.23f,2.08f),bedFabric);
            Box("Pillow",room,new Vector3(0,.74f,-2.92f),new Vector3(.95f,.16f,.43f),paleConcrete);
            Box("Folded blanket",room,new Vector3(0,.71f,-1.83f),new Vector3(1.43f,.055f,1.18f),darkConcrete);
            Box("Alarm bedside pedestal",room,new Vector3(-1.12f,.36f,-1.55f),new Vector3(.65f,.72f,.65f),concrete,true);
            Box("Desk",room,new Vector3(2.95f,.86f,1.35f),new Vector3(1.35f,.15f,2.1f),darkConcrete,true);
            Box("Desk support",room,new Vector3(3.3f,.4f,1.35f),new Vector3(.2f,.8f,1.8f),steel,true);
            Box("The small actual window",room,new Vector3(-3.965f,2.5f,1),new Vector3(.026f,.65f,.38f),glass);
            CameraUnit(room,new Vector3(3.7f,3.3f,-3.6f),new Vector3(0,1,-1));
            Text("UNIT 6A / OCCUPANCY: 1",room,new Vector3(0,3.24f,3.975f),.09f,new Color(.24f,.25f,.25f));

            var wallSkin=Digital("Apartment Overlays",Vector3.zero);
            Box("Headboard Overlay",wallSkin,new Vector3(0,1.65f,-3.96f),new Vector3(7.85f,3.15f,.025f),softPink);
            Box("Ocean side wall projection",wallSkin,new Vector3(3.96f,2,0),new Vector3(.025f,3.92f,7.9f),softCyan);
            Box("Holographic bed cover",wallSkin,new Vector3(0,.76f,-1.82f),new Vector3(1.44f,.015f,1.2f),violet);
            for(int side=-1;side<=1;side+=2)
            {
                var light=Digital("Circadian floor ribbon",new Vector3(side*3.84f,.035f,0));
                Box("Floor edge",light,Vector3.zero,new Vector3(.045f,.015f,7.8f),side<0?pink:cyan);
            }
            Panel("Wake-up projection",new Vector3(0,2.42f,.36f),0,new Vector2(2.7f,1.08f),"GOOD MORNING","06:00   /   A BETTER YOU STARTS NOW",amber);
            Panel("Wellbeing subscription",new Vector3(-3.82f,1.95f,.8f),-90,new Vector2(2.45f,1.8f),"YOU ARE THRIVING","MOOD 98%   •   REST 100%\nPERSONAL REALITY / OPTIMISED",cyan);
            Panel("Entertainment wall",new Vector3(3.78f,2.05f,1.6f),90,new Vector2(2.45f,1.65f),"DREAM+","YOUR WORLD. MORE BEAUTIFUL.",pink);
            Panel("Weather widget",new Vector3(-1.8f,2.45f,3.9f),0,new Vector2(1.8f,1.2f),"24°","SUNSHINE, AS ALWAYS",cyan);
            Panel("Breakfast suggestion",new Vector3(2.15f,2.4f,3.9f),0,new Vector2(1.3f,1.05f),"NOURISH","YOUR DAILY UPGRADE",amber);
            var art=Digital("Animated Artwork",new Vector3(2.75f,1.8f,-2.2f));
            for(int i=0;i<5;i++)
            {
                var ring=new GameObject(ObjectNames.Short("Orbit "+i)).transform;
                ring.SetParent(art,false);
                ring.localRotation=Quaternion.Euler(i*29,i*43,i*17);
                Ring(ring,"Light orbit",Vector3.zero,.34f+i*.095f,i%2==0?pink:cyan,.025f);
            }
            GreyboxUtil.Primitive("Artificial star",PrimitiveType.Sphere,art,Vector3.zero,Vector3.one*.25f,white,false);
            var ceiling=Digital("Ambient cloud",new Vector3(0,3.63f,-.15f));
            for(int i=0;i<5;i++)
                Box("Suspended light slat",ceiling,new Vector3((i-2)*.62f,0,0),new Vector3(.18f,.025f,4.6f),i%2==0?cyan:pink);
            LightAt(room,new Vector3(0,3.1f,0),new Color(.55f,.78f,1),2.1f,9);
        }

        void BuildExterior()
        {
            var city=new GameObject("ConcreteCity").transform;
            city.SetParent(transform,false);
            Box("Continuous ground",city,new Vector3(0,-.28f,20),new Vector3(30,.44f,34),darkConcrete,true);
            Box("Central pedestrian strip",city,new Vector3(0,-.06f,20),new Vector3(8.4f,.12f,32),concrete,true);
            Box("Landing canopy",city,new Vector3(0,3.7f,6.3f),new Vector3(8,.32f,4),darkConcrete,true);
            Box("Left landing support",city,new Vector3(-3.8f,1.8f,6.3f),new Vector3(.35f,3.6f,.35f),steel,true);
            Box("Right landing support",city,new Vector3(3.8f,1.8f,6.3f),new Vector3(.35f,3.6f,.35f),steel,true);
            for(int side=-1;side<=1;side+=2)
            {
                for(int block=0;block<4;block++)
                {
                    float z=10.8f+block*7.5f;
                    float height=8+(block%3)*2.4f;
                    Vector3 size=new Vector3(5.8f,height,7.2f);
                    Vector3 center=new Vector3(side*7.2f,height*.5f,z);
                    Box("Housing monolith "+side+"/"+block,city,center,size,block%2==0?concrete:paleConcrete,true);
                    var skin=Digital("Pleasure district facade "+side+"/"+block,new Vector3(side*4.285f,height*.5f,z),side<0?-90:90);
                    Box("Colour over concrete",skin,Vector3.zero,new Vector3(7.15f,height,.036f),block%2==0?softPink:softCyan);
                    for(int floor=0;floor<3;floor++)
                    {
                        for(int window=0;window<3;window++)
                        {
                            float wz=z+(window-1)*1.8f;
                            Box("Small sealed window",city,new Vector3(side*4.285f,3.65f+floor*2.2f,wz),new Vector3(.034f,.5f,.34f),glass);
                        }
                        Box("Projected panoramic windows",skin,new Vector3(0,3.65f+floor*2.2f-height*.5f,-.035f),new Vector3(6.8f,.08f,.055f),block%2==0?pink:cyan);
                    }
                    Box("Vertical facade luminaire",skin,new Vector3(-3.36f,0,-.04f),new Vector3(.055f,height,.055f),block%2==0?amber:cyan);
                    Box("Vertical facade luminaire",skin,new Vector3(3.36f,0,-.04f),new Vector3(.055f,height,.055f),block%2==0?amber:cyan);
                    CameraUnit(city,new Vector3(side*4.12f,3.2f,z+2.4f),new Vector3(0,1.5f,z));
                    if(block<3) Wire(city,new Vector3(side*4.12f,3.8f,z-3),new Vector3(side*4.12f,3.45f,z+3.6f));
                }
            }

            var welcome=Digital("Arrival hologram",new Vector3(0,3.25f,9.35f));
            Text("WELCOME TO A BETTER EVERYDAY",welcome,Vector3.zero,.23f,Color.white);
            Box("Welcome underline",welcome,new Vector3(0,-.25f,0),new Vector3(6.5f,.035f,.035f),cyan);
            Panel("City subscription billboard",new Vector3(-4.15f,3.12f,12),-90,new Vector2(4.6f,2.2f),"REALITY+","LIFE, WITHOUT THE ROUGH EDGES.",pink);
            Panel("Architecture advertisement",new Vector3(4.15f,3.5f,17.5f),90,new Vector2(5,2.75f),"FEEL MORE","EVERY SURFACE IS AN OPPORTUNITY",cyan);
            Panel("Reassurance facade",new Vector3(-4.15f,5.65f,24),-90,new Vector2(4.8f,2),"NOTHING TO WORRY ABOUT","YOUR PEACE OF MIND IS OUR PRIORITY",amber);
            Panel("Upgrade kiosk",new Vector3(3.55f,1.7f,12),-18,new Vector2(1.2f,1.8f),"UPGRADE","A BRIGHTER\nPOINT OF VIEW",pink);
            Panel("Social widget",new Vector3(-3.7f,1.8f,15.3f),-90,new Vector2(1.6f,1.6f),"99.9%","NEIGHBOURHOOD\nSATISFACTION",cyan);
            Panel("Alley advertisement",new Vector3(0,4.45f,31.7f),0,new Vector2(5.2f,1.6f),"YOU DESERVE BEAUTY","LET US TAKE CARE OF WHAT YOU SEE",pink);

            var sculpture=Digital("Holo Sunrise",new Vector3(2.9f,3.3f,22.2f));
            for(int i=0;i<4;i++)
            {
                var ring=new GameObject("Sunrisering").transform;
                ring.SetParent(sculpture,false);
                ring.localRotation=Quaternion.Euler(i*22,i*35,i*28);
                Ring(ring,"Solar halo",Vector3.zero,.55f+i*.17f,i%2==0?amber:pink,.05f);
            }
            GreyboxUtil.Primitive("Perfect sun",PrimitiveType.Sphere,sculpture,Vector3.zero,Vector3.one*.65f,amber,false);
            for(int side=-1;side<=1;side+=2)
            {
                for(int i=0;i<3;i++)
                {
                    var plant=Digital("Holo Planter",new Vector3(side*3.7f,0,10+i*8.6f));
                    Box("Projected planter",plant,new Vector3(0,.25f,0),new Vector3(.68f,.5f,.68f),blue);
                    Box("Luminous trunk",plant,new Vector3(0,1.05f,0),new Vector3(.04f,1.55f,.04f),cyan);
                    for(int leaf=0;leaf<4;leaf++)
                    {
                        float a=leaf*Mathf.PI*.5f;
                        var frond=GreyboxUtil.Primitive("Holographic frond",PrimitiveType.Sphere,plant,new Vector3(Mathf.Cos(a)*.26f,1.5f+leaf*.11f,Mathf.Sin(a)*.26f),new Vector3(.35f,.75f,.16f),cyan,false);
                        frond.transform.localRotation=Quaternion.Euler(0,-a*Mathf.Rad2Deg,35);
                    }
                }
            }
            for(int z=10;z<=32;z+=4)
            {
                var arch=Digital("AR canopy section",new Vector3(0,6.25f,z));
                Box("Overhead blue ribbon",arch,Vector3.zero,new Vector3(8.45f,.027f,.1f),z%8==2?pink:cyan);
            }
            for(int side=-1;side<=1;side+=2)
            {
                var strip=Digital("Street projection edge",new Vector3(side*2.65f,.014f,19.4f));
                Box("Continuous projected edge",strip,Vector3.zero,new Vector3(.035f,.022f,27),cyan);
            }

            Box("Distant Checkpoint",city,new Vector3(0,2.9f,36.2f),new Vector3(8.5f,.42f,.55f),concrete,true);
            for(int i=-4;i<=4;i++)
                Box("Checkpoint bars",city,new Vector3(i*.8f,1.3f,36.1f),new Vector3(.065f,2.6f,.12f),steel,true);
            Box("Checkpoint scanner post",city,new Vector3(-3.7f,1.35f,34.8f),new Vector3(.42f,2.7f,.44f),paleConcrete,true);
            Box("Checkpoint scanner post",city,new Vector3(3.7f,1.35f,34.8f),new Vector3(.42f,2.7f,.44f),paleConcrete,true);
            Text("MOVEMENT REQUIRES AUTHORISATION",city,new Vector3(0,2.9f,35.88f),.12f,new Color(.75f,.76f,.7f));
            Text("SECTOR 06 / COMPLIANCE IS CARE",city,new Vector3(0,3.3f,36.15f),.095f,new Color(.6f,.62f,.6f));
            Panel("Checkpoint beautification",new Vector3(0,2.15f,35.72f),0,new Vector2(7.5f,3.2f),"YOUR FUTURE IS BRIGHT","WE WILL SHOW YOU THE WAY",cyan);
            CameraUnit(city,new Vector3(0,3.55f,35.7f),new Vector3(0,1.6f,27));
            CameraUnit(city,new Vector3(-3.75f,3.5f,30.8f),new Vector3(0,1.6f,28));
            CameraUnit(city,new Vector3(3.75f,3.5f,26.8f),new Vector3(0,1.6f,29));
            for(int i=0;i<3;i++) Wire(city,new Vector3(-4.2f,5.3f+i*.13f,22+i*.28f),new Vector3(4.2f,5.1f+i*.13f,22+i*.28f));
            Wire(city,new Vector3(-4.2f,7.6f,31),new Vector3(4.2f,6.9f,31.5f));
            LightAt(city,new Vector3(0,5,13),new Color(.46f,.64f,.8f),2.8f,18);
            LightAt(city,new Vector3(0,5,28),new Color(.65f,.7f,.7f),2.5f,18);
        }

        void CameraUnit(Transform parent,Vector3 position,Vector3 looksAt)
        {
            var unit=new GameObject("Cameras").transform;
            unit.SetParent(parent,false);
            unit.localPosition=position;
            unit.localRotation=Quaternion.LookRotation(looksAt-position,Vector3.up);
            Box("Camera bracket",unit,new Vector3(0,-.12f,-.15f),new Vector3(.08f,.28f,.25f),steel);
            Box("Camera housing",unit,Vector3.zero,new Vector3(.22f,.19f,.45f),paleConcrete);
            Box("Black lens",unit,new Vector3(0,0,.228f),new Vector3(.13f,.11f,.014f),glass);
            Box("Recording indicator",unit,new Vector3(.078f,.057f,.236f),new Vector3(.021f,.015f,.014f),cameraRed);
        }

        void Wire(Transform parent,Vector3 a,Vector3 b)
        {
            var points=new Vector3[9];
            for(int i=0;i<points.Length;i++)
            {
                float t=i/(float)(points.Length-1);
                points[i]=Vector3.Lerp(a,b,t)-Vector3.up*Mathf.Sin(t*Mathf.PI)*.55f;
            }
            GreyboxUtil.Line("Unconcealed utility cable",parent,points,steel,.024f);
        }

        void LightAt(Transform parent,Vector3 p,Color color,float intensity,float range)
        {
            var lamp=new GameObject("Practicalambientlight").transform;
            lamp.SetParent(parent,false); lamp.localPosition=p;
            var light=lamp.gameObject.AddComponent<Light>();
            light.type=LightType.Point; light.color=color; light.intensity=intensity; light.range=range; light.shadows=LightShadows.None;
        }

        void BuildLiminal()
        {
            var space=liminalRoot.transform;
            Box("The space between floors",space,new Vector3(0,-.2f,84),new Vector3(18,.4f,18),steel,true);

            for(int i=0;i<10;i++)
            {
                float a=i*Mathf.PI*2/10;
                Vector3 p=new Vector3(Mathf.Sin(a)*7.4f,2.5f,84+Mathf.Cos(a)*7.4f);
                var pillar=Box("Interrupted column",space,p,new Vector3(.65f,5+i%3,.8f),darkConcrete);
                pillar.transform.localRotation=Quaternion.Euler(i%3*7,i*36,(i%2==0?1:-1)*8);
                Box("Signal sliver",space,p+Vector3.forward*.42f,new Vector3(.04f,4.5f,.02f),i%2==0?cyan:pink);
            }
            for(int i=0;i<5;i++)
            {
                var ring=new GameObject("BrokenOrbit").transform;
                ring.SetParent(space,false); ring.localPosition=new Vector3(0,4.2f,86);
                ring.localRotation=Quaternion.Euler(0,i*27,i*18);
                Ring(ring,"Broken signal orbit",Vector3.zero,2.5f+i*.52f,i%2==0?pink:cyan,.022f);
            }
            LightAt(space,new Vector3(0,4.5f,83),new Color(.42f,.12f,1),3.5f,15);
        }

        public void SetReveal(float amount)
        {
            revealAmount=Mathf.Clamp01(amount);
            if(!hyperrealityRoot) return;
            hyperrealityRoot.SetActive(revealAmount<.999f);
            if(digitalUnits==null || unitPositions==null || unitScales==null || unitRotations==null) return;
            for(int i=0;i<digitalUnits.Length;i++)
            {
                var unit=digitalUnits[i];
                if(!unit || i>=unitPositions.Length || i>=unitScales.Length || i>=unitRotations.Length) continue;
                float seed=Mathf.Repeat(i*.61803399f,.99f);
                float progress=Mathf.Clamp01((revealAmount-seed*.26f)/.73f);
                float fracture=progress*progress;
                unit.localPosition=unitPositions[i]+new Vector3(Mathf.Sin(i*7.1f)*.8f,1.2f+seed*2,Mathf.Cos(i*5.7f)*.8f)*fracture;
                unit.localRotation=unitRotations[i]*Quaternion.Euler(fracture*(seed-.5f)*35,fracture*(.5f-seed)*28,fracture*(seed-.5f)*60);
                unit.localScale=Vector3.Scale(unitScales[i],new Vector3(1+fracture*.18f,1-progress*.78f,1-progress*.58f));
                unit.gameObject.SetActive(progress<.995f);
            }
            if(digitalRenderers==null) return;
            if(revealProperties==null) revealProperties=new MaterialPropertyBlock();
            for(int i=0;i<digitalRenderers.Length;i++)
            {
                var renderer=digitalRenderers[i];
                if(!renderer) continue;
                renderer.GetPropertyBlock(revealProperties);
                revealProperties.SetFloat("_Reveal",revealAmount);
                revealProperties.SetFloat("_Seed",Mathf.Repeat(i*.381966f,1));
                renderer.SetPropertyBlock(revealProperties);
            }
        }
    }
}
