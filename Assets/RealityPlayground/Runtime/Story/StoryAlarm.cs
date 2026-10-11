using UnityEngine;
using UnityEngine.Events;

namespace RealityPlayground.Story
{

    public sealed class StoryAlarm : PlaygroundTarget
    {
        public bool interactionEnabled;
        [Tooltip("World-space floor height used by the flying hologram and its glass fragments.")]
        public float floorHeight;
        public UnityEvent snoozed = new UnityEvent();
        public bool Snoozed { get; private set; }
        public bool OwnsAlarmAudio => true;
        public bool Shattered { get; private set; }
        public bool MorningDismissed { get; private set; }
        public float FlightDistance { get; private set; }
        public Vector3 LaunchVelocity { get; private set; }
        public int GlassShardCount => shatter ? shatter.ShardCount : 0;
        public override bool SupportsTriggerActivation => true;
        [SerializeField] Transform rooster, leftWing, rightWing;
        [SerializeField] Collider touchVolume;
        [SerializeField] TextMesh instruction;
        [SerializeField] Transform morningDisplay;
        [SerializeField] AudioClip crowRecording;
        Vector3 home, flightStart, velocity, spin, previousLeft, previousRight;
        Vector3 displayScale, displayPosition;
        Quaternion displayRotation, leftWingRotation = Quaternion.identity, rightWingRotation = Quaternion.identity;
        Transform lastLeft, lastRight;
        float age, snoozeAge = -1, nextCrow = .5f;
        bool displayPrepared;
        AudioSource source;
        AudioClip squawk, glass;
        StoryHologramShatter shatter;
        readonly RaycastHit[] impacts = new RaycastHit[12];

        public static GameObject Create(Transform parent)
        {
            var root = new GameObject("Rooster"); root.transform.SetParent(parent, false);
            var alarm = root.AddComponent<StoryAlarm>();
            var gold = StoryVignetteGeometry.Hologram("Rooster Amber", new Color(1, .49f, .03f, .72f), 2.9f);
            var pink = StoryVignetteGeometry.Hologram("Rooster coral", new Color(1, .045f, .32f, .85f), 2.6f);
            var yellow = StoryVignetteGeometry.Hologram("Rooster Feathers", new Color(1, .8f, .04f, .74f), 3);
            var white = GreyboxUtil.Material("Rooster Eyes", Color.white, 3);
            alarm.rooster = StoryVignetteGeometry.Group("Rooster light sculpture", root.transform, new Vector3(0, .4f, 0));
            GreyboxUtil.Primitive("Body", PrimitiveType.Sphere, alarm.rooster, Vector3.zero, new Vector3(.3f, .33f, .22f), gold, false);
            GreyboxUtil.Primitive("Neck", PrimitiveType.Capsule, alarm.rooster, new Vector3(.085f, .15f, -.035f), new Vector3(.13f, .15f, .13f), gold, false);
            GreyboxUtil.Primitive("Head", PrimitiveType.Sphere, alarm.rooster, new Vector3(.1f, .27f, -.04f), Vector3.one * .17f, gold, false);
            var beak = GreyboxUtil.Primitive("Beak", PrimitiveType.Cube, alarm.rooster, new Vector3(.2f, .284f, -.04f), new Vector3(.07f, .019f, .045f), yellow, false);
            beak.transform.localRotation = Quaternion.Euler(0, 0, 24);
            var lowerBeak = GreyboxUtil.Primitive("LowerBeak", PrimitiveType.Cube, alarm.rooster, new Vector3(.2f, .256f, -.04f), new Vector3(.07f, .019f, .045f), yellow, false);
            lowerBeak.transform.localRotation = Quaternion.Euler(0, 0, -24);
            GreyboxUtil.Primitive("Visible eye", PrimitiveType.Sphere, alarm.rooster, new Vector3(.11f, .29f, -.12f), Vector3.one * .034f, white, false);
            GreyboxUtil.Primitive("Far eye", PrimitiveType.Sphere, alarm.rooster, new Vector3(.11f, .29f, .038f), Vector3.one * .034f, white, false);
            for (int i = 0; i < 3; i++)
                GreyboxUtil.Primitive("Crown comb " + i, PrimitiveType.Sphere, alarm.rooster, new Vector3(.04f + i * .052f, .357f + Mathf.Sin(i) * .012f, -.04f), new Vector3(.068f, .08f, .065f), pink, false);
            GreyboxUtil.Primitive("Wattle", PrimitiveType.Sphere, alarm.rooster, new Vector3(.15f, .195f, -.055f), new Vector3(.04f, .065f, .06f), pink, false);
            alarm.leftWing = GreyboxUtil.Primitive("Near wing", PrimitiveType.Sphere, alarm.rooster, new Vector3(-.025f, -.015f, -.135f), new Vector3(.22f, .15f, .035f), yellow, false).transform;
            alarm.rightWing = GreyboxUtil.Primitive("Far wing", PrimitiveType.Sphere, alarm.rooster, new Vector3(-.025f, -.015f, .135f), new Vector3(.22f, .15f, .035f), yellow, false).transform;
            for (int i = 0; i < 7; i++)
            {
                var feather = GreyboxUtil.Primitive("Tail feather " + i, PrimitiveType.Capsule, alarm.rooster, new Vector3(-.16f - i * .025f, .07f + i * .026f, (i - 3) * .026f), new Vector3(.045f, .15f + i * .007f, .022f), i % 2 == 0 ? yellow : pink, false);
                feather.transform.localRotation = Quaternion.Euler(0, 0, 20 + i * 8);
            }
            for (int i = -1; i <= 1; i += 2)
            {
                StoryVignetteGeometry.Rod("Light leg", alarm.rooster, new Vector3(0, -.14f, i * .062f), new Vector3(.035f, -.25f, i * .062f), .017f, pink);
                StoryVignetteGeometry.Rod("Light foot", alarm.rooster, new Vector3(-.02f, -.25f, i * .062f), new Vector3(.1f, -.25f, i * .062f), .017f, pink);
            }
            var halo = StoryVignetteGeometry.Ring("Sunrise projection halo", root.transform, .32f, .009f, gold);
            halo.localPosition = new Vector3(0, .055f, 0); halo.localRotation = Quaternion.Euler(90, 0, 0);
            var box = root.AddComponent<BoxCollider>(); box.center = new Vector3(0, .48f, 0); box.size = new Vector3(.75f, .7f, .4f); box.isTrigger = true; alarm.touchVolume = box;
            alarm.instruction = GreyboxUtil.Label("SWIPE TO SNOOZE", root.transform, new Vector3(0, .92f, -.07f), .09f, new Color(1, .65f, .28f));
            alarm.home = alarm.rooster.localPosition;
            return root;
        }

