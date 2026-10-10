using Garden;
using UnityEngine;

namespace Garden.Editor
{
    public static partial class GardenBuilder
    {
        static void BuildDetails()
        {
            BreakfastDetail();
            StationDetail();
            HomeDetail();
            OutsideDetail();
        }

        static void BreakfastDetail()
        {
            var root = Module("Wrapper", Breakfast + new Vector3(.05f,1.27f,.92f));
            var peel = root.gameObject.AddComponent<Peel>(); peel.loop = loop;
            peel.sheet = Cube("Sleeve", root, root.position + new Vector3(0,.018f,0), new Vector3(.38f,.035f,.26f),concrete,false).transform;
            var collider = peel.sheet.gameObject.AddComponent<BoxCollider>(); Wire(root.gameObject,collider);
            peel.clue = Text("STANDARD BASE\nPRESENTATION VARIABLE\nX  CHECK  X",root,root.position+new Vector3(0,.008f,-.07f),.036f).gameObject;
            peel.clue.transform.rotation=Quaternion.Euler(70,0,0);
            peel.clue.GetComponent<TMPro.TMP_Text>().rectTransform.sizeDelta=new Vector2(.37f,.19f);
            peel.presentation=Empty("Meal View",root,root.position).gameObject;
            for(int i=0;i<3;i++) Cube("Topping",peel.presentation.transform,root.position+new Vector3((i-1)*.085f,.055f,.15f),new Vector3(.09f,.025f,.08f),green,false).layer=11;
            Text("Grip sleeve. Pull down.",root,root.position+new Vector3(0,.23f,.04f),.045f).rectTransform.sizeDelta=new Vector2(.9f,.17f);
            Pad("Wrapper Pad",root,Breakfast+new Vector3(.08f,0,.3f));
        }

        static void StationDetail()
        {
            var root=Module("Map",SouthMetro+new Vector3(-.75f,1.25f,.5f));
            Cube("Frame",root,root.position+Vector3.forward*.04f,new Vector3(.8f,.8f,.06f),metal,false);
            var peel=root.gameObject.AddComponent<Peel>(); peel.loop=loop; peel.kind=Peel.Kind.Ad; peel.pull=new Vector3(0,1,-.5f); peel.distance=.22f;
            peel.sheet=Cube("Ad",root,root.position,new Vector3(.72f,.7f,.018f),blue,false).transform;
            Text("ALL USEFUL\nDESTINATIONS",peel.sheet,root.position+Vector3.back*.015f,.065f).rectTransform.sizeDelta=new Vector2(.68f,.6f);
            var col=peel.sheet.gameObject.AddComponent<BoxCollider>(); Wire(root.gameObject,col);
            peel.clue=Empty("Diagram",root,root.position).gameObject;
            Text("OLD SERVICE LINE\nResidence > Work > Exterior\n\nX  CHECK  X\nWatch the arms",peel.clue.transform,root.position+Vector3.back*.007f,.065f).rectTransform.sizeDelta=new Vector2(.7f,.65f);
            Act("Lift ad",root,root.position+new Vector3(0,-.5f,-.025f),peel.Open);
            var train=world.GetComponentInChildren<Train>(true);
            if(train && train.cabin)
            {
                var car=train.cabin.parent;
                var pos=car.position+new Vector3(-.9f,1.5f,2.3f);
                Text("SERVICE RECORD\nX > CHECK > X\nPhysical line continues beyond map",car,pos,.07f).rectTransform.sizeDelta=new Vector2(1.35f,.48f);
                Person("Passenger",car,car.position+new Vector3(-1.43f,-.15f,-.4f),road);
            }
        }

