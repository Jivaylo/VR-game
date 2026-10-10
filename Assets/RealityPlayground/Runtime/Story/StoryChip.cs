using UnityEngine;
using UnityEngine.Events;

namespace RealityPlayground.Story
{

    public sealed class StoryChip : PlaygroundTarget
    {
        public bool interactionEnabled;
        public UnityEvent inserted = new UnityEvent();
        public bool Inserted { get; private set; }
        public bool IsHeld => heldBy;
        public Transform HeldBy => heldBy;
        public Transform socketVisual;
        public override bool UsesTrackedHandForGrab => true;
        [SerializeField] Transform chip, halo;
        [SerializeField] Collider chipCollider;
        [SerializeField] TextMesh instruction;
        [SerializeField] Material socketMaterial;
        Transform heldBy, socketArm, explicitSocketArm;
        Vector3 home = new Vector3(0, 1.45f, 0);
        Quaternion homeRotation;
        bool returning;
        float age, insertedAge;

        public static GameObject Create(Transform parent)
        {
            var root = new GameObject("Chip"); root.transform.SetParent(parent, false);
            var target = root.AddComponent<StoryChip>();
            var dark = GreyboxUtil.Material("Chip Ceramic", new Color(.017f, .025f, .035f));
            var gold = GreyboxUtil.Material("Chip Contacts", new Color(1, .56f, .12f), 2);
            target.socketMaterial = StoryVignetteGeometry.Hologram("Chip Circuits", new Color(.22f, 1, .75f, .82f), 3.2f);
            target.chip = StoryVignetteGeometry.Group("Held decoder", root.transform, target.home);
            GreyboxUtil.Primitive("Ceramic chip", PrimitiveType.Cube, target.chip, Vector3.zero, new Vector3(.105f, .14f, .021f), dark, false);
            GreyboxUtil.Primitive("Unfilter core", PrimitiveType.Cube, target.chip, new Vector3(0, .007f, -.014f), new Vector3(.047f, .052f, .009f), target.socketMaterial, false);
            for (int i = 0; i < 6; i++)
            {
                GreyboxUtil.Primitive("Decoder contact " + i, PrimitiveType.Cube, target.chip, new Vector3((i - 2.5f) * .015f, -.076f, 0), new Vector3(.008f, .027f, .017f), gold, false);
                float x = (i % 2 == 0 ? -1 : 1) * .043f;
                StoryVignetteGeometry.Rod("Decoder trace " + i, target.chip, new Vector3(x, -.041f + i * .015f, -.013f), new Vector3(0, -.041f + i * .015f, -.013f), .003f, target.socketMaterial);
            }
            var box = target.chip.gameObject.AddComponent<BoxCollider>(); box.size = new Vector3(.16f, .2f, .09f); box.isTrigger = true; target.chipCollider = box;
            target.halo = StoryVignetteGeometry.Ring("Decoder discovery halo", root.transform, .15f, .008f, target.socketMaterial);
            target.halo.localPosition = target.home;
            target.instruction = GreyboxUtil.Label("TAKE IT.\nPLACE IT IN YOUR OTHER ARM.", root.transform, new Vector3(0, 1.04f, -.025f), .075f, new Color(.59f, 1, .8f));
            return root;
        }

