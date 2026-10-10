using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RealityPlayground;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Object = UnityEngine.Object;

namespace Garden.Editor
{
    public static class CommunityStyle
    {
        const string Folder = "Assets/Garden/Art/Community/";
        static Material wood, dark, metal, skin, cloth, sage, rust, bread, water, leaf, solar, earth, stone, plaster, dry;
        static Transform site;
        static Loop loop;
        public static string Report { get; private set; }

        public static void Apply(GameObject root)
        {
            if (!root || EditorApplication.isPlaying) throw new InvalidOperationException("Open Garden in edit mode.");
            loop = root.GetComponent<Loop>();
            site = ObjectNames.Find(root.transform, "Outside");
            if (!loop || !site) throw new InvalidOperationException("Outside is missing.");
            Directory.CreateDirectory(Folder + "Meshes");
            Directory.CreateDirectory(Folder + "Materials");
            AssetDatabase.Refresh();
            wood = Material("Wood", new Color(.33f, .22f, .13f));
            dark = Material("Ink", new Color(.09f, .105f, .11f));
            metal = Material("Steel", new Color(.29f, .32f, .31f));
            skin = Material("Skin", new Color(.50f, .34f, .23f));
            cloth = Material("Cloth", new Color(.31f, .39f, .45f));
            sage = Material("Sage", new Color(.37f, .43f, .29f));
            rust = Material("Clay", new Color(.48f, .27f, .18f));
            bread = Material("Crust", new Color(.69f, .44f, .19f));
            water = Material("Water", new Color(.18f, .31f, .32f));
            leaf = Material("Leaf", new Color(.23f, .37f, .17f));
            solar = Material("Solar", new Color(.07f, .12f, .18f));
            earth = Material("Earth", new Color(.34f, .32f, .24f));
            stone = Material("Stone", new Color(.44f, .43f, .37f));
            plaster = Material("Plaster", new Color(.53f, .51f, .44f));
            dry = Material("Grass", new Color(.43f, .44f, .28f));
            Person(ObjectNames.Find(root.transform, "Breakfast/Mara"), Citizen.Task.Wait, cloth);
            Person(ObjectNames.Find(site, "Noor"), Citizen.Task.Offer, sage);
            Person(ObjectNames.Find(site, "Repair"), Citizen.Task.Repair, rust);
            var home = Reset(site, "Community");
            Landscape(home);
            Repair(home);
            Power(home);
            Garden(home);
            Meal(root, home);
            Bird(root);
            if (loop.endCard)
            {
                Object.DestroyImmediate(loop.endCard);
                loop.endCard = null;
            }
            var habitat = home.gameObject.AddComponent<Habitat>();
            habitat.loop = loop;
            habitat.water = Sound(New(home, "Stream", new Vector3(7.3f, .13f, -3.6f)), "Stream", .20f, true, 9);
            habitat.ribbons = home.GetComponentsInChildren<Transform>(true).Where(x => ObjectNames.Matches(x.name, "Ribbon")).ToArray();
            habitat.sky = ObjectNames.Find(home, "Landscape/Sky").gameObject;
            habitat.sky.SetActive(false);
            habitat.exit = site;
            Report = "Living community with earth, worn paving, perimeter, scrub, ridges, patched shelters and exterior sky. Original pads unchanged.";
            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(loop);
            AssetDatabase.SaveAssets();
        }

