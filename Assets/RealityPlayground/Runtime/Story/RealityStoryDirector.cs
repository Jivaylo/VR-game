using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RealityPlayground.Story
{
    public enum StoryStage { Waking, Alarm, Bedroom, Door, Leaving, Landing, Street, Poster, PosterHacking, RiftReady, Encounter, Returning, Alley, Wall, WallHacking, Chip, Revealing, Complete }

    public sealed class RealityStoryDirector : MonoBehaviour
    {
        public StoryEnvironment environment;
        public RealityPlayer player;
        public StoryAlarm alarm;
        public StoryDoor door;
        public StoryPoster poster;
        public StoryAngel angel;
        public StoryChip chip;
        public LiquidMirror mirror;
        public WallBreach breach;
        public StoryFallingBricks fallingBricks;
        public StoryTransition transition;
        public StoryHackSequence hackSequence;
        public StoryNavMarker bedsideMarker, streetMarker, posterMarker, riftMarker, alleyMarker, mirrorMarker, chipMarker, endMarker;
        public GameObject wallInstruction, mirrorInstruction, closingMessage;
        public Light keyLight;
        public Volume atmosphere;
        public StoryStage Stage { get; private set; }
        public bool MirrorTouched { get; private set; }
        public float StageAge { get; private set; }
        ColorAdjustments grading;
        bool background;

        void Start()
        {
            background=Application.runInBackground;Application.runInBackground=true;
            riftMarker.ConfigurePortal(poster.transform);
            var links=GetComponent<StorySpatialBindings>();if(links)links.Synchronize();
            if(!hackSequence)hackSequence=GetComponentInChildren<StoryHackSequence>(true);
            poster.deferRiftOpening=hackSequence;
            breach.deferRuptureUntilAuthorized=hackSequence;
            breach.touched.AddListener(WallTouched);
            alarm.snoozed.AddListener(AlarmSnoozed);door.opened.AddListener(DoorOpened);
            poster.torn.AddListener(PosterTorn);angel.speechFinished.AddListener(AngelFinished);chip.inserted.AddListener(ChipInserted);
            bedsideMarker.arrived.AddListener(()=>{if(Stage==StoryStage.Bedroom){SetStage(StoryStage.Door);door.interactionEnabled=true;}});
            streetMarker.arrived.AddListener(()=>{if(Stage==StoryStage.Landing){SetStage(StoryStage.Street);ShowMarker(posterMarker);}});
            posterMarker.arrived.AddListener(()=>{if(Stage==StoryStage.Street){SetStage(StoryStage.Poster);poster.interactionEnabled=true;ShowMarker(null);}});
            riftMarker.arrived.AddListener(()=>{if(Stage==StoryStage.RiftReady)StartCoroutine(Encounter());});
            alleyMarker.arrived.AddListener(()=>{if(Stage==StoryStage.Alley)ShowMarker(mirrorMarker);});
            mirrorMarker.arrived.AddListener(()=>{if(Stage==StoryStage.Alley){ShowMarker(chipMarker);if(mirrorInstruction)mirrorInstruction.SetActive(true);}});
            chipMarker.arrived.AddListener(()=>{if(Stage==StoryStage.Alley){SetStage(StoryStage.Wall);ShowMarker(null);breach.enabled=true;breach.ArmForFreshTouch();if(wallInstruction)wallInstruction.SetActive(true);}});
            endMarker.arrived.AddListener(()=>{if(Stage==StoryStage.Complete){ShowMarker(null);if(closingMessage)closingMessage.SetActive(true);}});
            if(atmosphere && atmosphere.profile) atmosphere.profile.TryGet(out grading);
            alarm.interactionEnabled=false;door.interactionEnabled=false;poster.interactionEnabled=false;chip.interactionEnabled=false;
            chip.gameObject.SetActive(false);breach.enabled=false;angel.gameObject.SetActive(false);
            if(wallInstruction)wallInstruction.SetActive(false);if(mirrorInstruction)mirrorInstruction.SetActive(false);if(closingMessage)closingMessage.SetActive(false);
            environment.SetReveal(0);ShowMarker(null);SetStage(StoryStage.Waking);
            StartCoroutine(Wake());
        }
        void SetStage(StoryStage stage){Stage=stage;StageAge=0;}
        IEnumerator Wake()
        {
            float t=0;
            Vector3 xrStart=player.xrOrigin.transform.position;
            while(t<3.2f)
            {
                float p=Mathf.SmoothStep(0,1,t/3.2f);
                if(RealityPlayer.IsXR) player.xrOrigin.transform.position=xrStart-Vector3.up*(.48f*(1-p));
                else player.SetDesktopWakePose(p);
                t+=Time.deltaTime;yield return null;
            }
            player.SetDesktopWakePose(1);
            if(RealityPlayer.IsXR)player.xrOrigin.transform.position=xrStart;
            SetStage(StoryStage.Alarm);alarm.interactionEnabled=true;
        }
        void AlarmSnoozed(){if(Stage!=StoryStage.Alarm)return;SetStage(StoryStage.Bedroom);ShowMarker(bedsideMarker);}
        void DoorOpened(){if(Stage==StoryStage.Door)StartCoroutine(LeaveRoom());}
        IEnumerator LeaveRoom()
        {
            SetStage(StoryStage.Leaving);ShowMarker(null);
            yield return transition.PlayBlink(.84f,()=>{RealityPlayer.TeleportTo(environment.landingSpot.position);RealityPlayer.FaceDirection(0);});
            SetStage(StoryStage.Landing);ShowMarker(streetMarker);
        }
        void PosterTorn(){if(Stage==StoryStage.Poster)StartCoroutine(HackPoster());}
        IEnumerator HackPoster()
        {
            SetStage(StoryStage.PosterHacking);ShowMarker(null);poster.SetRiftOpen(false);
            if(hackSequence)yield return hackSequence.Play(poster.transform,false);
            environment.liminalRoot.SetActive(true);poster.SetRiftOpen(true);

            yield return new WaitForSeconds(1.7f);
            SetStage(StoryStage.RiftReady);ShowMarker(riftMarker);
        }
        void WallTouched(){if(Stage==StoryStage.Wall && hackSequence)StartCoroutine(HackWall());}
        IEnumerator HackWall()
        {
            SetStage(StoryStage.WallHacking);if(wallInstruction)wallInstruction.SetActive(false);
            breach.SetHackProgress(.12f);
            yield return hackSequence.Play(breach.transform,true);
            breach.SetHackProgress(1);breach.BeginRupture();SetStage(StoryStage.Wall);
        }
        IEnumerator Encounter()
        {
            SetStage(StoryStage.Encounter);ShowMarker(null);RealityPlayer.FaceDirection(0);environment.liminalRoot.SetActive(true);angel.gameObject.SetActive(true);
            yield return transition.Play(3.2f);
            angel.BeginEncounter();
        }
        void AngelFinished(){if(Stage==StoryStage.Encounter)StartCoroutine(ReturnOutside());}
        IEnumerator ReturnOutside()
        {
            SetStage(StoryStage.Returning);
            yield return transition.Play(4,()=>{RealityPlayer.TeleportTo(environment.returnSpot.position);RealityPlayer.FaceDirection(0);},true);
            environment.liminalRoot.SetActive(false);angel.gameObject.SetActive(false);poster.SetRiftOpen(false);SetStage(StoryStage.Alley);ShowMarker(alleyMarker);
        }
        void Update()
        {
            StageAge+=Time.deltaTime;
            if(Stage==StoryStage.WallHacking && breach)breach.SetHackProgress(Mathf.SmoothStep(.12f,1,Mathf.Clamp01(StageAge/1.15f)));
            if(mirror && mirror.AttachedHandCount>0)MirrorTouched=true;
            if(Stage==StoryStage.Wall && breach && breach.IsOpen)
            {
                fallingBricks.BeginFall();chip.gameObject.SetActive(true);chip.interactionEnabled=true;SetStage(StoryStage.Chip);
                if(wallInstruction)wallInstruction.SetActive(false);
            }
        }
        void ChipInserted(){if(Stage==StoryStage.Chip)StartCoroutine(Reveal());}
        IEnumerator Reveal()
        {
            SetStage(StoryStage.Revealing);ShowMarker(null);if(mirrorInstruction)mirrorInstruction.SetActive(false);
            yield return transition.Play(8,null,false,p=>
            {
                float reveal=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,.92f,p));environment.SetReveal(reveal);

                if(grading){grading.saturation.Override(-70*reveal);grading.postExposure.Override(-.45f*reveal);}
                if(keyLight){keyLight.intensity=Mathf.Lerp(1.15f,.48f,reveal);keyLight.color=Color.Lerp(new Color(.8f,.88f,1),new Color(.68f,.72f,.76f),reveal);}
                RenderSettings.ambientLight=Color.Lerp(new Color(.43f,.44f,.53f),new Color(.22f,.23f,.25f),reveal);
            });
            environment.SetReveal(1);poster.gameObject.SetActive(false);
            if(breach)
            {
                breach.enabled=false;
                foreach(var t in breach.GetComponentsInChildren<Transform>(true))if(ObjectNames.Contains(t.name, "volume")||ObjectNames.StartsWith(t.name, "Displaced reality"))t.gameObject.SetActive(false);
                var deadBrick=new Material(Shader.Find("Universal Render Pipeline/Lit"));deadBrick.color=new Color(.23f,.24f,.24f);
                foreach(var renderer in breach.GetComponentsInChildren<Renderer>())
                    if(ObjectNames.StartsWith(renderer.name, "Brick ")){renderer.SetPropertyBlock(null);renderer.sharedMaterial=deadBrick;}
            }
            SetStage(StoryStage.Complete);ShowMarker(endMarker);
        }
        void ShowMarker(StoryNavMarker marker)
        {
            foreach(var m in new[]{bedsideMarker,streetMarker,posterMarker,riftMarker,alleyMarker,mirrorMarker,chipMarker,endMarker})if(m)m.gameObject.SetActive(m==marker);
        }
        void OnDestroy(){Application.runInBackground=background;}
    }
}
