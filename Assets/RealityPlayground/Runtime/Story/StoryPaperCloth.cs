using System;
using UnityEngine;

namespace RealityPlayground.Story
{

    public sealed class StoryPaperCloth
    {
        public const int Columns = 8, Rows = 12;
        public const float StepDuration = 1f / 90f;
        const float Skin = .008f;
        const int Count = (Columns + 1) * (Rows + 1);
        readonly Vector3[] nodes = new Vector3[Count], previous = new Vector3[Count], beforeStep = new Vector3[Count];
        readonly Vector3[] lastSweep = new Vector3[Count];
        readonly Vector3[] frameRight=new Vector3[Count],frameUp=new Vector3[Count],frameNormal=new Vector3[Count];
        readonly bool[] contacting = new bool[Count];
        readonly Link[] links = new Link[Count * 6];
        readonly RaycastHit[] hits = new RaycastHit[16];
        readonly Transform owner;
        readonly Vector3 fallbackRight, fallbackUp;
        int linkCount, collisionCursor;
        float age, floorQueryAge, quietAge;
        Vector3 floorPoint, floorNormal = Vector3.up;
        bool floorFound;
        bool framesValid;
        Collider wallCollider, floorCollider;
        Vector3 wallPoint, wallNormal;
        Bounds wallBounds;

        struct Link { public int a, b; public float initial, length, stiffness; }
        public struct Binding
        {
            public int cell;
            public float u, v;
            public Vector3 offset;
        }

        public bool HasSettled { get; private set; }
        public bool FoundLandingSurface => floorFound;
        public int ParticleCount => Count;
        public int StepCount { get; private set; }
        public int CollisionCastCount { get; private set; }
        public int MaximumCastsInOneStep { get; private set; }
        public bool HasCachedWall => wallCollider;
        public float MaximumFloorPenetration { get; private set; }
        public float MaximumStretchError { get; private set; }
        public float MaximumSpeed { get; private set; }
        public float RootMeanSquareSpeed { get; private set; }
        public float MaximumReleaseSpeed { get; private set; }
        public Vector3 Center { get; private set; }
        public Vector3 FrontNormal { get { Frame((Rows / 2) * (Columns + 1) + Columns / 2, out _, out _, out var normal); return -normal; } }

        public StoryPaperCloth(Transform source, Vector3[] released, Vector3[] rest, Vector3[] worldVelocities, Vector3 releaseVelocity, Transform poster)
        {
            owner = poster; fallbackRight = source.right; fallbackUp = source.up;
            int width = 1;
            while (width < rest.Length && Mathf.Abs(rest[width].y - rest[0].y) < .0001f) width++;
            int height = rest.Length / width;
            var relaxed = new Vector3[Count];
            Matrix4x4 world = source.localToWorldMatrix;
            for (int y = 0; y <= Rows; y++)
            for (int x = 0; x <= Columns; x++)
            {
                int i = y * (Columns + 1) + x;
                float u = (float)x / Columns, v = (float)y / Rows;
                nodes[i] = world.MultiplyPoint3x4(SampleGrid(released, width, height, u, v));
                relaxed[i] = world.MultiplyPoint3x4(SampleGrid(rest, width, height, u, v));
                Vector3 velocity = worldVelocities != null && worldVelocities.Length == released.Length
                    ? SampleGrid(worldVelocities, width, height, u, v) : releaseVelocity;
                velocity = Vector3.ClampMagnitude(velocity, 3.5f);
                MaximumReleaseSpeed = Mathf.Max(MaximumReleaseSpeed, velocity.magnitude);
                previous[i] = nodes[i] - velocity * StepDuration;
                lastSweep[i] = nodes[i];
            }
            for (int y = 0; y <= Rows; y++)
            for (int x = 0; x <= Columns; x++)
            {
                int i = y * (Columns + 1) + x;
                if (x < Columns) AddLink(i, i + 1, relaxed, .92f);
                if (y < Rows) AddLink(i, i + Columns + 1, relaxed, .92f);
                if (x < Columns && y < Rows)
                {
                    AddLink(i, i + Columns + 2, relaxed, .58f);
                    AddLink(i + 1, i + Columns + 1, relaxed, .58f);
                }
                if (x + 2 <= Columns) AddLink(i, i + 2, relaxed, .12f);
                if (y + 2 <= Rows) AddLink(i, i + (Columns + 1) * 2, relaxed, .12f);
            }
            RefreshCenter(); FindFloor(); FindWall(source);
        }

