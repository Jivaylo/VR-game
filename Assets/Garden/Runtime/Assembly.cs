using System.Collections;
using TMPro;
using UnityEngine;

namespace Garden
{
    public sealed class Assembly : MonoBehaviour
    {
        public Loop loop;
        public Transform rejectArm;
        public Transform acceptArm;
        public Transform panel;
        public Transform gloveStand;
        public GameObject cabinet;
        public GameObject rawSample;
        public GameObject[] samples;
        public GameObject acceptGuide;
        public GameObject rejectGuide;
        public TMP_Text display;
        public Vector3 panelDrop = new Vector3(0f, -0.75f, 0.45f);
        public float motionTime = 0.85f;

        public bool Busy { get; private set; }
        public int Step { get; private set; }
        public int Sample { get; private set; }

        Vector3 rejectHome;
        Vector3 acceptHome;
        Vector3 panelHome;
        Quaternion rejectRotation;
        Quaternion acceptRotation;
        Quaternion panelRotation;
        bool ready;
        bool inspection;
        int day = int.MinValue;

        void Awake()
        {
            if (rejectArm) { rejectHome = rejectArm.localPosition; rejectRotation = rejectArm.localRotation; }
            if (acceptArm) { acceptHome = acceptArm.localPosition; acceptRotation = acceptArm.localRotation; }
            if (panel) { panelHome = panel.localPosition; panelRotation = panel.localRotation; }
            ready = true;
        }

        void OnEnable()
        {
            if (loop) loop.Changed += Refresh;
        }

        void Start() { Refresh(); }

        void OnDisable()
        {
            if (loop) loop.Changed -= Refresh;
            StopAllCoroutines();
            Busy = false;
            ClearPreview();
        }

        public void Accept() { Input(true); }
        public void Reject() { Input(false); }

        public void Review()
        {
            if (!loop || Busy || loop.Busy) return;
            if (loop.state.casing) { ViewSample(); return; }
            if (loop.state.local) return;
            if (!loop.state.meal && loop.Near(loop.factory, 6)) inspection = true;
            if (Sample < 2 && !inspection)
            {
                loop.Announce("STATION", "Classify the two components on the belt first.");
                loop.Cue(.65f);
                return;
            }
            Sample = 2;
            Step = 0;
            HomeArms();
            ShowSample();
            loop.Announce("SIGNAL", "X. Check. X. Watch the arms.");
            loop.Cue(0.9f);
        }

        public void ViewSample()
        {
            if (!loop || !loop.state.casing) return;
            loop.state.raw = true;
            loop.Notify();
            loop.Announce("CIVIC", "The returned sample is unsuitable for public presentation.");
            if (display) display.text = "EXTERIOR SAMPLE\nCurrent survey\nVegetation. Gate. Habitation.\n\nPUBLIC SERVICE UNAVAILABLE";
            loop.Cue(1.15f);
        }

        public void HoverAccept() { Preview(true); }
        public void HoverReject() { Preview(false); }

        public void ClearPreview()
        {
            if (acceptGuide) acceptGuide.SetActive(false);
            if (rejectGuide) rejectGuide.SetActive(false);
        }

        void Preview(bool accept)
        {
            if (Busy || !loop || loop.state.casing) return;
            if (acceptGuide) acceptGuide.SetActive(accept);
            if (rejectGuide) rejectGuide.SetActive(!accept);
        }