        void Awake() { if (chip) { home = chip.localPosition; homeRotation = chip.localRotation; } }
        public void AttachArmSocket(Transform hand)
        {
            explicitSocketArm = hand;
            if (heldBy) SetSocketArm(ChooseSocketArm(heldBy));
        }
        Transform ChooseSocketArm(Transform hand)
        {
            if (hand == RealityPlayer.LeftHand) return RealityPlayer.RightHand;
            if (hand == RealityPlayer.RightHand) return RealityPlayer.LeftHand;
            if (explicitSocketArm && explicitSocketArm != hand) return explicitSocketArm;
            return RealityPlayer.LeftHand;
        }
        void SetSocketArm(Transform arm)
        {
            socketArm = arm;
            if (!arm) return;
            if (!socketVisual)
            {
                socketVisual = StoryVignetteGeometry.Group("Arm Socket", arm, Vector3.zero);
                var rim = StoryVignetteGeometry.Ring("Arm port rim", socketVisual, .077f, .009f, socketMaterial, 40);
                rim.localScale = new Vector3(1, .65f, 1);
                GreyboxUtil.Primitive("Arm port slot", PrimitiveType.Cube, socketVisual, new Vector3(0, 0, .012f), new Vector3(.075f, .006f, .035f), socketMaterial, false);
                var arrow = new Vector3[] { new Vector3(-.024f, .075f, 0), new Vector3(0, .046f, 0), new Vector3(.024f, .075f, 0) };
                GreyboxUtil.Line("Insert direction", socketVisual, arrow, socketMaterial, .005f);
            }
            socketVisual.SetParent(arm, false);

            socketVisual.localPosition = new Vector3(0, -.025f, -.16f);
            socketVisual.localRotation = Quaternion.Euler(55, 0, 0);
            socketVisual.gameObject.SetActive(true);
        }
        public override void BeginInteraction(Transform hand)
        {
            if (!interactionEnabled || Inserted || !hand || heldBy) return;
            heldBy = hand; returning = false;
            if (halo) halo.gameObject.SetActive(false);
            if (instruction) instruction.gameObject.SetActive(false);
            SetSocketArm(ChooseSocketArm(hand));
            MoveWithHand();
        }
        public override void UpdateInteraction(Transform hand)
        {
            if (heldBy != hand || !hand || Inserted) return;
            MoveWithHand();
            TryInsertAt(socketArm);
        }
        public override void EndInteraction(Transform hand)
        {
            if (heldBy != hand || Inserted) return;
            heldBy = null; returning = true;
            if (socketVisual) socketVisual.gameObject.SetActive(false);
        }
        void Update()
        {
            age += Time.deltaTime;
            if (Inserted)
            {
                insertedAge += Time.deltaTime;
                if (chip && socketVisual)
                {
                    chip.position = socketVisual.position; chip.rotation = socketVisual.rotation;
                    chip.localScale = Vector3.one * (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(insertedAge / .8f)));
                }
                if (socketVisual && insertedAge > 1.2f) socketVisual.gameObject.SetActive(false);
                return;
            }
            if (heldBy)
            {
                if (!interactionEnabled || !heldBy.gameObject.activeInHierarchy) { EndInteraction(heldBy); return; }
                var expected = ChooseSocketArm(heldBy);
                if (expected && expected != socketArm) SetSocketArm(expected);
                MoveWithHand(); TryInsertAt(socketArm);
                return;
            }
            if (!chip) return;
            if (returning)
            {
                chip.localPosition = Vector3.MoveTowards(chip.localPosition, home, Time.deltaTime * 3);
                chip.localRotation = Quaternion.RotateTowards(chip.localRotation, homeRotation, Time.deltaTime * 250);
                if (Vector3.Distance(chip.localPosition, home) < .005f)
                {
                    returning = false;
                    if (halo) halo.gameObject.SetActive(true);
                    if (instruction) instruction.gameObject.SetActive(true);
                }
            }
            else
            {
                chip.localPosition = home + Vector3.up * Mathf.Sin(age * 2) * .018f;
                chip.localRotation = Quaternion.Euler(0, Mathf.Sin(age * .7f) * 20, Mathf.Sin(age * 1.3f) * 4);
            }
            if (halo) halo.localRotation = Quaternion.Euler(0, age * 23, age * 13);
        }
        void MoveWithHand()
        {
            if (!heldBy || !chip) return;
            chip.position = heldBy.TransformPoint(new Vector3(0, 0, .045f));
            chip.rotation = heldBy.rotation;
        }
        public void TryInsertAt(Transform arm)
        {
            if (!interactionEnabled || Inserted || !heldBy || !chip || !arm || arm == heldBy || arm != socketArm || !socketVisual) return;
            if (Vector3.Distance(chip.position, socketVisual.position) > .115f) return;
            Inserted = true; insertedAge = 0; heldBy = null;
            if (chipCollider) chipCollider.enabled = false;
            inserted.Invoke();
        }
        void OnDisable()
        {
            if (!Inserted && heldBy) { heldBy = null; returning = true; }
            if (socketVisual) socketVisual.gameObject.SetActive(false);
        }
        void OnDestroy() { if (socketVisual) Destroy(socketVisual.gameObject); }
    }
}