        static void Landscape(Transform home)
        {
            var root = New(home, "Landscape");
            var floor = ObjectNames.Find(site, "Soil");
            if (floor && floor.TryGetComponent<Renderer>(out var oldFloor)) oldFloor.enabled = false;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            const int columns = 28, rows = 41;
            for (int z = 0; z < rows; z++)
                for (int x = 0; x < columns; x++)
                {
                    float px = -5 + x * 4;
                    float pz = -80 + z * 4;
                    vertices.Add(new Vector3(px, Ground(px, pz), pz));
                }
            for (int z = 0; z < rows - 1; z++)
                for (int x = 0; x < columns - 1; x++)
                {
                    int a = z * columns + x, b = a + columns;
                    triangles.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
                }
            var ground = MeshPart(root, "Earth", vertices, triangles, earth);
            ground.gameObject.AddComponent<MeshCollider>().sharedMesh = ground.GetComponent<MeshFilter>().sharedMesh;
            var apron = New(root, "Apron");
            for (int x = 0; x < 7; x++) for (int z = 0; z < 6; z++)
            {
                float width = x == 6 ? 1 : 2;
                float px = -1.5f + x * 2 - (x == 6 ? .5f : 0);
                Box(apron, "Slab", new Vector3(px, -.108f, -5 + z * 2), new Vector3(width - .027f, .20f, 1.975f), (x + z * 2) % 6 == 0 ? plaster : stone);
            }
            for (int i = 0; i < 15; i++)
            {
                float z = -5.5f + i * .82f;
                var part = Box(apron, "Edge", new Vector3(10.65f + Mathf.Sin(i * 8) * .11f, -.12f, z), new Vector3(.42f, .21f, .7f), i % 3 == 0 ? plaster : stone);
                part.localRotation = Quaternion.Euler(0, i * 23, 3);
            }
            Batch(apron, "Apron");
            Perimeter(root);
            Shelters(root);
            var wild = New(root, "Scrub");
            for (int i = 0; i < 54; i++)
            {
                float x = 12.5f + Mathf.Repeat(i * 7.41f, 45);
                float z = -31 + Mathf.Repeat(i * 11.27f, 64);
                if (i < 18) { x = 1 + Mathf.Repeat(i * 2.19f, 10); z = (i % 2 == 0 ? -1 : 1) * (7.6f + Mathf.Repeat(i * .78f, 5)); }
                var at = new Vector3(x, Ground(x, z), z);
                for (int j = 0; j < 4; j++)
                {
                    float angle = (i * 1.7f + j * 2.39f);
                    float height = .25f + Mathf.Repeat(i * .37f + j * .11f, .5f);
                    Vector3 tip = at + new Vector3(Mathf.Cos(angle) * .18f, height, Mathf.Sin(angle) * .18f);
                    Blade(wild, at + Vector3.right * (j - 1.5f) * .045f, tip, .06f, i % 4 == 0 ? leaf : dry);
                }
            }
            for (int i = 0; i < 19; i++)
            {
                float x = 12 + Mathf.Repeat(i * 3.73f, 32);
                float z = -17 + Mathf.Repeat(i * 9.41f, 35);
                Mound(wild, "Rock", new Vector3(x, Ground(x, z) - .06f, z), new Vector3(.4f + i % 3 * .34f, .35f + i % 4 * .16f, .54f + i % 3 * .27f), stone, i);
            }
            Batch(wild, "Scrub");
            var ridge = New(root, "Ridge");
            for (int i = 0; i < 13; i++)
            {
                float angle = (-80 + i * 160f / 12) * Mathf.Deg2Rad;
                float x = 19 + Mathf.Cos(angle) * 55;
                float z = Mathf.Sin(angle) * 67;
                Mound(ridge, "Hill", new Vector3(x, -1.2f, z), new Vector3(22 + i % 3 * 5, 5 + i % 4 * 1.8f, 18 + i % 3 * 7), i % 2 == 0 ? sage : earth, i * 3);
            }
            Batch(ridge, "Ridge");
            Sky(root);
        }

        static float Ground(float x, float z)
        {
            float edge = Mathf.Max(Mathf.Max(x - 11, -x - 2), Mathf.Abs(z) - 6);
            float blend = Mathf.SmoothStep(0, 1, Mathf.Clamp01(edge / 7));
            return -.30f + blend * (-.56f + Mathf.Sin(x * .17f) * Mathf.Cos(z * .13f) * .43f + Mathf.Sin(z * .39f + x * .13f) * .13f);
        }

        static void Perimeter(Transform root)
        {
            var wall = New(root, "Perimeter");
            foreach (int side in new[] { -1, 1 })
            {
                for (int i = 0; i < 12; i++)
                {
                    float z = side * (4.25f + i * 5);
                    Box(wall, "Wall", new Vector3(-.29f, 3.1f, z), new Vector3(.54f, 6.8f, 4.96f), i % 4 == 0 ? stone : plaster);
                    Box(wall, "Cap", new Vector3(-.25f, 6.53f, z), new Vector3(.78f, .15f, 5), stone);
                    Box(wall, "Pier", new Vector3(.18f, 2.35f, side * (6.67f + i * 5)), new Vector3(.56f, 5.3f, .35f), stone);
                    if (i < 5)
                    {
                        Box(wall, "Patch", new Vector3(.003f, 1.2f + i % 2 * 2, z + side), new Vector3(.025f, 1.1f, 1.7f), i % 2 == 0 ? earth : stone);
                        for (int bolt = 0; bolt < 3; bolt++) Box(wall, "Joint", new Vector3(.027f, .3f + bolt * 2.1f, z - 1.8f), new Vector3(.018f, .055f, .055f), dark);
                    }
                }
            }
            Box(wall, "Lintel", new Vector3(-.28f, 5.8f, 0), new Vector3(.55f, 1.3f, 3.54f), plaster);
            var drain = new Vector3(.12f, .08f, -7.8f);
            Box(wall, "Drain", drain, new Vector3(.06f, .42f, .73f), dark);
            for (int i = 0; i < 6; i++) Box(wall, "Grille", drain + new Vector3(.04f, 0, -.30f + i * .12f), new Vector3(.045f, .41f, .035f), metal);
            Batch(wall, "Perimeter");
        }

