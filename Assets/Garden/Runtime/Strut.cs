using UnityEngine;

namespace Garden
{
    public sealed class Strut : MonoBehaviour
    {
        public Transform end;
        public Vector3 contact;
        public Transform shaft;
        public Transform sleeve;
        public Transform joint;

        void LateUpdate() { Refresh(); }

        public void Refresh()
        {
            if (!end || !shaft || !sleeve) return;
            Vector3 offset=transform.InverseTransformPoint(end.TransformPoint(contact));
            float length=offset.magnitude;
            if(length<.001f)return;
            Quaternion rotation=Quaternion.FromToRotation(Vector3.up,offset);
            shaft.SetLocalPositionAndRotation(offset*.5f,rotation);
            shaft.localScale=new Vector3(.075f,length*.5f,.075f);
            sleeve.SetLocalPositionAndRotation(offset*.24f,rotation);
            sleeve.localScale=new Vector3(.13f,length*.24f,.13f);
            if(joint)joint.localPosition=offset;
        }
    }
}