        void Input(bool accept)
        {
            if (!loop || Busy || loop.Busy) return;
            if (loop.state.local)
            {
                loop.Announce("STATION", "Public production offline.");
                return;
            }
            if (!loop.state.workStarted && !inspection && !loop.TryUse("BeginWork")) return;
            loop.BeginWork();
            if (loop.state.casing)
            {
                if (loop.state.workStarted && !loop.state.work)
                {
                    loop.CompleteWork();
                    loop.Cue(.9f);
                    loop.Announce("CIVIC", Briefing.ShiftDone);
                }
                else loop.Announce("CIVIC", "This station is being made safe.");
                return;
            }
            ClearPreview();
            if (Sample < 2)
            {
                bool correct = Sample == 0 ? accept : !accept;
                StartCoroutine(Ordinary(accept, correct));
                return;
            }
            if (Sample > 2)
            {
                if (loop.state.workStarted && !loop.state.work) loop.CompleteWork();
                loop.Announce("STATION", "Batch complete. Review the last sample to inspect it again.");
                return;
            }
            if (Step == 0 && accept)
            {
                StartCoroutine(FinishBatch());
                return;
            }
            bool expected = Step == 1;
            if (accept != expected)
            {
                StartCoroutine(ResetPattern());
                return;
            }
            StartCoroutine(Choreography());
        }

        IEnumerator Ordinary(bool accept, bool correct)
        {
            Busy = true;
            Transform arm = accept ? acceptArm : rejectArm;
            Vector3 home = accept ? acceptHome : rejectHome;
            Quaternion rotation = accept ? acceptRotation : rejectRotation;
            loop.Cue(correct ? 1.1f : 0.65f);
            yield return Move(arm, home + new Vector3(accept ? -0.2f : 0.2f, 0f, -0.2f), rotation * Quaternion.Euler(0f, accept ? -18f : 18f, 0f), motionTime * 0.55f);
            yield return Move(arm, home, rotation, motionTime * 0.55f);
            if (correct) Sample++;
            Busy = false;
            ShowSample();
            if (!correct) { if (loop.talk) loop.talk.TrySay("CIVIC", "Inspect the component. Try that sample again."); }
            else if (Sample == 2) loop.Announce("CIVIC", Briefing.Survey);
            else loop.Announce("CIVIC", Briefing.Damaged);
        }

        IEnumerator FinishBatch()
        {
            Busy = true;
            yield return Move(acceptArm, acceptHome + new Vector3(-0.25f, 0f, -0.16f), acceptRotation * Quaternion.Euler(0f, -22f, 0f), motionTime * 0.5f);
            yield return Move(acceptArm, acceptHome, acceptRotation, motionTime * 0.5f);
            Sample = 3;
            Busy = false;
            loop.CompleteWork();
            ShowSample();
            loop.Announce("CIVIC", loop.state.work ? Briefing.ShiftDone : Briefing.BeforeWork);
        }

        IEnumerator Choreography()
        {
            Busy = true;
            if (Step == 0)
            {
                if (display) display.text = "SERVICE REHEARSAL\nX  [ ]  [ ]\nReject arm: casing held";
                yield return Move(rejectArm, rejectHome + new Vector3(0.26f, 0.06f, -0.3f), rejectRotation * Quaternion.Euler(0f, 32f, -8f), motionTime);
                Step = 1;
                loop.Cue(0.8f);
            }
            else if (Step == 1)
            {
                if (display) display.text = "SERVICE REHEARSAL\nX  CHECK  [ ]\nAccept arm: panel displaced";
                yield return Move(acceptArm, acceptHome + new Vector3(-0.34f, 0.06f, -0.18f), acceptRotation * Quaternion.Euler(0f, -38f, 8f), motionTime);
                yield return Move(panel, panelHome + new Vector3(0f, 0f, 0.09f), panelRotation * Quaternion.Euler(-12f, 0f, 0f), 0.2f);
                Step = 2;
                loop.Cue(0.65f);
            }
            else
            {
                if (display) display.text = "SERVICE REHEARSAL\nX  CHECK  X\nCasing released";
                yield return Move(rejectArm, rejectHome, rejectRotation, motionTime * 0.65f);
                yield return Move(panel, panelHome + panelDrop, panelRotation * Quaternion.Euler(78f, 12f, 9f), motionTime * 0.7f);
                yield return Move(acceptArm, acceptHome, acceptRotation, motionTime * 0.5f);
                Step = 3;
                loop.state.casing = true;
                loop.CompleteWork();
                loop.Cue(0.55f);
                loop.Announce("CIVIC", "Production variance detected. Do not assist the machinery. This station is being made safe.");
            }
            Busy = false;
            loop.Notify();
            ShowSample();
        }