        static Vector3 SampleGrid(Vector3[] values, int width, int height, float u, float v)
        {
            float x = Mathf.Clamp01(u) * (width - 1), y = Mathf.Clamp01(v) * (height - 1);
            int ix = Mathf.Min(width - 2, Mathf.FloorToInt(x)), iy = Mathf.Min(height - 2, Mathf.FloorToInt(y));
            ix = Mathf.Max(0, ix); iy = Mathf.Max(0, iy);
            int a = iy * width + ix, b = Mathf.Min(a + width, values.Length - 1);
            return Vector3.Lerp(Vector3.Lerp(values[a], values[Mathf.Min(a + 1, values.Length - 1)], x - ix),
                Vector3.Lerp(values[b], values[Mathf.Min(b + 1, values.Length - 1)], x - ix), y - iy);
        }

        void AddLink(int a, int b, Vector3[] relaxed, float stiffness)
        {
            links[linkCount++] = new Link { a = a, b = b, initial = Vector3.Distance(nodes[a], nodes[b]),
                length = Vector3.Distance(relaxed[a], relaxed[b]), stiffness = stiffness };
        }

        void RefreshCenter()
        {
            Vector3 total = Vector3.zero;
            for (int i = 0; i < Count; i++) total += nodes[i];
            Center = total / Count;
        }

        void FindFloor()
        {
            int count = Physics.RaycastNonAlloc(Center + Vector3.up * .8f, Vector3.down, hits, 40,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (!hit.collider || hit.normal.y < .55f || hit.distance >= nearest || Ignore(hit.collider)) continue;
                nearest = hit.distance; floorPoint = hit.point; floorNormal = hit.normal; floorFound = true; floorCollider=hit.collider;
            }
        }

        bool Ignore(Collider collider) => owner && collider.transform.IsChildOf(owner);

