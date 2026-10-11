using System;
using System.Collections.Generic;
using RealityPlayground;
using TMPro;
using UnityEngine;

namespace Garden
{
    [DefaultExecutionOrder(-100)]
    public sealed class Guide : MonoBehaviour
    {
        [Serializable]
        public struct Edge
        {
            public int from, to;
            public float cost;
            public Collider boundary;
        }

        [Serializable]
        public struct Stop
        {
            public Transform point;
            public string title, need;
        }

        public Loop loop;
        public Train train;
        public Door homeDoor, innerDoor;
        public Gate gate;
        public Delivery delivery;
        public NavPad[] nodes = Array.Empty<NavPad>();
        public Edge[] edges = Array.Empty<Edge>();
        public Stop[] stops = Array.Empty<Stop>();
        public Transform[] doorHandles = Array.Empty<Transform>();
        public Transform[] innerHandles = Array.Empty<Transform>();
        public Transform breakfast, metro, station, dinner, bed;
        public Transform trainDoor, trainRide, trainExit, signalExit, threshold;
        public Renderer[] arrows = Array.Empty<Renderer>();
        public LineRenderer beam, pulse, pin;
        public TMP_Text label;
        public TMP_Text[] signs = Array.Empty<TMP_Text>();
        public Transform Target { get; private set; }
        public NavPad Next { get; private set; }
        public NavPad[] Path { get; private set; } = Array.Empty<NavPad>();
        public NavPad[] Choices { get; private set; } = Array.Empty<NavPad>();
        public bool RouteFound { get; private set; }
        public bool Arrival { get; private set; }
        public string Destination { get; private set; }
        public string Hint { get; private set; }
        public int VisibleArrows { get; private set; }
        List<Edge>[] graph;
        float[] costs;
        int[] previous;
        bool[] visited, travel;
        float nextRoute;
        bool physical;
        NavPad current, back;
        Vector3 lastFeet;
        string labelText;
        Renderer[] labelSources = Array.Empty<Renderer>();
        readonly List<Renderer> labelBlockers = new List<Renderer>();
        readonly Dictionary<Mesh, Shape> shapes = new Dictionary<Mesh, Shape>();
        Vector3 blockerPoint;
        Vector3 labelPoint, labelEye;
        Quaternion labelFacing;
        Transform labelTarget;
        float nextLabel;
        bool labelVisible;
        readonly RaycastHit[] hits = new RaycastHit[32];
        readonly List<NavPad> result = new List<NavPad>();
        readonly List<NavPad> choices = new List<NavPad>();
        readonly List<string> titles = new List<string>();
        readonly Vector3[] trail = new Vector3[17];
        MaterialPropertyBlock color;
        Color Tint => loop && (loop.state.local || loop.InAngel) ? new Color(.95f, .66f, .31f) : new Color(.28f, .93f, .80f);

        void Awake()
        {
            if (!loop) loop = Loop.Instance;
            color = new MaterialPropertyBlock();
            Bake();
            CacheLabelBlockers();
        }

        void OnEnable()
        {
            if (!loop) loop = Loop.Instance;
            if (loop) loop.Changed += Invalidate;
            Invalidate();
        }

        void OnDisable()
        {
            if (loop) loop.Changed -= Invalidate;
            Hide();
            ClearChoices();
            Target = null;
            Next = null;
            Path = Array.Empty<NavPad>();
            RouteFound = false;
        }

        void Invalidate()
        {
            nextRoute = nextLabel = 0;
        }

        public void Record(NavPad pad)
        {
            if (current && current != pad) back = current;
            current = pad;
            Invalidate();
        }

        void Bake()
        {
            graph = new List<Edge>[nodes.Length];
            costs = new float[nodes.Length];
            previous = new int[nodes.Length];
            visited = new bool[nodes.Length];
            travel = new bool[nodes.Length];
            for (int i = 0; i < graph.Length; i++)
            {
                graph[i] = new List<Edge>();
                if (nodes[i]) nodes[i].guide = this;
            }
            foreach (var edge in edges)
                if (edge.from >= 0 && edge.from < graph.Length && edge.to >= 0 && edge.to < graph.Length)
                    graph[edge.from].Add(edge);
        }

