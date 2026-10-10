using System.Collections.Generic;
using UnityEngine;

namespace RealityPlayground.Story
{
    [DefaultExecutionOrder(100)]
    public sealed class StoryFallingBricks : MonoBehaviour
    {
        public WallBreach breach;
        readonly List<Transform> bricks=new List<Transform>();
        readonly List<Vector3> rest=new List<Vector3>();
        float age=-1;
        public void BeginFall()
        {
            if(age>=0)return;
            foreach(var t in GetComponentsInChildren<Transform>())
                if(ObjectNames.StartsWith(t.name, "Brick ") && ObjectNames.Contains(t.name, ":")) {bricks.Add(t);rest.Add(t.localPosition);var c=t.GetComponent<Collider>();if(c)c.enabled=false;}
            age=0;
        }
        void LateUpdate()
        {
            if(age<0)return;age+=Time.deltaTime;
            for(int i=0;i<bricks.Count;i++) if(bricks[i])
            {
                float t=Mathf.Max(0,age-(i%9)*.065f);
                var start=rest[i];float sign=start.x>=0?1:-1;
                var end=new Vector3(start.x+sign*(.38f+(i%4)*.06f),.12f+(i%4)*.035f,start.z-.45f-(i%7)*.085f);
                bricks[i].localPosition=Vector3.Lerp(start,end,Mathf.Clamp01(t*t*.8f));
                bricks[i].localRotation=Quaternion.Euler(Mathf.Min(1,t)*((i%3)*33),Mathf.Min(1,t)*i*17,Mathf.Min(1,t)*sign*35);
            }
        }
    }
}