        void Awake()
        {
            if (rooster) home = rooster.localPosition;
            if (leftWing) leftWingRotation = leftWing.localRotation;
            if (rightWing) rightWingRotation = rightWing.localRotation;
            if (!morningDisplay)
            {
                var environment = GetComponentInParent<StoryEnvironment>();
                if (environment && environment.hyperrealityRoot)
                    morningDisplay = ObjectNames.Find(environment.hyperrealityRoot.transform, "Wake-up projection");
            }
            PrepareDisplay();
            var emitter = new GameObject("RoosterVoice");
            emitter.transform.SetParent(transform, false);
            source = emitter.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 1; source.minDistance = 1;
            source.maxDistance = 12; source.rolloffMode = AudioRolloffMode.Linear;
            source.dopplerLevel = .28f; source.priority = 96;
            if (!crowRecording) crowRecording = Resources.Load<AudioClip>("StoryAlarmAudio/rooster-crow");
            squawk = Resources.Load<AudioClip>("StoryAlarmAudio/rooster-surprised");
            glass = Resources.Load<AudioClip>("StoryAlarmAudio/hologram-glass");
            var fragments = new GameObject("RoosterShards");
            fragments.transform.SetParent(transform, false);
            shatter = fragments.AddComponent<StoryHologramShatter>();
            shatter.Prepare();
        }

        public void ConfigureMorningDisplay(Transform display)
        {
            morningDisplay = display;
            displayPrepared = false;
            if (Application.isPlaying) PrepareDisplay();
        }

        public void ConfigureCrow(AudioClip recording) { crowRecording = recording; }

        void PrepareDisplay()
        {
            if (!morningDisplay || displayPrepared) return;
            displayScale = morningDisplay.localScale;
            displayPosition = morningDisplay.localPosition;
            displayRotation = morningDisplay.localRotation;
            displayPrepared = true;
        }

        void Update()
        {
            age += Time.deltaTime;
            if (Snoozed)
            {
                snoozeAge += Time.deltaTime;
                AnimateDisplayDismissal();
                if (!Shattered) Fly();
                return;
            }
            if (rooster) rooster.localPosition = home + Vector3.up * (Mathf.Sin(age * 3) * .025f);
            if (source && rooster) source.transform.position = rooster.position;
            if (leftWing) leftWing.localRotation = leftWingRotation * Quaternion.Euler(Mathf.Sin(age * 7) * 14, 0, 0);
            if (rightWing) rightWing.localRotation = rightWingRotation * Quaternion.Euler(-Mathf.Sin(age * 7) * 14, 0, 0);
            if (interactionEnabled && crowRecording && source)
            {
                nextCrow -= Time.deltaTime;
                if (nextCrow <= 0)
                {
                    source.clip = crowRecording; source.volume = .62f; source.pitch = 1;
                    source.Play(); nextCrow = crowRecording.length + 1.7f;
                }
            }
            ObserveHand(RealityPlayer.LeftHand, ref lastLeft, ref previousLeft);
            ObserveHand(RealityPlayer.RightHand, ref lastRight, ref previousRight);
        }
        void ObserveHand(Transform hand, ref Transform previousHand, ref Vector3 previous)
        {
            if (!hand || !hand.gameObject.activeInHierarchy) { previousHand = null; return; }
            Vector3 displacement = previousHand == hand ? hand.position - previous : Vector3.zero;
            Vector3 handVelocity = Time.deltaTime > .0001f ? displacement / Time.deltaTime : Vector3.zero;
            Vector3 previousPosition = previous;
            previousHand = hand; previous = hand.position;
            if (!interactionEnabled || !touchVolume || Snoozed || !RealityPlayer.IsXR) return;

            if (displacement.sqrMagnitude > .65f * .65f || handVelocity.sqrMagnitude < .14f * .14f) return;
            bool touches = Vector3.Distance(touchVolume.ClosestPoint(hand.position), hand.position) < .07f;
            var sweptBounds = touchVolume.bounds; sweptBounds.Expand(.12f);
            if (!touches && displacement.sqrMagnitude > .00001f)
                touches = sweptBounds.IntersectRay(new Ray(previousPosition, displacement.normalized), out float entry) && entry <= displacement.magnitude;
            if (touches) ActivateWithSwipe(handVelocity);
        }
        public override void Activate()
        {
            Vector3 origin = RealityPlayer.Head ? RealityPlayer.Head.position : transform.position - transform.forward;
            Vector3 away = Vector3.ProjectOnPlane((rooster ? rooster.position : transform.position) - origin, Vector3.up);
            if (away.sqrMagnitude < .01f) away = transform.forward;
            ActivateWithSwipe(away.normalized * 2.8f + Vector3.up * .7f);
        }

