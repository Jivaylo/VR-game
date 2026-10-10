using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealityPlayground.Story
{

    public sealed class StoryCyberspace : MonoBehaviour
    {
        [Serializable] sealed class Motion
        {
            public Transform target;
            public Vector3 position, scale, axis;
            public Quaternion rotation;
            public float speed, phase, drift;
        }

        [SerializeField] StoryAngel angel;
        [SerializeField] Transform arrival, anchor;
        [SerializeField] Renderer[] surfaces = Array.Empty<Renderer>();
        [SerializeField] Motion[] motions = Array.Empty<Motion>();
        [SerializeField] Renderer[] retiredSurfaces = Array.Empty<Renderer>();
        [SerializeField] Light[] retiredLights = Array.Empty<Light>();
        [SerializeField] float span;
        [SerializeField] bool runtimeAnimate = true;
        MaterialPropertyBlock properties;
        float age;
        bool displayVisible=true;
        static readonly int VoiceId = Shader.PropertyToID("_Voice");
        static readonly int AccentId = Shader.PropertyToID("_Accent");
        public int SurfaceCount => surfaces.Length;
        public int AnimatedPartCount => motions.Length;
        public int RetiredSurfaceCount => retiredSurfaces.Length;
        public bool RuntimeAnimate { get => runtimeAnimate; set => runtimeAnimate = value; }

        public static StoryCyberspace Install(RealityStoryDirector story)
        {
            if (!story || !story.environment || !story.environment.liminalRoot || !story.angel) return null;
            var space = story.environment.liminalRoot.transform;
            var existing = space.GetComponentInChildren<StoryCyberspace>(true);
            if (existing)
            {
                existing.angel = story.angel;
                existing.arrival = story.environment.liminalSpot;
                existing.anchor = story.angel.transform;
                return existing;
            }
            var root = new GameObject("Cyberspace");
            root.transform.SetParent(space, false);
            var result = root.AddComponent<StoryCyberspace>();
            result.angel = story.angel;
            result.arrival = story.environment.liminalSpot;
            result.anchor = story.angel.transform;
            result.Align();
            result.Build();
            result.RetireRoomSurfaces(space);
            return result;
        }

        void Align()
        {
            Vector3 start = arrival ? arrival.position : anchor.position - anchor.forward * 6;
            Vector3 forward = Vector3.ProjectOnPlane(anchor.position - start, Vector3.up);
            span = Mathf.Max(3, forward.magnitude);
            if (forward.sqrMagnitude < .01f) forward = anchor.forward;
            transform.SetPositionAndRotation(start, Quaternion.LookRotation(forward.normalized, Vector3.up));
        }

        void RetireRoomSurfaces(Transform space)
        {
            var retired = new List<Renderer>();
            foreach (var renderer in space.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.transform.IsChildOf(transform)) continue;
                string n = renderer.name;
                if (!ObjectNames.Matches(n, "The space between floors") && !ObjectNames.Matches(n, "Interrupted column") && !ObjectNames.Matches(n, "Signal sliver") && !ObjectNames.Matches(n, "Broken signal orbit")) continue;
                retired.Add(renderer);

                renderer.enabled = false;
            }
            retiredSurfaces = retired.ToArray();
            retiredLights=space.GetComponentsInChildren<Light>(true);
            foreach(var light in retiredLights)if(light)light.enabled=false;
        }

        void Build()
        {
            var shader = Shader.Find("RealityPlayground/StoryCyberspace");
            if (!shader) throw new InvalidOperationException("The StoryCyberspace shader is missing.");
            var dark = new Material(shader) { name = "Cyberspace Dark" };
            dark.SetFloat("_Flow", 0); dark.SetFloat("_Glow", 0);
            var signal = new Material(shader) { name = "Spectral Material" };
            signal.SetFloat("_Flow", 1); signal.SetFloat("_Glow", 1.45f);
            var dust = new Material(shader) { name = "Data Material" };
            dust.SetFloat("_Flow", .6f); dust.SetFloat("_Glow", 1.1f); dust.SetFloat("_Drift", .6f);
            var records = new List<Motion>();

            var voidMesh = new Batch();
            const int rings = 18, columns = 32;
            Vector3 center = new Vector3(0, 5, span * .5f);
            for (int y = 0; y < rings; y++) for (int x = 0; x < columns; x++)
            {
                float v0 = y / (float)rings, v1 = (y + 1f) / rings;
                float a0 = x * Mathf.PI * 2 / columns, a1 = (x + 1f) * Mathf.PI * 2 / columns;
                var c0 = Color.Lerp(new Color(.009f,.002f,.021f),new Color(.001f,.003f,.009f),v0);
                var c1 = Color.Lerp(new Color(.009f,.002f,.021f),new Color(.001f,.003f,.009f),v1);
                voidMesh.Quad(center+Sphere(v0,a0)*44,center+Sphere(v0,a1)*44,center+Sphere(v1,a1)*44,center+Sphere(v1,a0)*44,c0,c0,c1,c1);
            }
            voidMesh.Finish("Cyberspace Sky", transform, dark);

            var platforms = new Batch(); var seams = new Batch();
            AddIsland(platforms,seams,new Vector3(0,-.055f,0),new Vector3(3.3f,.09f,3),Quaternion.identity,0);
            AddIsland(platforms,seams,new Vector3(0,-.11f,span),new Vector3(2.55f,.14f,2.8f),Quaternion.Euler(0,16,0),1);

            for(int i=0;i<26;i++)
            {
                float a=i*2.399963f, radius=3.2f+(i%5)*1.1f;
                var p=new Vector3(Mathf.Cos(a)*radius,-.18f-(i%4)*.55f,span*.5f+Mathf.Sin(a)*radius);
                var size=new Vector3(.6f+(i%3)*.38f,.12f,.8f+(i%4)*.36f);
                AddIsland(platforms,seams,p,size,Quaternion.Euler((i%3-1)*12,i*31,(i%5-2)*6),i+2);
            }
            platforms.Finish("Broken Floor",transform,dark);
            seams.Finish("Fault Lines",transform,signal);

            for(int i=0;i<5;i++)
            {
                var band=new Batch();
                float radius=3.15f+i*1.17f;
                for(int s=0;s<90;s++)
                {
                    if((s+i*3)%19<4 || (s+i*7)%37<3) continue;
                    float a=s*Mathf.PI*2/90,b=(s+1f)*Mathf.PI*2/90;
                    float ra=radius*(1+Mathf.Sin(a*3+i)*.065f),rb=radius*(1+Mathf.Sin(b*3+i)*.065f);
                    float w=.026f+(s%13==0?.09f:.018f);
                    band.Quad(Radial(a,ra-w),Radial(a,ra+w),Radial(b,rb+w),Radial(b,rb-w),Palette(i),s/90f,(s+1f)/90);
                    if(s%7==0)band.Box(Radial(a,ra+.21f),new Vector3(.04f,.18f,.055f),Quaternion.Euler(0,0,a*Mathf.Rad2Deg-90),Palette(i+1));
                }
                var t=band.Finish("Broken Orbit "+i,transform,signal).transform;
                t.localPosition=new Vector3(0,2.35f,span+1.5f+i*.42f);
                t.localRotation=Quaternion.Euler((i-2)*11,(i%2==0?1:-1)*17,i*24);
                Record(records,t,new Vector3(.22f,.15f,1), (i%2==0?1:-1)*(12+i*3), i, .12f);
            }

            for(int i=0;i<4;i++)
            {
                var ribbon=new Batch(); const int sections=112;
                for(int n=0;n<sections;n++)
                {
                    if((n+i*3)%31<3)continue;
                    float t=n/(float)sections,u=(n+1f)/sections;
                    RibbonPoint(t,i,out var p,out var side);
                    RibbonPoint(u,i,out var q,out var nextSide);
                    float width=.055f+.055f*Mathf.Sin(t*Mathf.PI);
                    ribbon.Quad(p-side*width,p+side*width,q+nextSide*width,q-nextSide*width,Palette(i+1),t,u);
                }
                var tRoot=ribbon.Finish("Data Ribbon "+i,transform,signal).transform;
                tRoot.localPosition=new Vector3(0,2.3f,span+3);
                Record(records,tRoot,new Vector3(.1f,.13f,1),(i%2==0?1:-1)*(5+i),i+3,.18f);
            }

            for(int i=0;i<3;i++)
            {
                var frame=new Batch();

                float r=1.7f+i*.45f;
                var vertices=new Vector3[16];
                for(int k=0;k<16;k++)
                {
                    float size=k<8?r:r*.53f;
                    vertices[k]=new Vector3((k&1)==0?-size:size,(k&2)==0?-size:size,(k&4)==0?-size:size);
                    if(k>=8)vertices[k]=Quaternion.Euler(26,33,18)*vertices[k]+new Vector3(.3f,-.4f,.6f);
                }
                for(int k=0;k<16;k++)for(int axis=0;axis<3;axis++)
                {
                    int next=k^(1<<axis);if(next<=k || (k+axis+i)%7==0)continue;
                    frame.Rod(vertices[k],vertices[next],.024f,Palette(i+2));
                }
                for(int k=0;k<8;k+=2)frame.Rod(vertices[k],vertices[8+((k+3)%8)],.015f,Palette(i));
                var f=frame.Finish("Address Frame "+i,transform,signal).transform;
                f.localPosition=new Vector3(i==1?0:(i==0?-10:10),i==1?11:4.5f,span+7+i*2);
                Record(records,f,new Vector3(.35f,.7f,.23f),11+i*7,i+6,.3f);
            }

            var drifting=new Batch();var distant=new Batch();
            for(int i=0;i<192;i++)
            {
                float angle=i*2.399963f, radius=7.5f+(i%19)*.9f;
                var p=new Vector3(Mathf.Cos(angle)*radius,-2+(i%17)*.93f,span+2+Mathf.Sin(angle)*radius);
                if(p.z<1.5f && Mathf.Abs(p.x)<4)continue;
                float size=.027f+(i%5)*.018f;
                drifting.Diamond(p,size,Palette(i),i*.137f);
                if(i%5==0)distant.Box(Vector3.Scale(p,new Vector3(1.1f,1,1.1f)),new Vector3(.35f,.8f,.4f),Quaternion.Euler(i*21,i*33,i*7),new Color(.018f,.012f,.032f));
            }
            drifting.Finish("Data Fragments",transform,dust);
            distant.Finish("Signal Blocks",transform,dark);
            motions=records.ToArray();
            surfaces=GetComponentsInChildren<Renderer>(true);
        }

        static void Record(List<Motion> list,Transform t,Vector3 axis,float speed,float phase,float drift)
            =>list.Add(new Motion{target=t,position=t.localPosition,rotation=t.localRotation,scale=t.localScale,axis=axis.normalized,speed=speed,phase=phase,drift=drift});
        static Vector3 Sphere(float v,float a){float b=v*Mathf.PI;return new Vector3(Mathf.Sin(b)*Mathf.Cos(a),Mathf.Cos(b),Mathf.Sin(b)*Mathf.Sin(a));}
        static Vector3 Radial(float a,float r)=>new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,0);
        static Color Palette(int i)
        {
            switch(i%5){case 0:return new Color(.03f,.85f,1);case 1:return new Color(1,.035f,.34f);case 2:return new Color(.36f,.06f,1);case 3:return new Color(1,.43f,.045f);default:return new Color(.08f,1,.56f);}
        }
        static void AddIsland(Batch solid,Batch edge,Vector3 p,Vector3 size,Quaternion rotation,int index)
        {
            solid.Box(p,size,rotation,new Color(.017f,.023f,.04f));
            Vector3 a=new Vector3(-size.x*.5f,size.y*.51f,-size.z*.5f),b=new Vector3(size.x*.5f,size.y*.51f,-size.z*.5f);
            Vector3 c=new Vector3(size.x*.5f,size.y*.51f,size.z*.5f),d=new Vector3(-size.x*.5f,size.y*.51f,size.z*.5f);
            edge.Rod(p+rotation*a,p+rotation*b,.012f,Palette(index));
            edge.Rod(p+rotation*b,p+rotation*c,.012f,Palette(index+1));
            if(index%3!=1)edge.Rod(p+rotation*c,p+rotation*d,.012f,Palette(index+2));
            edge.Rod(p+rotation*d,p+rotation*a,.012f,Palette(index));
        }
        static void RibbonPoint(float t,int i,out Vector3 p,out Vector3 side)
        {
            float a=t*Mathf.PI*3+i*Mathf.PI*.5f,r=5+t*8;
            p=new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r*.65f,t*17);
            side=new Vector3(Mathf.Cos(a),Mathf.Sin(a),.1f*Mathf.Sin(a*3)).normalized;
        }

        void OnEnable()
        {
            SetVisible(!Application.isPlaying || (angel && angel.gameObject.activeInHierarchy));
        }

        void SetVisible(bool visible)
        {
            displayVisible=visible;
            for(int i=0;i<surfaces.Length;i++)if(surfaces[i])surfaces[i].enabled=visible;
        }

        void Update()
        {

            bool visible=!Application.isPlaying || (angel && angel.gameObject.activeInHierarchy);
            if(visible!=displayVisible)SetVisible(visible);
            if(!visible)return;
            if(!runtimeAnimate)return;
            age+=Time.deltaTime;
            float energy=angel?angel.VoiceEnergy:0,accent=angel?angel.AccentEnergy:0;
            for(int i=0;i<motions.Length;i++)
            {
                var m=motions[i];if(!m.target)continue;
                m.target.localRotation=m.rotation*Quaternion.AngleAxis(age*m.speed,m.axis)*Quaternion.Euler(Mathf.Sin(age*.8f+m.phase)*(4+energy*4),0,0);
                m.target.localPosition=m.position+new Vector3(Mathf.Sin(age*.6f+m.phase),Mathf.Cos(age*.8f+m.phase),Mathf.Sin(age*.4f+m.phase))*m.drift*(1+energy*.6f);
                m.target.localScale=m.scale*(1+energy*.012f+accent*.018f);
            }
            if(properties==null)properties=new MaterialPropertyBlock();
            properties.SetFloat(VoiceId,energy);properties.SetFloat(AccentId,accent);
            for(int i=0;i<surfaces.Length;i++)if(surfaces[i])surfaces[i].SetPropertyBlock(properties);
        }

        sealed class Batch
        {
            readonly List<Vector3> vertices=new List<Vector3>();
            readonly List<Color> colors=new List<Color>();
            readonly List<Vector2> uv=new List<Vector2>(),seeds=new List<Vector2>();
            readonly List<int> triangles=new List<int>();
            float seed;
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,float u=0,float v=1)
                =>Quad(a,b,c,d,color,color,color,color,u,v);
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color ca,Color cb,Color cc,Color cd,float u=0,float v=1)
            {
                int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
                colors.Add(ca);colors.Add(cb);colors.Add(cc);colors.Add(cd);
                uv.Add(new Vector2(u,0));uv.Add(new Vector2(u,1));uv.Add(new Vector2(v,1));uv.Add(new Vector2(v,0));
                for(int k=0;k<4;k++)seeds.Add(new Vector2(seed,0));
                triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);triangles.Add(n);triangles.Add(n+2);triangles.Add(n+3);
            }
            public void Box(Vector3 center,Vector3 size,Quaternion rotation,Color color)
            {
                Vector3 x=rotation*Vector3.right*size.x*.5f,y=rotation*Vector3.up*size.y*.5f,z=rotation*Vector3.forward*size.z*.5f;
                Quad(center-x-y-z,center+x-y-z,center+x+y-z,center-x+y-z,color);
                Quad(center+x-y+z,center-x-y+z,center-x+y+z,center+x+y+z,color*.82f);
                Quad(center-x-y+z,center-x-y-z,center-x+y-z,center-x+y+z,color*.72f);
                Quad(center+x-y-z,center+x-y+z,center+x+y+z,center+x+y-z,color*.72f);
                Quad(center-x+y-z,center+x+y-z,center+x+y+z,center-x+y+z,color);
                Quad(center-x-y+z,center+x-y+z,center+x-y-z,center-x-y-z,color*.6f);
            }
            public void Rod(Vector3 a,Vector3 b,float width,Color color)
            {
                Vector3 d=b-a;if(d.sqrMagnitude<.00001f)return;
                Box((a+b)*.5f,new Vector3(width,width,d.magnitude),Quaternion.LookRotation(d),color);
            }
            public void Diamond(Vector3 p,float size,Color color,float random)
            {
                seed=random;

                Quad(p+Vector3.up*size,p+Vector3.right*size*.45f,p-Vector3.up*size,p-Vector3.right*size*.45f,color);
                Quad(p+Vector3.up*size,p+Vector3.forward*size*.45f,p-Vector3.up*size,p-Vector3.forward*size*.45f,color);
                seed=0;
            }
            public GameObject Finish(string name,Transform parent,Material material)
            {
                var mesh=new Mesh{name="Cyberspace "+name};
                if(vertices.Count>65535)mesh.indexFormat=IndexFormat.UInt32;
                mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetUVs(1,seeds);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();

                var bounds=mesh.bounds;bounds.Expand(1.8f);mesh.bounds=bounds;
                var go=new GameObject(ObjectNames.Short(name));go.transform.SetParent(parent,false);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                return go;
            }
        }
    }
}
