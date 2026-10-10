using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealityPlayground.Story
{

    public sealed class StoryArchitecture : MonoBehaviour
    {
        [Serializable] sealed class Motion
        {
            public Transform target;
            public Vector3 position, scale;
            public Quaternion rotation;
            public float speed, phase;
            public int kind;
        }

        public StoryEnvironment environment;
        [SerializeField] Renderer[] retiredRenderers = Array.Empty<Renderer>();
        [SerializeField] Renderer[] surfaces = Array.Empty<Renderer>();
        [SerializeField] Motion[] motions = Array.Empty<Motion>();
        [SerializeField] int facadeCount, treeCount, dimensionalSignCount, geometryVertexCount;
        [SerializeField] int refinementVersion;
        [SerializeField] Material architecturalMaterial;
        [SerializeField] Transform[] units = Array.Empty<Transform>();
        [SerializeField] Vector3[] unitPositions = Array.Empty<Vector3>();
        [SerializeField] Quaternion[] unitRotations = Array.Empty<Quaternion>();
        [SerializeField] Vector3[] unitScales = Array.Empty<Vector3>();
        readonly List<Renderer> retiring = new List<Renderer>();
        readonly List<Motion> moving = new List<Motion>();
        readonly List<Transform> buildingUnits = new List<Transform>();
        Material material;
        TMP_FontAsset font;
        Material fontMaterial;
        float lastReveal = -1;
        MaterialPropertyBlock properties;

        static readonly Color Cyan = new Color(.035f,.7f,.94f);
        static readonly Color Mint = new Color(.16f,.84f,.45f);
        static readonly Color Coral = new Color(.96f,.16f,.3f);
        static readonly Color Amber = new Color(1,.55f,.16f);
        static readonly Color Cream = new Color(.72f,.8f,.71f);
        static readonly Color Indigo = new Color(.045f,.09f,.2f);
        static readonly Color Rose = new Color(.24f,.09f,.19f);
        static readonly Color Limestone = new Color(.61f,.53f,.405f);
        static readonly Color Sage = new Color(.245f,.285f,.225f);
        static readonly Color Slate = new Color(.205f,.24f,.25f);
        static readonly Color Metal = new Color(.065f,.075f,.08f);
        static readonly Color Timber = new Color(.285f,.19f,.115f);
        static readonly Color LeafGreen = new Color(.15f,.285f,.135f);
        static readonly Color Soil = new Color(.075f,.059f,.043f);

        public int FacadeCount => facadeCount;
        public int TreeCount => treeCount;
        public int DimensionalSignCount => dimensionalSignCount;
        public int AddedRendererCount => surfaces.Length;
        public int GeometryVertexCount => geometryVertexCount;
        public int AnimatedObjectCount => motions.Length;
        public int RefinementVersion => refinementVersion;
        public Renderer[] RetiredRenderers => retiredRenderers;
        public Renderer[] Surfaces => surfaces;
        public bool IsOwnedByDigitalLayer => environment && environment.hyperrealityRoot && transform.IsChildOf(environment.hyperrealityRoot.transform);

        void Awake()=>CaptureAuthoredPoses();
        public void CaptureAuthoredPoses()
        {
            if(units==null)units=Array.Empty<Transform>();
            unitPositions=new Vector3[units.Length];unitRotations=new Quaternion[units.Length];unitScales=new Vector3[units.Length];
            for(int i=0;i<units.Length;i++)
            {
                if(!units[i])continue;
                unitPositions[i]=units[i].localPosition;unitRotations[i]=units[i].localRotation;unitScales[i]=units[i].localScale;
            }
            if(motions!=null)foreach(var motion in motions)
            {
                if(motion==null || !motion.target)continue;
                motion.position=motion.target.localPosition;motion.rotation=motion.target.localRotation;motion.scale=motion.target.localScale;
            }
            lastReveal=-1;
        }

        public static StoryArchitecture Install(RealityStoryDirector story)
        {
            if (!story || !story.environment || !story.environment.hyperrealityRoot) return null;
            var city = story.environment;
            var existing = city.hyperrealityRoot.GetComponentInChildren<StoryArchitecture>(true);
            if (existing) { existing.environment = city; return existing; }
            var group = new GameObject("FacadeOverlays");
            group.transform.SetParent(city.hyperrealityRoot.transform,false);
            var polish = group.AddComponent<StoryArchitecture>();
            polish.environment = city;
            polish.Build();
            city.digitalRenderers = city.hyperrealityRoot.GetComponentsInChildren<Renderer>(true);
            return polish;
        }

        public static StoryArchitecture ApplyRefinement(RealityStoryDirector story,bool force=false)
        {
            if(!story || !story.environment)return null;
            var polish=story.environment.hyperrealityRoot.GetComponentInChildren<StoryArchitecture>(true);
            if(!polish)return Install(story);
            polish.environment=story.environment;
            if(polish.refinementVersion>=2 && !force)return polish;
            polish.EnsureArchitecturalMaterial();
            foreach(var renderer in polish.GetComponentsInChildren<MeshRenderer>(true))
                if(renderer.sharedMaterial && renderer.sharedMaterial.shader.name=="RealityPlayground/StoryHypercityGeometry" && renderer.sharedMaterial.GetFloat("_Naturalness")<.5f)
                {polish.material=renderer.sharedMaterial;break;}
            for(int side=-1;side<=1;side+=2)for(int block=0;block<4;block++)
            {
                var unit=polish.FindUnit("Townhouse "+side+" "+block);
                polish.ReplaceGeneratedMesh(unit,"Townhouse Facade",polish.FacadeGeometry(side,block),polish.architecturalMaterial);
            }
            for(int i=0;i<polish.treeCount;i++)
                polish.ReplaceGeneratedMesh(polish.FindUnit("Holo Tree "+i),"Tree Canopy",polish.TreeGeometry(i),polish.architecturalMaterial);
            polish.ReplaceGeneratedMesh(polish.FindUnit("Bedroom Ad"),"Herb Planter",polish.GardenGeometry(),polish.architecturalMaterial);
            polish.ReplaceGeneratedMesh(polish.FindUnit("Flower Ad"),"Flower Petals",polish.BlossomGeometry(),polish.material);
            polish.geometryVertexCount=0;
            foreach(var filter in polish.GetComponentsInChildren<MeshFilter>(true))
                if(filter.sharedMesh && !filter.GetComponent<TMP_Text>())polish.geometryVertexCount+=filter.sharedMesh.vertexCount;
            polish.surfaces=polish.GetComponentsInChildren<Renderer>(true);
            polish.environment.digitalRenderers=polish.environment.hyperrealityRoot.GetComponentsInChildren<Renderer>(true);
            polish.refinementVersion=2;polish.lastReveal=-1;
            return polish;
        }
        Transform FindUnit(string name)
        {foreach(Transform child in transform)if(ObjectNames.Matches(child.name, name))return child;return null;}
        void ReplaceGeneratedMesh(Transform unit,string meshName,Geometry geometry,Material replacement)
        {
            if(!unit)return;
            foreach(var filter in unit.GetComponentsInChildren<MeshFilter>(true))
            {
                if(!ObjectNames.Matches(filter.name, meshName))continue;
                filter.sharedMesh=geometry.Mesh(meshName);
                var renderer=filter.GetComponent<MeshRenderer>();if(renderer && replacement)renderer.sharedMaterial=replacement;
                break;
            }
        }
        void EnsureArchitecturalMaterial()
        {
            if(!architecturalMaterial)architecturalMaterial=new Material(Shader.Find("RealityPlayground/StoryHypercityGeometry")){name="Facade Surfaces"};
            architecturalMaterial.SetFloat("_Naturalness",1);
            architecturalMaterial.SetFloat("_Glow",0);
            architecturalMaterial.SetFloat("_Roughness",.87f);
        }

        void Build()
        {
            var shader = Shader.Find("RealityPlayground/StoryHypercityGeometry");
            if (!shader) throw new InvalidOperationException("StoryHypercityGeometry shader is required.");
            material = new Material(shader) { name = "Facade Glow" };
            material.SetFloat("_Glow",1.18f);
            material.SetFloat("_Naturalness",0);
            EnsureArchitecturalMaterial();
            foreach (var text in environment.GetComponentsInChildren<StoryWorldText>(true))
            {
                if (!text.UsesDistanceField || !text.DistanceFieldText) continue;
                font = text.DistanceFieldText.font; fontMaterial = text.DistanceFieldText.fontSharedMaterial; break;
            }
            Facades(); Trees(); SpatialSigns(); LivingSculptures(); BedroomGarden();
            retiredRenderers = retiring.ToArray();
            motions = moving.ToArray();
            units = buildingUnits.ToArray();
            unitPositions = new Vector3[units.Length];
            unitRotations = new Quaternion[units.Length];
            unitScales = new Vector3[units.Length];
            for (int i=0;i<units.Length;i++)
            {
                unitPositions[i]=units[i].localPosition;unitRotations[i]=units[i].localRotation;unitScales[i]=units[i].localScale;
            }
            surfaces = GetComponentsInChildren<Renderer>(true);
            refinementVersion=2;
        }

        Transform Unit(string name,Vector3 position,Quaternion rotation)
        {
            var target = new GameObject(ObjectNames.Short(name)).transform;
            target.SetParent(transform,false);target.localPosition=position;target.localRotation=rotation;
            buildingUnits.Add(target);return target;
        }
        Transform UnitAt(string name,Transform source)
        {
            var target=Unit(name,transform.InverseTransformPoint(source.position),Quaternion.Inverse(transform.rotation)*source.rotation);
            target.localScale=source.lossyScale;return target;
        }
        Transform Group(string name,Transform parent,Vector3 position)
        {
            var group=new GameObject(ObjectNames.Short(name)).transform;group.SetParent(parent,false);group.localPosition=position;return group;
        }
        void Animate(Transform target,int kind,float speed,float phase=0)
        {
            moving.Add(new Motion {target=target,kind=kind,speed=speed,phase=phase,position=target.localPosition,rotation=target.localRotation,scale=target.localScale});
        }
        Transform Finish(string name,Transform parent,Geometry geometry,bool natural=false)
        {
            var mesh=geometry.Mesh(name);geometryVertexCount+=mesh.vertexCount;
            return StoryVignetteGeometry.Surface(name,parent,Vector3.zero,mesh,natural?architecturalMaterial:material).transform;
        }
        void Retire(Renderer renderer)
        {
            if(!renderer || !renderer.enabled || retiring.Contains(renderer))return;
            renderer.enabled=false;retiring.Add(renderer);
        }
        void RetireText(Transform target)
        {
            foreach(var text in target.GetComponentsInChildren<TextMesh>(true))
            {
                var own=text.GetComponent<Renderer>();Retire(own);
                var display=text.GetComponent<StoryWorldText>();if(display && display.DistanceFieldText)Retire(display.DistanceFieldText.renderer);
            }
        }
        Transform FindDigital(string exact)
        {
            foreach(Transform child in environment.hyperrealityRoot.transform)if(ObjectNames.Matches(child.name, exact))return child;
            return null;
        }
        void Caption(string text,Transform parent,Vector3 position,float size,Color color)
        {
            var label=GreyboxUtil.Label(text,parent,position,size,color);
            var display=label.gameObject.AddComponent<StoryWorldText>();
            if(font)display.SetDistanceFieldFont(font,fontMaterial);
        }

        void Facades()
        {
            for(int side=-1;side<=1;side+=2)
            for(int block=0;block<4;block++)
            {
                var previous=FindDigital("Pleasure district facade "+side+"/"+block);
                if(!previous)continue;
                var facade=UnitAt("Townhouse "+side+" "+block,previous);
                Finish("Townhouse Facade",facade,FacadeGeometry(side,block),true);facadeCount++;
            }
        }

        Geometry FacadeGeometry(int side,int block)
        {
            float height=8+(block%3)*2.4f,baseY=-height*.5f;
            var g=new Geometry();

            Color plaster=block%3==0?new Color(.56f,.435f,.315f):block%3==1?new Color(.47f,.405f,.32f):new Color(.435f,.44f,.35f);
            Color joinery=block%2==0?Sage:Slate;

            g.Box(new Vector3(0,baseY+1.16f+(height-1.16f)*.5f,-.065f),new Vector3(6.98f,height-1.16f,.03f),plaster);
            g.Box(new Vector3(0,baseY+.58f,-.065f),new Vector3(6.98f,1.16f,.03f),plaster*.87f);
            g.Box(new Vector3(0,height*.5f-.14f,-.12f),new Vector3(7.04f,.18f,.18f),Limestone);
            g.Box(new Vector3(0,baseY+2.17f,-.091f),new Vector3(6.99f,.11f,.082f),Limestone*.85f);
            for(int row=0;row<3;row++)for(int bay=-1;bay<=1;bay+=2)
            {
                float x=bay*1.65f,y=baseY+3.16f+row*2.1f;
                bool inhabited=(block+row+(bay>0?1:0)+(side>0?1:0))%3==0;

                g.Box(new Vector3(x,y,-.103f),new Vector3(1.58f,1.48f,.06f),Metal*.75f);
                g.Frame(new Vector3(x,y,-.165f),new Vector2(1.57f,1.47f),.085f,.085f,Limestone);
                for(int pane=-1;pane<=1;pane+=2)
                {
                    g.emission=inhabited?.32f:0;
                    g.texture=inhabited?.2f:.07f;
                    Color glass=inhabited?new Color(.39f,.285f,.165f):new Color(.115f,.15f,.157f);
                    g.Box(new Vector3(x+pane*.344f,y,-.147f),new Vector3(.65f,1.30f,.02f),glass);
                    g.emission=0;g.texture=1;

                    if(inhabited)g.Box(new Vector3(x+pane*.585f,y,-.177f),new Vector3(.14f,1.28f,.023f),new Color(.38f,.325f,.25f));
                }
                g.Frame(new Vector3(x,y,-.184f),new Vector2(1.40f,1.30f),.035f,.037f,joinery);
                g.Box(new Vector3(x,y,-.187f),new Vector3(.044f,1.31f,.039f),joinery);
                g.Box(new Vector3(x,y+.24f,-.187f),new Vector3(1.4f,.034f,.038f),joinery);
                g.Box(new Vector3(x,y-.79f,-.19f),new Vector3(1.76f,.105f,.3f),Limestone*.9f);
                bool balcony=row==0 && bay==((block+side)%2==0?1:-1);
                if(!balcony)continue;
                float floor=y-.82f;
                g.Box(new Vector3(x,floor,-.48f),new Vector3(1.87f,.13f,.72f),Limestone*.76f);
                for(int edge=-1;edge<=1;edge+=2)
                {
                    g.Rod(new Vector3(x+edge*.865f,floor+.72f,-.15f),new Vector3(x+edge*.865f,floor+.72f,-.81f),.021f,Metal);
                    g.Rod(new Vector3(x+edge*.865f,floor+.04f,-.81f),new Vector3(x+edge*.865f,floor+.72f,-.81f),.022f,Metal);
                }
                g.Rod(new Vector3(x-.87f,floor+.72f,-.81f),new Vector3(x+.87f,floor+.72f,-.81f),.026f,Metal);
                for(int rail=0;rail<6;rail++)g.Rod(new Vector3(x-.72f+rail*.288f,floor+.05f,-.81f),new Vector3(x-.72f+rail*.288f,floor+.72f,-.81f),.011f,Metal);

                g.Box(new Vector3(x,y+.86f,-.35f),new Vector3(1.86f,.043f,.54f),joinery,Quaternion.Euler(-9,0,0));
                g.Box(new Vector3(x,y+.795f,-.602f),new Vector3(1.86f,.14f,.023f),joinery*.89f);
                var soilCenter=new Vector3(x+.53f,floor+.29f,-.47f);
                g.Box(soilCenter-Vector3.up*.12f,new Vector3(.38f,.24f,.27f),Timber);
                g.Box(soilCenter,new Vector3(.335f,.023f,.23f),Soil);
                Herb(g,soilCenter-Vector3.up*.015f,.31f,block);
            }
            return g;
        }

        void Trees()
        {
            var previous=new List<Transform>();
            foreach(Transform child in environment.hyperrealityRoot.transform)if(ObjectNames.StartsWith(child.name, "Holo Planter",StringComparison.Ordinal))previous.Add(child);
            for(int i=0;i<previous.Count;i++)
            {
                var tree=UnitAt("Holo Tree "+i,previous[i]);
                var oldPlanter=ObjectNames.Find(previous[i], "Projected planter");
                if(oldPlanter)
                {

                    tree.position+=previous[i].TransformVector(oldPlanter.localPosition-Vector3.up*.25f);
                }
                foreach(var renderer in previous[i].GetComponentsInChildren<Renderer>(true))Retire(renderer);
                Finish("Tree Canopy",tree,TreeGeometry(i),true);treeCount++;
            }
        }

        Geometry TreeGeometry(int index)
        {
            var g=new Geometry();
            g.Box(new Vector3(0,.2f,0),new Vector3(.66f,.4f,.66f),Slate);
            g.Box(new Vector3(0,.405f,0),new Vector3(.57f,.025f,.57f),Soil);
            g.windBase=.39f;g.coherentWind=true;
            var path=new Vector3[13];
            for(int k=0;k<path.Length;k++)
            {
                float t=k/(float)(path.Length-1);
                path[k]=new Vector3((Mathf.Sin(t*3.2f+index)-Mathf.Sin(index))*.075f,.39f+t*1.7f,(Mathf.Sin(t*3.9f+index)-Mathf.Sin(index))*.06f);
            }
            g.Curve(path,.048f,.013f,Timber);
            for(int branch=0;branch<7;branch++)
            {
                float a=branch*2.39996f+index*.63f;

                float sample=4.2f+branch*.85f;
                int segment=Mathf.FloorToInt(sample);
                Vector3 start=Vector3.Lerp(path[segment],path[segment+1],sample-segment);
                Vector3 end=start+new Vector3(Mathf.Cos(a),.36f,Mathf.Sin(a))*(.42f+branch*.02f);
                var branchPath=new Vector3[6];
                for(int k=0;k<branchPath.Length;k++)
                {
                    float t=k/(float)(branchPath.Length-1);
                    branchPath[k]=Vector3.Lerp(start,end,t)+Vector3.up*Mathf.Sin(t*Mathf.PI)*.1f;
                }
                g.Curve(branchPath,.024f,.007f,Timber);
                for(int leaf=0;leaf<5;leaf++)
                {
                    float la=leaf*1.256637f+a;
                    Vector3 direction=new Vector3(Mathf.Cos(la)*.31f,.17f+Mathf.Sin(leaf*1.4f)*.1f,Mathf.Sin(la)*.31f);
                    Vector3 petiole=end+direction*.18f;
                    g.Rod(end,petiole,.007f,Timber);
                    g.Leaf(petiole,direction*.82f,.105f,leaf%2==0?LeafGreen:LeafGreen*1.18f,0);
                }
                if(branch%3!=0)continue;
                Vector3 bloom=end+Vector3.up*.115f;
                g.Rod(end,bloom,.009f,LeafGreen);
                g.Ellipsoid(bloom,Vector3.one*.034f,new Color(.49f,.41f,.24f),8,4);
                for(int petal=0;petal<5;petal++)
                {
                    float pa=petal*Mathf.PI*2/5;
                    g.Leaf(bloom,new Vector3(Mathf.Cos(pa)*.12f,.026f,Mathf.Sin(pa)*.12f),.06f,new Color(.46f,.31f,.305f),0);
                }
            }
            g.coherentWind=false;return g;
        }

        static void Herb(Geometry g,Vector3 root,float height,int seed)
        {
            g.coherentWind=true;g.windBase=root.y;
            for(int stem=0;stem<3;stem++)
            {
                float a=stem*2.39996f+seed*.63f;
                Vector3 end=root+new Vector3(Mathf.Cos(a)*height*.24f,height*(.8f+stem*.1f),Mathf.Sin(a)*height*.24f);
                g.Rod(root,end,.006f,LeafGreen*.8f);
                for(int level=0;level<3;level++)
                {
                    float t=.28f+level*.22f;
                    Vector3 joint=Vector3.Lerp(root,end,t);
                    for(int side=-1;side<=1;side+=2)
                    {
                        Vector3 d=new Vector3(Mathf.Cos(a)*side,height*.9f,Mathf.Sin(a)*side).normalized*height*.47f;
                        Vector3 leafRoot=joint+d*.1f;
                        g.Rod(joint,leafRoot,.004f,LeafGreen);
                        g.Leaf(leafRoot,d*.9f,height*.15f,level%2==0?LeafGreen:LeafGreen*1.17f,0);
                    }
                }
            }
            g.coherentWind=false;
        }

        void SpatialSigns()
        {
            ReplaceSign("City subscription billboard","REALITY+","EVERY DAY / BEAUTIFULLY FILTERED",4.4f,.46f,Coral);
            ReplaceSign("Architecture advertisement","FEEL MORE","COMFORT, RENDERED FOR YOU",4.7f,.47f,Cyan);
            ReplaceSign("Entertainment wall","DREAM+","A WORLD THAT NEVER SLEEPS",2.25f,.32f,Coral);
            ReplaceSign("Alley advertisement","BEAUTY","YOUR VIEW / OUR PROMISE",4.2f,.54f,Coral);
        }
        void ReplaceSign(string original,string title,string subtitle,float width,float size,Color color)
        {
            var source=FindDigital(original);if(!source)return;
            var sign=UnitAt("Sign "+title,source);

            float baseline=original=="Entertainment wall"?1.1f:original=="Architecture advertisement"?-1.67f:original=="Alley advertisement"?-1.64f:-1.12f;
            RetireText(source);

            foreach(Transform child in source)
            {
                if(ObjectNames.Matches(child.name, "Glass projection"))Retire(child.GetComponent<Renderer>());
            }
            var carrier=new Geometry();
            carrier.Box(new Vector3(0,baseline-.25f,-.028f),new Vector3(width+.12f,.32f,.045f),Indigo);
            carrier.Box(new Vector3(0,baseline-.075f,-.06f),new Vector3(width+.14f,.025f,.045f),color);
            for(int edge=-1;edge<=1;edge+=2)
            {
                carrier.Rod(new Vector3(edge*(width*.5f+.055f),baseline-.41f,-.06f),new Vector3(edge*(width*.5f+.055f),baseline-.1f,-.06f),.018f,Cream);
                for(int dash=0;dash<3;dash++)carrier.Box(new Vector3(edge*(width*.5f-.09f),baseline-.34f+dash*.07f,-.064f),new Vector3(.075f,.025f,.025f),color);
            }
            Finish("Sign Frame",sign,carrier);
            var letters=Group("Letters",sign,new Vector3(0,baseline,-.13f));
            var strokes=new Geometry();
            strokes.Lettering(title,size,color,Mathf.Min(width,(title.Length*.77f-.15f)*size));
            Finish("Tube Letters",letters,strokes);
            Animate(letters,0,.55f,dimensionalSignCount*1.2f);
            Caption(subtitle,sign,new Vector3(0,baseline-.24f,-.066f),original=="Entertainment wall"?.078f:.112f,new Color(.66f,.93f,1));
            dimensionalSignCount++;
        }

        void LivingSculptures()
        {

            var advert=FindDigital("Architecture advertisement");
            if(advert)
            {
                var flower=UnitAt("Flower Ad",advert);
                var core=Group("Flower Sculpture",flower,new Vector3(0,1.48f,-.76f));
                Finish("Flower Petals",core,BlossomGeometry());Animate(core,1,13);
                var orbit=Group("Tree Orbits",flower,new Vector3(0,1.48f,-.79f));
                var ribbons=new Geometry();
                ribbons.Ring(Vector3.zero,.88f,.018f,Cyan,Quaternion.Euler(37,19,0),48);
                ribbons.Ring(Vector3.zero,.98f,.012f,Mint,Quaternion.Euler(-31,-26,0),48);
                for(int i=0;i<8;i++)
                {
                    float a=i*Mathf.PI*.25f;ribbons.Ellipsoid(new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*.88f,Vector3.one*.033f,Amber,6,3);
                }
                Finish("Pollen Orbits",orbit,ribbons);Animate(orbit,2,-18);
            }

            var sun=FindDigital("Holo Sunrise");
            if(sun)
            {
                var ribbon=UnitAt("Sun Ribbon",sun);
                var sculpture=Group("Flowing Ribbon",ribbon,new Vector3(0,.4f,0));
                var g=new Geometry();
                g.Ribbon(1.07f,.2f,Coral,Amber,96);
                Finish("Ribbon",sculpture,g);Animate(sculpture,3,11);
            }

            var source=FindDigital("Pleasure district facade -1/1");
            if(source)
            {
                var record=UnitAt("Family Portrait",source);
                var portraits=new Geometry();
                for(int i=0;i<3;i++)
                {
                    var p=new Vector3(-2.55f+i*2.55f,1.6f,-.28f);
                    portraits.Box(p,new Vector3(.55f,.73f,.028f),Indigo);
                    portraits.Frame(p+Vector3.back*.025f,new Vector2(.61f,.79f),.04f,.07f,Amber);
                    for(int person=0;person<2;person++)
                    {
                        portraits.Ellipsoid(p+new Vector3(-.115f+person*.22f,.11f,-.039f),new Vector3(.067f,.077f,.025f),Cream,8,4);
                        portraits.Ellipsoid(p+new Vector3(-.115f+person*.22f,-.11f,-.044f),new Vector3(.092f,.15f,.027f),person==0?Coral:Cyan,8,4);
                    }
                }
                Finish("Family Portraits",record,portraits);
            }
        }

        void BedroomGarden()
        {
            var room=Unit("Bedroom Ad",new Vector3(-3.83f,2.2f,2.93f),Quaternion.Euler(0,-90,0));
            Finish("Herb Planter",room,GardenGeometry(),true);
        }
        Geometry GardenGeometry()
        {
            var g=new Geometry();
            g.Frame(Vector3.zero,new Vector2(1.12f,1.55f),.048f,.1f,Sage);
            g.Box(new Vector3(0,-.77f,-.19f),new Vector3(1.21f,.09f,.5f),Timber);
            for(int i=0;i<3;i++)
            {
                var p=new Vector3((i-1)*.34f,-.61f,-.21f);
                g.Ellipsoid(p,new Vector3(.112f,.13f,.112f),i==1?new Color(.30f,.29f,.25f):new Color(.28f,.16f,.12f),10,5);
                Vector3 soil=p+Vector3.up*.112f;
                g.Ellipsoid(soil,new Vector3(.078f,.012f,.078f),Soil,10,3);
                Herb(g,soil-Vector3.up*.01f,.29f+i*.035f,i);
            }
            return g;
        }
        Geometry BlossomGeometry()
        {
            var g=new Geometry();
            Vector3 centre=new Vector3(0,0,-.09f);
            g.Ellipsoid(centre,new Vector3(.19f,.19f,.125f),Amber,12,6);
            for(int layer=0;layer<3;layer++)for(int i=0;i<9;i++)
            {
                float a=(i*40+layer*19)*Mathf.Deg2Rad;

                Vector3 root=centre+new Vector3(Mathf.Cos(a),Mathf.Sin(a),0)*(.05f+layer*.025f);
                Vector3 direction=new Vector3(Mathf.Cos(a)*(.57f-layer*.13f),Mathf.Sin(a)*(.57f-layer*.13f),-.15f-layer*.065f);
                g.Leaf(root,direction,.21f-layer*.035f,layer%2==0?Coral:Amber,0,Vector3.forward);
            }
            return g;
        }

        void Update()=>Step(Time.time);
        public void Step(float time)
        {
            float reveal=environment?environment.RevealAmount:0;
            foreach(var motion in motions)
            {
                if(!motion.target)continue;
                float t=time*motion.speed+motion.phase;
                var p=motion.position;var r=motion.rotation;var s=motion.scale;
                switch(motion.kind)
                {
                    case 0:p+=Vector3.up*Mathf.Sin(t)*.025f;break;
                    case 1:r*=Quaternion.Euler(Mathf.Sin(t*.017f)*13,Mathf.Sin(t*.012f)*19,t);s*=1+Mathf.Sin(t*.12f)*.035f;break;
                    case 2:r*=Quaternion.Euler(0,t*.36f,t);break;
                    case 3:r*=Quaternion.Euler(t*.35f,t,Mathf.Sin(t*.018f)*17);break;
                }
                motion.target.localPosition=p;motion.target.localRotation=r;motion.target.localScale=s;
            }
            if(Mathf.Abs(lastReveal-reveal)<.0001f)return;
            lastReveal=reveal;
            for(int i=0;i<units.Length;i++)
            {
                if(!units[i])continue;
                float seed=Mathf.Repeat(i*.61803399f,.99f);
                float p=Mathf.Clamp01((reveal-seed*.24f)/.76f);float fracture=p*p;
                units[i].localPosition=unitPositions[i]+new Vector3(Mathf.Sin(i*7.1f)*.32f,1+seed*1.4f,Mathf.Cos(i*4.3f)*.35f)*fracture;
                units[i].localRotation=unitRotations[i]*Quaternion.Euler(fracture*(seed-.5f)*22,fracture*(.5f-seed)*23,fracture*(seed-.5f)*32);
                units[i].localScale=Vector3.Scale(unitScales[i],new Vector3(1+fracture*.12f,1-p*.7f,1-p*.45f));
                units[i].gameObject.SetActive(p<.995f);
            }
            if(properties==null)properties=new MaterialPropertyBlock();
            for(int i=0;i<surfaces.Length;i++)
            {
                var surface=surfaces[i];if(!surface)continue;
                surface.GetPropertyBlock(properties);properties.SetFloat("_Reveal",reveal);properties.SetFloat("_Seed",Mathf.Repeat(i*.381966f,1));surface.SetPropertyBlock(properties);
            }
        }

        sealed class Geometry
        {
            readonly List<Vector3> vertices=new List<Vector3>();
            readonly List<Color> colors=new List<Color>();
            readonly List<Vector2> surfaceData=new List<Vector2>();
            readonly List<int> triangles=new List<int>();
            public float emission,texture=1,windBase;
            public bool coherentWind;
            static readonly Vector3[] Cube={new Vector3(-1,-1,-1),new Vector3(1,-1,-1),new Vector3(1,1,-1),new Vector3(-1,1,-1),new Vector3(-1,-1,1),new Vector3(1,-1,1),new Vector3(1,1,1),new Vector3(-1,1,1)};
            static readonly int[] Faces={0,3,2,1,5,6,7,4,4,7,3,0,1,2,6,5,3,7,6,2,4,0,1,5};
            void Vertex(Vector3 point,Color color,float wind=0)
            {
                vertices.Add(point);
                float height=Mathf.Clamp01((point.y-windBase)*.62f);
                color.a=coherentWind?height*height:wind;
                colors.Add(color);surfaceData.Add(new Vector2(emission,texture));
            }
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,float wind=0)
            {
                int n=vertices.Count;Vertex(a,color,wind);Vertex(b,color,wind);Vertex(c,color,wind);Vertex(d,color,wind);
                triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);triangles.Add(n);triangles.Add(n+2);triangles.Add(n+3);
            }
            public void Box(Vector3 p,Vector3 size,Color color,Quaternion rotation=default)
            {
                if(rotation==default)rotation=Quaternion.identity;
                for(int f=0;f<6;f++)Quad(p+rotation*Vector3.Scale(Cube[Faces[f*4]],size*.5f),p+rotation*Vector3.Scale(Cube[Faces[f*4+1]],size*.5f),p+rotation*Vector3.Scale(Cube[Faces[f*4+2]],size*.5f),p+rotation*Vector3.Scale(Cube[Faces[f*4+3]],size*.5f),color);
            }
            public void Frame(Vector3 p,Vector2 size,float width,float depth,Color color)
            {
                Box(p+Vector3.up*size.y*.5f,new Vector3(size.x+width,width,depth),color);Box(p-Vector3.up*size.y*.5f,new Vector3(size.x+width,width,depth),color);
                Box(p+Vector3.right*size.x*.5f,new Vector3(width,size.y,depth),color);Box(p-Vector3.right*size.x*.5f,new Vector3(width,size.y,depth),color);
            }
            public void Rod(Vector3 a,Vector3 b,float radius,Color color)
            {
                Vector3 along=b-a;if(along.sqrMagnitude<.000001f)return;
                var rotation=Quaternion.FromToRotation(Vector3.up,along.normalized);
                for(int i=0;i<6;i++)
                {
                    float t=i*Mathf.PI/3,u=(i+1)*Mathf.PI/3;
                    Vector3 one=rotation*new Vector3(Mathf.Cos(t)*radius,0,Mathf.Sin(t)*radius),two=rotation*new Vector3(Mathf.Cos(u)*radius,0,Mathf.Sin(u)*radius);
                    Quad(a+one,b+one,b+two,a+two,color);
                }
            }
            public void Curve(Vector3[] points,float start,float end,Color color)
            {
                const int sides=6;
                var rings=new Vector3[points.Length*sides];
                for(int i=0;i<points.Length;i++)
                {
                    var direction=(points[Mathf.Min(i+1,points.Length-1)]-points[Mathf.Max(i-1,0)]).normalized;
                    var rotation=Quaternion.FromToRotation(Vector3.up,direction);float radius=Mathf.Lerp(start,end,i/(float)(points.Length-1));
                    for(int side=0;side<sides;side++){float a=side*Mathf.PI*2/sides;rings[i*sides+side]=points[i]+rotation*new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);}
                }
                for(int i=0;i<points.Length-1;i++)for(int side=0;side<sides;side++)
                {
                    int next=(side+1)%sides;Quad(rings[i*sides+side],rings[(i+1)*sides+side],rings[(i+1)*sides+next],rings[i*sides+next],color);
                }
            }
            public void Ring(Vector3 center,float radius,float width,Color color,Quaternion rotation,int segments=32)
            {
                for(int i=0;i<segments;i++)
                {
                    float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;
                    Rod(center+rotation*new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0),center+rotation*new Vector3(Mathf.Cos(b)*radius,Mathf.Sin(b)*radius,0),width,color);
                }
            }
            public void Ellipsoid(Vector3 center,Vector3 scale,Color color,int sides=8,int rows=4)
            {
                int first=vertices.Count;
                for(int row=0;row<=rows;row++)for(int col=0;col<=sides;col++)
                {
                    float a=col*Mathf.PI*2/sides,p=row*Mathf.PI/rows;
                    Vertex(center+Vector3.Scale(new Vector3(Mathf.Sin(p)*Mathf.Cos(a),Mathf.Cos(p),Mathf.Sin(p)*Mathf.Sin(a)),scale),color);
                }
                for(int row=0;row<rows;row++)for(int col=0;col<sides;col++)
                {
                    int a=first+row*(sides+1)+col,b=a+sides+1;
                    triangles.Add(a);triangles.Add(a+1);triangles.Add(b);triangles.Add(a+1);triangles.Add(b+1);triangles.Add(b);
                }
            }
            public void Leaf(Vector3 origin,Vector3 direction,float width,Color color,float wind,Vector3 normal=default)
            {
                if(normal==default)normal=Vector3.up;
                var side=Vector3.Cross(direction,normal).normalized;if(side.sqrMagnitude<.1f)side=Vector3.right;
                var n=Vector3.Cross(side,direction).normalized;
                const int segments=5;
                for(int step=0;step<segments;step++)
                {
                    float a=step/(float)segments,b=(step+1)/(float)segments;
                    Vector3 ca=origin+direction*a+n*(Mathf.Sin(a*Mathf.PI)*width*.65f),cb=origin+direction*b+n*(Mathf.Sin(b*Mathf.PI)*width*.65f);
                    float wa=Mathf.Sin(a*Mathf.PI)*width,wb=Mathf.Sin(b*Mathf.PI)*width;

                    Quad(ca+n*wa*.23f,cb+n*wb*.23f,cb-side*wb,ca-side*wa,color,wind);
                    Quad(ca+side*wa,cb+side*wb,cb+n*wb*.23f,ca+n*wa*.23f,color*.83f,wind);
                    Quad(ca-side*wa,cb-side*wb,cb+side*wb,ca+side*wa,color*.62f,wind);
                }
            }
            public void Ribbon(float radius,float width,Color one,Color two,int steps)
            {
                for(int i=0;i<steps;i++)
                {
                    float a=i*Mathf.PI*2/steps,b=(i+1)*Mathf.PI*2/steps;
                    Vector3 ca=new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,Mathf.Sin(a*3)*.32f),cb=new Vector3(Mathf.Cos(b)*radius,Mathf.Sin(b)*radius,Mathf.Sin(b*3)*.32f);
                    Vector3 wa=new Vector3(Mathf.Cos(a)*Mathf.Cos(a*1.5f),Mathf.Sin(a)*Mathf.Cos(a*1.5f),Mathf.Sin(a*1.5f))*width,wb=new Vector3(Mathf.Cos(b)*Mathf.Cos(b*1.5f),Mathf.Sin(b)*Mathf.Cos(b*1.5f),Mathf.Sin(b*1.5f))*width;
                    Color color=Color.Lerp(one,two,Mathf.Sin(a*2)*.5f+.5f);
                    Quad(ca-wa,cb-wb,cb+wb,ca+wa,color);
                    Quad(ca+wa,cb+wb,cb-wb,ca-wa,color*.72f);
                    Rod(ca+wa,cb+wb,.011f,Cream);Rod(ca-wa,cb-wb,.011f,one);
                }
            }
            public void Lettering(string text,float height,Color color,float maxWidth)
            {
                float advance=height*.77f,total=(text.Length*.77f-.15f)*height;
                float sx=Mathf.Min(1,maxWidth/total);
                for(int i=0;i<text.Length;i++)
                {
                    string[] lines=Glyph(text[i]);
                    foreach(var line in lines)
                    {
                        var values=line.Split(',');
                        var a=new Vector3((float.Parse(values[0],System.Globalization.CultureInfo.InvariantCulture)*height+i*advance-total*.5f)*sx,float.Parse(values[1],System.Globalization.CultureInfo.InvariantCulture)*height,0);
                        var b=new Vector3((float.Parse(values[2],System.Globalization.CultureInfo.InvariantCulture)*height+i*advance-total*.5f)*sx,float.Parse(values[3],System.Globalization.CultureInfo.InvariantCulture)*height,0);

                        Box((a+b)*.5f+Vector3.forward*.045f,new Vector3(height*.072f,Vector3.Distance(a,b)+height*.07f,.1f),Indigo,Quaternion.FromToRotation(Vector3.up,(b-a).normalized));
                        Rod(a,b,height*.032f,color);Ellipsoid(a,Vector3.one*height*.034f,color,6,3);Ellipsoid(b,Vector3.one*height*.034f,color,6,3);
                    }
                }
            }
            static string[] Glyph(char c)
            {
                switch(c)
                {
                    case 'A':return new[]{"0,0,.3,1",".3,1,.6,0",".14,.42,.46,.42"};
                    case 'B':return new[]{"0,0,0,1","0,1,.44,1",".44,1,.6,.78",".6,.78,.44,.53",".44,.53,0,.53",".44,.53,.6,.25",".6,.25,.44,0",".44,0,0,0"};
                    case 'D':return new[]{"0,0,0,1","0,1,.37,1",".37,1,.6,.78",".6,.78,.6,.22",".6,.22,.37,0",".37,0,0,0"};
                    case 'E':return new[]{"0,0,0,1","0,1,.6,1","0,.5,.49,.5","0,0,.6,0"};
                    case 'F':return new[]{"0,0,0,1","0,1,.6,1","0,.52,.47,.52"};
                    case 'I':return new[]{".05,1,.55,1",".3,1,.3,0",".05,0,.55,0"};
                    case 'L':return new[]{"0,1,0,0","0,0,.6,0"};
                    case 'M':return new[]{"0,0,0,1","0,1,.3,.49",".3,.49,.6,1",".6,1,.6,0"};
                    case 'O':return new[]{".13,0,.47,0",".47,0,.6,.17",".6,.17,.6,.83",".6,.83,.47,1",".47,1,.13,1",".13,1,0,.83","0,.83,0,.17","0,.17,.13,0"};
                    case 'R':return new[]{"0,0,0,1","0,1,.45,1",".45,1,.6,.78",".6,.78,.45,.55",".45,.55,0,.55",".3,.55,.6,0"};
                    case 'T':return new[]{"0,1,.6,1",".3,1,.3,0"};
                    case 'U':return new[]{"0,1,0,.17","0,.17,.13,0",".13,0,.47,0",".47,0,.6,.17",".6,.17,.6,1"};
                    case 'Y':return new[]{"0,1,.3,.5",".6,1,.3,.5",".3,.5,.3,0"};
                    case '+':return new[]{".3,.2,.3,.8","0,.5,.6,.5"};
                    default:return Array.Empty<string>();
                }
            }
            public Mesh Mesh(string name)
            {
                var mesh=new Mesh{name=name};if(vertices.Count>65535)mesh.indexFormat=IndexFormat.UInt32;
                mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,surfaceData);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();

                var bounds=mesh.bounds;bounds.Expand(.14f);mesh.bounds=bounds;
                return mesh;
            }
        }
    }
}
