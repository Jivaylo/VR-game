using System.Collections;
using RealityPlayground;
using TMPro;
using UnityEngine;

namespace Garden
{
    public sealed class Train : MonoBehaviour
    {
        public Loop loop;
        public Transform south;
        public Transform work;
        public Transform cabin;
        public GameObject southView;
        public GameObject workView;
        public GameObject scenicView;
        public GameObject rawView;
        public GameObject reflection;
        public TMP_Text interruption;
        public Transform door;
        public TMP_Text display;
        public float rideTime = 3f;
        public bool Inside { get; private set; }
        public bool Riding { get; private set; }
        public bool AtWork { get; private set; }
        public bool HasArrived { get; private set; }
        public bool RawVisible { get; private set; }
        public bool Closed => doorAmount > .98f;
        public bool Open => doorAmount <= .02f && doorTarget <= 0f;
        Vector3 doorHome;
        float doorAmount;
        float doorTarget;
        float nextNotice;
        float noticeUntil;
        string notice;
        string shownText;
        int day = -1;
        float traceUntil;
        bool corrected;

        void Awake()
        {
            if (!loop) loop = Loop.Instance;
            if (door) doorHome = door.localPosition;
            doorAmount = doorTarget = 0f;
            ApplyDoor();
            ShowStation();
            if (scenicView) scenicView.SetActive(false);
            if (rawView) rawView.SetActive(false);
            if (interruption) interruption.gameObject.SetActive(false);
            if (reflection) reflection.SetActive(false);
        }

        void Update()
        {
            if (loop && day != loop.state.day)
            {
                day = loop.state.day;
                if (!Inside && !Riding)
                {
                    AtWork = false;
                    HasArrived = false;
                    doorAmount = doorTarget = 0;
                    noticeUntil = 0;
                    ShowStation();
                }
            }
            doorAmount = Mathf.MoveTowards(doorAmount, doorTarget, Time.deltaTime * 2f);
            ApplyDoor();
            if (Inside && !Riding && loop && !loop.Busy && cabin && RealityPlayer.Head && Vector3.Distance(RealityPlayer.FeetPosition, cabin.position) > 6f)
            {
                Inside = false;
                HasArrived = false;
                doorTarget = 0f;
            }
            ShowStatus();
            if (reflection && reflection.activeSelf != Inside) reflection.SetActive(Inside);
            if (interruption)
            {
                bool trace = Inside && !RawVisible && Time.time < traceUntil;
                interruption.gameObject.SetActive(trace);
            }
        }

        public void BoardSouth() { Board(false); }
        public void BoardWork() { Board(true); }

        void Board(bool fromWork)
        {
            if (Session.IsOpen || !loop || loop.Busy || Riding || Inside || !cabin || !RealityPlayer.Head) return;
            if (loop.state.local) { Notice("Service offline"); return; }
            var station = fromWork ? work : south;
            if (!station || Vector3.Distance(RealityPlayer.FeetPosition, station.position) > 5f) return;
            loop.Travel(cabin.position, () =>
            {
                Inside = true;
                HasArrived = false;
                AtWork = fromWork;
                if (!fromWork) loop.BeginCommute();
                doorAmount = doorTarget = 0f;
                ApplyDoor();
                ShowStation();
                loop.Cue(1f);
            });
        }

        public void CloseDoor()
        {
            if (!CanUse() || Riding) return;
            doorTarget = 1f;
            loop.Cue(.8f);
        }

        public void ToggleDoor()
        {
            if (doorTarget > .5f) OpenDoor();
            else CloseDoor();
        }

        public void OpenDoor()
        {
            if (!CanUse()) return;
            if (Riding) { Notice("Doors locked"); return; }
            doorTarget = 0f;
            loop.Cue(1.1f);
        }

        public void Travel() { Ride(); }

