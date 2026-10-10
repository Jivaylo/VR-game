using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Garden.Editor
{
    public static class SignalStyle
    {
        public static void Apply(GameObject root)
        {
            if (!root || EditorApplication.isPlaying) throw new InvalidOperationException("Open Garden in edit mode.");
            var loop=root.GetComponent<Loop>();
            var signal=ObjectNames.Find(root.transform, "Signal");
            var evidence=root.GetComponentInChildren<Evidence>(true);
            var bridge=root.GetComponentInChildren<Bridge>(true);
            if(!loop || !signal || !evidence || evidence.slots.Length<2 || !bridge || !bridge.model) throw new InvalidOperationException("Signal references are missing.");
            var old=ObjectNames.Find(signal, "Reference");
            if(old)Object.DestroyImmediate(old.gameObject);
            var group=new GameObject("Reference").transform;
            group.SetParent(signal,false);
            group.localPosition=new Vector3(0,1.65f,-.05f);
            var clue=group.gameObject.AddComponent<SignalClue>();
            clue.loop=loop;
            clue.views=new GameObject[3];
            for(int i=0;i<2;i++)
            {
                var copy=Object.Instantiate(evidence.slots[i],group,false);
                copy.name=ObjectNames.Short(i==0?"Arms":"Mirror");
                copy.transform.localPosition=Vector3.zero;
                copy.transform.localRotation=Quaternion.identity;
                copy.transform.localScale=Vector3.one*7;
                copy.SetActive(false);
                clue.views[i]=copy;
            }
            clue.arms=clue.views[0].GetComponentsInChildren<Transform>(true).Where(x=>ObjectNames.Matches(x.name, "Arm")).ToArray();
            var dark=AssetDatabase.LoadAssetAtPath<Material>("Assets/Garden/Art/Story/Screen.mat");
            var trim=AssetDatabase.LoadAssetAtPath<Material>("Assets/Garden/Art/Story/Label.mat");
            Frame(clue.views[1].transform,"Glass",new Vector3(0,-.004f,.004f),new Vector3(.15f,.13f,.003f),dark);
            for(int i=0;i<2;i++)
            {
                Frame(clue.views[1].transform,"Edge",new Vector3(i==0?-.078f:.078f,-.004f,.003f),new Vector3(.004f,.14f,.004f),trim);
                Frame(clue.views[1].transform,"Edge",new Vector3(0,i==0?-.073f:.066f,.003f),new Vector3(.16f,.004f,.004f),trim);
            }
            var device=Object.Instantiate(bridge.model,group,false);
            device.name="Bridge";
            foreach(var behavior in device.GetComponentsInChildren<MonoBehaviour>(true).Reverse()) Object.DestroyImmediate(behavior);
            foreach(var collider in device.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
            foreach(var audio in device.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(audio);
            device.transform.localPosition=Vector3.zero;
            device.transform.localRotation=Quaternion.identity;
            device.transform.localScale=bridge.model.transform.localScale*3;
            device.SetActive(false);
            clue.views[2]=device;
            foreach(var node in group.GetComponentsInChildren<Transform>(true))node.gameObject.layer=11;
            EditorUtility.SetDirty(clue);
        }

        static void Frame(Transform parent,string name,Vector3 point,Vector3 size,Material material)
        {
            var part=GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name=ObjectNames.Short(name);
            part.transform.SetParent(parent,false);
            part.transform.localPosition=point;
            part.transform.localScale=size;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            var renderer=part.GetComponent<Renderer>();
            renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
    }
}
