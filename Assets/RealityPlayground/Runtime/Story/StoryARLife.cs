using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealityPlayground.Story
{

    public sealed class StoryARLife : MonoBehaviour
    {
        enum Movement { Spin, Float, Orbit, Swim, Petal, Blink, Stream, Breathe }
        [Serializable] sealed class Motion
        {
            public Transform target;
            public Movement movement;
            public Vector3 position, scale, axis;
            public Quaternion rotation;
            public float speed, radius, phase;
            public bool responsive;
        }

        public StoryEnvironment environment;
        [SerializeField] Motion[] motions = Array.Empty<Motion>();
        [SerializeField] Renderer[] surfaces = Array.Empty<Renderer>();
        [SerializeField] Transform[] installations = Array.Empty<Transform>();
        [SerializeField] Vector3[] installationPositions = Array.Empty<Vector3>();
        readonly List<Motion> buildingMotions = new List<Motion>();
        readonly List<Transform> buildingInstallations = new List<Transform>();
        Material cyan, coral, amber, violet, mint, ink;
        MaterialPropertyBlock properties;
        float lastReveal = -1;
        public int AnimatedObjectCount => motions == null ? 0 : motions.Length;

        void Awake()=>CaptureAuthoredPoses();
        public void CaptureAuthoredPoses()
        {
            foreach(var motion in motions)
                if(motion!=null && motion.target){motion.position=motion.target.localPosition;motion.rotation=motion.target.localRotation;motion.scale=motion.target.localScale;}
            for(int i=0;i<installations.Length && i<installationPositions.Length;i++)
                if(installations[i])installationPositions[i]=installations[i].localPosition;
        }

        public static StoryARLife Install(StoryEnvironment city)
        {
            if (!city || !city.hyperrealityRoot) return null;
            var existing = city.hyperrealityRoot.GetComponentInChildren<StoryARLife>(true);
            if (existing) { existing.environment = city; return existing; }
            var root = new GameObject("AnimatedAds");
            root.transform.SetParent(city.hyperrealityRoot.transform, false);
            var life = root.AddComponent<StoryARLife>();
            life.environment = city;
            life.Build();
            city.digitalRenderers = city.hyperrealityRoot.GetComponentsInChildren<Renderer>(true);
            return life;
        }

        void Build()
        {
            cyan = Projection("ion cyan", new Color(.025f, .7f, 1f, .78f), 2.05f);
            coral = Projection("laser coral", new Color(1f, .035f, .24f, .8f), 1.95f);
            amber = Projection("solar amber", new Color(1f, .39f, .018f, .82f), 2f);
            violet = Projection("violet", new Color(.42f, .055f, 1f, .8f), 2.05f);
            mint = Projection("electric mint", new Color(.025f, 1f, .4f, .76f), 1.75f);
            ink = Projection("indigo core", new Color(.015f, .04f, .16f, .95f), 1.15f);
            Weather(); Breakfast(); Aquarium(); ProductShoe(); RealityBrand(); AttentionEye();
            LivingData(); JoyFlower(); StreetCurrents(); FacadeSignals(); AnimateExistingProjections();
            motions = buildingMotions.ToArray();
            installations = buildingInstallations.ToArray();
            installationPositions = new Vector3[installations.Length];
            for (int i = 0; i < installations.Length; i++) installationPositions[i] = installations[i].localPosition;
            surfaces = GetComponentsInChildren<Renderer>(true);
            foreach (var surface in surfaces)
            {
                surface.shadowCastingMode = ShadowCastingMode.Off;
                surface.receiveShadows = false;
            }
        }

        Material Projection(string name, Color color, float glow)
        {
            var shader = Shader.Find("RealityPlayground/StoryLivingHologram");
            if (!shader) shader = Shader.Find("RealityPlayground/StoryARProjection");
            var material = new Material(shader) { name = "Living AR " + name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Glow", glow);
            return material;
        }

        Transform Group(string name, Transform parent, Vector3 position)
            => StoryVignetteGeometry.Group(name, parent, position);
        Transform Installation(string name, Vector3 position, float yaw = 0)
        {
            var group = Group(name, transform, position);
            group.localRotation = Quaternion.Euler(0, yaw, 0);
            buildingInstallations.Add(group);
            return group;
        }
        Transform Shape(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
            => GreyboxUtil.Primitive(name, type, parent, position, scale, material, false).transform;
        Transform Ring(string name, Transform parent, float radius, float width, Material material)
            => StoryVignetteGeometry.Ring(name, parent, radius, width, material, 64);
        void Animate(Transform target, Movement movement, float speed, float radius = 0, Vector3 axis = default, float phase = 0, bool responsive = false)
        {
            buildingMotions.Add(new Motion { target = target, movement = movement, position = target.localPosition,
                rotation = target.localRotation, scale = target.localScale, axis = axis, speed = speed, radius = radius,
                phase = phase, responsive = responsive });
        }

        void Caption(string name, float drop)
        {
            var panel = ObjectNames.Find(environment.hyperrealityRoot.transform, name);
            if (!panel) return;
            foreach (Transform child in panel)
            {
                if (ObjectNames.Matches(child.name, "Glass projection") || ObjectNames.Contains(child.name, "luminous edge") || ObjectNames.Matches(child.name, "Live data"))
                    child.gameObject.SetActive(false);
            }
            var labels = panel.GetComponentsInChildren<TextMesh>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (i > 0) { labels[i].gameObject.SetActive(false); continue; }
                labels[i].transform.localPosition = new Vector3(0, drop, -.14f);
                labels[i].transform.localScale = Vector3.one * .6f;
            }
        }

        void Weather()
        {
            Caption("Weather widget", -.53f);
            var weather = Installation("Weather Ad", new Vector3(-1.8f, 2.76f, 3.38f));
            var solar = Group("The scheduled sun", weather, new Vector3(-.25f, .06f, 0));
            Shape("Solar body", PrimitiveType.Sphere, solar, Vector3.zero, Vector3.one * .4f, amber);
            var rays = Group("Rotating sun rays", solar, Vector3.zero);
            for (int i = 0; i < 10; i++)
            {
                float angle = i * Mathf.PI * .2f;
                var ray = Shape("Solid solar ray", PrimitiveType.Cube, rays, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * .35f,
                    new Vector3(.16f, .027f, .035f), amber);
                ray.localRotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg);
            }
            Animate(rays, Movement.Spin, 19, 0, Vector3.forward);
            var cloud = Group("Forecast Cloud", weather, new Vector3(.13f, -.13f, -.2f));
            for (int i = 0; i < 3; i++)
                Shape("Cloud volume", PrimitiveType.Sphere, cloud, new Vector3((i - 1) * .22f, i == 1 ? .07f : 0, 0),
                    new Vector3(.43f, .26f + (i == 1 ? .1f : 0), .2f), cyan);
            Animate(cloud, Movement.Float, .7f, .11f, Vector3.right, 0, true);
            for (int i = 0; i < 4; i++)
            {
                var drop = Shape("Animated forecast drop", PrimitiveType.Capsule, cloud, new Vector3((i - 1.5f) * .13f, -.25f, 0),
                    new Vector3(.022f, .055f, .022f), cyan);
                Animate(drop, Movement.Stream, .45f, .23f, Vector3.down, i * .24f);
            }
        }

        void Breakfast()
        {
            Caption("Breakfast suggestion", -.49f);
            var group = Installation("Food Ad", new Vector3(2.15f, 2.55f, 3.4f));
            var can = Group("Food Hologram", group, Vector3.zero);
            Shape("Drink body", PrimitiveType.Cylinder, can, Vector3.zero, new Vector3(.31f, .23f, .31f), amber);
            Shape("Indigo flavour band", PrimitiveType.Cylinder, can, new Vector3(0, .025f, 0), new Vector3(.316f, .075f, .316f), violet);
            var lid = Ring("Open liquid crown", can, .16f, .018f, mint); lid.localPosition = Vector3.up * .24f; lid.localRotation = Quaternion.Euler(90, 0, 0);
            var leaf = Shape("Mint leaf", PrimitiveType.Sphere, can, new Vector3(.1f, .31f, 0), new Vector3(.23f, .055f, .1f), mint);
            leaf.localRotation = Quaternion.Euler(0, 15, 35);
            Animate(can, Movement.Spin, 28, .055f, new Vector3(.13f, 1, .08f), 0, true);
            var orbit = Ring("Floating ingredient orbit", group, .45f, .012f, amber);
            orbit.localRotation = Quaternion.Euler(64, 0, 20);
            Animate(orbit, Movement.Spin, -18, 0, Vector3.up);
            for (int i = 0; i < 3; i++)
            {
                var bead = Shape("Ingredient molecule", PrimitiveType.Sphere, group, Vector3.zero, Vector3.one * .075f, i % 2 == 0 ? mint : coral);
                Animate(bead, Movement.Orbit, .7f, .46f, new Vector3(1, .3f, .5f), i * Mathf.PI * 2 / 3);
            }
        }

        void Aquarium()
        {
            Caption("Entertainment wall", -.73f);
            var indoor = Installation("Aquarium Ad", new Vector3(3.04f, 2.15f, 1.3f), 90);
            var glassOrbit = Ring("Aquarium field", indoor, .72f, .015f, cyan);
            Animate(glassOrbit, Movement.Spin, 12, 0, new Vector3(.3f, 1, .2f));
            Fish(indoor, new Vector3(0, 0, 0), coral, .56f, .75f, 0);
            Fish(indoor, new Vector3(0, .28f, 0), cyan, .4f, .65f, 2.8f);
            var sky = Installation("Sky Ads", new Vector3(0, 4.3f, 18.9f));
            Fish(sky, new Vector3(0, 0, 0), coral, .8f, 2.35f, 1);
            Fish(sky, new Vector3(0, .46f, 0), cyan, .65f, 2.8f, 3.4f);
            Fish(sky, new Vector3(0, -.25f, 0), amber, .55f, 1.85f, 5.1f);
        }

        void Fish(Transform parent, Vector3 position, Material material, float size, float radius, float phase)
        {
            var fish = Group("Holographic Koi", parent, position);
            fish.localScale = Vector3.one * size;
            Shape("Translucent fish body", PrimitiveType.Sphere, fish, Vector3.zero, new Vector3(.94f, .35f, .26f), material);
            var tail = Group("Swishing forked tail", fish, new Vector3(-.44f, 0, 0));
            var vertices = new[] { Vector3.zero, new Vector3(-.32f, .28f, .02f), new Vector3(-.25f, 0, 0), new Vector3(-.32f, -.28f, .02f) };
            MeshObject("Tail membrane", tail, MakeMesh("Holographic fish tail", vertices, new[] { 0, 1, 2, 0, 2, 3 }), material);
            Animate(tail, Movement.Float, 5.2f, 0, Vector3.zero, phase);
            var fin = Shape("Dorsal light fin", PrimitiveType.Sphere, fish, new Vector3(-.09f, .15f, 0), new Vector3(.5f, .2f, .035f), cyan);
            fin.localRotation = Quaternion.Euler(0, 0, -16);
            Shape("Glowing eye", PrimitiveType.Sphere, fish, new Vector3(.32f, .075f, -.107f), Vector3.one * .049f, ink);
            Animate(fish, Movement.Swim, .34f + phase * .022f, radius, new Vector3(1, .22f, .48f), phase, true);
        }

        void ProductShoe()
        {
            Caption("Upgrade kiosk", -.63f);
            var product = Installation("Shoe Ad", new Vector3(3.1f, 1.95f, 11.6f), -20);
            var shoe = Group("Shoe Model", product, Vector3.zero);
            var profile = new[] { new Vector2(-.49f,-.16f),new Vector2(.48f,-.16f),new Vector2(.58f,-.06f),new Vector2(.49f,.06f),
                new Vector2(.12f,.12f),new Vector2(-.05f,.31f),new Vector2(-.34f,.37f),new Vector2(-.4f,.1f),new Vector2(-.52f,.06f) };
            MeshObject("Sculpted floating shoe", shoe, Extrude("Spatial product shoe", profile, .29f), coral);
            var sole = Shape("Suspended sole", PrimitiveType.Cube, shoe, new Vector3(.02f, -.225f, 0), new Vector3(1.05f, .035f, .35f), cyan);
            Animate(sole, Movement.Float, 2, .04f, Vector3.up);
            for (int i = 0; i < 3; i++)
            {
                var lace = Shape("Floating digital lace", PrimitiveType.Cube, shoe, new Vector3(-.07f + i * .085f, .19f - i * .047f, 0),
                    new Vector3(.027f, .035f, .35f), mint);
                lace.localRotation = Quaternion.Euler(0, -15, 0);
            }
            Animate(shoe, Movement.Spin, 26, .07f, Vector3.up, 0, true);
            var stage = Ring("Product carousel", product, .64f, .018f, cyan);
            stage.localPosition = Vector3.down * .38f; stage.localRotation = Quaternion.Euler(80, 0, 0);
            Animate(stage, Movement.Spin, -21, 0, Vector3.forward);
        }

        void RealityBrand()
        {
            Caption("City subscription billboard", -.91f);
            var brand = Installation("Reality Ad", new Vector3(-3.3f, 3.7f, 12), -90);
            var sculpture = MeshObject("Infinity Emblem", brand, InfinityRibbon(), coral);
            Animate(sculpture, Movement.Spin, 20, .12f, new Vector3(.1f, 1, .17f), 0, true);
            var hoop = Ring("Trademark orbit", brand, 1.16f, .025f, cyan); hoop.localRotation = Quaternion.Euler(20, 28, 0);
            Animate(hoop, Movement.Spin, -16, 0, new Vector3(.1f, 1, .3f));
            for (int i = 0; i < 4; i++)
            {
                var tile = Shape("Benefit Orbit", PrimitiveType.Cube, brand, Vector3.zero, Vector3.one * .15f, i % 2 == 0 ? amber : mint);
                Animate(tile, Movement.Orbit, .55f, 1.08f, new Vector3(1, .8f, .4f), i * Mathf.PI * .5f);
            }
        }

        void AttentionEye()
        {
            Caption("Architecture advertisement", -1.22f);
            var eye = Installation("Eye Ad", new Vector3(3.28f, 3.6f, 17.5f), 90);
            var blinking = Group("Blinking optical sculpture", eye, Vector3.zero);
            Shape("Eye volume", PrimitiveType.Sphere, blinking, Vector3.zero, new Vector3(1.75f, .98f, .38f), cyan);
            var gaze = Group("Responsive iris", blinking, new Vector3(0, 0, -.2f));
            Shape("Iris", PrimitiveType.Sphere, gaze, Vector3.zero, new Vector3(.67f, .67f, .16f), violet);
            Shape("Pupil", PrimitiveType.Sphere, gaze, new Vector3(0, 0, -.083f), new Vector3(.26f, .36f, .04f), ink);
            var irisRing = Ring("Radial scanning iris", gaze, .3f, .026f, coral); irisRing.localPosition = new Vector3(0, 0, -.08f);
            Animate(irisRing, Movement.Spin, 47, 0, Vector3.forward);
            Animate(gaze, Movement.Float, .53f, .17f, Vector3.right, 0, true);
            Animate(blinking, Movement.Blink, .21f);
            var halo = Ring("Eye meridian", eye, 1.17f, .016f, coral); halo.localRotation = Quaternion.Euler(0, 35, 0);
            Animate(halo, Movement.Spin, 14, 0, Vector3.up);
            var second = Ring("Eye inclined meridian", eye, 1.3f, .011f, violet); second.localRotation = Quaternion.Euler(42, -23, 0);
            Animate(second, Movement.Spin, -13, 0, Vector3.up);
        }

        void LivingData()
        {
            Caption("Social widget", -.72f);
            var data = Installation("Wellbeing Meter", new Vector3(-3.23f, 2.2f, 15.3f), -90);
            var helix = Group("Kinetic social data helix", data, Vector3.zero);
            for (int i = 0; i < 12; i++)
            {
                float height = (i - 5.5f) * .1f;
                var bead = Shape("Social data point", PrimitiveType.Sphere, helix, new Vector3(0, height, 0), Vector3.one * .095f, i % 2 == 0 ? cyan : coral);
                Animate(bead, Movement.Orbit, .8f, .38f, new Vector3(1, .04f, 1), i * .64f);
            }
            var axis = Shape("Live data axis", PrimitiveType.Cylinder, helix, Vector3.zero, new Vector3(.017f, .66f, .017f), violet);
            Animate(axis, Movement.Breathe, 2.2f, .12f);
            Animate(helix, Movement.Float, .9f, .06f, Vector3.up, 0, true);
            Caption("Wellbeing subscription", -.72f);
            var pulse = Installation("Wellbeing Ad", new Vector3(-3.32f, 2.25f, .8f), -90);
            var heartOutline = new[] { new Vector2(0,-.5f),new Vector2(.53f,.01f),new Vector2(.5f,.38f),new Vector2(.27f,.49f),
                new Vector2(0,.28f),new Vector2(-.27f,.49f),new Vector2(-.5f,.38f),new Vector2(-.53f,.01f) };
            var heart = MeshObject("Three dimensional heartbeat", pulse, Extrude("Spatial heart", heartOutline, .16f), coral);
            Animate(heart, Movement.Breathe, 4.1f, .13f, Vector3.zero, 0, true);
            var orbit = Ring("Biometric scanner", pulse, .78f, .017f, cyan); orbit.localRotation = Quaternion.Euler(27, 0, 0);
            Animate(orbit, Movement.Spin, 36, 0, Vector3.up);
            var points = new Vector3[31];
            for (int i = 0; i < points.Length; i++)
            {
                float x = i / 30f;
                points[i] = new Vector3((x - .5f) * 1.85f, -.38f + Mathf.Exp(-Mathf.Pow((x - .5f) * 30, 2)) * .48f - Mathf.Exp(-Mathf.Pow((x - .44f) * 35, 2)) * .2f, -.22f);
            }
            GreyboxUtil.Line("Physical heartbeat trace", pulse, points, mint, .018f);
        }

        void JoyFlower()
        {
            Caption("Alley advertisement", -.66f);
            var bloom = Installation("Beauty Ad", new Vector3(0, 4.9f, 31.25f));
            var flower = Group("Animated Flower", bloom, Vector3.zero);
            Shape("Flower core", PrimitiveType.Sphere, flower, Vector3.zero, Vector3.one * .4f, amber);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * .25f;
                var petal = Shape("Opening crystal petal", PrimitiveType.Sphere, flower, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * .64f,
                    new Vector3(.88f, .31f, .12f), i % 2 == 0 ? coral : violet);
                petal.localRotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg);
                Animate(petal, Movement.Petal, .8f, .18f, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0), i * .23f);
            }
            Animate(flower, Movement.Spin, 12, .1f, new Vector3(.2f, .1f, 1));
            var orbit = Ring("Petal dust orbit", bloom, 1.35f, .018f, mint); orbit.localRotation = Quaternion.Euler(0, 28, 0);
            Animate(orbit, Movement.Spin, -19, 0, Vector3.up);
        }

        void StreetCurrents()
        {
            var currents = Installation("Data Stream", Vector3.zero);
            for (int side = -1; side <= 1; side += 2)
            {
                var points = new Vector3[64];
                for (int i = 0; i < points.Length; i++)
                {
                    float p = i / 63f;
                    points[i] = new Vector3(side * (2.76f + Mathf.Sin(p * Mathf.PI * 8) * .085f), .07f, 8 + p * 25);
                }
                GreyboxUtil.Line("Braided data current", currents, points, side > 0 ? coral : cyan, .012f);
                for (int i = 0; i < 4; i++)
                {
                    var pulse = Shape("Travelling network pulse", PrimitiveType.Sphere, currents, new Vector3(side * 2.76f, .075f, 8),
                        new Vector3(.065f, .035f, .24f), side > 0 ? coral : cyan);
                    Animate(pulse, Movement.Stream, 3.2f, 25, Vector3.forward, i * .25f);
                }
            }
        }

        void FacadeSignals()
        {
            var signals = Installation("Facade Ribbons", Vector3.zero);
            for (int side = -1; side <= 1; side += 2)
            for (int block = 0; block < 4; block++)
            {
                float z = 10.8f + block * 7.5f;
                var ribbon = Shape("Rising facade scanning band", PrimitiveType.Cube, signals, new Vector3(side * 4.24f, 2.85f, z),
                    new Vector3(.036f, .14f, 5.6f), block % 2 == 0 ? coral : cyan);
                Animate(ribbon, Movement.Stream, .55f, 4.2f, Vector3.up, block * .21f + (side > 0 ? .13f : 0));
                var burst = Ring("Facade Data", signals, .41f + .04f * block, .018f, block % 2 == 0 ? mint : amber);
                burst.localPosition = new Vector3(side * 3.9f, 5.7f, z);
                burst.localRotation = Quaternion.Euler(0, side * 90, 0);
                Animate(burst, Movement.Spin, 31, .14f, new Vector3(.15f, .4f, 1), block);
            }
        }

        void AnimateExistingProjections()
        {

            foreach (Transform unit in environment.hyperrealityRoot.transform)
            {
                if (unit == transform) continue;
                if (ObjectNames.Matches(unit.name, "Animated Artwork") || ObjectNames.Matches(unit.name, "Holo Sunrise"))
                {
                    int index = 0;
                    foreach (Transform child in unit)
                    {
                        if (child.childCount > 0) Animate(child, Movement.Spin, (index % 2 == 0 ? 1 : -1) * (18 + index * 9), 0,
                            new Vector3(.25f + index * .13f, 1, .3f), index++);
                        else Animate(child, Movement.Breathe, 1.3f, .07f);
                    }
                }
                if (ObjectNames.Matches(unit.name, "Holo Planter"))
                {
                    int index = 0;
                    foreach (Transform child in unit)
                        if (ObjectNames.Matches(child.name, "Holographic frond")) Animate(child, Movement.Float, .7f, .055f, Vector3.up, index++ * .8f, true);
                }
                if (ObjectNames.Matches(unit.name, "Ambient cloud"))
                {
                    int index = 0;
                    foreach (Transform child in unit) Animate(child, Movement.Float, .52f, .09f, Vector3.up, index++ * .6f);
                }
            }
        }

        void LateUpdate() => Step(Time.time);

        public void Step(float time)
        {
            float reveal = environment ? environment.RevealAmount : 0;
            if (motions == null) return;
            var left = RealityPlayer.LeftHand;
            var right = RealityPlayer.RightHand;
            foreach (var motion in motions)
            {
                if (motion == null || !motion.target) continue;
                float t = time * motion.speed + motion.phase;
                Vector3 position = motion.position;
                Quaternion rotation = motion.rotation;
                Vector3 scale = motion.scale;
                switch (motion.movement)
                {
                    case Movement.Spin:
                        rotation *= Quaternion.Euler(motion.axis * (time * motion.speed + motion.phase * 40));
                        position.y += Mathf.Sin(time * 1.4f + motion.phase) * motion.radius;
                        break;
                    case Movement.Float:
                        position += motion.axis * Mathf.Sin(t) * motion.radius;
                        rotation *= Quaternion.Euler(Mathf.Sin(t * .71f) * 5, Mathf.Sin(t * 1.13f) * 11, Mathf.Sin(t) * 5);
                        break;
                    case Movement.Orbit:
                        position += new Vector3(Mathf.Cos(t) * motion.axis.x, Mathf.Sin(t) * motion.axis.y, Mathf.Sin(t) * motion.axis.z) * motion.radius;
                        rotation *= Quaternion.Euler(t * 21, t * 53, t * 17);
                        break;
                    case Movement.Swim:
                        position += new Vector3(Mathf.Sin(t) * motion.axis.x, Mathf.Sin(t * 2) * motion.axis.y, Mathf.Cos(t) * motion.axis.z) * motion.radius;
                        rotation *= Quaternion.Euler(Mathf.Cos(t * 2) * 7, Mathf.Atan2(Mathf.Sin(t) * motion.axis.z, Mathf.Cos(t) * motion.axis.x) * Mathf.Rad2Deg, Mathf.Sin(t * 2) * 9);
                        break;
                    case Movement.Petal:

                        Vector3 hinge=motion.position-motion.rotation*Vector3.right*(motion.scale.x*.5f+.03f);
                        rotation *= Quaternion.Euler(Mathf.Sin(t)*16,Mathf.Cos(t*.7f)*14,0);
                        position=hinge+rotation*Vector3.right*(motion.scale.x*.5f);
                        break;
                    case Movement.Blink:
                        float blink = Mathf.Pow(Mathf.Max(0, Mathf.Cos(t * Mathf.PI * 2)), 44);
                        scale.y *= 1 - blink * .91f;
                        break;
                    case Movement.Stream:
                        position += motion.axis * Mathf.Repeat(time * motion.speed + motion.phase * motion.radius, Mathf.Max(.001f, motion.radius));
                        break;
                    case Movement.Breathe:
                        float beat = Mathf.Sin(t) * .65f + Mathf.Sin(t * 2) * .35f;
                        scale *= 1 + beat * motion.radius;
                        rotation *= Quaternion.Euler(0, Mathf.Sin(t * .19f) * 17, 0);
                        break;
                }
                if (motion.responsive && motion.target.parent)
                {
                    Vector3 worldRest = motion.target.parent.TransformPoint(position);
                    Transform hand = NearestHand(left, right, worldRest);
                    if (hand)
                    {
                        Vector3 delta = hand.position - worldRest;
                        float response = Mathf.Clamp01(1 - delta.magnitude / 1.1f);
                        position += motion.target.parent.InverseTransformVector(delta.normalized * response * .12f);
                        scale *= 1 + response * .12f;
                    }
                }
                motion.target.localPosition = position;
                motion.target.localRotation = rotation;
                motion.target.localScale = scale;
            }
            for (int i = 0; i < installations.Length && i < installationPositions.Length; i++)
            {
                if (!installations[i]) continue;
                float fracture = reveal * reveal;
                installations[i].localPosition = installationPositions[i] + new Vector3(Mathf.Sin(i * 4.6f) * .85f,
                    .8f + Mathf.Repeat(i * .31f, 1) * 1.5f, Mathf.Cos(i * 3.7f) * .6f) * fracture;
            }
            if (Mathf.Abs(reveal - lastReveal) > .0001f)
            {
                if (properties == null) properties = new MaterialPropertyBlock();
                for (int i = 0; i < surfaces.Length; i++)
                {
                    if (!surfaces[i]) continue;
                    surfaces[i].GetPropertyBlock(properties);
                    properties.SetFloat("_Reveal", reveal);
                    properties.SetFloat("_Seed", Mathf.Repeat(i * .618034f, 1));
                    surfaces[i].SetPropertyBlock(properties);
                }
                lastReveal = reveal;
            }
        }

        static Transform NearestHand(Transform a, Transform b, Vector3 position)
        {
            if (!a || !a.gameObject.activeInHierarchy) return b && b.gameObject.activeInHierarchy ? b : null;
            if (!b || !b.gameObject.activeInHierarchy) return a;
            return (a.position - position).sqrMagnitude < (b.position - position).sqrMagnitude ? a : b;
        }

        static Transform MeshObject(string name, Transform parent, Mesh mesh, Material material)
            => StoryVignetteGeometry.Surface(name, parent, Vector3.zero, mesh, material).transform;

        static Mesh MakeMesh(string name, Vector3[] vertices, int[] triangles)
        {
            var uv = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++) uv[i] = new Vector2(vertices[i].x + .5f, vertices[i].y + .5f);
            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles, uv = uv };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        static Mesh Extrude(string name, Vector2[] outline, float depth)
        {
            int count = outline.Length;
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            Vector2 center = Vector2.zero;
            foreach (var point in outline) center += point;
            center /= count;
            for (int i = 0; i < count; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % count];
                int start = vertices.Count;
                vertices.Add(new Vector3(center.x, center.y, -depth * .5f));
                vertices.Add(new Vector3(b.x, b.y, -depth * .5f));
                vertices.Add(new Vector3(a.x, a.y, -depth * .5f));
                vertices.Add(new Vector3(center.x, center.y, depth * .5f));
                vertices.Add(new Vector3(a.x, a.y, depth * .5f));
                vertices.Add(new Vector3(b.x, b.y, depth * .5f));
                for (int j = 0; j < 6; j++) triangles.Add(start + j);
                start = vertices.Count;
                vertices.Add(new Vector3(a.x, a.y, -depth * .5f)); vertices.Add(new Vector3(b.x, b.y, -depth * .5f));
                vertices.Add(new Vector3(b.x, b.y, depth * .5f)); vertices.Add(new Vector3(a.x, a.y, depth * .5f));
                triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
            return MakeMesh(name, vertices.ToArray(), triangles.ToArray());
        }

        static Mesh InfinityRibbon()
        {
            const int segments = 96, sides = 6;
            var vertices = new Vector3[(segments + 1) * sides];
            var triangles = new int[segments * sides * 6];
            for (int i = 0; i <= segments; i++)
            {
                float t = i * Mathf.PI * 2 / segments;
                Vector3 center = new Vector3(Mathf.Sin(t) * 1.05f, Mathf.Sin(t * 2) * .42f, Mathf.Cos(t) * .24f);
                Vector3 tangent = new Vector3(Mathf.Cos(t) * 1.05f, Mathf.Cos(t * 2) * .84f, -Mathf.Sin(t) * .24f).normalized;
                Vector3 a = Vector3.Cross(tangent, Vector3.forward).normalized;
                Vector3 b = Vector3.Cross(tangent, a).normalized;
                for (int j = 0; j < sides; j++)
                {
                    float angle = j * Mathf.PI * 2 / sides;
                    vertices[i * sides + j] = center + (a * Mathf.Cos(angle) + b * Mathf.Sin(angle)) * .09f;
                    if (i == segments) continue;
                    int p = (i * sides + j) * 6, v = i * sides + j, n = i * sides + (j + 1) % sides;
                    triangles[p] = v; triangles[p + 1] = n; triangles[p + 2] = v + sides;
                    triangles[p + 3] = n; triangles[p + 4] = n + sides; triangles[p + 5] = v + sides;
                }
            }
            return MakeMesh("Infinity Logo", vertices, triangles);
        }
    }
}