        void Update()
        {
            if (!loop || !RealityPlayer.Head || loop.Busy || Session.IsOpen)
            {
                Hide();
                ClearChoices();
                Invalidate();
                return;
            }
            if (Time.unscaledTime >= nextRoute || (RealityPlayer.FeetPosition - lastFeet).sqrMagnitude > .25f)
            {
                nextRoute = Time.unscaledTime + .35f;
                RefreshRoute();
            }
            Show();
        }

        public void RefreshRoute()
        {
            if (graph == null || graph.Length != nodes.Length) Bake();
            if (!loop || !RealityPlayer.Head || loop.Busy)
            {
                ClearChoices();
                return;
            }
            lastFeet = RealityPlayer.FeetPosition;
            physical = true;
            Target = Goal(out var destination, out var hint);
            Destination = destination;
            Hint = hint;
            labelText = (physical || loop.InAngel ? "CONTROLS" : "CIVIC") + " / " + destination + "\n" + hint;
            Arrival = Target && physical && Flat(Target.position - lastFeet) <= 1.05f && Mathf.Abs(Target.position.y - lastFeet.y) < 2.3f;
            for (int i = 0; i < nodes.Length; i++) travel[i] = nodes[i] && nodes[i].CanTravel(out _);
            Path = Array.Empty<NavPad>();
            Next = null;
            RouteFound = Target && FindPath(Closest(Target.position));
            if (RouteFound && !Arrival)
                foreach (var pad in Path)
                    if (Usable(pad)) { Next = pad; break; }
            SelectChoices();
            BuildTrail();
        }

        Transform Goal(out string title, out string hint)
        {
            title = "ROUTE";
            hint = "FOLLOW THE LIGHT";
            if (loop.state.local || loop.state.outside) return null;
            if (loop.InAngel)
            {
                title = "RETURN";
                hint = "PULL TO RETURN";
                return signalExit;
            }
            if (train && train.Inside)
            {
                title = "METRO";
                if (train.Riding) { hint = "IN TRANSIT"; return train.display ? train.display.transform : train.cabin; }
                if (!train.HasArrived)
                {
                    if (!train.Closed) { hint = "PULL TO CLOSE DOORS"; return trainDoor; }
                    hint = "TURN TO DEPART";
                    return trainRide;
                }
                if (!train.Open) { hint = "PULL TO OPEN DOORS"; return trainDoor; }
                title = train.AtWork ? "ASSEMBLY" : "RESIDENCE";
                hint = "TELEPORT TO THE PLATFORM";
                physical = false;
                return trainExit;
            }
            if (homeDoor && !homeDoor.open && loop.Near(loop.home, 4.2f) && (loop.Phase == DayPhase.Commute || loop.Phase == DayPhase.Work))
            {
                title = "DOOR";
                hint = "PULL THE HANDLE";
                return ClosestHandle(doorHandles, homeDoor);
            }
            switch (loop.Phase)
            {
                case DayPhase.Morning:
                    title = "WAKE";
                    hint = "SWAT THE ALARM";
                    return loop.alarmPoint;
                case DayPhase.Breakfast:
                    if (loop.Near(loop.home, 4.2f) && homeDoor && !homeDoor.open)
                    {
                        title = "DOOR";
                        hint = "PULL THE HANDLE";
                        return ClosestHandle(doorHandles, homeDoor);
                    }
                    title = "COMMONS";
                    hint = "GRIP / LIFT TO MOUTH";
                    return breakfast ? breakfast : loop.breakfast;
                case DayPhase.Commute:
                    if (loop.Near(loop.factory, 15) || train && loop.Near(train.work, 9))
                    {
                        title = "ASSEMBLY";
                        hint = "PRESS ACCEPT OR REJECT";
                        return station ? station : loop.factory;
                    }
                    title = "METRO";
                    hint = "TELEPORT TO BOARD";
                    physical = false;
                    return metro;
                case DayPhase.Work:
                    title = "ASSEMBLY";
                    hint = "PRESS ACCEPT OR REJECT";
                    return station ? station : loop.factory;
                case DayPhase.Return:
                    physical = false;
                    if (!loop.state.returnArcade) { title = "ARCADE"; hint = "RETURN THROUGH THE ARCADE"; return loop.concourse; }
                    if (!loop.state.returnCommons) { title = "COMMONS"; hint = "CONTINUE THROUGH COMMONS"; return loop.breakfast; }
                    title = "HOME";
                    hint = "CONTINUE HOME";
                    if (loop.Near(loop.home, 6) && homeDoor && !homeDoor.open)
                    {
                        title = "DOOR";
                        hint = "PULL THE HANDLE";
                        physical = true;
                        return ClosestHandle(doorHandles, homeDoor);
                    }
                    return loop.home;
                case DayPhase.Dinner:
                    title = loop.state.dinnerReady ? "MEAL" : loop.state.dinner ? "DELIVERY" : "ORDER";
                    hint = loop.state.dinnerReady ? "GRIP / LIFT TO MOUTH" : loop.state.dinner ? "WAIT FOR DELIVERY" : "PULL RECEIVER HANDLE";
                    return loop.state.dinnerReady && delivery && delivery.package ? delivery.package.transform : dinner ? dinner : loop.dinnerPoint;
                case DayPhase.Evening:
                    title = "REST";
                    hint = "HOLD BED CONTROL";
                    return bed ? bed : loop.bed;
                case DayPhase.Local:
                    title = "SERVICE";
                    if (loop.Near(loop.home, 4.2f) && homeDoor && !homeDoor.open)
                    {
                        hint = "PULL THE DOOR HANDLE";
                        return ClosestHandle(doorHandles, homeDoor);
                    }
                    if (innerDoor && !innerDoor.open && innerDoor.leaf && gate)
                    {
                        var direction = gate.transform.position - innerDoor.transform.position;
                        if (Vector3.Dot(lastFeet - innerDoor.leaf.position, direction) < 0)
                        {
                            hint = "PULL SERVICE DOOR";
                            return ClosestHandle(innerHandles, innerDoor);
                        }
                    }
                    if (!gate) { physical = false; hint = "MOVE BETWEEN COVER"; return loop.checkpoint; }
                    if (!loop.state.gateLocal) { hint = "TURN NETWORK TO LOCAL"; return gate.selector; }
                    if (!loop.state.gateEqual) { hint = "PULL BYPASS LEVER"; return gate.lever; }
                    if (!loop.state.gateOpen) { hint = "TURN WHEEL WHEN SAFE"; return gate.wheel; }
                    title = "OUTSIDE";
                    hint = "PASS THROUGH OPEN GATE";
                    physical = false;
                    return threshold ? threshold : loop.outside;
                case DayPhase.Outside:
                    title = "GARDEN";
                    hint = "TAKE YOUR TIME";
                    physical = false;
                    return loop.outside;
                default:
                    return null;
            }
        }