        public void Ride()
        {
            if (!CanUse() || Riding) return;
            if (loop.state.local) { Notice("Service offline"); return; }
            if (!Closed || doorTarget < 1f) { Notice("Close doors"); return; }
            StartCoroutine(Journey());
        }

        IEnumerator Journey()
        {
            Riding = true;
            HasArrived = false;
            corrected = false;
            traceUntil = 0f;
            SetWindow(false, true);
            if (southView) southView.SetActive(false);
            if (workView) workView.SetActive(false);
            loop.Cue(.65f);
            if (loop.talk) loop.talk.TrySay("METRO", AtWork ? "Next stop: Residence." : "Next stop: Work.");
            float elapsed = 0f;
            float duration = Mathf.Max(3.4f, rideTime);
            while (elapsed < duration)
            {
                if (!loop || loop.state.local) break;
                if (Session.IsOpen) { yield return null; continue; }
                elapsed += Time.deltaTime;
                bool raw = elapsed >= duration * .32f && elapsed < duration * .69f;
                if (RawVisible != raw)
                {
                    SetWindow(raw, true);
                    if (raw) loop.Cue(.72f);
                    else if (!corrected)
                    {
                        corrected = true;
                        traceUntil = Time.time + 5f;
                        loop.Announce("CIVIC", "A presentation interruption has been corrected.");
                    }
                }
                yield return null;
            }
            if (loop && !loop.state.local)
            {
                AtWork = !AtWork;
                HasArrived = true;
                loop.Cue(1.2f);
            }
            else if (loop) Notice("Service interrupted");
            Riding = false;
            SetWindow(false, false);
            ShowStation();
        }

        public void Leave()
        {
            if (!CanUse() || Riding) return;
            if (doorAmount > .02f || doorTarget > 0f) { Notice("Open doors"); return; }
            var station = AtWork ? work : south;
            if (!station) return;
            loop.Travel(station.position, () =>
            {
                Inside = false;
                HasArrived = false;
                if (AtWork) loop.BeginWork();
            });
        }

        bool CanUse()
        {
            return !Session.IsOpen && loop && !loop.Busy && Inside && cabin && RealityPlayer.Head && Vector3.Distance(RealityPlayer.FeetPosition, cabin.position) <= 6f;
        }

        void ApplyDoor()
        {
            if (door) door.localPosition = doorHome + Vector3.right * (1f - Mathf.SmoothStep(0f, 1f, doorAmount)) * 1.65f;
        }

        void ShowStation()
        {
            if (southView) southView.SetActive(!AtWork);
            if (workView) workView.SetActive(AtWork);
        }

        void SetWindow(bool raw, bool journey)
        {
            RawVisible = raw && journey;
            if (rawView) rawView.SetActive(RawVisible);
            if (scenicView) scenicView.SetActive(journey && !raw);
            if (interruption && !journey && Time.time >= traceUntil) interruption.gameObject.SetActive(false);
        }

        void ShowStatus()
        {
            if (!display) return;
            string station = (Riding ? !AtWork : AtWork) ? "WORK" : "RESIDENCE";
            string status = Riding ? "In transit" : loop && loop.state.local ? "Service offline"
                : Time.time < noticeUntil ? notice : Mathf.Abs(doorAmount - doorTarget) > .02f
                ? (doorTarget > .5f ? "Closing doors" : "Opening doors") : Closed ? "Ready" : "Doors open";
            string text = station + "\n" + status;
            if (shownText == text) return;
            shownText = text;
            display.text = text;
        }

        void Notice(string text)
        {
            if (!loop || Time.time < nextNotice) return;
            nextNotice = Time.time + 1.5f;
            noticeUntil = Time.time + 2f;
            notice = text;
            loop.Cue(.55f);
            ShowStatus();
        }

        void OnDisable()
        {
            StopAllCoroutines();
            Riding = false;
            Inside = false;
            HasArrived = false;
            SetWindow(false, false);
            if (reflection) reflection.SetActive(false);
            if (interruption) interruption.gameObject.SetActive(false);
        }
    }
}
