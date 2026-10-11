using System.Collections.Generic;
using UnityEngine;

namespace RealityPlayground
{

    public sealed class RealityWindow : MonoBehaviour
    {
        [SerializeField] bool nature;
        [SerializeField] Transform[] movingDetails;
        Quaternion[] restRotations;
        Vector3[] restPositions;

        void Awake()
        {
            if (movingDetails == null) return;
            restRotations = new Quaternion[movingDetails.Length];
            restPositions = new Vector3[movingDetails.Length];
            for (int i = 0; i < movingDetails.Length; i++)
            {
                if (!movingDetails[i]) continue;
                restRotations[i] = movingDetails[i].localRotation;
                restPositions[i] = movingDetails[i].localPosition;
            }
        }

        void Update()
        {
            if (movingDetails == null || restRotations == null) return;
            for (int i = 0; i < movingDetails.Length; i++)
            {
                var item = movingDetails[i];
                if (!item) continue;
                float wave = Mathf.Sin(Time.time * (nature ? .75f : .19f) + i * 1.83f);
                if (nature)
                {
                    item.localRotation = restRotations[i] * Quaternion.Euler(wave * 2, 0, Mathf.Sin(Time.time * .61f + i) * 2.5f);
                    item.localPosition = restPositions[i] + Vector3.up * wave * .013f;
                }
                else
                    item.localPosition = restPositions[i] + Vector3.up * wave * .012f;
            }
        }

        public static GameObject Create(Transform parent, bool nature)
        {
            var root = new GameObject(ObjectNames.Short(nature ? "Forest Window" : "Concrete Window"));
            root.transform.SetParent(parent, false);
            var rootTransform = root.transform;
            var facade = GreyboxUtil.Material(nature ? "Concrete ash facade" : "Hyperbright ivory facade",
                nature ? new Color(.27f, .285f, .29f) : new Color(.94f, .85f, .60f), nature ? 0 : .3f);
            var edge = GreyboxUtil.Material(nature ? "Nature aperture green seam" : "Bleak aperture white seam",
                nature ? new Color(.26f, 1, .46f) : new Color(1, .9f, .6f), nature ? 1.7f : 1.6f);
            var inner = GreyboxUtil.Material(nature ? "Forest inner rock" : "Dead concrete", nature ? new Color(.11f, .20f, .14f) : new Color(.25f, .27f, .29f));
            var dark = GreyboxUtil.Material("Aperture outer casing", new Color(.09f, .105f, .115f));

            Part("Left wall", rootTransform, new Vector3(-1.45f, 1.55f, 0), new Vector3(1.1f, 3.1f, .38f), facade);
            Part("Right wall", rootTransform, new Vector3(1.45f, 1.55f, 0), new Vector3(1.1f, 3.1f, .38f), facade);
            Part("Wall below opening", rootTransform, new Vector3(0, .375f, 0), new Vector3(1.8f, .75f, .38f), facade);
            Part("Wall above opening", rootTransform, new Vector3(0, 2.825f, 0), new Vector3(1.8f, .55f, .38f), facade);
            for (int side = -1; side <= 1; side += 2)
            {
                Part("Vertical Seam", rootTransform, new Vector3(side * .906f, 1.65f, -.204f), new Vector3(.025f, 1.84f, .032f), edge, false);
                Part("Horizontal Seam", rootTransform, new Vector3(0, 1.65f + side * .91f, -.204f), new Vector3(1.84f, .025f, .032f), edge, false);
            }

            Part("World floor", rootTransform, new Vector3(0, .67f, 2.95f), new Vector3(4, .12f, 5.6f), inner);
            Part("World ceiling", rootTransform, new Vector3(0, 3.22f, 2.95f), new Vector3(4, .12f, 5.6f), dark);
            Part("World left enclosure", rootTransform, new Vector3(-1.96f, 1.95f, 2.95f), new Vector3(.08f, 2.5f, 5.6f), inner);
            Part("World right enclosure", rootTransform, new Vector3(1.96f, 1.95f, 2.95f), new Vector3(.08f, 2.5f, 5.6f), inner);
            var backdrop = GreyboxUtil.Material(nature ? "Forest distant mist" : "Dystopian grey smog", nature ? new Color(.38f, .58f, .43f) : new Color(.33f, .35f, .37f), .35f);
            Part("Distant world backdrop", rootTransform, new Vector3(0, 1.95f, 5.76f), new Vector3(4, 2.5f, .12f), backdrop);
            var moving = new List<Transform>();
            if (nature) BuildForest(rootTransform, moving); else BuildDystopia(rootTransform, moving);
            var titleColor = nature ? new Color(.61f, .85f, .67f) : new Color(.98f, .86f, .55f);
            GreyboxUtil.Label("Lean in and move your head to look through.", rootTransform, new Vector3(0, 3.16f, -.21f), .052f, titleColor);
            var component = root.AddComponent<RealityWindow>();
            component.nature = nature;
            component.movingDetails = moving.ToArray();
            return root;
        }