        Transform ClosestHandle(Transform[] handles, Door door)
        {
            Transform best = null;
            float distance = float.PositiveInfinity;
            foreach (var handle in handles)
            {
                if (!handle || !handle.gameObject.activeInHierarchy) continue;
                float candidate = (handle.position - RealityPlayer.Head.position).sqrMagnitude;
                if (candidate < distance && Clear(RealityPlayer.Head.position, handle.position))
                {
                    best = handle;
                    distance = candidate;
                }
            }
            return best ? best : door && door.leaf ? door.leaf : loop.home;
        }

        static Vector3 Point(NavPad pad) => pad.destination ? pad.destination.position : pad.transform.position;
        static float Flat(Vector3 point) => new Vector2(point.x, point.z).magnitude;

        int Closest(Vector3 point)
        {
            if (Target)
            {
                var pad = Target.GetComponent<NavPad>();
                if (pad && pad.isActiveAndEnabled && pad.unlocked) return Array.IndexOf(nodes, pad);
            }
            int best = -1;
            float distance = float.PositiveInfinity;
            Vector3 sight = point + Vector3.up * (physical ? .18f : 1.15f);
            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                if (!node || !node.isActiveAndEnabled || !node.unlocked) continue;
                if (train && train.Inside && Target != trainExit && node.transform == trainExit) continue;
                var gap = Point(node) - point;
                if (Mathf.Abs(gap.y) > 2.2f) continue;
                if (physical && Flat(gap) > 2.25f) continue;
                float candidate = gap.sqrMagnitude;
                if (candidate >= distance || !Clear(Point(node) + Vector3.up * 1.65f, sight)) continue;
                best = i;
                distance = candidate;
            }
            return best;
        }

