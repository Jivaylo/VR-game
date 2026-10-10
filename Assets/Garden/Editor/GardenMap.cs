using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Garden.Editor
{
    public static partial class GardenBuilder
    {
        public static Vector3 Home, Breakfast, SouthMetro, WorkMetro, Factory, Arcade, Poster, Hatch, Yard, Gate, Outside;
        static readonly List<Rect> MapRooms = new List<Rect>();
        static readonly List<Rect> MapSolids = new List<Rect>();
        static readonly List<Rect> MapWalks = new List<Rect>();
        static readonly List<Rect> MapFlights = new List<Rect>();
        static readonly List<Vector3> MapPads = new List<Vector3>();
        static readonly List<NavPad> MapTargets = new List<NavPad>();
        static readonly HashSet<NavPad> MapLandings = new HashSet<NavPad>();
        static readonly List<Vector3> StairTops = new List<Vector3>();
        static readonly List<Vector3> StairDirections = new List<Vector3>();
        const float Upper = 3.2f;
        public static string MapReport { get; private set; }

        static Vector3 Site(float x, float y, float level = 0)
        {
            return MapPoint(x / 1334f * 2480f, y / 1888f * 3508f, level);
        }

        static Rect MapRect(Vector4 data)
        {
            return new Rect((data.x - .5f) * 100f, (.5f - data.y - data.w) * 141.451613f, data.z * 100f, data.w * 141.451613f);
        }

        static Rect RoomRect(Vector3 center, float width, float depth)
        {
            return new Rect(center.x - width * .5f, center.z - depth * .5f, width, depth);
        }

        static bool Contains(Rect r, Vector3 p, float margin = 0)
        {
            return p.x >= r.xMin - margin && p.x <= r.xMax + margin && p.z >= r.yMin - margin && p.z <= r.yMax + margin;
        }

        static float SharedArea(Rect a, Rect b)
        {
            return Mathf.Max(0, Mathf.Min(a.xMax,b.xMax)-Mathf.Max(a.xMin,b.xMin)) * Mathf.Max(0, Mathf.Min(a.yMax,b.yMax)-Mathf.Max(a.yMin,b.yMin));
        }

        static List<Rect> Cut(Rect source, Rect hole)
        {
            if (!source.Overlaps(hole)) return new List<Rect> { source };
            float x0 = Mathf.Max(source.xMin, hole.xMin), x1 = Mathf.Min(source.xMax,hole.xMax);
            float z0 = Mathf.Max(source.yMin, hole.yMin), z1 = Mathf.Min(source.yMax,hole.yMax);
            var result = new List<Rect>();
            if (x0-source.xMin > .12f) result.Add(new Rect(source.xMin,source.yMin,x0-source.xMin,source.height));
            if (source.xMax-x1 > .12f) result.Add(new Rect(x1,source.yMin,source.xMax-x1,source.height));
            if (z0-source.yMin > .12f) result.Add(new Rect(x0,source.yMin,x1-x0,z0-source.yMin));
            if (source.yMax-z1 > .12f) result.Add(new Rect(x0,z1,x1-x0,source.yMax-z1));
            return result;
        }

        static List<Rect> Carve(Rect rect)
        {
            var parts = new List<Rect>{rect};
            foreach (var hole in MapRooms)
            {
                var next = new List<Rect>();
                foreach(var part in parts) next.AddRange(Cut(part,hole));
                parts=next;
            }
            return parts;
        }

        static GameObject MapBox(string name, Transform parent, Vector3 position, Vector3 size, Material material, bool collision = true)
        {
            var go = Cube(name,parent,position,size,material,collision);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }

        static void Slab(string name, Transform parent, Rect r, float top, float thickness, Material material, bool collision = true)
        {
            MapBox(name,parent,new Vector3(r.center.x,top-thickness*.5f,r.center.y),new Vector3(r.width,thickness,r.height),material,collision);
        }

        public static void BuildMap(Transform parent)
        {
            MapRooms.Clear(); MapSolids.Clear(); MapWalks.Clear(); MapFlights.Clear(); MapPads.Clear(); MapTargets.Clear(); MapLandings.Clear(); StairTops.Clear(); StairDirections.Clear();
            Home=Site(260,905,Upper); Breakfast=Site(385,719.35f); SouthMetro=Site(386,957); WorkMetro=Site(911,687);
            Factory=Site(654,745); Arcade=Site(877,845); Poster=Site(915,965,Upper); Hatch=Site(895,875);
            Yard=Site(1035,849); Gate=Site(1136,849); Outside=Site(1468,849);
            var city=Empty("City",parent,Vector3.zero);
            var roads=Empty("Streets",city,Vector3.zero);
            var blocks=Empty("Blocks",city,Vector3.zero);
            var decks=Empty("Walkways",city,Vector3.zero);
            var stairs=Empty("Stairs",city,Vector3.zero);
            var pads=Empty("Pads",city,Vector3.zero);
            MapBox("Ground",roads,new Vector3(0,-.2f,0),new Vector3(104,.4f,145),concrete);
            MapRooms.Add(RoomRect(Home,6.4f,5.4f));
            MapRooms.Add(RoomRect(Site(1020,845),19.2f,14.2f));
            MapRooms.Add(new Rect(Gate.x-.5f,Gate.z-1.7f,51f-Gate.x,3.4f));
            MapRooms.Add(RoomRect(Breakfast,4,4));
            MapRooms.Add(RoomRect(SouthMetro,5,5));
            MapRooms.Add(RoomRect(WorkMetro,5,5));
            MapRooms.Add(RoomRect(Site(879,866),3.8f,9));
            foreach(var data in WalkData)
            {
                var r=MapRect(data);
                if (r.width<1.8f) {r.xMin-= (1.8f-r.width)*.5f; r.width=1.8f;}
                if (r.height<1.8f) {r.yMin-= (1.8f-r.height)*.5f; r.height=1.8f;}
                r.xMin-=.04f; r.yMin-=.04f; r.width+=.08f; r.height+=.08f;
                MapWalks.Add(r); MapRooms.Add(r);
            }
            var streetRects=new List<Rect>();
            foreach(var data in RoadData)
            {
                var r=MapRect(data);
                if(Mathf.Max(r.width,r.height)<1.3f) continue;
                if(r.width<2.2f) {r.xMin-=(2.2f-r.width)*.5f; r.width=2.2f;}
                if(r.height<2.2f) {r.yMin-=(2.2f-r.height)*.5f; r.height=2.2f;}
                streetRects.Add(r); MapRooms.Add(r);
                Slab("Lane",roads,r,.014f,.028f,road,false);
            }
            foreach(var point in StairData) PlanStair(point);
            CutDecks();
            for(int i=0;i<BlockData.Length;i++)
            {
                var r=MapRect(BlockData[i]);
                r.xMin+=.13f; r.yMin+=.13f; r.width-=.26f; r.height-=.26f;
                if(r.width<.3f || r.height<.3f) continue;
                foreach(var part in Carve(r))
                {
                    if(part.width<.28f || part.height<.28f) continue;
                    MapSolids.Add(part);
                    float h=6.4f+(i%4)*1.15f;
                    MapBox("Block"+(i+1),blocks,new Vector3(part.center.x,h*.5f,part.center.y),new Vector3(part.width,h,part.height),concrete);
                }
            }
            int deck=0;
            foreach(var r in MapWalks)
            {
                Slab("Walk"+(++deck),decks,r,Upper,.22f,blue);
                DeckRails(decks,r);
                MapLine(pads,r,Upper);
            }
            Slab("Home",decks,RoomRect(Home,6.2f,5.2f),Upper,.22f,concrete);
            var homeLink=RoomRect(Home+Vector3.back*3,2.4f,2.5f);
            Slab("Landing",decks,homeLink,Upper,.22f,blue);
            TryPad(pads,Site(260,935,Upper),false);
            var factoryRect=MapRect(FactoryData);
            Slab("Factory",roads,factoryRect,.016f,.032f,red,false);
            Mesh steps=MakeSteps();
            for(int i=0;i<StairTops.Count;i++) BuildStair(stairs,pads,StairTops[i],StairDirections[i],steps,i+1);
            foreach(var r in streetRects) MapLine(pads,r,0);
            foreach(var r in MapWalks) MapLine(pads,r,0);
            MapCorners(pads,streetRects,0);
            MapCorners(pads,MapWalks,Upper);
            foreach(var p in new[]{Breakfast,SouthMetro,WorkMetro,Arcade}) TryPad(pads,p,true);
            TryPad(pads,Poster,false);
            OptimizeMapPads();
        }

        static void PlanStair(Vector2 uv)
        {
            Vector3 top=MapPoint(uv.x*2480f,uv.y*3508f,0);
            Rect near=MapWalks[0]; float distance=float.MaxValue;
            foreach(var walk in MapWalks)
            {
                var p=new Vector2(Mathf.Clamp(top.x,walk.xMin+.6f,walk.xMax-.6f),Mathf.Clamp(top.z,walk.yMin+.6f,walk.yMax-.6f));
                float d=(p-new Vector2(top.x,top.z)).sqrMagnitude;
                if(d<distance) {distance=d;near=walk;}
            }
            top.x=Mathf.Clamp(top.x,near.xMin+.4f,near.xMax-.4f);
            top.z=Mathf.Clamp(top.z,near.yMin+.4f,near.yMax-.4f);
            foreach(var old in StairTops) if((old-top).sqrMagnitude<9) return;
            Vector3 direction=Vector3.forward; float score=float.MaxValue;
            foreach(var axis in new[]{Vector3.forward,Vector3.back,Vector3.left,Vector3.right})
            {
                Vector3 side=Vector3.Cross(Vector3.up,axis);
                Rect flight=RoomRect(top+axis*3.8f+side*.8f,axis.x==0?4.6f:11.4f,axis.x==0?11.4f:4.6f);
                float cost=0;
                foreach(var data in BlockData) cost+=SharedArea(flight,MapRect(data));
                foreach(var old in MapFlights) cost+=SharedArea(flight,old)*8;
                foreach(var walk in MapWalks) cost+=SharedArea(flight,walk)*24;
                cost+=SharedArea(flight,MapRect(FactoryData))*20;
                cost+=SharedArea(flight,RoomRect(Home,6.4f,5.4f))*40;
                cost+=SharedArea(flight,RoomRect(Site(1020,845),19.2f,14.2f))*80;
                for(int room=0;room<7;room++) cost+=SharedArea(flight,MapRooms[room])*80;
                if(flight.xMin<-51.8f || flight.xMax>51.8f || flight.yMin<-72.3f || flight.yMax>72.3f) cost+=100000;
                if(cost<score) {score=cost;direction=axis;}
            }
            var footprint=RoomRect(top+direction*3.8f+Vector3.Cross(Vector3.up,direction)*.8f,direction.x==0?4.6f:11.4f,direction.x==0?11.4f:4.6f);
            MapRooms.Add(footprint); MapFlights.Add(footprint); StairTops.Add(top); StairDirections.Add(direction);
        }

        static void CutDecks()
        {
            for(int i=0;i<StairTops.Count;i++)
            {
                Vector3 direction=StairDirections[i];
                Rect opening=RoomRect(StairTops[i]+direction*4.6f,direction.x==0?2.35f:7.4f,direction.x==0?7.4f:2.35f);
                var parts=new List<Rect>();
                foreach(var walk in MapWalks) parts.AddRange(Cut(walk,opening));
                MapWalks.Clear();
                foreach(var part in parts) if(part.width>.28f && part.height>.28f) MapWalks.Add(part);
            }
        }

        static bool OpenEdge(Vector3 point,Vector3 outward)
        {
            Vector3 check=point+outward*.2f;
            foreach(var walk in MapWalks) if(Contains(walk,check,-.02f)) return true;
            for(int i=0;i<StairTops.Count;i++) if(Contains(RoomRect(StairTops[i],2.9f,2.9f),check)) return true;
            foreach(var flight in MapFlights) if(Contains(flight,check)) return true;
            if(Contains(RoomRect(Home,6.4f,6.8f),check)) return true;
            return false;
        }

        static void DeckRails(Transform parent,Rect r)
        {
            Edge(parent,new Vector3(r.xMin,Upper,r.yMin),new Vector3(r.xMax,Upper,r.yMin),Vector3.back);
            Edge(parent,new Vector3(r.xMin,Upper,r.yMax),new Vector3(r.xMax,Upper,r.yMax),Vector3.forward);
            Edge(parent,new Vector3(r.xMin,Upper,r.yMin),new Vector3(r.xMin,Upper,r.yMax),Vector3.left);
            Edge(parent,new Vector3(r.xMax,Upper,r.yMin),new Vector3(r.xMax,Upper,r.yMax),Vector3.right);
        }

        static void Edge(Transform parent,Vector3 start,Vector3 end,Vector3 outward)
        {
            float length=Vector3.Distance(start,end); int count=Mathf.CeilToInt(length/.4f); float first=-1;
            for(int i=0;i<=count;i++)
            {
                bool open=i==count || OpenEdge(Vector3.Lerp(start,end,(i+.5f)/count),outward);
                if(!open && first<0) first=(float)i/count;
                if(open && first>=0)
                {
                    var a=Vector3.Lerp(start,end,first); var b=Vector3.Lerp(start,end,(float)i/count);
                    if(Vector3.Distance(a,b)>.18f) Rail(parent,a,b);
                    first=-1;
                }
            }
        }

        static void Rail(Transform parent,Vector3 a,Vector3 b)
        {
            Vector3 d=b-a; float length=d.magnitude;
            var bar=MapBox("Rail",parent,(a+b)*.5f+Vector3.up*1.05f,new Vector3(.065f,.065f,length),metal);
            bar.transform.rotation=Quaternion.LookRotation(d);
            int count=Mathf.Max(1,Mathf.CeilToInt(length/2.5f));
            for(int i=0;i<=count;i++) MapBox("Post",parent,Vector3.Lerp(a,b,(float)i/count)+Vector3.up*.52f,new Vector3(.055f,1.04f,.055f),metal);
        }

        static void MapLine(Transform parent,Rect r,float height)
        {
            bool horizontal=r.width>=r.height;
            float length=horizontal?r.width:r.height;
            if(length<.9f) return;
            int count=Mathf.Max(1,Mathf.CeilToInt(length/8));
            for(int i=0;i<=count;i++)
            {
                float t=Mathf.Lerp(.35f,Mathf.Max(.35f,length-.35f),(float)i/count);
                Vector3 point=horizontal?new Vector3(r.xMin+t,height,r.center.y):new Vector3(r.center.x,height,r.yMin+t);
                TryPad(parent,point,height<1);
            }
        }

        static void TryPad(Transform parent,Vector3 point,bool ground)
        {
            foreach(var old in MapPads) if(Vector3.Distance(old,point)<2.1f) return;
            foreach(var block in MapSolids) if(Contains(block,point,.65f)) return;
            if(ground)
            {
                for(int i=0;i<MapFlights.Count;i++) if(Contains(MapFlights[i],point,.1f)) return;
                if(Contains(RoomRect(Site(1020,845),19.2f,14.2f),point)) return;
                if(point.x>Gate.x-.5f && Mathf.Abs(point.z-Gate.z)<1.8f) return;
            }
            MapPads.Add(point); MapTargets.Add(Pad("Pad",parent,point));
        }

        static void MapCorners(Transform parent,List<Rect> rects,float height)
        {
            for(int i=0;i<rects.Count;i++)
                for(int j=i+1;j<rects.Count;j++)
                {
                    var a=rects[i];var b=rects[j];
                    if((a.width>=a.height)==(b.width>=b.height)) continue;
                    Rect horizontal=a.width>=a.height?a:b;
                    Rect vertical=a.width>=a.height?b:a;
                    var point=new Vector3(vertical.center.x,height,horizontal.center.y);
                    if(!Contains(a,point,1.2f) || !Contains(b,point,1.2f)) continue;
                    bool blocked=false;
                    foreach(var solid in MapSolids) if(Contains(solid,point,.45f)) {blocked=true;break;}
                    foreach(var old in MapPads) if(Vector3.Distance(old,point)<.8f) {blocked=true;break;}
                    if(height<1) foreach(var flight in MapFlights) if(Contains(flight,point,.1f)) {blocked=true;break;}
                    if(blocked) continue;
                    MapPads.Add(point);MapTargets.Add(Pad("Pad",parent,point));
                }
        }

        static bool MapSight(Vector3 a,Vector3 b)
        {
            if(Mathf.Abs(a.y-b.y)>1.8f || Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z))>10.5f) return false;
            Vector3 start=a+Vector3.up*1.25f;
            Vector3 delta=b+Vector3.up*.12f-start;
            foreach(var hit in Physics.RaycastAll(start,delta.normalized,delta.magnitude,1,QueryTriggerInteraction.Ignore))
                if(!hit.collider.GetComponentInParent<NavPad>()) return false;
            return true;
        }

        static void OptimizeMapPads()
        {
            Physics.SyncTransforms();
            int count=MapTargets.Count;
            var graph=new List<int>[count];
            var alive=new bool[count];
            for(int i=0;i<count;i++)
            {
                graph[i]=new List<int>();alive[i]=true;
                foreach(var collider in Physics.OverlapCapsule(MapPads[i]+Vector3.up*.27f,MapPads[i]+Vector3.up*1.48f,.22f,1,QueryTriggerInteraction.Ignore))
                    if(!collider.GetComponentInParent<NavPad>()) {alive[i]=false;break;}
            }
            for(int i=0;i<count;i++)
                for(int j=i+1;j<count;j++)
                    if(alive[i] && alive[j] && MapSight(MapPads[i],MapPads[j]) && MapSight(MapPads[j],MapPads[i])) {graph[i].Add(j);graph[j].Add(i);}
            int kept=0;
            for(int i=0;i<count;i++)
            {
                if(graph[i].Count==0 && !MapLandings.Contains(MapTargets[i])) alive[i]=false;
                if(alive[i]) kept++;
            }
            bool changed=true;
            while(changed && kept>330)
            {
                changed=false;
                for(int i=0;i<count && kept>330;i++)
                {
                    if(!alive[i] || MapLandings.Contains(MapTargets[i])) continue;
                    var neighbors=graph[i].FindAll(n=>alive[n]);
                    if(neighbors.Count<2) continue;
                    var seen=new HashSet<int>{neighbors[0]};
                    var todo=new Queue<int>();todo.Enqueue(neighbors[0]);
                    while(todo.Count>0)
                    {
                        int current=todo.Dequeue();
                        foreach(int next in graph[current]) if(next!=i && alive[next] && neighbors.Contains(next) && seen.Add(next)) todo.Enqueue(next);
                    }
                    if(seen.Count!=neighbors.Count) continue;
                    bool covered=true;
                    foreach(int old in graph[i])
                    {
                        if(alive[old]) continue;
                        bool found=false;
                        foreach(int replacement in graph[old]) if(replacement!=i && alive[replacement]) {found=true;break;}
                        if(!found) {covered=false;break;}
                    }
                    if(!covered) continue;
                    alive[i]=false;kept--;changed=true;
                }
            }
            var visited=new HashSet<int>();var sizes=new List<int>();
            for(int i=0;i<count;i++)
            {
                if(!alive[i] || visited.Contains(i)) continue;
                int size=0;var todo=new Queue<int>();todo.Enqueue(i);visited.Add(i);
                while(todo.Count>0)
                {
                    int current=todo.Dequeue();size++;
                    foreach(int next in graph[current]) if(alive[next] && visited.Add(next)) todo.Enqueue(next);
                }
                sizes.Add(size);
            }
            sizes.Sort((a,b)=>b.CompareTo(a));
            int blocked=0;
            for(int i=0;i<count;i++)
            {
                if(!alive[i]) {Object.DestroyImmediate(MapTargets[i].gameObject);continue;}
                Vector3 p=MapPads[i];
                foreach(var hit in Physics.OverlapCapsule(p+Vector3.up*.3f,p+Vector3.up*1.4f,.22f,1,QueryTriggerInteraction.Ignore))
                    if(!hit.GetComponentInParent<NavPad>()) {blocked++;break;}
            }
            MapReport="Pads: "+kept+". Blocks: "+MapSolids.Count+". Stairs: "+StairTops.Count+". Components: "+string.Join(", ",sizes)+". Occupied pads: "+blocked+".";
        }

        static Mesh MakeSteps()
        {
            const string folder="Assets/Garden/Meshes";
            if(!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Garden","Meshes");
            const string path=folder+"/Stair.asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing!=null) return existing;
            var vertices=new List<Vector3>(); var triangles=new List<int>();
            for(int i=0;i<18;i++)
            {
                float a=i/3f+(i>=9?2:0),b=(i+1)/3f+(i>=9?2:0),h=Upper*(18-i)/18f;
                StepFace(vertices,triangles,new Vector3(-1.1f,h,a),new Vector3(-1.1f,h,b),new Vector3(1.1f,h,b),new Vector3(1.1f,h,a));
                StepFace(vertices,triangles,new Vector3(-1.1f,0,b),new Vector3(-1.1f,h,b),new Vector3(-1.1f,h,a),new Vector3(-1.1f,0,a));
                StepFace(vertices,triangles,new Vector3(1.1f,0,a),new Vector3(1.1f,h,a),new Vector3(1.1f,h,b),new Vector3(1.1f,0,b));
                float low=i==17?0:Upper*(17-i)/18f;
                StepFace(vertices,triangles,new Vector3(-1.1f,low,b),new Vector3(1.1f,low,b),new Vector3(1.1f,h,b),new Vector3(-1.1f,h,b));
            }
            StepFace(vertices,triangles,new Vector3(-1.1f,1.6f,3),new Vector3(-1.1f,1.6f,5),new Vector3(1.1f,1.6f,5),new Vector3(1.1f,1.6f,3));
            StepFace(vertices,triangles,new Vector3(-1.1f,0,5),new Vector3(-1.1f,1.6f,5),new Vector3(-1.1f,1.6f,3),new Vector3(-1.1f,0,3));
            StepFace(vertices,triangles,new Vector3(1.1f,0,3),new Vector3(1.1f,1.6f,3),new Vector3(1.1f,1.6f,5),new Vector3(1.1f,0,5));
            StepFace(vertices,triangles,new Vector3(-1.1f,0,0),new Vector3(-1.1f,Upper,0),new Vector3(1.1f,Upper,0),new Vector3(1.1f,0,0));
            var mesh=new Mesh{name="Stair"}; mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,path); return mesh;
        }

        static void StepFace(List<Vector3> vertices,List<int> indices,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            int n=vertices.Count; vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
            indices.Add(n);indices.Add(n+1);indices.Add(n+2);indices.Add(n);indices.Add(n+2);indices.Add(n+3);
        }

        static void BuildStair(Transform parent,Transform pads,Vector3 top,Vector3 direction,Mesh mesh,int index)
        {
            var root=Empty("Stair"+index,parent,top);
            root.rotation=Quaternion.LookRotation(direction);
            var go=new GameObject("Steps"); go.transform.SetParent(root,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh; go.AddComponent<MeshRenderer>().sharedMaterial=concrete; go.AddComponent<MeshCollider>().sharedMesh=mesh;
            GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccluderStatic|StaticEditorFlags.OccludeeStatic);
            var landing=RoomRect(top-direction*.55f,2.4f,2.4f);
            Slab("Landing",parent,landing,Upper,.2f,blue);
            Vector3 side=Vector3.Cross(Vector3.up,direction)*1.12f;
            Rail(parent,top+side+Vector3.up*Upper,top+direction*8+side);
            Rail(parent,top-side+Vector3.up*Upper,top+direction*8-side);
            Vector3 upper=top+direction*.52f+Vector3.up*Upper;
            Vector3 middle=top+direction*4.8f+Vector3.up*1.6f;
            Vector3 lower=top+direction*8.7f;
            foreach(var point in new[]{upper,middle,lower})
            {
                var pad=Pad("Landing",pads,point);
                MapPads.Add(point);MapTargets.Add(pad);MapLandings.Add(pad);
            }
            Vector3 lane=Vector3.Cross(Vector3.up,direction)*2;
            foreach(var point in new[]{top-direction*.9f+lane,top+direction*8.7f+lane})
            {
                MapPads.Add(point);MapTargets.Add(Pad("Pad",pads,point));
            }
        }

        static readonly Vector4[] BlockData=
        {
            new Vector4(0.182635f,0.851695f,0.326347f,0.148305f),
            new Vector4(0.619760f,0.129237f,0.212575f,0.199153f),
            new Vector4(0.703593f,0.900424f,0.296407f,0.099576f),
            new Vector4(0.083832f,0.116525f,0.179641f,0.158898f),
            new Vector4(0.000000f,0.764831f,0.164671f,0.165254f),
            new Vector4(0.209581f,0.650424f,0.182635f,0.144068f),
            new Vector4(0.000000f,0.519068f,0.263473f,0.086864f),
            new Vector4(0.664671f,0.391949f,0.197605f,0.105932f),
            new Vector4(0.664671f,0.531780f,0.200599f,0.103814f),
            new Vector4(0.511976f,0.576271f,0.125749f,0.161017f),
            new Vector4(0.766467f,0.000000f,0.230539f,0.084746f),
            new Vector4(0.940120f,0.324153f,0.059880f,0.262712f),
            new Vector4(0.000000f,0.622881f,0.173653f,0.088983f),
            new Vector4(0.278443f,0.097458f,0.194611f,0.076271f),
            new Vector4(0.000000f,0.307203f,0.254491f,0.055085f),
            new Vector4(0.523952f,0.000000f,0.074850f,0.175847f),
            new Vector4(0.739521f,0.661017f,0.257485f,0.050847f),
            new Vector4(0.000000f,0.000000f,0.200599f,0.061441f),
            new Vector4(0.844311f,0.188559f,0.095808f,0.122881f),
            new Vector4(0.113772f,0.375000f,0.101796f,0.112288f),
            new Vector4(0.449102f,0.260593f,0.104790f,0.105932f),
            new Vector4(0.344311f,0.000000f,0.128743f,0.080508f),
            new Vector4(0.703593f,0.824153f,0.152695f,0.067797f),
            new Vector4(0.000000f,0.370763f,0.068862f,0.148305f),
            new Vector4(0.745509f,0.726695f,0.110778f,0.091102f),
            new Vector4(0.491018f,0.766949f,0.239521f,0.040254f),
            new Vector4(0.916168f,0.779661f,0.083832f,0.108051f),
            new Vector4(0.404192f,0.652542f,0.074850f,0.112288f),
            new Vector4(0.523952f,0.934322f,0.119760f,0.065678f),
            new Vector4(0.212575f,0.000000f,0.095808f,0.080508f),
            new Vector4(0.000000f,0.930085f,0.107784f,0.069915f),
            new Vector4(0.302395f,0.264831f,0.134731f,0.055085f),
            new Vector4(0.000000f,0.116525f,0.065868f,0.112288f),
            new Vector4(0.329341f,0.569915f,0.146707f,0.048729f),
            new Vector4(0.616766f,0.000000f,0.083832f,0.082627f),
            new Vector4(0.649701f,0.667373f,0.077844f,0.086864f),
            new Vector4(0.874251f,0.516949f,0.050898f,0.118644f),
            new Vector4(0.227545f,0.379237f,0.086826f,0.067797f),
            new Vector4(0.520958f,0.817797f,0.122754f,0.046610f),
            new Vector4(0.949102f,0.129237f,0.050898f,0.112288f),
            new Vector4(0.302395f,0.326271f,0.134731f,0.040254f),
            new Vector4(0.368263f,0.209746f,0.194611f,0.027542f),
            new Vector4(0.892216f,0.726695f,0.107784f,0.044492f),
            new Vector4(0.532934f,0.877119f,0.101796f,0.046610f),
            new Vector4(0.844311f,0.129237f,0.098802f,0.046610f),
            new Vector4(0.874251f,0.322034f,0.053892f,0.076271f),
            new Vector4(0.889222f,0.406780f,0.050898f,0.074153f),
            new Vector4(0.940120f,0.250000f,0.059880f,0.061441f),
            new Vector4(0.278443f,0.516949f,0.032934f,0.101695f),
            new Vector4(0.000000f,0.243644f,0.068862f,0.042373f),
            new Vector4(0.119760f,0.942797f,0.050898f,0.057203f),
            new Vector4(0.494012f,0.747881f,0.146707f,0.019068f),
            new Vector4(0.832335f,0.084746f,0.164671f,0.016949f),
            new Vector4(0.568862f,0.207627f,0.041916f,0.065678f),
            new Vector4(0.940120f,0.616525f,0.059880f,0.044492f),
            new Vector4(0.233533f,0.455508f,0.080838f,0.031780f),
            new Vector4(0.302395f,0.207627f,0.044910f,0.048729f),
            new Vector4(0.419162f,0.777542f,0.056886f,0.036017f),
            new Vector4(0.209581f,0.616525f,0.047904f,0.033898f),
            new Vector4(0.505988f,0.000000f,0.017964f,0.080508f),
            new Vector4(0.562874f,0.286017f,0.047904f,0.029661f),
            new Vector4(0.571856f,0.322034f,0.029940f,0.040254f),
            new Vector4(0.850299f,0.322034f,0.023952f,0.040254f),
            new Vector4(0.000000f,0.298729f,0.083832f,0.008475f),
            new Vector4(0.353293f,0.826271f,0.155689f,0.004237f),
            new Vector4(0.182635f,0.826271f,0.140719f,0.004237f),
            new Vector4(0.874251f,0.489407f,0.050898f,0.008475f),
        };

        static readonly Vector4[] WalkData=
        {
            new Vector4(0.652695f,0.826271f,0.050898f,0.173729f),
            new Vector4(0.188623f,0.830508f,0.308383f,0.021186f),
            new Vector4(0.628743f,0.110169f,0.338323f,0.019068f),
            new Vector4(0.473054f,0.000000f,0.032934f,0.194915f),
            new Vector4(0.095808f,0.277542f,0.206587f,0.029661f),
            new Vector4(0.000000f,0.093220f,0.257485f,0.023305f),
            new Vector4(0.619760f,0.328390f,0.203593f,0.025424f),
            new Vector4(0.667665f,0.635593f,0.254491f,0.019068f),
            new Vector4(0.068862f,0.375000f,0.032934f,0.144068f),
            new Vector4(0.856287f,0.733051f,0.029940f,0.156780f),
            new Vector4(0.670659f,0.500000f,0.251497f,0.016949f),
            new Vector4(0.020958f,0.741525f,0.173653f,0.023305f),
            new Vector4(0.275449f,0.620763f,0.236527f,0.016949f),
            new Vector4(0.694611f,0.368644f,0.164671f,0.023305f),
            new Vector4(0.284431f,0.175847f,0.188623f,0.019068f),
            new Vector4(0.023952f,0.061441f,0.176647f,0.019068f),
            new Vector4(0.000000f,0.711864f,0.194611f,0.014831f),
            new Vector4(0.362275f,0.243644f,0.161677f,0.016949f),
            new Vector4(0.221557f,0.794492f,0.197605f,0.012712f),
            new Vector4(0.742515f,0.000000f,0.023952f,0.099576f),
            new Vector4(0.895210f,0.779661f,0.020958f,0.110169f),
            new Vector4(0.485030f,0.637712f,0.026946f,0.080508f),
            new Vector4(0.308383f,0.000000f,0.029940f,0.072034f),
            new Vector4(0.122754f,0.487288f,0.167665f,0.012712f),
            new Vector4(0.101796f,0.506356f,0.152695f,0.012712f),
            new Vector4(0.505988f,0.175847f,0.095808f,0.019068f),
            new Vector4(0.700599f,0.002119f,0.017964f,0.099576f),
            new Vector4(0.173653f,0.629237f,0.020958f,0.082627f),
            new Vector4(0.275449f,0.216102f,0.026946f,0.061441f),
            new Vector4(0.275449f,0.307203f,0.026946f,0.055085f),
            new Vector4(0.952096f,0.586864f,0.047904f,0.029661f),
            new Vector4(0.485030f,0.580508f,0.026946f,0.040254f),
            new Vector4(0.619760f,0.082627f,0.080838f,0.012712f),
            new Vector4(0.643713f,0.815678f,0.008982f,0.099576f),
            new Vector4(0.173653f,0.764831f,0.020958f,0.040254f),
            new Vector4(0.766467f,0.084746f,0.065868f,0.012712f),
            new Vector4(0.323353f,0.807203f,0.029940f,0.023305f),
            new Vector4(0.643713f,0.927966f,0.008982f,0.072034f),
            new Vector4(0.661677f,0.375000f,0.032934f,0.016949f),
            new Vector4(0.718563f,0.040254f,0.023952f,0.019068f),
            new Vector4(0.125749f,0.080508f,0.032934f,0.012712f),
            new Vector4(0.967066f,0.116525f,0.032934f,0.012712f),
            new Vector4(0.664671f,0.497881f,0.197605f,0.002119f),
            new Vector4(0.757485f,0.353814f,0.026946f,0.014831f),
            new Vector4(0.077844f,0.726695f,0.023952f,0.014831f),
            new Vector4(0.000000f,0.747881f,0.020958f,0.016949f),
            new Vector4(0.497006f,0.718220f,0.014970f,0.021186f),
            new Vector4(0.173653f,0.726695f,0.020958f,0.014831f),
            new Vector4(0.655689f,0.095339f,0.044910f,0.006356f),
            new Vector4(0.778443f,0.097458f,0.020958f,0.012712f),
            new Vector4(0.000000f,0.061441f,0.023952f,0.010593f),
            new Vector4(0.652695f,0.815678f,0.023952f,0.010593f),
            new Vector4(0.523952f,0.252119f,0.026946f,0.008475f),
            new Vector4(0.083832f,0.288136f,0.011976f,0.019068f),
            new Vector4(0.149701f,0.500000f,0.035928f,0.006356f),
            new Vector4(0.886228f,0.847458f,0.008982f,0.016949f),
            new Vector4(0.619760f,0.095339f,0.017964f,0.006356f),
            new Vector4(0.943114f,0.603814f,0.008982f,0.012712f),
        };

        static readonly Vector4[] RoadData=
        {
            new Vector4(0.296407f,0.194915f,0.323353f,0.012712f),
            new Vector4(0.299401f,0.366525f,0.362275f,0.010593f),
            new Vector4(0.643713f,0.516949f,0.230539f,0.014831f),
            new Vector4(0.314371f,0.559322f,0.347305f,0.008475f),
            new Vector4(0.194611f,0.608051f,0.014970f,0.186441f),
            new Vector4(0.158683f,0.084746f,0.314371f,0.008475f),
            new Vector4(0.257485f,0.637712f,0.209581f,0.012712f),
            new Vector4(0.000000f,0.608051f,0.176647f,0.014831f),
            new Vector4(0.703593f,0.891949f,0.296407f,0.008475f),
            new Vector4(0.862275f,0.398305f,0.026946f,0.091102f),
            new Vector4(0.832335f,0.129237f,0.011976f,0.201271f),
            new Vector4(0.068862f,0.116525f,0.014970f,0.158898f),
            new Vector4(0.727545f,0.711864f,0.272455f,0.008475f),
            new Vector4(0.263473f,0.093220f,0.011976f,0.184322f),
            new Vector4(0.643713f,0.377119f,0.017964f,0.122881f),
            new Vector4(0.314371f,0.377119f,0.011976f,0.182203f),
            new Vector4(0.194611f,0.809322f,0.128743f,0.016949f),
            new Vector4(0.637725f,0.567797f,0.011976f,0.180085f),
            new Vector4(0.353293f,0.813559f,0.167665f,0.012712f),
            new Vector4(0.598802f,0.000000f,0.011976f,0.175847f),
            new Vector4(0.101796f,0.364407f,0.173653f,0.010593f),
            new Vector4(0.649701f,0.654661f,0.287425f,0.006356f),
            new Vector4(0.505988f,0.080508f,0.017964f,0.095339f),
            new Vector4(0.511976f,0.864407f,0.131737f,0.012712f),
            new Vector4(0.844311f,0.311441f,0.155689f,0.010593f),
            new Vector4(0.263473f,0.519068f,0.014970f,0.099576f),
            new Vector4(0.511976f,0.877119f,0.011976f,0.122881f),
            new Vector4(0.610778f,0.207627f,0.008982f,0.158898f),
            new Vector4(0.164671f,0.849576f,0.014970f,0.093220f),
            new Vector4(0.101796f,0.375000f,0.011976f,0.112288f),
            new Vector4(0.215569f,0.375000f,0.011976f,0.112288f),
            new Vector4(0.479042f,0.739407f,0.158683f,0.008475f),
            new Vector4(0.844311f,0.175847f,0.104790f,0.012712f),
            new Vector4(0.640719f,0.754237f,0.104790f,0.012712f),
            new Vector4(0.023952f,0.080508f,0.101796f,0.012712f),
            new Vector4(0.392216f,0.650424f,0.008982f,0.144068f),
            new Vector4(0.254491f,0.307203f,0.020958f,0.057203f),
            new Vector4(0.523952f,0.923729f,0.110778f,0.010593f),
            new Vector4(0.619760f,0.353814f,0.137725f,0.008475f),
            new Vector4(0.553892f,0.237288f,0.008982f,0.129237f),
            new Vector4(0.476048f,0.809322f,0.269461f,0.004237f),
            new Vector4(0.347305f,0.237288f,0.176647f,0.006356f),
            new Vector4(0.832335f,0.101695f,0.131737f,0.008475f),
            new Vector4(0.925150f,0.516949f,0.014970f,0.074153f),
            new Vector4(0.511976f,0.567797f,0.125749f,0.008475f),
            new Vector4(0.101796f,0.726695f,0.071856f,0.014831f),
            new Vector4(0.000000f,0.286017f,0.080838f,0.012712f),
            new Vector4(0.000000f,0.228814f,0.068862f,0.014831f),
            new Vector4(0.200599f,0.000000f,0.011976f,0.084746f),
            new Vector4(0.928144f,0.322034f,0.011976f,0.084746f),
            new Vector4(0.784431f,0.353814f,0.065868f,0.014831f),
            new Vector4(0.703593f,0.817797f,0.152695f,0.006356f),
            new Vector4(0.886228f,0.771186f,0.113772f,0.008475f),
            new Vector4(0.401198f,0.764831f,0.089820f,0.010593f),
            new Vector4(0.865269f,0.531780f,0.008982f,0.103814f),
            new Vector4(0.302395f,0.319915f,0.143713f,0.006356f),
            new Vector4(0.275449f,0.093220f,0.197605f,0.004237f),
            new Vector4(0.727545f,0.720339f,0.131737f,0.006356f),
            new Vector4(0.107784f,0.930085f,0.011976f,0.069915f),
            new Vector4(0.649701f,0.567797f,0.011976f,0.067797f),
            new Vector4(0.655689f,0.103814f,0.122754f,0.006356f),
            new Vector4(0.877246f,0.720339f,0.122754f,0.006356f),
            new Vector4(0.314371f,0.567797f,0.014970f,0.050847f),
            new Vector4(0.023952f,0.726695f,0.050898f,0.014831f),
            new Vector4(0.227545f,0.447034f,0.086826f,0.008475f),
            new Vector4(0.610778f,0.000000f,0.005988f,0.114407f),
            new Vector4(0.721557f,0.059322f,0.014970f,0.044492f),
            new Vector4(0.185629f,0.500000f,0.104790f,0.006356f),
            new Vector4(0.347305f,0.207627f,0.020958f,0.029661f),
            new Vector4(0.610778f,0.127119f,0.008982f,0.067797f),
            new Vector4(0.727545f,0.661017f,0.011976f,0.050847f),
            new Vector4(0.335329f,0.080508f,0.137725f,0.004237f),
            new Vector4(0.721557f,0.002119f,0.014970f,0.038136f),
            new Vector4(0.940120f,0.188559f,0.008982f,0.061441f),
            new Vector4(0.520958f,0.813559f,0.122754f,0.004237f),
            new Vector4(0.170659f,0.942797f,0.008982f,0.057203f),
            new Vector4(0.562874f,0.275424f,0.047904f,0.010593f),
            new Vector4(0.476048f,0.775424f,0.014970f,0.033898f),
            new Vector4(0.733533f,0.766949f,0.011976f,0.042373f),
            new Vector4(0.643713f,0.531780f,0.017964f,0.027542f),
            new Vector4(0.649701f,0.661017f,0.077844f,0.006356f),
            new Vector4(0.727545f,0.726695f,0.017964f,0.027542f),
            new Vector4(0.338323f,0.000000f,0.005988f,0.080508f),
            new Vector4(0.302395f,0.256356f,0.056886f,0.008475f),
            new Vector4(0.479042f,0.639831f,0.005988f,0.080508f),
            new Vector4(0.476048f,0.567797f,0.008982f,0.052966f),
            new Vector4(0.119760f,0.932203f,0.044910f,0.010593f),
            new Vector4(0.562874f,0.315678f,0.008982f,0.050847f),
            new Vector4(0.601796f,0.315678f,0.008982f,0.050847f),
            new Vector4(0.209581f,0.608051f,0.053892f,0.008475f),
            new Vector4(0.212575f,0.080508f,0.104790f,0.004237f),
            new Vector4(0.272455f,0.506356f,0.041916f,0.010593f),
            new Vector4(0.000000f,0.364407f,0.068862f,0.006356f),
            new Vector4(0.949102f,0.241525f,0.050898f,0.008475f),
            new Vector4(0.862275f,0.362288f,0.011976f,0.036017f),
            new Vector4(0.368263f,0.207627f,0.200599f,0.002119f),
            new Vector4(0.523952f,0.877119f,0.008982f,0.046610f),
            new Vector4(0.886228f,0.779661f,0.005988f,0.067797f),
            new Vector4(0.694611f,0.362288f,0.062874f,0.006356f),
            new Vector4(0.925150f,0.591102f,0.008982f,0.044492f),
            new Vector4(0.562874f,0.209746f,0.005988f,0.065678f),
            new Vector4(0.437126f,0.326271f,0.008982f,0.040254f),
            new Vector4(0.440120f,0.260593f,0.005988f,0.059322f),
            new Vector4(0.916168f,0.887712f,0.083832f,0.004237f),
            new Vector4(0.257485f,0.618644f,0.017964f,0.019068f),
            new Vector4(0.065868f,0.116525f,0.002994f,0.112288f),
            new Vector4(0.997006f,0.000000f,0.002994f,0.110169f),
            new Vector4(0.275449f,0.097458f,0.002994f,0.110169f),
            new Vector4(0.889222f,0.398305f,0.038922f,0.008475f),
            new Vector4(0.164671f,0.809322f,0.011976f,0.027542f),
            new Vector4(0.889222f,0.483051f,0.050898f,0.006356f),
            new Vector4(0.329341f,0.567797f,0.146707f,0.002119f),
            new Vector4(0.227545f,0.375000f,0.071856f,0.004237f),
            new Vector4(0.101796f,0.500000f,0.047904f,0.006356f),
            new Vector4(0.943114f,0.129237f,0.005988f,0.046610f),
            new Vector4(0.511976f,0.737288f,0.125749f,0.002119f),
            new Vector4(0.886228f,0.726695f,0.005988f,0.044492f),
            new Vector4(0.479042f,0.747881f,0.014970f,0.016949f),
            new Vector4(0.619760f,0.362288f,0.056886f,0.004237f),
            new Vector4(0.841317f,0.330508f,0.008982f,0.023305f),
            new Vector4(0.353293f,0.809322f,0.047904f,0.004237f),
            new Vector4(0.571856f,0.315678f,0.029940f,0.006356f),
            new Vector4(0.227545f,0.455508f,0.005988f,0.031780f),
            new Vector4(0.158683f,0.080508f,0.041916f,0.004237f),
            new Vector4(0.703593f,0.813559f,0.041916f,0.004237f),
            new Vector4(0.601796f,0.175847f,0.008982f,0.019068f),
            new Vector4(0.401198f,0.650424f,0.077844f,0.002119f),
            new Vector4(0.886228f,0.864407f,0.005988f,0.027542f),
            new Vector4(0.278443f,0.194915f,0.017964f,0.008475f),
            new Vector4(0.347305f,0.243644f,0.011976f,0.012712f),
            new Vector4(0.928144f,0.489407f,0.011976f,0.012712f),
            new Vector4(0.643713f,0.500000f,0.008982f,0.016949f),
            new Vector4(0.176647f,0.608051f,0.017964f,0.008475f),
            new Vector4(0.000000f,0.726695f,0.023952f,0.006356f),
            new Vector4(0.176647f,0.817797f,0.017964f,0.008475f),
            new Vector4(0.616766f,0.103814f,0.020958f,0.006356f),
            new Vector4(0.194611f,0.794492f,0.008982f,0.014831f),
            new Vector4(0.511976f,0.849576f,0.008982f,0.014831f),
            new Vector4(0.799401f,0.101695f,0.014970f,0.008475f),
            new Vector4(0.940120f,0.322034f,0.059880f,0.002119f),
            new Vector4(0.571856f,0.362288f,0.029940f,0.004237f),
            new Vector4(0.467066f,0.639831f,0.011976f,0.010593f),
            new Vector4(0.511976f,0.826271f,0.008982f,0.012712f),
            new Vector4(0.000000f,0.088983f,0.023952f,0.004237f),
            new Vector4(0.985030f,0.101695f,0.011976f,0.008475f),
            new Vector4(0.862275f,0.489407f,0.011976f,0.008475f),
            new Vector4(0.934132f,0.603814f,0.002994f,0.033898f),
            new Vector4(0.832335f,0.343220f,0.008982f,0.010593f),
            new Vector4(0.736527f,0.101695f,0.041916f,0.002119f),
            new Vector4(0.541916f,0.237288f,0.011976f,0.006356f),
            new Vector4(0.850299f,0.362288f,0.011976f,0.006356f),
            new Vector4(0.649701f,0.648305f,0.011976f,0.006356f),
            new Vector4(0.925150f,0.648305f,0.011976f,0.006356f),
            new Vector4(0.643713f,0.813559f,0.035928f,0.002119f),
            new Vector4(0.164671f,0.836864f,0.005988f,0.012712f),
        };

        static readonly Vector4 FactoryData=new Vector4(0.326347f,0.377119f,0.317365f,0.182203f);

        static readonly Vector2[] StairData=
        {
            new Vector2(0.720060f,0.000000f),
            new Vector2(0.008370f,0.079257f),
            new Vector2(0.324850f,0.077936f),
            new Vector2(0.645154f,0.101460f),
            new Vector2(0.821598f,0.103229f),
            new Vector2(0.973719f,0.108679f),
            new Vector2(0.618522f,0.119229f),
            new Vector2(0.285928f,0.208081f),
            new Vector2(0.532108f,0.244521f),
            new Vector2(0.084165f,0.280760f),
            new Vector2(0.830439f,0.336088f),
            new Vector2(0.084658f,0.367402f),
            new Vector2(0.286599f,0.367402f),
            new Vector2(0.682967f,0.367546f),
            new Vector2(0.112131f,0.492414f),
            new Vector2(0.299313f,0.498068f),
            new Vector2(0.659324f,0.505297f),
            new Vector2(0.930312f,0.507233f),
            new Vector2(0.261718f,0.511178f),
            new Vector2(0.495509f,0.573699f),
            new Vector2(0.941007f,0.596987f),
            new Vector2(0.184390f,0.621347f),
            new Vector2(0.657463f,0.640380f),
            new Vector2(0.929641f,0.641495f),
            new Vector2(0.487678f,0.724576f),
            new Vector2(0.868263f,0.726450f),
            new Vector2(0.011976f,0.739180f),
            new Vector2(0.210407f,0.799971f),
            new Vector2(0.184373f,0.810484f),
            new Vector2(0.685740f,0.818267f),
            new Vector2(0.178999f,0.842161f),
            new Vector2(0.505026f,0.842539f),
            new Vector2(0.642216f,0.921156f),
        };
    }
}
