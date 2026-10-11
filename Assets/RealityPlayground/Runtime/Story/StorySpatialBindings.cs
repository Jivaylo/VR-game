using UnityEngine;

namespace RealityPlayground.Story
{

    [ExecuteAlways]
    public sealed class StorySpatialBindings : MonoBehaviour
    {
        [SerializeField] StoryNavMarker[] routes;
        [SerializeField] Transform poster, returnPoint;
        [SerializeField] Vector3 returnOffset;

        public void Configure(RealityStoryDirector story)
        {
            routes=new[]{story.bedsideMarker,story.streetMarker,story.posterMarker,story.riftMarker,story.alleyMarker,story.mirrorMarker,story.chipMarker,story.endMarker};
            poster=story.poster.transform;returnPoint=story.environment.returnSpot;
            returnOffset=poster.InverseTransformPoint(returnPoint.position);
            foreach(var route in routes)if(route)route.destinationFollowsMarker=!route.isPortal;
            story.riftMarker.ConfigurePortal(poster);
            Synchronize();
        }
        void OnEnable()=>Synchronize();
        void Update()=>Synchronize();
        public void Synchronize()
        {
            if(routes!=null)foreach(var route in routes)if(route)route.SynchronizePlacement();
            if(poster && returnPoint)SetPosition(returnPoint,poster.TransformPoint(returnOffset));
        }
        public static void SetPosition(Transform target,Vector3 position)
        {
            if((target.position-position).sqrMagnitude<.00000001f)return;
            target.position=position;Record(target);
        }
        public static void Record(Transform target)
        {
            #if UNITY_EDITOR
            if(!Application.isPlaying && UnityEditor.PrefabUtility.IsPartOfPrefabInstance(target))
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            #endif
        }
    }
}
