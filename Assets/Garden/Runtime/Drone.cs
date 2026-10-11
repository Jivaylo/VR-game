using System.Collections.Generic;
using RealityPlayground;
using TMPro;
using UnityEngine;

namespace Garden
{
    public sealed class Drone : MonoBehaviour
    {
        public Loop loop;
        public Transform pointA;
        public Transform pointB;
        public bool cameraOnly;
        public bool practice;
        public float speed = 1.2f;
        public float sightRange = 12f;
        public float viewAngle = 85f;
        public float warningTime = 3f;
        public float patrolRate = 1f;
        public Transform visual;
        public Renderer lamp;
        public TMP_Text status;
        public float settle = 1.15f;
        public static IReadOnlyList<Drone> Targets => targets;
        public bool Visible { get; private set; }
        public float Alert { get; private set; }
        public bool Suspended => Time.time < resumeAt;
        public bool Active => isActiveAndEnabled && loop && loop.state.local && !loop.state.outside;
        public Vector3 SensorPosition => visual ? visual.position : transform.position;
        public float Remaining => Mathf.Max(0f, resumeAt - Time.time);
        float resumeAt;
        float recoverAfter;
        float lastWarning = -20f;
        bool towardB = true;
        Quaternion rest;
        Vector3 visualHome;
        float shownDrop;
        float nextSweep;
        float patrolClock;
        RealityPlayer player;
        MaterialPropertyBlock properties;
        string shownText;
        Color shownColor;
        static readonly List<Drone> targets = new List<Drone>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetTargets() { targets.Clear(); }

        void OnEnable()
        {
            if (!targets.Contains(this)) targets.Add(this);
        }

        void Awake()
        {
            properties = new MaterialPropertyBlock();
            rest = visual ? visual.localRotation : transform.localRotation;
            if (visual) visualHome = visual.localPosition;
            player = FindFirstObjectByType<RealityPlayer>(FindObjectsInactive.Include);
        }

        void OnDisable()
        {
            Visible = false;
            Alert = 0f;
            targets.Remove(this);
        }

        void Update()
        {
            if (Session.IsOpen) return;
            shownDrop = Mathf.MoveTowards(shownDrop, Suspended && !cameraOnly ? practice ? .035f : settle : 0f, Time.deltaTime * .9f);
            if (visual && visual != transform) visual.localPosition = visualHome + Vector3.down * shownDrop;
            if (!Active)
            {
                Visible = false;
                Alert = 0f;
                Show("STANDBY", new Color(.13f, .19f, .2f));
                return;
            }
            if (Suspended)
            {
                Visible = false;
                Alert = 0f;
                Show("LOCAL HOLD\n" + Mathf.CeilToInt(Remaining) + " s", new Color(.12f, .55f, .65f));
                return;
            }
            if (practice)
            {
                Visible = false;
                Alert = 0f;
                Show("TEST SENSOR\nPALM > LOCK > PULSE", new Color(.16f, .58f, .52f));
                return;
            }
            Patrol();
            Visible = Time.time >= recoverAfter && Detect();
            float before = Alert;
            float rate = Mathf.Clamp(patrolRate, .1f, 1f);
            Alert = Mathf.MoveTowards(Alert, Visible ? 1f : 0f, Time.deltaTime * (Visible ? rate / Mathf.Max(.5f, warningTime) : 2f));
            if (Visible && before <= .001f && Time.time - lastWarning > 4f)
            {
                lastWarning = Time.time;
                loop.Cue(.6f);
                if (loop.talk) loop.talk.TrySay("DRONE", "Citizen identified. Please remain available for assistance.");
            }
            if (Alert >= 1f)
            {
                Alert = 0f;
                Visible = false;
                recoverAfter = Time.time + 6f;
                loop.Recover();
            }
            var color = Color.Lerp(new Color(.56f, .39f, .1f), new Color(.95f, .12f, .06f), Alert);
            Show(Visible ? "! OPTICAL CONTACT !\n" + Mathf.CeilToInt((1f - Alert) * warningTime / rate) + " s" : cameraOnly ? "CAMERA\nSCANNING" : "RECOVERY\nSEARCHING", color);
        }