        IEnumerator ResetPattern()
        {
            Busy = true;
            if (display) display.text = "REHEARSAL RESET\nX  CHECK  X\nNo change to the shift";
            loop.Cue(0.6f);
            yield return Move(rejectArm, rejectHome, rejectRotation, motionTime * 0.45f);
            yield return Move(acceptArm, acceptHome, acceptRotation, motionTime * 0.45f);
            yield return Move(panel, panelHome, panelRotation, 0.2f);
            Step = 0;
            Busy = false;
            ShowSample();
            loop.Announce("STATION", "Rehearsal reset. Reject, accept, reject.");
        }

        IEnumerator Move(Transform part, Vector3 target, Quaternion rotation, float duration)
        {
            if (!part) yield break;
            Vector3 start = part.localPosition;
            Quaternion turn = part.localRotation;
            float elapsed = 0f;
            duration = Mathf.Max(0.05f, duration);
            while (elapsed < duration)
            {
                if (!part) yield break;
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                part.SetLocalPositionAndRotation(Vector3.LerpUnclamped(start, target, t), Quaternion.SlerpUnclamped(turn, rotation, t));
                yield return null;
            }
            if (part) part.SetLocalPositionAndRotation(target, rotation);
        }

        void Refresh()
        {
            if (!ready || !loop || Busy) return;
            bool opened = loop.state.casing;
            if (day != loop.state.day)
            {
                day = loop.state.day;
                inspection = false;
                Sample = loop.state.work ? 3 : loop.state.day > 1 ? 2 : 0;
                Step = opened ? 3 : 0;
                if (!opened) HomeArms();
            }
            if (cabinet) cabinet.SetActive(opened);
            if (rawSample) rawSample.SetActive(opened);
            if (gloveStand) gloveStand.gameObject.SetActive(opened && !loop.state.glove);
            if (opened)
            {
                Step = 3;
                HomeArms();
                if (panel) panel.SetLocalPositionAndRotation(panelHome + panelDrop, panelRotation * Quaternion.Euler(78f, 12f, 9f));
            }
            ShowSample();
        }

        void HomeArms()
        {
            if (rejectArm) rejectArm.SetLocalPositionAndRotation(rejectHome, rejectRotation);
            if (acceptArm) acceptArm.SetLocalPositionAndRotation(acceptHome, acceptRotation);
            if (panel && loop && !loop.state.casing) panel.SetLocalPositionAndRotation(panelHome, panelRotation);
        }

        void ShowSample()
        {
            if (samples != null)
                for (int i = 0; i < samples.Length; i++)
                    if (samples[i]) samples[i].SetActive(loop && !loop.state.casing && Sample == i);
            if (!display || !loop) return;
            if (loop.state.casing)
                display.text = loop.state.glove ? "STATION SAFE\nCasing open\nRaw survey available" : "SERVICE CABINET\nEquip glove\nMIRROR > CONTACT > TRACE";
            else if (Sample == 0) display.text = "UNIT 01\nAligned component\nCORRECT / INCORRECT";
            else if (Sample == 1) display.text = "UNIT 02\nDamaged component\nCORRECT / INCORRECT";
            else if (Sample == 2) display.text = "RETURNED SURVEY\nApproved: CORRECT\n\nService: X  CHECK  X\n" + (Step == 0 ? "[ ]  [ ]  [ ]" : Step == 1 ? "X  [ ]  [ ]" : "X  CHECK  [ ]");
            else display.text = loop.state.work ? "BATCH COMPLETE\n17:00\nReview last sample" : "BATCH RECORDED\nReview last sample";
        }
    }
}