        bool FindPath(int end)
        {
            if (end < 0) return false;
            for (int i = 0; i < nodes.Length; i++)
            {
                costs[i] = travel[i] ? Vector3.Distance(lastFeet, Point(nodes[i])) : float.PositiveInfinity;
                previous[i] = -1;
                visited[i] = false;
            }
            for (int pass = 0; pass < nodes.Length; pass++)
            {
                int current = -1;
                float cost = float.PositiveInfinity;
                for (int i = 0; i < nodes.Length; i++)
                    if (!visited[i] && costs[i] < cost) { current = i; cost = costs[i]; }
                if (current < 0) break;
                if (current == end)
                {
                    result.Clear();
                    for (int i = end; i >= 0; i = previous[i]) result.Add(nodes[i]);
                    result.Reverse();
                    Path = result.ToArray();
                    return true;
                }
                visited[current] = true;
                foreach (var edge in graph[current])
                {
                    if (edge.boundary && edge.boundary.enabled && (!homeDoor || edge.boundary != homeDoor.barrier) && (!innerDoor || edge.boundary != innerDoor.barrier)) continue;
                    if (!nodes[edge.to] || !nodes[edge.to].isActiveAndEnabled || !nodes[edge.to].unlocked) continue;
                    float candidate = cost + Mathf.Max(.01f, edge.cost);
                    if (candidate >= costs[edge.to]) continue;
                    costs[edge.to] = candidate;
                    previous[edge.to] = current;
                }
            }
            return false;
        }

        bool Usable(NavPad pad, bool reachable = false)
        {
            if (!pad || !pad.Ready || !reachable && !pad.CanTravel(out _)) return false;
            var gap = Point(pad) - lastFeet;
            if (Flat(gap) < .6f && Mathf.Abs(gap.y) < .4f) return false;
            if (loop.state.local && train && (Flat(Point(pad) - train.south.position) < .2f || Flat(Point(pad) - train.work.position) < .2f)) return false;
            return true;
        }

        void ClearChoices()
        {
            foreach (var node in nodes) if (node && node.Offered) node.Offer(false);
            Choices = Array.Empty<NavPad>();
            choices.Clear();
            titles.Clear();
            Next = null;
        }

        void SelectChoices()
        {
            choices.Clear();
            titles.Clear();
            if (train && train.Riding) Next = null;
            else
            {
                if (Next && !loop.state.local && !loop.state.outside) Add(Next, "NEXT / " + Destination);
                if (Usable(back) && Distinct(back)) Add(back, "BACK");
                for (int i = choices.Count; i < 4; i++)
                {
                    var pad = LookExit();
                    if (!pad) break;
                    Add(pad, "");
                }
            }
            Choices = choices.ToArray();
            foreach (var pad in nodes)
            {
                if (!pad) continue;
                int index = choices.IndexOf(pad);
                bool offered = index >= 0;
                string role = offered ? pad == Next ? "NEXT" : titles[index] == "BACK" ? "BACK" : "EXPLORE" : "";
                if (offered || pad.Offered) pad.Offer(offered, role);
            }
        }

        bool Distinct(NavPad pad)
        {
            foreach (var chosen in choices)
                if (chosen == pad || Flat(Point(chosen) - Point(pad)) < 1.8f) return false;
            return true;
        }

        NavPad LookExit()
        {
            NavPad best = null;
            float score = float.NegativeInfinity;
            Vector3 gaze = Vector3.ProjectOnPlane(RealityPlayer.Head.forward, Vector3.up).normalized;
            for (int i = 0; i < nodes.Length; i++)
            {
                var pad = nodes[i];
                if (!travel[i] || !Usable(pad, true) || !Distinct(pad)) continue;
                var gap = Point(pad) - lastFeet;
                float distance = Flat(gap);
                if (distance < 1.5f || distance > 7f) continue;
                Vector3 direction = Vector3.ProjectOnPlane(gap, Vector3.up).normalized;
                float looking = Vector3.Dot(direction, gaze);
                if (choices.Count > 1 && looking < -.35f) continue;
                float value = looking * 3 - Mathf.Abs(distance - 4f);
                if (value <= score) continue;
                score = value;
                best = pad;
            }
            return best;
        }