        void Patrol()
        {
            float elapsed = Time.deltaTime * Mathf.Clamp(patrolRate, .1f, 1f);
            patrolClock += elapsed;
            if (patrolClock >= nextSweep)
            {
                nextSweep = patrolClock + (cameraOnly ? 6.4f : 5.3f);
                if (RealityPlayer.Head && (RealityPlayer.Head.position - SensorPosition).sqrMagnitude < 144f) loop.Cue(cameraOnly ? .78f : .94f);
            }
            if (cameraOnly)
            {
                var scan = rest * Quaternion.Euler(0f, Mathf.Sin(patrolClock * .48f) * 55f, 0f);
                if (visual) visual.localRotation = scan;
                else transform.localRotation = scan;
                return;
            }
            if (!pointA || !pointB) return;
            var target = towardB ? pointB.position : pointA.position;
            var offset = target - transform.position;
            if (offset.sqrMagnitude < .04f)
            {
                towardB = !towardB;
                target = towardB ? pointB.position : pointA.position;
                offset = target - transform.position;
            }
            transform.position = Vector3.MoveTowards(transform.position, target, Mathf.Max(0f, speed) * elapsed);
            offset.y = 0f;
            if (offset.sqrMagnitude > .001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(offset), 110f * elapsed);
        }

        bool Detect()
        {
            if (!RealityPlayer.Head) return false;
            var offset = RealityPlayer.Head.position - SensorPosition;
            if (offset.sqrMagnitude > sightRange * sightRange) return false;
            var forward = visual ? visual.forward : transform.forward;
            if (Vector3.Angle(forward, offset) > viewAngle * .5f) return false;
            return HasSight(RealityPlayer.Head.position);
        }

        public bool HasSight(Vector3 target)
        {
            var from = SensorPosition;
            var direction = (target - from).normalized;
            for (int pass = 0; pass < 6; pass++)
            {
                if (!Physics.Linecast(from, target, out var hit, 1, QueryTriggerInteraction.Ignore)) return true;
                if (PlayerCollider(hit.collider)) return true;
                if (!hit.collider.transform.IsChildOf(transform)) return false;
                from = hit.point + direction * .03f;
                if (Vector3.Dot(target - from, direction) <= 0f) return true;
            }
            return false;
        }

        bool PlayerCollider(Collider collider)
        {
            if (!player) player = FindFirstObjectByType<RealityPlayer>(FindObjectsInactive.Include);
            if (!player) return false;
            var t = collider.transform;
            return (player.xrOrigin && t.IsChildOf(player.xrOrigin.transform)) ||
                   (player.desktopRoot && t.IsChildOf(player.desktopRoot.transform)) ||
                   (player.avatarBody && t.IsChildOf(player.avatarBody)) ||
                   (player.avatarHead && t.IsChildOf(player.avatarHead));
        }

        public void Suspend(float duration = 8f)
        {
            if (!Active || cameraOnly) return;
            resumeAt = Mathf.Max(resumeAt, Time.time + Mathf.Max(.1f, duration));
            Visible = false;
            Alert = 0f;
            Show("LOCAL HOLD\n" + Mathf.CeilToInt(Remaining) + " s", new Color(.12f, .55f, .65f));
        }

        void Show(string text, Color color)
        {
            if (status && shownText != text)
            {
                shownText = text;
                status.text = text;
            }
            if (!lamp || shownColor == color) return;
            shownColor = color;
            lamp.GetPropertyBlock(properties);
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            properties.SetColor("_EmissionColor", color * 1.5f);
            lamp.SetPropertyBlock(properties);
        }
    }
}