        static GameObject Part(string name, Transform parent, Vector3 position, Vector3 scale, Material material, bool collider = true)
            => GreyboxUtil.Primitive(name, PrimitiveType.Cube, parent, position, scale, material, collider);

        static void BuildDystopia(Transform parent, List<Transform> moving)
        {
            var tower = GreyboxUtil.Material("Dystopia tower", new Color(.14f, .16f, .18f));
            var towerLight = GreyboxUtil.Material("Dystopia light concrete", new Color(.36f, .38f, .40f));
            var window = GreyboxUtil.Material("Dystopia dead window", new Color(.55f, .58f, .60f), .25f);
            var road = GreyboxUtil.Material("Dystopia asphalt", new Color(.07f, .08f, .09f));
            var wire = GreyboxUtil.Material("Dystopia cable", new Color(.06f, .065f, .07f));
            Part("Central avenue", parent, new Vector3(0, .744f, 3.0f), new Vector3(.65f, .015f, 5.35f), road, false);
            for (int i = 0; i < 8; i++)
                Part("Worn road marker", parent, new Vector3(0, .755f, .7f + i * .63f), new Vector3(.026f, .01f, .22f), window, false);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 5; i++)
                {
                    float height = .9f + ((i * 3 + (side == 1 ? 2 : 0)) % 5) * .22f;
                    float x = side * (1.1f + (i % 2) * .28f);
                    float z = 1.3f + i * .89f;
                    Part("Brutalist tower", parent, new Vector3(x, .75f + height / 2, z), new Vector3(.61f, height, .59f), i % 2 == 0 ? tower : towerLight, false);
                    Part("Tower roof vent", parent, new Vector3(x + .08f, .78f + height, z), new Vector3(.22f, .09f, .19f), road, false);
                    for (int row = 0; row < (int)(height / .20f) - 1; row++)
                        for (int col = 0; col < 3; col++)
                            if ((row + col + i) % 4 != 0)
                                Part("Cold slit window", parent, new Vector3(x + (col - 1) * .15f, .94f + row * .20f, z - .301f), new Vector3(.07f, .068f, .008f), window, false);
                }
                for (int i = 0; i < 3; i++)
                {
                    float z = 1.1f + i * 1.3f;
                    Part("Utility pole", parent, new Vector3(side * .51f, 1.23f, z), new Vector3(.025f, .97f, .025f), wire, false);
                    Part("Utility pole crossbar", parent, new Vector3(side * .51f, 1.64f, z), new Vector3(.27f, .024f, .025f), wire, false);
                }
                GreyboxUtil.Line("Sagging power cable", parent, new[] { new Vector3(side * .5f, 1.67f, 1.1f), new Vector3(side * .5f, 1.55f, 1.75f), new Vector3(side * .5f, 1.67f, 2.4f), new Vector3(side * .5f, 1.55f, 3.05f), new Vector3(side * .5f, 1.67f, 3.7f) }, wire, .008f);
            }
            Part("Monolithic tower", parent, new Vector3(.1f, 1.96f, 5.30f), new Vector3(.53f, 2.43f, .48f), tower, false);
            for (int i = 0; i < 9; i++) Part("Monolith slit", parent, new Vector3(.1f, 1 + i * .23f, 5.05f), new Vector3(.23f, .018f, .01f), window, false);
            var warning = Part("Propaganda slab", parent, new Vector3(-.62f, 1.36f, 2.3f), new Vector3(.34f, .35f, .05f), road, false);
            moving.Add(warning.transform);
            for (int i = 0; i < 3; i++)
                GreyboxUtil.Primitive("Ash heap", PrimitiveType.Sphere, parent, new Vector3((i - 1) * .54f, .78f, .62f + i * .19f), new Vector3(.38f, .1f, .25f), road, false);
            AddLight(parent, new Vector3(0, 2.85f, 2.65f), new Color(.7f, .75f, .85f), 1.4f, 5);
        }

        static void BuildForest(Transform parent, List<Transform> moving)
        {
            var moss = GreyboxUtil.Material("Forest velvet moss", new Color(.15f, .34f, .075f), .14f);
            var grass = GreyboxUtil.Material("Forest fern green", new Color(.21f, .57f, .12f), .2f);
            var canopy = GreyboxUtil.Material("Forest canopy", new Color(.075f, .34f, .11f), .14f);
            var canopyLight = GreyboxUtil.Material("Forest sunlit canopy", new Color(.38f, .60f, .12f), .25f);
            var bark = GreyboxUtil.Material("Forest cedar bark", new Color(.25f, .17f, .08f));
            var stone = GreyboxUtil.Material("Forest river stone", new Color(.36f, .43f, .32f));
            var water = GreyboxUtil.Material("Forest jade water", new Color(.13f, .58f, .54f), .30f);
            var waterGlint = GreyboxUtil.Material("Forest water glints", new Color(.67f, .98f, .79f), 1.1f);
            var firefly = GreyboxUtil.Material("Forest golden spores", new Color(1, .84f, .26f), 3);
            Part("Living forest floor", parent, new Vector3(0, .748f, 3), new Vector3(3.86f, .025f, 5.4f), moss, false);
            for (int i = 0; i < 8; i++)
            {
                float z = .63f + i * .64f;
                float x = Mathf.Sin(i * .8f) * .25f;
                var stream = Part("Winding jade stream", parent, new Vector3(x, .773f, z), new Vector3(.43f, .016f, .72f), water, false);
                stream.transform.localRotation = Quaternion.Euler(0, Mathf.Cos(i * .8f) * 17, 0);
                Part("Stream shimmer", parent, new Vector3(x + .06f, .784f, z), new Vector3(.15f, .006f, .016f), waterGlint, false);
                GreyboxUtil.Primitive("Riverbank boulder", PrimitiveType.Sphere, parent, new Vector3(x + (i % 2 == 0 ? .32f : -.32f), .81f, z), new Vector3(.3f, .16f, .24f), stone, false);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 5; i++)
                {
                    float x = side * (1.08f + (i % 2) * .35f), z = 1.3f + i * .91f;
                    float height = 1.30f + ((i + (side == 1 ? 1 : 0)) % 3) * .24f;
                    GreyboxUtil.Primitive("Cedar trunk", PrimitiveType.Cylinder, parent, new Vector3(x, .75f + height * .5f, z), new Vector3(.12f, height * .5f, .12f), bark, false);
                    var crown = GreyboxUtil.Primitive("Living canopy", PrimitiveType.Sphere, parent, new Vector3(x, .83f + height, z), new Vector3(.84f, .67f, .77f), i % 2 == 0 ? canopy : canopyLight, false);
                    moving.Add(crown.transform);
                    GreyboxUtil.Primitive("Moss bank", PrimitiveType.Sphere, parent, new Vector3(side * .7f, .78f, z - .32f), new Vector3(.65f, .15f, .54f), grass, false);
                }
            }

            for (int i = 0; i < 9; i++)
            {
                var fern = new GameObject("Fernfan");
                fern.transform.SetParent(parent, false);
                fern.transform.localPosition = new Vector3((i % 2 == 0 ? -.53f : .63f) + Mathf.Sin(i * 1.7f) * .15f, .82f, .5f + i * .55f);
                for (int leaf = 0; leaf < 5; leaf++)
                {
                    var frond = GreyboxUtil.Primitive("Fern frond", PrimitiveType.Sphere, fern.transform, new Vector3(0, .12f, 0), new Vector3(.048f, .30f, .09f), grass, false);
                    frond.transform.localRotation = Quaternion.Euler(18 + leaf * 9, leaf * 72, 25 + leaf * 7);
                }
                moving.Add(fern.transform);
            }
            for (int i = 0; i < 13; i++)
            {
                var spore = GreyboxUtil.Primitive("Golden forest spore", PrimitiveType.Sphere, parent,
                    new Vector3(Mathf.Sin(i * 2.4f) * .76f, 1.1f + Mathf.Repeat(i * .37f, 1.3f), .75f + Mathf.Repeat(i * .79f, 4.5f)), Vector3.one * .015f, firefly, false);
                moving.Add(spore.transform);
            }
            AddLight(parent, new Vector3(.1f, 2.8f, 2.2f), new Color(1, .88f, .52f), 2.8f, 5);
        }

        static void AddLight(Transform parent, Vector3 position, Color color, float intensity, float range)
        {
            var lightObject = new GameObject("Worldinteriorlight");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = position;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }
    }
}