        static void HomeDetail()
        {
            var root=Module("Delivery",Home+new Vector3(1.45f,1.08f,.74f));
            Cube("Shelf",root,root.position+Vector3.down*.105f,new Vector3(.68f,.11f,.5f),metal);
            Cube("Hatch",root,Home+new Vector3(1.8f,1.6f,2.35f),new Vector3(.8f,.7f,.08f),metal,false);
            var delivery=root.gameObject.AddComponent<Delivery>(); delivery.loop=loop;
            delivery.drone=Empty("Drone",root,Home+new Vector3(1.8f,1.75f,2.23f));
            Cube("Housing",delivery.drone,delivery.drone.position,new Vector3(.42f,.18f,.26f),metal,false);
            Cube("Optic",delivery.drone,delivery.drone.position+new Vector3(0,0,-.15f),new Vector3(.13f,.09f,.035f),glow,false);
            delivery.landing=Empty("Approach",root,root.position+new Vector3(0,.30f,-.06f));
            delivery.package=Cube("Dinner",root,root.position,new Vector3(.31f,.10f,.23f),concrete,false);
            Text("STANDARD BASE\n07",delivery.package.transform,root.position+new Vector3(0,.055f,0),.025f).transform.rotation=Quaternion.Euler(90,0,0);
            if(loop.receiver) { loop.receiver.transform.position=Home+new Vector3(1.8f,1.94f,2.22f); loop.receiver.rectTransform.sizeDelta=new Vector2(1.2f,.43f); }
            Text("NEWS\n06 OCT 2056",root,Home+new Vector3(.4f,1.86f,2.2f),.07f).rectTransform.sizeDelta=new Vector2(.9f,.27f);
            if(loop.clock) { loop.clock.transform.position=Home+new Vector3(-.75f,2.02f,2.33f); loop.clock.rectTransform.sizeDelta=new Vector2(1.2f,.85f); loop.clock.fontSize=.9f; }
            var view=GardenView(Home+new Vector3(.4f,2.3f,2.12f));
            GardenChoiceDetail(view);
            Text("Tomorrow is prepared\n07:00  07:30  08:00  09:00\n17:00  19:00  22:00",root,Home+new Vector3(-2.05f,1.25f,2.34f),.075f).rectTransform.sizeDelta=new Vector2(1.15f,.5f);
        }

