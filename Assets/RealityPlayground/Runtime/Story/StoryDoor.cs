using UnityEngine;
using UnityEngine.Events;

namespace RealityPlayground.Story
{
    public sealed class StoryDoor : PlaygroundTarget
    {
        public bool interactionEnabled;
        public UnityEvent opened = new UnityEvent();
        public Transform leaf;
        bool used;
        public override bool SupportsTriggerActivation => true;
        public override void Activate()
        {
            if(!interactionEnabled || used) return;
            used=true; opened.Invoke();
        }
        void Update() { if(leaf) leaf.localRotation=Quaternion.Slerp(leaf.localRotation,Quaternion.Euler(0,used?-88:0,0),Time.deltaTime*3); }
        public static StoryDoor Create(Transform parent)
        {
            var root=new GameObject("ApartmentDoor"); root.transform.SetParent(parent,false);
            var door=root.AddComponent<StoryDoor>();
            var stone=GreyboxUtil.Material("Door Porcelain",new Color(.34f,.28f,.44f));
            var light=GreyboxUtil.Material("Door Access",new Color(.08f,1,.8f),2);
            var hinge=new GameObject("Doorhinge").transform; hinge.SetParent(root.transform,false); hinge.localPosition=new Vector3(-.8f,0,0); door.leaf=hinge;
            GreyboxUtil.Primitive("Apartment door",PrimitiveType.Cube,hinge,new Vector3(.8f,1.3f,0),new Vector3(1.6f,2.6f,.12f),stone);
            GreyboxUtil.Primitive("Glowing access bar",PrimitiveType.Cube,hinge,new Vector3(1.35f,1.25f,-.1f),new Vector3(.045f,.45f,.035f),light,false);
            GreyboxUtil.Label("YOUR DAY IS READY\nPoint + trigger to leave",hinge,new Vector3(.8f,1.9f,-.075f),.11f,Color.white);
            return door;
        }
    }
}