        void Add(NavPad pad, string title)
        {
            if (!pad || choices.Contains(pad)) return;
            choices.Add(pad);
            titles.Add(title);
        }

        void Show()
        {
            foreach (var arrow in arrows) if (arrow) arrow.enabled = false;
            VisibleArrows = 0;
            if (loop.state.local || loop.state.outside) { Hide(); return; }
            ShowRoute();
            for (int i = 0; i < signs.Length; i++)
            {
                var sign = signs[i];
                if (!sign) continue;
                bool shown = i < choices.Count && choices[i] && choices[i].Available && !string.IsNullOrEmpty(titles[i]) && Vector3.Distance(Point(choices[i]), RealityPlayer.Head.position) > 2.2f;
                sign.gameObject.SetActive(shown);
                if (!shown) continue;
                var toward = Vector3.ProjectOnPlane(RealityPlayer.Head.position - Point(choices[i]), Vector3.up).normalized;
                var at = Point(choices[i]) + Vector3.up * .35f + Vector3.Cross(Vector3.up, toward) * .85f;
                sign.transform.SetPositionAndRotation(at, Quaternion.LookRotation(at - RealityPlayer.Head.position));
                sign.color = choices[i] == Next ? Tint : new Color(.62f, .72f, .71f);
                sign.text = titles[i];
            }
            if (!label) return;
            bool near = Target && Vector3.Distance(Target.position, RealityPlayer.Head.position) < 8f && !(loop.Phase == DayPhase.Morning && loop.morning && loop.morning.activeInHierarchy);
            if (!near)
            {
                label.gameObject.SetActive(false);
                if (pin) pin.enabled = false;
                labelVisible = false;
                return;
            }
            if (Time.unscaledTime >= nextLabel || labelTarget != Target || (RealityPlayer.Head.position - labelEye).sqrMagnitude > .04f)
            {
                nextLabel = Time.unscaledTime + .22f;
                labelEye = RealityPlayer.Head.position;
                if (labelTarget != Target || (Target.position - blockerPoint).sqrMagnitude > .25f) CacheNearbyLabels();
                labelTarget = Target;
                labelVisible = PlaceLabel();
            }
            label.gameObject.SetActive(labelVisible);
            if (pin) pin.enabled = labelVisible;
            if (!labelVisible) return;
            label.text = labelText;
            label.color = Tint;
            label.transform.SetPositionAndRotation(labelPoint, labelFacing);
            if (pin)
            {
                Paint(pin, Tint * .65f);
                pin.SetPosition(0, Target.position + Vector3.up * .06f);
                pin.SetPosition(1, labelPoint - Vector3.up * .18f);
            }
        }

        void ShowRoute()
        {
            bool shown = Next && Next.Available && !(train && train.Riding);
            if (beam) beam.enabled = shown;
            if (pulse) pulse.enabled = shown;
            if (!shown || !beam || !pulse) return;
            Paint(beam, Tint * .30f);
            float t = Mathf.Repeat(Time.unscaledTime * .55f, 1f);
            pulse.SetPosition(0, TrailPoint(Mathf.Max(0, t - .18f)));
            pulse.SetPosition(1, TrailPoint(t));
            Paint(pulse, Tint);
        }

        void Paint(Renderer renderer, Color tint)
        {
            if (color == null) color = new MaterialPropertyBlock();
            tint.a = 1;
            color.SetColor("_BaseColor", tint);
            color.SetColor("_Color", tint);
            renderer.SetPropertyBlock(color);
        }

        Vector3 TrailPoint(float t)
        {
            float index = t * (trail.Length - 1);
            int low = Mathf.Min(trail.Length - 2, Mathf.FloorToInt(index));
            return Vector3.Lerp(trail[low], trail[low + 1], index - low);
        }