        static Transform GardenView(Vector3 point)
        {
            var root=Module("Garden View",point);
            root.SetParent(loop.overlays.transform,true);
            Cube("Sky",root,point+Vector3.forward*.13f,new Vector3(1.15f,.6f,.025f),blue,false);
            Cube("Grass",root,point+new Vector3(0,-.25f,.005f),new Vector3(1.15f,.035f,.23f),green,false);
            GardenHill(root,new Vector3(-.26f,-.235f,.065f),.64f,.33f,green);
            GardenHill(root,new Vector3(.2f,-.235f,.035f),.62f,.24f,concrete);
            var sun=Cylinder("Sun",root,point+new Vector3(.39f,.18f,.095f),new Vector3(.14f,.009f,.14f),glow,false);
            sun.transform.localRotation=Quaternion.Euler(90,0,0);
            for(int i=0;i<2;i++)
            {
                var tree=point+new Vector3(i==0?-.4f:-.17f,-.15f,-.025f);
                Cube("Trunk",root,tree,new Vector3(.022f,.17f,.022f),metal,false);
                var crown=Cube("Crown",root,tree+Vector3.up*.135f,new Vector3(.14f,.15f,.105f),green,false);
                crown.transform.localRotation=Quaternion.Euler(0,20+i*25,12-i*20);
            }
            var bench=point+new Vector3(.21f,-.17f,-.065f);
            Cube("Seat",root,bench,new Vector3(.26f,.025f,.08f),metal,false);
            Cube("Back",root,bench+new Vector3(0,.065f,.034f),new Vector3(.26f,.1f,.018f),metal,false);
            for(int i=0;i<2;i++) Cube("Leg",root,bench+new Vector3(i==0?-.09f:.09f,-.038f,0),new Vector3(.018f,.075f,.05f),metal,false);
            for(int i=0;i<2;i++)
            {
                Cube("Frame",root,point+new Vector3(i==0?-.59f:.59f,0,-.11f),new Vector3(.035f,.64f,.035f),glow,false);
                Cube("Frame",root,point+new Vector3(0,i==0?-.315f:.315f,-.11f),new Vector3(1.215f,.035f,.035f),glow,false);
            }
            var label=Text("GARDEN 07",root,point+new Vector3(0,-.315f,-.135f),.025f);
            label.rectTransform.sizeDelta=new Vector2(.5f,.035f);
            foreach(var child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=11;
            return root;
        }

        static void GardenHill(Transform root,Vector3 position,float width,float height,Material material)
        {
            var hill=Empty("Hill",root,root.position+position);
            var mesh=new Mesh{name="Hill"};
            float x=width*.5f;
            mesh.vertices=new[]{new Vector3(-x,0,0),new Vector3(0,height,0),new Vector3(x,0,0),new Vector3(-x,0,.035f),new Vector3(0,height,.035f),new Vector3(x,0,.035f)};
            mesh.triangles=new[]{0,1,2,3,5,4,0,3,4,0,4,1,1,4,5,1,5,2,2,5,3,2,3,0};
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            hill.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=hill.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial=material; renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static void GardenChoiceDetail(Transform source)
        {
            if(!loop.gardenChoice) return;
            var view=UnityEngine.Object.Instantiate(source.gameObject,loop.gardenChoice.transform,false);
            view.name="GardenView";
            view.transform.localPosition=new Vector3(0,1.0f,.05f);
            view.transform.localRotation=Quaternion.identity;
        }

        static void OutsideDetail()
        {
            var root=Module("Table",Outside+new Vector3(4,.86f,1));
            var stand=root.gameObject.AddComponent<CupStand>(); stand.loop=loop;
            var cup=Empty("Cup",root,root.position+new Vector3(-.45f,0,0));
            stand.cup=cup.gameObject;
            var carry=cup.gameObject.AddComponent<Carry>(); carry.model=Cylinder("Model",cup,cup.position,new Vector3(.13f,.09f,.13f),blue,false).transform;
            var cupCollider=cup.gameObject.AddComponent<BoxCollider>(); cupCollider.size=Vector3.one*.22f; Wire(cup.gameObject,cupCollider);
            Cube("Bread",root,root.position+new Vector3(.4f,-.025f,0),new Vector3(.24f,.08f,.14f),concrete,false);
            var water=root.gameObject.AddComponent<Water>(); water.loop=loop;
            water.spout=Empty("Spout",root,root.position+new Vector3(.1f,.08f,0));
            Cube("Pot",root,root.position+new Vector3(.46f,-.04f,-.08f),new Vector3(.2f,.18f,.2f),metal,false);
            water.soil=Empty("Soil",root,root.position+new Vector3(.46f,.05f,-.08f));
            Cube("Stem",root,water.soil.position+Vector3.up*.1f,new Vector3(.018f,.2f,.018f),green,false);
            Cube("Leaf",root,water.soil.position+new Vector3(.04f,.18f,0),new Vector3(.1f,.035f,.06f),green,false);
            water.drops=new Transform[10];
            for(int i=0;i<water.drops.Length;i++) water.drops[i]=Cube("Drop",root,water.spout.position,Vector3.one*.012f,blue,false).transform;
            var outsideRoot=ObjectNames.Find(world, "Outside");
            var original=outsideRoot?ObjectNames.Find(outsideRoot, "Bird"):null;
            if(original) UnityEngine.Object.DestroyImmediate(original.gameObject);
            var bird=Empty("Bird",root,Outside+new Vector3(3.4f,1.15f,1));
            Cube("Body",bird,bird.position,new Vector3(.15f,.11f,.23f),concrete,false);
            Cube("Head",bird,bird.position+new Vector3(0,.06f,.13f),Vector3.one*.09f,concrete,false);
            var flight=bird.gameObject.AddComponent<Bird>(); flight.loop=loop; flight.wings=new Transform[2];
            for(int i=0;i<2;i++) flight.wings[i]=Cube("Wing",bird,bird.position+new Vector3(i==0?-.12f:.12f,0,0),new Vector3(.19f,.025f,.16f),road,false).transform;
        }
    }
}
