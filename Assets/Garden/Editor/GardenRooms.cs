using System.Linq;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Garden.Editor
{
    public static partial class GardenBuilder
    {
        static void ImproveRooms()
        {
            var existing = ObjectNames.Find(world, "Fittings");
            if (existing) Object.DestroyImmediate(existing.gameObject);
            var fittings = Empty("Fittings", world, Vector3.zero);
            var linen = Mat("Linen", new Color(.57f, .55f, .49f));
            var ceramic = Mat("Ceramic", new Color(.66f, .68f, .62f));
            FitHome(fittings, linen, ceramic);
            FitBreakfast(fittings, ceramic);
            FitFactory(fittings);
        }

        static Transform RoomGroup(string name, Transform fittings, Transform source)
        {
            var group = Empty(name, fittings, source.position);
            group.rotation = source.rotation;
            return group;
        }

        static GameObject RoomPart(string name, Transform group, Vector3 local, Vector3 size, Material material)
        {
            var part = Cube(name, group, group.TransformPoint(local), size, material, false);
            part.transform.rotation = group.rotation;
            return part;
        }

        static void BackText(Transform group, TMP_Text label, Vector2 size)
        {
            if (!label) return;
            var back = Cube("Frame", group, label.transform.position + label.transform.forward * .037f, new Vector3(size.x + .10f, size.y + .10f, .035f), metal, false);
            back.transform.rotation = label.transform.rotation;
            var rim = Cube("Edge", group, label.transform.position + label.transform.up * (size.y * .5f + .044f) + label.transform.forward * .01f, new Vector3(size.x + .10f, .018f, .026f), concrete, false);
            rim.transform.rotation = label.transform.rotation;
            label.rectTransform.sizeDelta = size;
            label.margin = new Vector4(.025f, .02f, .025f, .02f);
        }

        static void FitHome(Transform fittings, Material linen, Material ceramic)
        {
            var home = ObjectNames.Find(world, "Home");
            if (!home) return;
            var r = RoomGroup("Home", fittings, home);
            RoomPart("Skirt", r, new Vector3(0, .08f, 2.39f), new Vector3(5.8f, .16f, .035f), metal);
            RoomPart("Skirt", r, new Vector3(-2.89f, .08f, 0), new Vector3(.035f, .16f, 4.7f), metal);
            RoomPart("Skirt", r, new Vector3(2.89f, .08f, 0), new Vector3(.035f, .16f, 4.7f), metal);
            RoomPart("Light", r, new Vector3(0, 3.015f, -.3f), new Vector3(2.3f, .035f, .12f), ceramic);
            RoomPart("Lintel", r, new Vector3(0, 2.49f, -2.385f), new Vector3(1.94f, .09f, .07f), linen);
            foreach (float side in new[] { -.935f, .935f }) RoomPart("Jamb", r, new Vector3(side, 1.23f, -2.385f), new Vector3(.07f, 2.48f, .07f), linen);
            var mattress = ObjectNames.Find(home, "Mattress");
            if (mattress)
            {
                var local = home.InverseTransformPoint(mattress.position);
                RoomPart("Blanket", r, local + new Vector3(0, .082f, -.24f), new Vector3(1.01f, .028f, 1.36f), linen);
                RoomPart("Pillow", r, local + new Vector3(0, .11f, .64f), new Vector3(.78f, .10f, .40f), ceramic);
                RoomPart("Headboard", r, local + new Vector3(0, .13f, 1.05f), new Vector3(1.16f, .46f, .075f), metal);
            }
            var table = ObjectNames.Find(home, "Table");
            if (table)
            {
                var center = home.InverseTransformPoint(table.position);
                foreach (float x in new[] { -.51f, .51f })
                    foreach (float z in new[] { -.32f, .32f }) RoomPart("Leg", r, new Vector3(center.x + x, .31f, center.z + z), new Vector3(.055f, .62f, .055f), metal);
            }
            var chair = ObjectNames.Find(home, "Chair");
            if (chair)
            {
                var center = home.InverseTransformPoint(chair.position);
                RoomPart("Back", r, new Vector3(center.x, .76f, center.z - .25f), new Vector3(.50f, .54f, .055f), linen);
                RoomPart("Seat", r, new Vector3(center.x, .512f, center.z), new Vector3(.46f, .025f, .46f), linen);
            }
            var sink = ObjectNames.Find(home, "Sink");
            if (sink)
            {
                var center = home.InverseTransformPoint(sink.position);
                RoomPart("Basin", r, new Vector3(center.x, .938f, center.z), new Vector3(.28f, .025f, .72f), metal);
                RoomPart("Tap", r, new Vector3(center.x + .1f, 1.045f, center.z + .29f), new Vector3(.025f, .23f, .025f), ceramic);
                RoomPart("Spout", r, new Vector3(center.x + .04f, 1.15f, center.z + .29f), new Vector3(.145f, .027f, .025f), ceramic);
            }
            if (loop.clock)
            {
                loop.clock.transform.SetPositionAndRotation(home.TransformPoint(new Vector3(-.67f, 1.70f, 2.34f)), home.rotation);
                loop.clock.fontSize = .83f;
                loop.clock.alignment = TextAlignmentOptions.Center;
                BackText(r, loop.clock, new Vector2(1.20f, .87f));
            }
            if (loop.receiver)
            {
                loop.receiver.transform.SetPositionAndRotation(home.TransformPoint(new Vector3(2.84f, 1.86f, .15f)), home.rotation * Quaternion.Euler(0, 90, 0));
                loop.receiver.fontSize = .80f;
                loop.receiver.alignment = TextAlignmentOptions.Center;
                BackText(r, loop.receiver, new Vector2(1.14f, .60f));
            }
            if (loop.overlays)
            {
                var view = ObjectNames.Find(loop.overlays.transform, "Garden View");
                if (view) view.SetPositionAndRotation(home.TransformPoint(new Vector3(.06f, 2.58f, 2.25f)), home.rotation);
            }
            foreach (var label in world.GetComponentsInChildren<TMP_Text>(true).ToArray())
            {
                if (label.text.StartsWith("Tomorrow is prepared") || label.text.StartsWith("NEWS\n06 OCT")) Object.DestroyImmediate(label.gameObject);
            }
        }

        static void FitBreakfast(Transform fittings, Material ceramic)
        {
            var breakfast = ObjectNames.Find(world, "Breakfast");
            if (!breakfast) return;
            var r = RoomGroup("Breakfast", fittings, breakfast);
            var counter = ObjectNames.Find(breakfast, "Counter");
            if (counter)
            {
                var center = breakfast.InverseTransformPoint(counter.position);
                RoomPart("Top", r, center + Vector3.up * .614f, new Vector3(2.38f, .028f, .77f), ceramic);
                RoomPart("Inset", r, center + new Vector3(0, -.05f, -.362f), new Vector3(2.14f, .91f, .024f), metal);
                RoomPart("Rail", r, center + new Vector3(0, -.42f, -.387f), new Vector3(2.15f, .028f, .024f), ceramic);
                var label = Text("BREAKFAST", r, breakfast.TransformPoint(center + new Vector3(0, .33f, -.384f)), .10f);
                label.rectTransform.sizeDelta = new Vector2(1.4f, .20f);
                label.transform.rotation = breakfast.rotation;
                label.fontStyle = FontStyles.Normal;
            }
            var table = ObjectNames.Find(breakfast, "Table");
            if (table)
            {
                var center = breakfast.InverseTransformPoint(table.position);
                foreach (float x in new[] { -.46f, .46f })
                    RoomPart("Support", r, new Vector3(center.x + x, .33f, center.z), new Vector3(.055f, .66f, .52f), metal);
            }
            foreach (var label in breakfast.GetComponentsInChildren<TMP_Text>(true).ToArray())
                if (label.text.StartsWith("NUTRITION\n")) Object.DestroyImmediate(label.gameObject);
        }

        static void FitFactory(Transform fittings)
        {
            var factory = ObjectNames.Find(world, "Factory");
            if (!factory) return;
            var r = RoomGroup("Factory", fittings, factory);
            RoomPart("Console", r, new Vector3(.40f, 1.0f, .88f), new Vector3(2.95f, .54f, .18f), metal);
            RoomPart("Rail", r, new Vector3(.40f, 1.29f, .88f), new Vector3(3.03f, .035f, .20f), concrete);
            foreach (float side in new[] { -1.0f, 1.8f }) RoomPart("Support", r, new Vector3(side, .37f, .9f), new Vector3(.075f, .74f, .14f), metal);
            var assembly = factory.GetComponent<Assembly>();
            if (assembly && assembly.display)
            {
                assembly.display.fontSize = 1.0f;
                assembly.display.transform.SetPositionAndRotation(factory.TransformPoint(new Vector3(0, 2.40f, 2.62f)), factory.rotation);
                BackText(r, assembly.display, new Vector2(3.3f, 1.05f));
            }
            foreach (var label in factory.GetComponentsInChildren<TMP_Text>(true).ToArray())
                if (label.text.StartsWith("ASSEMBLY 07\n")) Object.DestroyImmediate(label.gameObject);
        }
    }
}