        static void Shelters(Transform root)
        {
            var sheds = New(root, "Shelters");
            Box(sheds, "Back", new Vector3(7, 1.2f, 5.08f), new Vector3(4.2f, 2.4f, .08f), wood);
            for (int i = 0; i < 11; i++) Box(sheds, "Batten", new Vector3(5 + i * .39f, 1.18f, 5.025f), new Vector3(.035f, 2.32f, .025f), dark);
            Box(sheds, "Patch", new Vector3(6.1f, 1.31f, 4.967f), new Vector3(.84f, 1.21f, .09f), metal);
            for (int i = 0; i < 5; i++) Box(sheds, "Rib", new Vector3(5.77f + i * .17f, 1.31f, 4.913f), new Vector3(.025f, 1.19f, .018f), stone);
            Box(sheds, "Shelf", new Vector3(7.85f, 1.27f, 4.84f), new Vector3(1.4f, .07f, .35f), wood);
            for (int i = 0; i < 4; i++) Cylinder(sheds, "Jar", new Vector3(7.34f + i * .3f, 1.44f, 4.84f), new Vector3(.18f, .14f, .18f), i % 2 == 0 ? rust : stone);
            for (int i = 0; i < 4; i++)
            {
                var panel = Box(sheds, "Roof", new Vector3(5.48f + i * 1.0f, 2.806f + i % 2 * .018f, 3.6f), new Vector3(.97f, .035f, 2.49f), i % 2 == 0 ? metal : rust);
                panel.localRotation = Quaternion.Euler(1.3f, 0, 0);
            }
            var shed = New(sheds, "Store", new Vector3(9.2f, Ground(9.2f, 9), 9));
            Box(shed, "Base", new Vector3(0, .08f, 0), new Vector3(3.8f, .22f, 2.6f), stone);
            Box(shed, "Back", new Vector3(0, 1.2f, 1.2f), new Vector3(3.6f, 2.4f, .10f), wood);
            foreach (float x in new[] { -1.8f, 1.8f }) Box(shed, "Side", new Vector3(x, 1.2f, 0), new Vector3(.12f, 2.4f, 2.5f), sage);
            Box(shed, "Front", new Vector3(-1.15f, 1.2f, -1.2f), new Vector3(1.3f, 2.4f, .10f), metal);
            Box(shed, "Front", new Vector3(1.18f, 1.2f, -1.2f), new Vector3(1.24f, 2.4f, .10f), wood);
            var roof = Box(shed, "Roof", new Vector3(0, 2.5f, 0), new Vector3(4.05f, .09f, 3.1f), metal);
            roof.localRotation = Quaternion.Euler(-6, 0, 0);
            Box(shed, "Crate", new Vector3(.93f, .4f, .22f), new Vector3(.81f, .7f, .7f), rust);
            Box(shed, "Crate", new Vector3(-.89f, .27f, -.28f), new Vector3(.65f, .45f, .7f), wood);
            var fence = new[] { new Vector3(11, -.1f, -6.6f), new Vector3(14, -.5f, -5.8f), new Vector3(15, -.5f, -2), new Vector3(14.6f, -.5f, 3), new Vector3(15.2f, -.55f, 7.5f) };
            for (int i = 0; i < fence.Length; i++)
            {
                Box(sheds, "Post", fence[i] + Vector3.up * .48f, new Vector3(.075f, 1.3f, .075f), wood);
                if (i > 0) foreach (float y in new[] { .35f, .78f }) Rod(sheds, fence[i - 1] + Vector3.up * y, fence[i] + Vector3.up * y, .009f, metal);
            }
            Batch(sheds, "Shelters");
        }

        static void Blade(Transform root, Vector3 a, Vector3 b, float width, Material material)
        {
            Vector3 side = Vector3.Cross((b - a).normalized, Vector3.forward) * width;
            MeshPart(root, "Blade", new List<Vector3> { a - side, a + side, b, a, a + Vector3.forward * width, b }, new List<int> { 0, 1, 2, 2, 1, 0, 3, 4, 5, 5, 4, 3 }, material);
        }

