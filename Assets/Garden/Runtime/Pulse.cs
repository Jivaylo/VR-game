using RealityPlayground;
using TMPro;
using UnityEngine;

namespace Garden
{
    public sealed class Pulse : MonoBehaviour
    {
        public Loop loop;
        public Transform emitter;
        public float range = 6f;
        public float cooldown = 5f;
        public float aimAngle = 28f;
        public float lockTime = .22f;
        public TMP_Text status;
        public LineRenderer beam;
        public Drone LastTarget { get; private set; }
        public Drone Target { get; private set; }
        public Drone Threat { get; private set; }
        public bool Locked => Ready && Target && Time.time >= lockAt;
        public float ReadyIn => Mathf.Max(0f, readyAt - Time.time);
        public bool Ready => loop && loop.state.local && loop.state.glove && !loop.state.outside && Time.time >= readyAt;
        float readyAt;
        float beamUntil;
        float nextNotice;
        float lockAt;
        float nextTargetCheck;
        string shownText;

        public void Fire()
        {
            if (!Ready)
            {
                Notice(ReadyIn > 0f ? "Pulse charging." : "Local maintenance mode required.");
                return;
            }
            Acquire();
            if (!Locked)
            {
                Notice(Target ? "Hold your palm on the target until LOCK appears." : "Aim your palm at a nearby sensor. Keep a clear view.");
                return;
            }
            var origin = Origin.position;
            Drone nearest = Target;
            LastTarget = nearest;
            nearest.Suspend();
            readyAt = Time.time + Mathf.Max(.1f, cooldown);
            Target = null;
            Button.Haptic(RealityPlayer.LeftHand, .25f, .08f);
            loop.Cue(1.35f);
            if (beam)
            {
                beam.useWorldSpace = true;
                beam.positionCount = 2;
                beam.SetPosition(0, origin);
                beam.SetPosition(1, nearest.SensorPosition);
                beam.enabled = true;
                beamUntil = Time.time + .16f;
            }
        }

        void Notice(string text)
        {
            if (!loop || Time.time < nextNotice) return;
            nextNotice = Time.time + 2f;
            loop.Announce("GLOVE", text);
            loop.Cue(.65f);
        }

        void Update()
        {
            if (beam && beam.enabled && Time.time >= beamUntil) beam.enabled = false;
            if (Time.time >= nextTargetCheck)
            {
                nextTargetCheck = Time.time + .06f;
                Acquire();
                Threat = null;
                float warning = 0f;
                for (int i = 0; i < Drone.Targets.Count; i++)
                {
                    var candidate = Drone.Targets[i];
                    if (!candidate || !candidate.Active || !candidate.Visible || candidate.Alert <= warning) continue;
                    Threat = candidate;
                    warning = candidate.Alert;
                }
            }
            if (!status) return;
            int seconds = Mathf.CeilToInt(ReadyIn);
            string text = !loop || !loop.state.local ? "PULSE\nLOCAL REQUIRED" : loop.state.outside ? "PULSE\nSTANDBY" : seconds > 0 ? "PULSE\n" + seconds + " s" : Locked ? "[ LOCK ]\nRELEASE PULSE" : Target ? "[ ... ]\nACQUIRING" : "PULSE READY\nAIM PALM";
            if (Threat) text = (Threat.cameraOnly ? "! CAMERA CONTACT" : "! DRONE CONTACT") + "\n" + (Locked ? "[ LOCK ] PULSE" : "MOVE BEHIND COVER");
            if (text == shownText) return;
            shownText = text;
            status.text = text;
        }

        void OnDisable()
        {
            if (beam) beam.enabled = false;
            Target = null;
            Threat = null;
        }

        Transform Origin => emitter ? emitter : RealityPlayer.LeftHand ? RealityPlayer.LeftHand : transform;

        void Acquire()
        {
            if (!Ready) { Target = null; return; }
            var origin = Origin;
            Drone best = null;
            float score = float.MaxValue;
            for (int i = 0; i < Drone.Targets.Count; i++)
            {
                var drone = Drone.Targets[i];
                if (!drone || !drone.Active || drone.Suspended || drone.cameraOnly) continue;
                Vector3 offset = drone.SensorPosition - origin.position;
                float distance = offset.magnitude;
                if (distance > range || distance < .03f) continue;
                float angle = Vector3.Angle(origin.forward, offset);
                if (angle > aimAngle || !drone.HasSight(origin.position)) continue;
                float candidate = angle * .15f + distance / Mathf.Max(.1f, range);
                if (candidate >= score) continue;
                best = drone;
                score = candidate;
            }
            if (best == Target) return;
            Target = best;
            lockAt = Time.time + Mathf.Max(0f, lockTime);
            if (Target) Button.Haptic(RealityPlayer.LeftHand, .06f, .025f);
        }
    }
}
