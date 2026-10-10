using UnityEngine;

namespace Garden.Editor
{
    public static partial class GardenBuilder
    {
        static void BuildTrain()
        {
            var metro = Module("Metro", Vector3.zero);
            var train = metro.gameObject.AddComponent<Train>();
            train.loop = loop;
            train.south = Empty("Residence", metro, SouthMetro);
            train.work = Empty("Work", metro, WorkMetro);
            var car = Empty("Carriage", metro, new Vector3(140, 0, 0));
            var center = car.position;
            train.cabin = Empty("Stand", car, center + new Vector3(0, 0, -.65f));
            Cube("Floor", car, center + new Vector3(0, -.15f, 0), new Vector3(4.2f, .3f, 5.2f), road);
            Cube("Roof", car, center + new Vector3(0, 2.7f, 0), new Vector3(4.2f, .2f, 5.2f), concrete);
            Cube("Front", car, center + new Vector3(0, 1.3f, 2.5f), new Vector3(4.2f, 2.6f, .15f), concrete);
            foreach (float side in new[] { -1f, 1f })
            {
                Cube("Sill", car, center + new Vector3(side * 2f, .4f, 0), new Vector3(.15f, .8f, 5), metal);
                Cube("Lintel", car, center + new Vector3(side * 2f, 2.4f, 0), new Vector3(.15f, .5f, 5), concrete);
                foreach (float z in new[] { -2.4f, 0f, 2.4f })
                    Cube("Pillar", car, center + new Vector3(side * 2f, 1.5f, z), new Vector3(.16f, 1.8f, .16f), metal);
                Cube("Window", car, center + new Vector3(side * 2f, 1.5f, 0), new Vector3(.035f, 1.35f, 4.7f), glass, false);
                Cube("Seat", car, center + new Vector3(side * 1.45f, .43f, -.6f), new Vector3(.65f, .15f, 1.9f), blue);
                Cube("Back", car, center + new Vector3(side * 1.78f, .7f, -.6f), new Vector3(.12f, .55f, 1.9f), blue);
                Cube("Jamb", car, center + new Vector3(side * 1.42f, 1.3f, -2.5f), new Vector3(1.16f, 2.6f, .15f), concrete);
            }
            train.door = Cube("Door", car, center + new Vector3(0, 1.1f, -2.5f), new Vector3(1.6f, 2.2f, .13f), metal).transform;
            Cube("Light", car, center + new Vector3(0, 2.55f, .3f), new Vector3(.15f, .025f, 3.3f), glow, false);
            train.display = Text("METRO", car, center + new Vector3(0, 2.05f, 2.39f), .12f);
            Act("Close", car, center + new Vector3(-1f, .8f, 2.2f), train.CloseDoor);
            Act("Ride", car, center + new Vector3(1f, .8f, 2.2f), train.Ride);
            Act("Open", car, center + new Vector3(-1f, 1.3f, 2.2f), train.OpenDoor);
            Act("Leave", car, center + new Vector3(1f, 1.3f, 2.2f), train.Leave);
            Pad("Stand", car, train.cabin.position);
            Pad("Console", car, center + new Vector3(0, 0, .7f));
            train.southView = TrainView("Residence", metro, center, blue);
            train.workView = TrainView("Work", metro, center, red);
            train.workView.SetActive(false);
            BuildStop("Residence", metro, SouthMetro, train.BoardSouth);
            BuildStop("Work", metro, WorkMetro, train.BoardWork);
        }

        static void BuildStop(string name, Transform parent, Vector3 position, UnityEngine.Events.UnityAction board)
        {
            var stop = Empty(name, parent, position);
            var post = position + (name == "Residence" ? Vector3.forward * .7f : Vector3.zero);
            Cube("Post", stop, post + new Vector3(1.6f, 1.1f, .6f), new Vector3(.16f, 2.2f, .16f), metal);
            Cube("Sign", stop, post + new Vector3(1.6f, 2f, .6f), new Vector3(1.6f, .6f, .12f), blue);
            Text("METRO\n" + name, stop, post + new Vector3(1.6f, 2f, .52f), .11f);
            Act("Board", stop, post + new Vector3(1.6f, 1.1f, .45f), board);
            Cube("Bench", stop, position + new Vector3(-1.4f, .45f, 1.5f), new Vector3(1.5f, .14f, .55f), metal);
            Text("Residence / Work\nWalkway also open", stop, position + new Vector3(0, .9f, 1.6f), .09f);
        }

        static GameObject TrainView(string name, Transform parent, Vector3 center, Material trim)
        {
            var view = Empty(name + " View", parent, center);
            foreach (float side in new[] { -1f, 1f })
            {
                Cube("Platform", view, center + new Vector3(side * 3.7f, -.1f, 0), new Vector3(2.8f, .2f, 8), concrete, false);
                Cube("Wall", view, center + new Vector3(side * 5f, 1.6f, 0), new Vector3(.2f, 3.2f, 8), concrete, false);
                Cube("Band", view, center + new Vector3(side * 4.85f, 1.5f, 0), new Vector3(.035f, .4f, 8), trim, false);
                for (int i = -1; i <= 1; i++)
                    Cube("Column", view, center + new Vector3(side * 4f, 1.5f, i * 3f), new Vector3(.3f, 3f, .3f), metal, false);
            }
            return view.gameObject;
        }
    }
}