        void FindWall(Transform source)
        {
            int count=Physics.RaycastNonAlloc(source.position-source.forward*.55f,source.forward,hits,1.6f,
                Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            float nearest=float.PositiveInfinity;
            for(int i=0;i<count;i++)
            {
                var hit=hits[i];
                if(!hit.collider || Ignore(hit.collider) || hit.distance>=nearest || Mathf.Abs(hit.normal.y)>.55f)continue;
                nearest=hit.distance;wallCollider=hit.collider;wallPoint=hit.point;wallNormal=hit.normal;
                wallBounds=hit.collider.bounds;wallBounds.Expand(.05f);
            }
        }

        void ProjectWall(int index)
        {
            if(!wallCollider)return;
            float depth=Vector3.Dot(nodes[index]-wallPoint,wallNormal);
            if(depth>=Skin)return;
            Vector3 projected=nodes[index]+wallNormal*(Skin-depth);
            if(wallBounds.Contains(projected))nodes[index]=projected;
        }

        public Binding Bind(float u, float v, Vector3 exactWorldPosition)
        {
            float x = Mathf.Clamp01(u) * Columns, y = Mathf.Clamp01(v) * Rows;
            int ix = Mathf.Min(Columns - 1, Mathf.FloorToInt(x)), iy = Mathf.Min(Rows - 1, Mathf.FloorToInt(y));
            var binding = new Binding { cell = iy * (Columns + 1) + ix, u = x - ix, v = y - iy };
            Frame(binding.cell, out var right, out var up, out var normal);
            Vector3 difference = exactWorldPosition - Surface(binding);
            binding.offset = new Vector3(Vector3.Dot(difference, right), Vector3.Dot(difference, up), Vector3.Dot(difference, normal));
            return binding;
        }

        public Binding BindNearest(Vector3 point)
        {
            float best = float.PositiveInfinity;
            Binding result = default;
            for (int y = 0; y < Rows; y++)
            for (int x = 0; x < Columns; x++)
            {
                int a = y * (Columns + 1) + x;
                Vector3 horizontal = nodes[a + 1] - nodes[a], vertical = nodes[a + Columns + 1] - nodes[a];
                Vector3 delta = point - nodes[a];
                float xx = Vector3.Dot(horizontal, horizontal), yy = Vector3.Dot(vertical, vertical), xy = Vector3.Dot(horizontal, vertical);
                float denominator = Mathf.Max(.0000001f, xx * yy - xy * xy);
                float dx = Vector3.Dot(delta, horizontal), dy = Vector3.Dot(delta, vertical);
                var binding = new Binding { cell = a, u = Mathf.Clamp01((dx * yy - dy * xy) / denominator), v = Mathf.Clamp01((dy * xx - dx * xy) / denominator) };
                float distance = (Surface(binding) - point).sqrMagnitude;
                if (distance >= best) continue;
                best = distance; result = binding;
            }
            Frame(result.cell, out var right, out var up, out var normal);
            Vector3 offset = point - Surface(result);
            result.offset = new Vector3(Vector3.Dot(offset, right), Vector3.Dot(offset, up), Vector3.Dot(offset, normal));
            return result;
        }

        Vector3 Surface(Binding binding)
        {
            int a = binding.cell, b = a + Columns + 1;
            return Vector3.Lerp(Vector3.Lerp(nodes[a], nodes[a + 1], binding.u), Vector3.Lerp(nodes[b], nodes[b + 1], binding.u), binding.v);
        }

        void Frame(int cell, out Vector3 right, out Vector3 up, out Vector3 normal)
        {
            if(!framesValid)
            {
                for(int y=0;y<Rows;y++)for(int x=0;x<Columns;x++)
                {
                    int index=y*(Columns+1)+x;
                    CalculateFrame(index,out frameRight[index],out frameUp[index],out frameNormal[index]);
                }
                framesValid=true;
            }
            right=frameRight[cell];up=frameUp[cell];normal=frameNormal[cell];
        }

        void CalculateFrame(int cell, out Vector3 right, out Vector3 up, out Vector3 normal)
        {
            int below = cell + Columns + 1;
            right = (nodes[cell + 1] - nodes[cell] + nodes[below + 1] - nodes[below]).normalized;
            if (right.sqrMagnitude < .01f) right = fallbackRight;
            up = nodes[below] - nodes[cell] + nodes[below + 1] - nodes[cell + 1];
            up = Vector3.ProjectOnPlane(up, right).normalized;
            if (up.sqrMagnitude < .01f) up = fallbackUp;
            normal = Vector3.Cross(right, up).normalized;
        }

        public Vector3 EvaluateFrontNormal(Binding binding)
        {
            Frame(binding.cell,out _,out _,out var normal);return -normal;
        }

        public Vector3 Evaluate(Binding binding, float residual = 1, bool printed = false)
        {
            Frame(binding.cell, out var right, out var up, out var normal);
            Vector3 offset = binding.offset * residual;

            if (printed) offset.z = Mathf.Lerp(binding.offset.z, -.012f, Mathf.SmoothStep(0, 1, age / 1.1f));
            Vector3 world = Surface(binding) + right * offset.x + up * offset.y + normal * offset.z;
            if (!printed && age > 0 && floorFound)
            {
                float depth = Vector3.Dot(world - floorPoint, floorNormal);
                if (depth < Skin) world += floorNormal * (Skin - depth);
            }
            if(!printed && age>0 && wallCollider)
            {
                float depth=Vector3.Dot(world-wallPoint,wallNormal);
                if(depth<Skin)
                {
                    Vector3 projected=world+wallNormal*(Skin-depth);
                    if(wallBounds.Contains(projected))world=projected;
                }
            }
            return world;
        }

        public void Step()
        {
            if (HasSettled) return;
            framesValid=false;
            age += StepDuration; StepCount++;
            floorQueryAge -= StepDuration;
            if (floorQueryAge <= 0) { RefreshCenter(); FindFloor(); floorQueryAge = .22f; }
            float relaxation = Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / .9f));
            int oldContacts = 0;
            for (int i = 0; i < Count; i++) if (contacting[i]) oldContacts++;
            float freeAir = 1 - Mathf.Clamp01(oldContacts / (Count * .28f));
            for (int i = 0; i < Count; i++)
            {
                Vector3 point = nodes[i]; beforeStep[i] = point;
                Vector3 travel = Vector3.ClampMagnitude(point-previous[i],3.5f*StepDuration) * (contacting[i] ? .48f : .986f);
                previous[i] = point;
                float flutter = Mathf.Sin(age * 7.2f + i * .59f) * .58f * freeAir;
                Vector3 wind = Vector3.Cross(fallbackRight, fallbackUp) * flutter;
                nodes[i] = point + travel + (Vector3.down * 7.2f + wind) * (StepDuration * StepDuration);
                contacting[i] = false;
            }
            for (int pass = 0; pass < 5; pass++)
            {
                for (int i = 0; i < linkCount; i++)
                {
                    var link = links[i]; Vector3 delta = nodes[link.b] - nodes[link.a];
                    float length = delta.magnitude;
                    if (length < .00001f) continue;
                    float target = Mathf.Lerp(link.initial, link.length, relaxation);
                    Vector3 correction = delta * ((length - target) / length * link.stiffness * .5f);
                    nodes[link.a] += correction; nodes[link.b] -= correction;
                }
                for (int i = 0; i < Count; i++){if(floorFound)ProjectFloor(i);ProjectWall(i);}
            }
            int casts=0;
            for (int probe = 0; probe < 8; probe++)
            {

                int i=collisionCursor++%Count;
                Vector3 start=lastSweep[i];
                Vector3 delta = nodes[i] - start; float distance = delta.magnitude;
                if (distance > .0015f && (!contacting[i] || Vector3.ProjectOnPlane(delta,floorNormal).sqrMagnitude>.003f*.003f))
                {
                    int count = Physics.SphereCastNonAlloc(start, Skin, delta / distance, hits, distance,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    casts++;CollisionCastCount++;
                    float nearest = distance; Vector3 normal = Vector3.zero,contactPoint=Vector3.zero;
                    for (int j = 0; j < count; j++)
                    {
                        if (!hits[j].collider || hits[j].collider==wallCollider || hits[j].collider==floorCollider || Ignore(hits[j].collider) || hits[j].distance >= nearest) continue;
                        if(floorFound && Vector3.Dot(hits[j].normal,floorNormal)>.98f && Mathf.Abs(Vector3.Dot(hits[j].point-floorPoint,floorNormal))<.025f)continue;

                        if(hits[j].distance<=.0001f)continue;
                        nearest = hits[j].distance; normal = hits[j].normal;contactPoint=hits[j].point;
                    }
                    if (normal.sqrMagnitude > .1f)
                    {
                        float depth=Vector3.Dot(nodes[i]-contactPoint,normal);
                        if(depth<Skin)nodes[i]+=normal*(Skin-depth);
                        previous[i] = nodes[i] - Vector3.ClampMagnitude(Vector3.ProjectOnPlane(nodes[i] - beforeStep[i], normal),3.5f*StepDuration) * .45f;
                        contacting[i] = true;
                    }
                }
                lastSweep[i]=nodes[i];
            }
            MaximumCastsInOneStep=Mathf.Max(MaximumCastsInOneStep,casts);
            MaximumFloorPenetration = 0; MaximumSpeed = 0; int contacts = 0;float summedSpeedSquared=0;
            for (int i = 0; i < Count; i++)
            {
                bool floorContact=false;
                if (floorFound)
                {
                    ProjectFloor(i);
                    float height = Vector3.Dot(nodes[i] - floorPoint, floorNormal);
                    MaximumFloorPenetration = Mathf.Max(MaximumFloorPenetration, -height);
                    floorContact=height <= Skin + .006f;
                }

                contacting[i]=floorContact;
                Vector3 motion=Vector3.ClampMagnitude(nodes[i]-beforeStep[i],3.5f*StepDuration);
                if (floorContact)
                {
                    contacts++;
                    motion=Vector3.ProjectOnPlane(motion,floorNormal)*.28f;
                }
                previous[i]=nodes[i]-motion;
                float speed=(nodes[i]-beforeStep[i]).magnitude/StepDuration;
                MaximumSpeed = Mathf.Max(MaximumSpeed, speed);summedSpeedSquared+=speed*speed;
            }
            MaximumStretchError = 0;
            for (int i = 0; i < linkCount; i++)
            {
                var link = links[i]; if (link.stiffness < .9f) continue;
                MaximumStretchError = Mathf.Max(MaximumStretchError, Mathf.Abs(Vector3.Distance(nodes[link.a], nodes[link.b]) / Mathf.Max(.001f, link.length) - 1));
            }
            RefreshCenter();
            RootMeanSquareSpeed=Mathf.Sqrt(summedSpeedSquared/Count);

            float centreHeight=floorFound?Vector3.Dot(Center-floorPoint,floorNormal):float.PositiveInfinity;
            bool supported=floorFound && contacts>=Count*.1f && centreHeight<.16f;
            bool quiet=supported && RootMeanSquareSpeed<(age>3.5f?.08f:.055f) && MaximumSpeed<(age>3.5f?.30f:.18f);
            quietAge = quiet ? quietAge + StepDuration : 0;
            if (age > 1.25f && quietAge > .35f) HasSettled = true;
        }

        void ProjectFloor(int index)
        {
            float height = Vector3.Dot(nodes[index] - floorPoint, floorNormal);
            if (height >= Skin) return;
            nodes[index] += floorNormal * (Skin - height); contacting[index] = true;
        }
    }
}