        static void Mound(Transform root, string name, Vector3 at, Vector3 size, Material material, int seed)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int ring = 0; ring < 3; ring++) for (int i = 0; i < 9; i++)
            {
                float angle = i * Mathf.PI * 2 / 9;
                float r = ring == 0 ? 1 : ring == 1 ? .63f : .19f;
                r *= 1 + Mathf.Sin(i * 5.1f + seed) * .16f;
                float y = ring == 0 ? 0 : ring == 1 ? .52f + Mathf.Sin(i * 2.7f + seed) * .11f : .94f + Mathf.Sin(i + seed) * .04f;
                v.Add(at + Vector3.Scale(size, new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r)));
            }
            v.Add(at + Vector3.up * size.y);
            for (int r = 0; r < 2; r++) for (int i = 0; i < 9; i++)
            {
                int a = r * 9 + i, b = r * 9 + (i + 1) % 9;
                t.AddRange(new[] { a, a + 9, b, b, a + 9, b + 9 });
            }
            for (int i = 0; i < 9; i++) t.AddRange(new[] { 18 + i, 27, 18 + (i + 1) % 9 });
            MeshPart(root, name, v, t, material);
        }

        static Transform MeshPart(Transform parent, string name, List<Vector3> vertices, List<int> triangles, Material material)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var part = New(parent, name);
            part.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (name == "Earth")
            {
                string path = Folder + "Meshes/Earth.asset";
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (!saved) { saved = mesh; AssetDatabase.CreateAsset(saved, path); }
                else { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); }
                part.GetComponent<MeshFilter>().sharedMesh = saved;
            }
            return part;
        }

        static void Sky(Transform root)
        {
            string path = Folder + "Materials/Sky.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(Shader.Find("Garden/Sky")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_Horizon", new Color(.66f, .68f, .63f));
            material.SetColor("_Zenith", new Color(.29f, .43f, .52f));
            material.SetColor("_Glow", new Color(.75f, .67f, .51f));
            EditorUtility.SetDirty(material);
            var dome = Sphere(root, "Sky", new Vector3(8, -8, 0), Vector3.one * 360, material);
            dome.gameObject.layer = 0;
        }

        static void Person(Transform person, Citizen.Task task, Material jacket)
        {
            if (!person) return;
            foreach (string name in new[] { "Body", "Arm" })
                foreach (var child in person.Cast<Transform>().Where(x => ObjectNames.Matches(x.name, name)))
                    if (child.TryGetComponent<Renderer>(out var renderer)) renderer.enabled = false;
            var rig = Reset(person, "Art");
            var citizen = person.GetComponent<Citizen>();
            if (!citizen) citizen = person.gameObject.AddComponent<Citizen>();
            citizen.task = task;
            citizen.phase = task == Citizen.Task.Repair ? .6f : task == Citizen.Task.Offer ? 1.4f : 2.6f;
            var body = New(rig, "Body");
            Sphere(body, "Hip", new Vector3(0, .79f, 0), new Vector3(.33f, .23f, .27f), dark);
            for (int i = -1; i <= 1; i += 2)
            {
                Rod(body, new Vector3(i * .12f, .79f, 0), new Vector3(i * .14f, .17f, .02f), .068f, dark);
                Box(body, "Shoe", new Vector3(i * .14f, .075f, -.04f), new Vector3(.14f, .14f, .28f), wood);
                Sphere(body, "Knee", new Vector3(i * .13f, .48f, -.015f), new Vector3(.16f, .20f, .17f), dark);
            }
            Batch(body, person.name + "Body");
            var chest = New(rig, "Chest");
            Sphere(chest, "Coat", new Vector3(0, 1.13f, 0), new Vector3(.43f, .50f, .29f), jacket);
            Box(chest, "Hem", new Vector3(0, .9f, 0), new Vector3(.35f, .05f, .25f), jacket);
            Box(chest, "Seam", new Vector3(0, 1.13f, -.149f), new Vector3(.014f, .33f, .008f), dark);
            Box(chest, "Pocket", new Vector3(.115f, 1.18f, -.146f), new Vector3(.10f, .10f, .018f), jacket);
            Rod(chest, new Vector3(0, 1.32f, 0), new Vector3(0, 1.47f, 0), .06f, skin);
            Batch(chest, person.name + "Coat");
            citizen.chest = chest;
            citizen.left = Arm(rig, -1, jacket);
            citizen.right = Arm(rig, 1, jacket);
            var head = ObjectNames.Find(person, "Head");
            if (!head) head = New(person, "Head", new Vector3(0, 1.6f, 0));
            if (head.TryGetComponent<Renderer>(out var oldHead)) oldHead.enabled = false;
            head.localScale = Vector3.one;
            var face = Reset(head, "Art");
            Sphere(face, "Face", Vector3.zero, new Vector3(.245f, .31f, .25f), skin);
            Sphere(face, "Hair", new Vector3(0, .078f, .031f), new Vector3(.261f, .19f, .25f), dark);
            Sphere(face, "Nose", new Vector3(0, -.005f, -.126f), new Vector3(.046f, .060f, .057f), skin);
            foreach (float x in new[] { -.054f, .054f })
            {
                Sphere(face, "Eye", new Vector3(x, .032f, -.118f), new Vector3(.026f, .014f, .018f), dark);
                Box(face, "Brow", new Vector3(x, .052f, -.118f), new Vector3(.034f, .009f, .011f), dark);
            }
            Box(face, "Mouth", new Vector3(0, -.063f, -.114f), new Vector3(.049f, .008f, .007f), rust);
            Batch(face, person.name + "Face");
            var meet = person.GetComponent<Meet>();
            if (meet) { meet.face = head; meet.forward = Vector3.back; EditorUtility.SetDirty(meet); }
            if (task == Citizen.Task.Repair)
            {
                var hammer = New(citizen.right.palm, "Hammer");
                Rod(hammer, Vector3.zero, Vector3.down * .12f, .013f, wood);
                Box(hammer, "Head", new Vector3(0, -.135f, 0), new Vector3(.13f, .05f, .047f), metal);
                Batch(hammer, "Hammer");
                var tool = Reset(person, "Tool");
                tool.localPosition = new Vector3(0, .75f, -.6f);
                citizen.foley = Sound(tool, "Tool", .14f, false, 8);
            }
            else if (task == Citizen.Task.Offer)
            {
                var bowl = New(rig, "Bowl", new Vector3(0, 1.08f, -.36f));
                Dish(bowl, .16f, rust);
                Batch(bowl, "NoorBowl");
            }
            else
            {
                var cup = New(citizen.right.palm, "Cup", new Vector3(0, -.018f, -.025f));
                Cylinder(cup, "Cup", Vector3.zero, new Vector3(.09f, .057f, .09f), metal);
                Cylinder(cup, "Drink", new Vector3(0, .057f, 0), new Vector3(.078f, .001f, .078f), dark);
                Batch(cup, "MaraCup");
            }
            EditorUtility.SetDirty(citizen);
        }

        static Citizen.Arm Arm(Transform root, int side, Material jacket)
        {
            var upper = Cylinder(root, "Sleeve", new Vector3(side * .28f, 1.16f, -.06f), new Vector3(.14f, .15f, .14f), jacket);
            var lower = Cylinder(root, "Sleeve", new Vector3(side * .3f, .97f, -.14f), new Vector3(.115f, .13f, .115f), jacket);
            var palm = New(root, side < 0 ? "Left" : "Right", new Vector3(side * .31f, .90f, -.17f));
            Sphere(palm, "Hand", Vector3.zero, new Vector3(.102f, .13f, .070f), skin);
            Sphere(palm, "Thumb", new Vector3(side * -.043f, -.006f, -.004f), new Vector3(.038f, .070f, .05f), skin);
            return new Citizen.Arm { upper = upper, lower = lower, palm = palm, shoulder = new Vector3(side * .21f, 1.29f, 0) };
        }

        static void Repair(Transform root)
        {
            var workshop = New(root, "Workshop");
            Box(workshop, "Bench", new Vector3(8, .71f, 2.25f), new Vector3(1.4f, .075f, .72f), wood);
            foreach (float x in new[] { 7.45f, 8.55f }) foreach (float z in new[] { 2.0f, 2.5f })
                Box(workshop, "Leg", new Vector3(x, .335f, z), new Vector3(.065f, .67f, .065f), metal);
            Box(workshop, "Slat", new Vector3(8, .78f, 2.44f), new Vector3(.57f, .05f, .15f), wood);
            Box(workshop, "Patch", new Vector3(8.13f, .809f, 2.44f), new Vector3(.11f, .008f, .16f), metal);
            for (int i = 0; i < 5; i++) Cylinder(workshop, "Screw", new Vector3(7.51f + i * .055f, .751f, 2.18f), new Vector3(.018f, .008f, .018f), metal);
            var chair = New(workshop, "Chair", new Vector3(9, 0, 2.75f));
            Box(chair, "Seat", new Vector3(0, .44f, 0), new Vector3(.48f, .06f, .43f), wood);
            foreach (float x in new[] { -.19f, .19f }) foreach (float z in new[] { -.16f, .16f })
                Box(chair, "Leg", new Vector3(x, .21f, z), new Vector3(.05f, .42f, .05f), wood);
            foreach (float x in new[] { -.19f, .19f }) Box(chair, "Back", new Vector3(x, .73f, .17f), new Vector3(.05f, .57f, .05f), wood);
            Box(chair, "Slat", new Vector3(0, .88f, .17f), new Vector3(.44f, .09f, .035f), wood);
            Box(chair, "Brace", new Vector3(-.19f, .22f, -.185f), new Vector3(.075f, .14f, .014f), metal);
            Batch(workshop, "Workshop");
        }

        static void Power(Transform root)
        {
            var old = ObjectNames.Find(site, "Solar");
            if (old && old.TryGetComponent<Renderer>(out var renderer)) renderer.enabled = false;
            var power = New(root, "Power");
            var frame = New(power, "Solar", new Vector3(7, 3.0f, 3.6f));
            frame.localRotation = Quaternion.Euler(12, 0, 0);
            Box(frame, "Frame", Vector3.zero, new Vector3(1.68f, .05f, 1.28f), metal);
            foreach (float x in new[] { -.70f, .70f }) foreach (float z in new[] { -.48f, .48f })
            {
                Vector3 end = frame.localPosition + frame.localRotation * new Vector3(x, -.025f, z);
                Rod(power, new Vector3(end.x, 2.79f, end.z), end, .025f, metal);
            }
            for (int x = 0; x < 6; x++) for (int z = 0; z < 4; z++)
                Box(frame, "Cell", new Vector3(-.665f + x * .266f, .029f, -.465f + z * .31f), new Vector3(.252f, .010f, .296f), solar);
            var line = new[] { new Vector3(7.9f, 2.98f, 3.6f), new Vector3(8.75f, 2.8f, 3.6f), new Vector3(8.84f, 2.5f, 4.55f), new Vector3(8.84f, .49f, 4.55f) };
            for (int i = 1; i < line.Length; i++) Rod(power, line[i - 1], line[i], .015f, dark);
            Box(power, "Battery", new Vector3(8.7f, .23f, 4.2f), new Vector3(.58f, .46f, .42f), metal);
            Box(power, "Meter", new Vector3(8.7f, .29f, 3.986f), new Vector3(.18f, .09f, .012f), dark);
            Box(power, "Indicator", new Vector3(8.70f, .29f, 3.978f), new Vector3(.11f, .015f, .008f), sage);
            Batch(power, "Power");
        }

        static void Garden(Transform root)
        {
            var garden = New(root, "Channel");
            Box(garden, "Bed", new Vector3(7.2f, .09f, -3.65f), new Vector3(.55f, .16f, 4.0f), metal);
            Box(garden, "Water", new Vector3(7.2f, .176f, -3.65f), new Vector3(.39f, .01f, 3.95f), water);
            foreach (float x in new[] { 6.93f, 7.47f }) Box(garden, "Edge", new Vector3(x, .16f, -3.65f), new Vector3(.08f, .21f, 4.05f), wood);
            for (int i = 0; i < 4; i++)
            {
                float z = -2.1f - i * .9f;
                Rod(garden, new Vector3(6.9f, .19f, z), new Vector3(6.35f, .19f, z), .017f, metal);
            }
            var barrel = New(garden, "Tank", new Vector3(8.1f, .41f, -4.3f));
            Cylinder(barrel, "Tank", Vector3.zero, new Vector3(.70f, .41f, .70f), rust);
            foreach (float y in new[] { -.25f, .25f }) Cylinder(barrel, "Band", new Vector3(0, y, 0), new Vector3(.72f, .014f, .72f), metal);
            Rod(garden, new Vector3(8.1f, .23f, -4.3f), new Vector3(7.3f, .23f, -4.3f), .027f, metal);
            Batch(garden, "Channel");
            var line = New(root, "Clothes");
            Rod(line, new Vector3(5.2f, 2.3f, 4.55f), new Vector3(8.6f, 2.3f, 4.55f), .008f, dark);
            Batch(line, "Line");
            for (int i = 0; i < 3; i++)
            {
                var ribbon = New(line, "Ribbon", new Vector3(5.6f + i * .8f, 2.3f, 4.55f));
                Box(ribbon, "Cloth", new Vector3(0, -.32f, 0), new Vector3(.5f, .64f, .008f), i == 1 ? sage : cloth);
            }
        }

        static void Meal(GameObject root, Transform community)
        {
            var table = ObjectNames.Find(root.transform, "Table");
            if (!table) return;
            var oldBread = ObjectNames.Find(table, "Bread");
            if (oldBread) Object.DestroyImmediate(oldBread.gameObject);
            var food = New(table, "Bread");
            food.position = site.TransformPoint(new Vector3(4.42f, .83f, 1.18f));
            var model = New(food, "Loaf");
            Sphere(model, "Crust", Vector3.zero, new Vector3(.24f, .105f, .14f), bread);
            for (int i = 0; i < 3; i++)
            {
                var cut = Box(model, "Cut", new Vector3((i - 1) * .062f, .050f, -.004f), new Vector3(.006f, .003f, .064f), wood);
                cut.localRotation = Quaternion.Euler(0, 25, 0);
            }
            Batch(model, "Bread");
            var carry = food.gameObject.AddComponent<Carry>();
            carry.offered = true;
            carry.model = model;
            var shape = food.gameObject.AddComponent<BoxCollider>();
            shape.size = new Vector3(.24f, .11f, .14f);
            Wire(food.gameObject, shape);
            var plate = New(community, "Plate", new Vector3(4.42f, .768f, 1.18f));
            Dish(plate, .18f, rust);
            Batch(plate, "Plate");
            var jug = Reset(table, "Jug");
            jug.position = site.TransformPoint(new Vector3(3.91f, .92f, 1.12f));
            Cylinder(jug, "Body", Vector3.zero, new Vector3(.17f, .15f, .17f), metal);
            Cylinder(jug, "Water", new Vector3(0, .147f, 0), new Vector3(.14f, .003f, .14f), water);
            Rod(jug, new Vector3(.07f, -.015f, 0), new Vector3(.19f, .115f, 0), .022f, metal);
            for (int i = 0; i < 9; i++)
            {
                float a = Mathf.PI * (.5f + i / 9f), b = Mathf.PI * (.5f + (i + 1) / 9f);
                Rod(jug, new Vector3(-.072f + Mathf.Cos(a) * .07f, Mathf.Sin(a) * .1f, 0), new Vector3(-.072f + Mathf.Cos(b) * .07f, Mathf.Sin(b) * .1f, 0), .012f, metal);
            }
            Batch(jug, "Jug");
            var held = jug.gameObject.AddComponent<Carry>();
            held.model = jug;
            var collider = jug.gameObject.AddComponent<BoxCollider>(); collider.size = new Vector3(.34f, .30f, .18f);
            Wire(jug.gameObject, collider);
            var pour = jug.gameObject.AddComponent<Pour>();
            pour.carry = held;
            pour.water = table.GetComponent<Water>();
            pour.spout = New(jug, "Spout", new Vector3(.19f, .115f, 0));
            pour.sound = Sound(jug, "Stream", .13f, true, 3);
        }

        static void Bird(GameObject root)
        {
            var bird = root.GetComponentInChildren<Garden.Bird>(true);
            if (!bird) return;
            bird.transform.position = site.TransformPoint(new Vector3(3.25f, .85f, .96f));
            foreach (var renderer in bird.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            var art = Reset(bird.transform, "Art");
            Sphere(art, "Body", Vector3.zero, new Vector3(.14f, .105f, .21f), wood);
            Sphere(art, "Breast", new Vector3(0, -.02f, .04f), new Vector3(.12f, .08f, .12f), rust);
            var head = New(art, "Head", new Vector3(0, .048f, .094f));
            Sphere(head, "Head", Vector3.zero, new Vector3(.083f, .085f, .082f), wood);
            var beak = Box(head, "Beak", new Vector3(0, -.005f, .057f), new Vector3(.026f, .021f, .041f), dark);
            beak.localRotation = Quaternion.Euler(0, 45, 0);
            foreach (float x in new[] { -.034f, .034f }) Sphere(head, "Eye", new Vector3(x, .014f, .025f), Vector3.one * .009f, dark);
            for (int i = -1; i <= 1; i += 2) Rod(art, new Vector3(i * .032f, -.04f, 0), new Vector3(i * .032f, -.085f, .025f), .005f, dark);
            var tail = Box(art, "Tail", new Vector3(0, .014f, -.124f), new Vector3(.07f, .014f, .13f), dark);
            tail.localRotation = Quaternion.Euler(-15, 0, 0);
            bird.wings = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                var wing = New(art, "Wing", new Vector3(i == 0 ? -.053f : .053f, .025f, -.015f));
                Sphere(wing, "Feathers", new Vector3(i == 0 ? -.041f : .041f, -.015f, -.025f), new Vector3(.10f, .022f, .16f), dark);
                bird.wings[i] = wing;
            }
            bird.head = head;
            bird.call = Sound(Reset(bird.transform, "Call"), "Bird", .26f, false, 13);
            bird.flight = new Vector3(6, 4, 4);
            EditorUtility.SetDirty(bird);
        }

        static void Dish(Transform parent, float radius, Material material)
        {
            Cylinder(parent, "Base", Vector3.zero, new Vector3(radius * 1.65f, .008f, radius * 1.65f), material);
            for (int i = 0; i < 20; i++)
            {
                float a = i * Mathf.PI / 10, b = (i + 1) * Mathf.PI / 10;
                Rod(parent, new Vector3(Mathf.Cos(a) * radius, .025f, Mathf.Sin(a) * radius), new Vector3(Mathf.Cos(b) * radius, .025f, Mathf.Sin(b) * radius), .014f, material);
            }
        }

        static Material Material(string name, Color color)
        {
            string path = Folder + "Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.name = name;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .18f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Transform New(Transform parent, string name, Vector3 position = default)
        {
            var t = new GameObject(ObjectNames.Short(name)).transform;
            t.SetParent(parent, false);
            t.localPosition = position;
            return t;
        }

        static Transform Reset(Transform parent, string name)
        {
            var old = ObjectNames.Find(parent, name);
            if (old) Object.DestroyImmediate(old.gameObject);
            return New(parent, name);
        }

        static Transform Shape(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var obj = GameObject.CreatePrimitive(type);
            obj.name = ObjectNames.Short(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = scale;
            Object.DestroyImmediate(obj.GetComponent<Collider>());
            var renderer = obj.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return obj.transform;
        }

        static Transform Box(Transform p, string n, Vector3 at, Vector3 size, Material m) => Shape(p, n, PrimitiveType.Cube, at, size, m);
        static Transform Sphere(Transform p, string n, Vector3 at, Vector3 size, Material m) => Shape(p, n, PrimitiveType.Sphere, at, size, m);
        static Transform Cylinder(Transform p, string n, Vector3 at, Vector3 size, Material m) => Shape(p, n, PrimitiveType.Cylinder, at, size, m);

        static void Rod(Transform parent, Vector3 a, Vector3 b, float radius, Material material)
        {
            var part = Cylinder(parent, "Bar", (a + b) * .5f, new Vector3(radius * 2, Vector3.Distance(a, b) * .5f, radius * 2), material);
            part.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
        }

        static AudioSource Sound(Transform parent, string clip, float volume, bool looped, float range)
        {
            var source = parent.GetComponent<AudioSource>();
            if (!source) source = parent.gameObject.AddComponent<AudioSource>();
            source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Garden/Audio/" + clip + ".wav");
            source.volume = volume;
            source.spatialBlend = 1;
            source.minDistance = .6f;
            source.maxDistance = range;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.loop = looped;
            source.playOnAwake = false;
            source.dopplerLevel = 0;
            return source;
        }

        static void Wire(GameObject obj, Collider collider)
        {
            obj.layer = 8;
            foreach (var t in obj.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 8;
            var grab = obj.GetComponent<XRSimpleInteractable>();
            if (!grab) grab = obj.AddComponent<XRSimpleInteractable>();
            grab.colliders.Clear(); grab.colliders.Add(collider);
            if (!obj.GetComponent<XRExperimentBridge>()) obj.AddComponent<XRExperimentBridge>();
        }

        static void Batch(Transform root, string name)
        {
            var filters = root.GetComponentsInChildren<MeshFilter>(true).Where(x => x.sharedMesh).ToArray();
            foreach (var group in filters.GroupBy(x => x.GetComponent<Renderer>().sharedMaterial))
            {
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                var items = group.Select(x => new CombineInstance { mesh = x.sharedMesh, transform = root.worldToLocalMatrix * x.transform.localToWorldMatrix }).ToArray();
                mesh.CombineMeshes(items, true, true, false);
                string path = Folder + "Meshes/" + name + group.Key.name + ".asset";
                var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (!saved) { saved = mesh; AssetDatabase.CreateAsset(saved, path); }
                else
                {
                    saved.Clear(); saved.indexFormat = mesh.indexFormat; saved.vertices = mesh.vertices; saved.normals = mesh.normals; saved.uv = mesh.uv; saved.triangles = mesh.triangles;
                    saved.RecalculateBounds(); saved.UploadMeshData(false); EditorUtility.SetDirty(saved); Object.DestroyImmediate(mesh);
                }
                var obj = New(root, name);
                obj.gameObject.AddComponent<MeshFilter>().sharedMesh = saved;
                var renderer = obj.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = group.Key;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false; renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            foreach (var filter in filters) if (filter) Object.DestroyImmediate(filter.gameObject);
        }
    }
}