        public void ActivateWithSwipe(Vector3 handVelocity)
        {
            if (!interactionEnabled || Snoozed) return;
            if (!float.IsFinite(handVelocity.x) || !float.IsFinite(handVelocity.y) || !float.IsFinite(handVelocity.z)) return;
            Snoozed = true; snoozeAge = 0;
            flightStart = rooster ? rooster.position : transform.position;
            Vector3 horizontal = Vector3.ProjectOnPlane(handVelocity, Vector3.up);
            if (horizontal.sqrMagnitude < .01f) horizontal = transform.forward;
            velocity = horizontal.normalized * Mathf.Clamp(handVelocity.magnitude * 1.9f, 3.9f, 7.5f);
            velocity.y = Mathf.Clamp(handVelocity.y * .6f + 2.1f, 1.1f, 3.8f);
            LaunchVelocity = velocity;
            spin = Vector3.Cross(Vector3.up, velocity.normalized) * 680 + Vector3.up * 210;
            if (instruction) instruction.gameObject.SetActive(false);
            if (touchVolume) touchVolume.enabled = false;
            if (source) { source.Stop(); source.volume = 1; source.pitch = 1; if (squawk) source.PlayOneShot(squawk, .65f); }
            snoozed.Invoke();
        }

        void Fly()
        {
            if (!rooster) { Shattered = true; return; }
            float dt = Mathf.Min(Time.deltaTime, .06f);
            velocity += Vector3.down * (6.8f * dt);
            Vector3 step = velocity * dt;
            bool hit = false;
            float distance = step.magnitude;
            float nearest = distance;
            if (snoozeAge > .1f && distance > .0001f)
            {
                int count = Physics.SphereCastNonAlloc(rooster.position, .105f, step / distance, impacts,
                    distance, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    var collider = impacts[i].collider;
                    if (!collider || collider.transform.IsChildOf(transform)) continue;
                    if (impacts[i].distance < nearest) { nearest = impacts[i].distance; hit = true; }
                }
            }
            rooster.position += distance > .0001f ? step * (nearest / distance) : Vector3.zero;
            rooster.Rotate(spin * dt, Space.World);
            if (leftWing) leftWing.localRotation = leftWingRotation * Quaternion.Euler(Mathf.Sin(snoozeAge * 56) * 75, 0, 0);
            if (rightWing) rightWing.localRotation = rightWingRotation * Quaternion.Euler(-Mathf.Sin(snoozeAge * 56) * 75, 0, 0);
            FlightDistance = Mathf.Max(FlightDistance, Vector3.Distance(flightStart, rooster.position));
            if (source) source.transform.position = rooster.position;
            if (hit || snoozeAge >= .98f || rooster.position.y < floorHeight + .18f) Shatter();
        }

        void Shatter()
        {
            Shattered = true;
            Vector3 center = rooster ? rooster.position : flightStart;
            if (shatter) shatter.Burst(center, velocity, floorHeight);
            if (rooster) rooster.gameObject.SetActive(false);
            if (source && glass) source.PlayOneShot(glass, .72f);
        }

        void AnimateDisplayDismissal()
        {
            if (MorningDismissed) return;
            PrepareDisplay();
            if (morningDisplay)
            {
                float fold = Mathf.SmoothStep(0, 1, Mathf.Clamp01(snoozeAge / .56f));
                float close = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.36f, .86f, snoozeAge));
                morningDisplay.localScale = Vector3.Scale(displayScale, new Vector3(Mathf.Lerp(1, 1.06f, fold) * (1 - close), Mathf.Lerp(1, .018f, fold), 1 - close));
                morningDisplay.localRotation = displayRotation * Quaternion.Euler(-fold * 38, 0, fold * 3);
                morningDisplay.localPosition = displayPosition + Vector3.up * (.1f * fold);
                if (snoozeAge < .88f) return;
                morningDisplay.gameObject.SetActive(false);
            }
            MorningDismissed = true;
        }
    }
}
