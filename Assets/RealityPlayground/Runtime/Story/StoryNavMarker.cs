using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace RealityPlayground.Story
{

    public sealed class StoryNavMarker : PlaygroundTarget
    {
        public Transform destination;
        public TeleportationAnchor anchor;
        public Transform glyph;
        public UnityEvent arrived = new UnityEvent();
        public float desktopFacing;
        public bool isPortal;
        [Tooltip("Keep this route's landing point under its visible marker when it is moved.")]
        public bool destinationFollowsMarker;
        [SerializeField] Transform portalFrame;
        [SerializeField] Vector3 glyphRest=new Vector3(0,.33f,0);
        public Vector3 AimWorldPoint => transform.TransformPoint(isPortal ? new Vector3(0,1.45f,0) : new Vector3(0,.14f,0));
        bool used;
        readonly Dictionary<XRBaseInputInteractor, bool> hoverers = new Dictionary<XRBaseInputInteractor, bool>();
        public bool Available => isActiveAndEnabled && !used;

        public void ConfigurePortal(Transform wall)
        {
            if(!wall || !anchor)return;
            isPortal=true;
            portalFrame=wall;destinationFollowsMarker=false;
            SynchronizePlacement();

            anchor.filterSelectionByHitNormal=false;
            var opening=ObjectNames.Find(transform, "Portal opening target");

            for(int i=transform.childCount-1;i>=0;i--)
            {
                var child=transform.GetChild(i);
                if(!ObjectNames.Matches(child.name, "Portal Target"))continue;
                if(!opening){opening=child;opening.name="Portalopeningtarget";}
                else
                {
                    var oldCollider=child.GetComponent<Collider>();
                    if(oldCollider)anchor.colliders.Remove(oldCollider);
                    child.gameObject.SetActive(false);
                    if(Application.isPlaying)Destroy(child.gameObject);else DestroyImmediate(child.gameObject);
                }
            }
            if(!opening){opening=new GameObject("Portalopeningtarget").transform;opening.SetParent(transform,false);}
            var hitbox=opening.GetComponent<BoxCollider>();if(!hitbox)hitbox=opening.gameObject.AddComponent<BoxCollider>();
            hitbox.center=new Vector3(0,1.45f,0);hitbox.size=new Vector3(1.7f,2.25f,.24f);
            if(!anchor.colliders.Contains(hitbox))anchor.colliders.Add(hitbox);
            foreach(var collider in anchor.colliders)if(collider)collider.isTrigger=true;
            var halo=ObjectNames.Find(transform, "Arrival halo");
            if(halo){halo.localPosition=new Vector3(0,1.45f,-.2f);halo.localRotation=Quaternion.Euler(90,0,0);halo.localScale=new Vector3(1.35f,1,1.75f);}
            glyphRest=new Vector3(0,1.28f,-.22f);
            if(glyph){glyph.localPosition=glyphRest;glyph.localScale=Vector3.one*.045f;}
            var hint=GetComponentInChildren<TextMesh>(true);
            if(hint){hint.text="AIM INTO THE RIFT\nTRIGGER / CLICK";hint.transform.localPosition=new Vector3(0,1.56f,-.16f);hint.transform.localRotation=Quaternion.identity;hint.characterSize=.028f*10/hint.fontSize;}
        }

        public void SynchronizePlacement()
        {
            if(isPortal && portalFrame)
            {
                StorySpatialBindings.SetPosition(transform,portalFrame.TransformPoint(new Vector3(0,0,-.18f)));
                if(Quaternion.Angle(transform.rotation,portalFrame.rotation)>.001f)
                {transform.rotation=portalFrame.rotation;StorySpatialBindings.Record(transform);}
                Vector3 parentScale=transform.parent?transform.parent.lossyScale:Vector3.one;
                Vector3 scale=portalFrame.lossyScale;
                scale=new Vector3(scale.x/Mathf.Max(.0001f,parentScale.x),scale.y/Mathf.Max(.0001f,parentScale.y),scale.z/Mathf.Max(.0001f,parentScale.z));
                if((transform.localScale-scale).sqrMagnitude>.00000001f)
                {transform.localScale=scale;StorySpatialBindings.Record(transform);}
            }
            else if(destinationFollowsMarker && destination && destination!=transform)
                StorySpatialBindings.SetPosition(destination,transform.position);
            if(anchor && destination && anchor.teleportAnchorTransform!=destination)anchor.teleportAnchorTransform=destination;
        }

        public static StoryNavMarker Create(Transform parent, string label, Vector3 position, Transform destination)
        {
            var go = new GameObject("TeleportRoute");
            go.transform.SetParent(parent, false); go.transform.position = position;
            var marker = go.AddComponent<StoryNavMarker>(); marker.destination = destination;
            var cyan = GreyboxUtil.Material("Route liquid cyan", new Color(.03f,.95f,.73f), 2.2f);
            var points = new Vector3[48];
            for (int i=0;i<points.Length;i++) { float a=i*Mathf.PI*2/points.Length; points[i]=new Vector3(Mathf.Cos(a)*.55f,.025f,Mathf.Sin(a)*.55f); }
            GreyboxUtil.Line("Arrival halo", go.transform, points, cyan, .022f, true);
            marker.glyph = GreyboxUtil.Primitive("Floating navigation diamond", PrimitiveType.Cube,go.transform,new Vector3(0,.33f,0),Vector3.one*.13f,cyan,false).transform;
            marker.glyph.localRotation=Quaternion.Euler(0,45,45);
            var plate = go.AddComponent<BoxCollider>(); plate.center=new Vector3(0,.12f,0); plate.size=new Vector3(1.25f,.35f,1.25f);
            var text=GreyboxUtil.Label(label+"\nAIM / TELEPORT",go.transform,new Vector3(0,.73f,0),.105f,new Color(.5f,1,.87f));
            marker.anchor=go.AddComponent<TeleportationAnchor>();
            marker.anchor.teleportAnchorTransform=destination;
            marker.anchor.matchOrientation=MatchOrientation.None;
            marker.anchor.matchDirectionalInput=false;
            marker.anchor.teleportTrigger=BaseTeleportationInteractable.TeleportTrigger.OnSelectExited;
            marker.anchor.colliders.Clear(); marker.anchor.colliders.Add(plate);
            marker.anchor.interactionLayers=~0;
            return marker;
        }
        void OnEnable()
        {
            used=false;
            if (!anchor) anchor=GetComponent<TeleportationAnchor>();
            if (!anchor) return;
            SynchronizePlacement();
            anchor.teleporting.AddListener(Queued);
            anchor.activated.AddListener(Triggered);
            anchor.hoverEntered.AddListener(HoverEnter);
            anchor.hoverExited.AddListener(HoverExit);
        }
        void OnDisable()
        {
            if (anchor) { anchor.teleporting.RemoveListener(Queued); anchor.activated.RemoveListener(Triggered); anchor.hoverEntered.RemoveListener(HoverEnter); anchor.hoverExited.RemoveListener(HoverExit); }
            foreach(var pair in hoverers) if(pair.Key) pair.Key.allowHoveredActivate=pair.Value;
            hoverers.Clear();
        }
        void HoverEnter(HoverEnterEventArgs e)
        {
            if(e.interactorObject is XRBaseInputInteractor input && !hoverers.ContainsKey(input)) { hoverers.Add(input,input.allowHoveredActivate); input.allowHoveredActivate=true; }
        }
        void HoverExit(HoverExitEventArgs e)
        {
            if(e.interactorObject is XRBaseInputInteractor input && hoverers.TryGetValue(input,out bool previous)) { input.allowHoveredActivate=previous; hoverers.Remove(input); }
        }
        void Triggered(ActivateEventArgs e) { if(Available) anchor.RequestTeleport(); }
        void Queued(TeleportingEventArgs e) { if(Available) { used=true; StartCoroutine(Arrival()); } }
        IEnumerator Arrival()
        {

            float until=Time.unscaledTime+2;
            yield return null;yield return null;
            while(destination && Vector2.Distance(new Vector2(RealityPlayer.FeetPosition.x,RealityPlayer.FeetPosition.z),new Vector2(destination.position.x,destination.position.z))>.4f && Time.unscaledTime<until)yield return null;
            if(!destination || Vector2.Distance(new Vector2(RealityPlayer.FeetPosition.x,RealityPlayer.FeetPosition.z),new Vector2(destination.position.x,destination.position.z))>.4f)
            {
                used=false;
                Debug.LogWarning("The route teleport did not complete; its destination remains available to try again.",this);
                yield break;
            }
            RealityPlayer.FaceDirection(desktopFacing);
            arrived.Invoke();
        }
        public override void Activate()
        {
            if(!Available || !destination) return;
            if(RealityPlayer.IsXR) anchor.RequestTeleport();
            else { used=true; RealityPlayer.TeleportTo(destination.position); StartCoroutine(Arrival()); }
        }
        void Update()
        {
            if(glyph) { glyph.localPosition=glyphRest+Vector3.up*Mathf.Sin(Time.time*2.5f)*.035f; glyph.Rotate(0,45*Time.deltaTime,0,Space.World); }
        }
    }
}