        void BuildTrail()
        {
            if (!Next || !beam) return;
            Vector3 a = lastFeet;
            Vector3 b = Point(Next);
            a += (b - a).normalized * .5f;
            for (int i = 0; i < trail.Length; i++)
            {
                Vector3 point = Vector3.Lerp(a, b, i / (float)(trail.Length - 1));
                int count = Physics.RaycastNonAlloc(point + Vector3.up * .7f, Vector3.down, hits, 1.4f, 1, QueryTriggerInteraction.Ignore);
                float height = float.NegativeInfinity;
                for (int j = 0; j < count; j++)
                {
                    var hit = hits[j];
                    if (!hit.collider || hit.normal.y < .65f || hit.collider.GetComponentInParent<NavPad>() || PlayerPart(hit.collider.transform)) continue;
                    if (Mathf.Abs(hit.point.y - point.y) < .65f) height = Mathf.Max(height, hit.point.y);
                }
                if (!float.IsNegativeInfinity(height)) point.y = height;
                trail[i] = point + Vector3.up * .035f;
            }
            beam.positionCount = trail.Length;
            beam.SetPositions(trail);
        }
        void CacheLabelBlockers()
        {
            if (!loop) return;
            var candidates = new List<Renderer>();
            foreach (var renderer in loop.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer || renderer.transform.IsChildOf(transform) || renderer.GetComponentInParent<NavPad>()) continue;
                var material = renderer.sharedMaterial;
                if (!material || material.renderQueue > 2500 && !renderer.GetComponent<TMP_Text>() || PlayerPart(renderer.transform) || renderer.transform.IsChildOf(transform)) continue;
                Vector3 size = renderer.bounds.size;
                if (Mathf.Max(size.x, Mathf.Max(size.y, size.z)) > 8f) continue;
                candidates.Add(renderer);
            }
            labelSources = candidates.ToArray();
        }

        void CacheNearbyLabels()
        {
            labelBlockers.Clear();
            blockerPoint = Target.position;
            foreach (var renderer in labelSources)
                if (renderer && (renderer.bounds.center - blockerPoint).sqrMagnitude <= 25) labelBlockers.Add(renderer);
        }

        bool PlaceLabel()
        {
            Vector3 toward = labelEye - Target.position;
            toward.y = 0;
            toward = toward.sqrMagnitude > .001f ? toward.normalized : Vector3.back;
            Vector3 side = Vector3.Cross(Vector3.up, toward);
            float top = Target.position.y + .18f;
            foreach (var renderer in Target.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || renderer.GetComponent<TMP_Text>() || renderer is ParticleSystemRenderer || renderer is LineRenderer) continue;
                var bounds = renderer.bounds;
                if (bounds.size.magnitude > 4 || Flat(bounds.center - Target.position) > 1.1f) continue;
                top = Mathf.Max(top, Mathf.Min(bounds.max.y, Target.position.y + 1.7f));
            }
            float ceiling = Mathf.Min(lastFeet.y + 2.15f, labelEye.y + .28f);
            float level = Mathf.Clamp(Target.position.y + .3f, Mathf.Min(lastFeet.y + .9f, ceiling), ceiling);
            for (int i = 0; i < 9; i++)
            {
                Vector3 point = Target.position - toward * (i < 3 ? .08f + i * .22f : .04f);
                point.y = i < 3 ? Mathf.Min(top + .16f + i * .24f, ceiling) : level;
                if (i < 3 && top + .1f > ceiling) continue;
                if (i >= 3) point += side * (i % 2 == 0 ? 1 : -1) * (i < 5 ? .62f : i < 7 ? .82f : .98f);
                if (i >= 7) point -= toward * .22f;
                if ((point - labelEye).sqrMagnitude < 2.25f) continue;
                Quaternion facing = i < 3 ? Quaternion.LookRotation(point - labelEye) : Quaternion.LookRotation(-toward);
                if (!LabelClear(point, facing)) continue;
                labelPoint = point;
                labelFacing = facing;
                return true;
            }
            return false;
        }

        bool LabelClear(Vector3 point, Quaternion facing)
        {
            if ((point - labelEye).sqrMagnitude < 2.25f) return false;
            Vector3 scale = label.transform.lossyScale;
            Vector2 size = label.rectTransform.rect.size;
            Vector3 right = facing * Vector3.right * (size.x + .04f) * Mathf.Abs(scale.x) * .5f;
            Vector3 up = facing * Vector3.up * (size.y + .02f) * Mathf.Abs(scale.y) * .5f;
            for (int i = 0; i < 9; i++)
            {
                Vector3 corner = point + right * (i % 3 - 1) + up * (i / 3 - 1);
                if (!Clear(labelEye, corner)) return false;
                Vector3 direction = corner - labelEye;
                float distance = direction.magnitude;
                if (distance < .9f) return false;
                var ray = new Ray(labelEye, direction / distance);
                foreach (var renderer in labelBlockers)
                {
                    if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy || PlayerPart(renderer.transform)) continue;
                    Bounds bounds = renderer.bounds;
                    if (bounds.Contains(labelEye)) continue;
                    if (bounds.IntersectRay(ray, out float hit) && hit < distance - .015f && Blocks(renderer, ray, distance - .015f)) return false;
                }
            }
            return true;
        }

        bool Blocks(Renderer renderer, Ray ray, float distance)
        {
            if (renderer.GetComponent<TMP_Text>()) return true;
            var filter = renderer.GetComponent<MeshFilter>();
            var mesh = filter ? filter.sharedMesh : null;
            if (!mesh || !mesh.isReadable) return true;
            if (!shapes.TryGetValue(mesh, out var shape))
            {
                shape = new Shape(mesh);
                shapes.Add(mesh, shape);
            }
            var part = renderer.transform;
            return shape.Hit(part.InverseTransformPoint(ray.origin), part.InverseTransformVector(ray.direction), distance);
        }

        sealed class Shape
        {
            readonly Vector3[] vertices;
            readonly int[] triangles;

            public Shape(Mesh mesh) { vertices = mesh.vertices; triangles = mesh.triangles; }

            public bool Hit(Vector3 origin, Vector3 direction, float distance)
            {
                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    Vector3 a = vertices[triangles[i]];
                    Vector3 ab = vertices[triangles[i + 1]] - a;
                    Vector3 ac = vertices[triangles[i + 2]] - a;
                    Vector3 normal = Vector3.Cross(direction, ac);
                    float determinant = Vector3.Dot(ab, normal);
                    if (Mathf.Abs(determinant) < .0000001f) continue;
                    float inverse = 1f / determinant;
                    Vector3 offset = origin - a;
                    float u = Vector3.Dot(offset, normal) * inverse;
                    if (u < 0 || u > 1) continue;
                    Vector3 cross = Vector3.Cross(offset, ab);
                    float v = Vector3.Dot(direction, cross) * inverse;
                    if (v < 0 || u + v > 1) continue;
                    float hit = Vector3.Dot(ac, cross) * inverse;
                    if (hit > .015f && hit < distance) return true;
                }
                return false;
            }
        }

        bool PlayerPart(Transform part)
        {
            if (loop)
            {
                if (loop.gloveModel && part.IsChildOf(loop.gloveModel) || loop.state.installed && loop.bridgeModel && part.IsChildOf(loop.bridgeModel)) return true;
                var wrist = loop.talk ? loop.talk.wrist : null;
                if (wrist && (wrist.housing && part.IsChildOf(wrist.housing) || wrist.Attached && wrist.board && part.IsChildOf(wrist.board))) return true;
            }
            var player = loop ? loop.player : null;
            return player && (player.xrOrigin && part.IsChildOf(player.xrOrigin.transform)
                || player.desktopRoot && part.IsChildOf(player.desktopRoot.transform)
                || player.avatarBody && part.IsChildOf(player.avatarBody)
                || player.avatarHead && part.IsChildOf(player.avatarHead));
        }

        bool Clear(Vector3 from, Vector3 to)
        {
            Vector3 offset = to - from;
            float distance = offset.magnitude;
            if (distance < .02f) return true;
            int count = Physics.RaycastNonAlloc(from, offset / distance, hits, distance - .015f, 1, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                var collider = hits[i].collider;
                if (!collider || collider.GetComponentInParent<NavPad>()) continue;
                if (PlayerPart(collider.transform)) continue;
                return false;
            }
            return true;
        }

        void Hide()
        {
            foreach (var arrow in arrows) if (arrow) arrow.enabled = false;
            if (label) label.gameObject.SetActive(false);
            foreach (var sign in signs) if (sign) sign.gameObject.SetActive(false);
            if (beam) beam.enabled = false;
            if (pulse) pulse.enabled = false;
            if (pin) pin.enabled = false;
            VisibleArrows = 0;
        }
    }
}
