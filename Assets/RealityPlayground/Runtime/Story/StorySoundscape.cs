using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR;

namespace RealityPlayground.Story
{

    public sealed class StorySoundscape : MonoBehaviour
    {
        public enum Cue { Awaken, Confirm, Route, Door, Peel, Rift, MirrorTouch, MirrorSnap, Wall, Chip, Unfilter, Secret, Ending }
        public RealityStoryDirector story;
        [SerializeField] AudioClip[] ambience;
        [SerializeField] AudioClip[] cues;
        [SerializeField] AudioClip peelLoop, mirrorLoop;
        [SerializeField, Range(0, 1)] float ambienceVolume = .22f;
        [SerializeField, Range(0, 1)] float feedbackVolume = .72f;
        readonly List<KeyValuePair<UnityEvent, UnityAction>> subscriptions = new List<KeyValuePair<UnityEvent, UnityAction>>();
        AudioSource[] beds, accents;
        AudioSource tension, liquid;
        StoryStage previousStage = (StoryStage)(-1);
        int nextAccent, previousMirrorHands;
        bool previousHeld, started, endingPlayed;
        float nextAlarm, previousPeel;
        public int CueCount { get; private set; }
        public float AmbienceLevel => beds == null ? 0 : beds[0].volume + beds[1].volume + beds[2].volume + beds[3].volume;
        public bool HasAudioAssets
        {
            get
            {
                if (ambience == null || ambience.Length != 4 || cues == null || cues.Length != 13 || !peelLoop || !mirrorLoop) return false;
                foreach (var clip in ambience) if (!clip) return false;
                foreach (var clip in cues) if (!clip) return false;
                return true;
            }
        }

        public static StorySoundscape Install(RealityStoryDirector owner)
        {
            if (!owner) return null;
            var soundscape = owner.GetComponentInChildren<StorySoundscape>(true);
            if (!soundscape)
            {
                var root = new GameObject("Soundscape");
                root.transform.SetParent(owner.transform, false);
                soundscape = root.AddComponent<StorySoundscape>();
            }
            soundscape.story = owner;
#if UNITY_EDITOR
            const string folder = "Assets/RealityPlayground/StoryAudio/";
            string[] beds = { "bedroom", "hypercity", "between", "unfiltered" };
            string[] names = { "awaken", "confirm", "route", "door", "peel", "rift", "mirror-touch", "mirror-snap", "wall", "chip", "unfilter", "secret", "ending" };
            soundscape.ambience = new AudioClip[beds.Length];
            soundscape.cues = new AudioClip[names.Length];
            for (int i = 0; i < beds.Length; i++) soundscape.ambience[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(folder + "ambience-" + beds[i] + ".wav");
            for (int i = 0; i < names.Length; i++) soundscape.cues[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(folder + "cue-" + names[i] + ".wav");
            soundscape.peelLoop = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(folder + "loop-reality-tension.wav");
            soundscape.mirrorLoop = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(folder + "loop-liquid-mirror.wav");
            if (owner.angel)
            {
                owner.angel.speechClips = new AudioClip[4];
                for (int i = 0; i < 4; i++) owner.angel.speechClips[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(folder + "angel-divine-" + (i + 1).ToString("00") + ".wav");
                UnityEditor.EditorUtility.SetDirty(owner.angel);
            }
            UnityEditor.EditorUtility.SetDirty(soundscape);
#endif
            return soundscape;
        }

        void Start()
        {
            if (!story) story = GetComponentInParent<RealityStoryDirector>();
            if (!story) { enabled = false; return; }
            beds = new AudioSource[4]; accents = new AudioSource[4];
            for (int i = 0; i < beds.Length; i++)
            {
                beds[i] = Source("Ambience layer " + i, false, 160);
                beds[i].loop = true; beds[i].clip = ambience != null && i < ambience.Length ? ambience[i] : null;
                beds[i].volume = 0;
                if (beds[i].clip) beds[i].Play();
            }
            for (int i = 0; i < accents.Length; i++) accents[i] = Source("Spatial interaction cue " + i, true, 48);
            tension = Source("Reality under tension", true, 90); tension.clip = peelLoop; tension.loop = true; tension.volume = 0;
            liquid = Source("Mirror Audio", true, 90); liquid.clip = mirrorLoop; liquid.loop = true; liquid.volume = 0;
            foreach (var marker in new[] { story.bedsideMarker, story.streetMarker, story.posterMarker, story.riftMarker, story.alleyMarker, story.mirrorMarker, story.chipMarker, story.endMarker })
            {
                if (!marker) continue;
                var captured = marker;
                Subscribe(marker.arrived, () =>
                {
                    Play(Cue.Route, captured.destination ? captured.destination.position + Vector3.up : captured.transform.position, .45f);
                    Haptic(.085f, .035f);
                });
            }
            started = true;
        }

        AudioSource Source(string label, bool spatial, int priority)
        {
            var go = new GameObject(ObjectNames.Short(label)); go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>(); source.playOnAwake = false;
            source.spatialBlend = spatial ? 1 : 0; source.dopplerLevel = 0;
            source.minDistance = 1.7f; source.maxDistance = 18;
            source.rolloffMode = AudioRolloffMode.Linear; source.priority = priority;
            return source;
        }

        void Subscribe(UnityEvent signal, UnityAction action)
        {
            signal.AddListener(action); subscriptions.Add(new KeyValuePair<UnityEvent, UnityAction>(signal, action));
        }

        public void PlaySecretAt(Vector3 position)
        {
            Play(Cue.Secret, position, .5f); Haptic(.1f, .045f);
        }

        public void Play(Cue cue, Vector3 position, float gain = 1)
        {
            if (accents == null || cues == null || (int)cue >= cues.Length || !cues[(int)cue]) return;
            AudioSource source = null;
            for (int i = 0; i < accents.Length; i++)
            {
                int index = (nextAccent + i) % accents.Length;
                if (!accents[index].isPlaying) { source = accents[index]; nextAccent = (index + 1) % accents.Length; break; }
            }
            if (!source) { source = accents[nextAccent]; nextAccent = (nextAccent + 1) % accents.Length; }
            source.Stop(); source.transform.position = position;
            source.pitch = 1; source.volume = feedbackVolume * Mathf.Clamp01(gain);
            source.clip = cues[(int)cue]; source.Play(); CueCount++;
        }

        Vector3 At(Transform target, float height = 1.2f) => target ? target.position + Vector3.up * height : (RealityPlayer.Head ? RealityPlayer.Head.position : transform.position);

        void Update()
        {
            if (!started || !story || !story.environment) return;
            if (story.Stage != previousStage) { OnStage(story.Stage); previousStage = story.Stage; }
            float reveal = story.environment.RevealAmount;
            bool bedroom = story.Stage <= StoryStage.Leaving;
            bool between = story.Stage == StoryStage.Encounter || story.Stage == StoryStage.Returning;
            float speechDuck = story.angel && story.angel.IsSpeaking ? .25f : 1;
            float speed = Time.deltaTime * .17f;
            Fade(beds[0], bedroom ? ambienceVolume * .78f : 0, speed);
            Fade(beds[1], !bedroom && !between ? ambienceVolume * (1 - reveal) : 0, speed);
            Fade(beds[2], between ? ambienceVolume * 1.12f * speechDuck : 0, Time.deltaTime * .28f);
            Fade(beds[3], !bedroom && !between ? ambienceVolume * (.09f + .85f * reveal) : 0, speed);

            if (story.Stage == StoryStage.Alarm && story.StageAge > nextAlarm && (!story.alarm || !story.alarm.OwnsAlarmAudio))
            {
                nextAlarm = story.StageAge + 5.2f;
                Play(Cue.Awaken, At(story.alarm ? story.alarm.transform : null, .45f), .3f);
            }
            float peel = story.poster && !story.poster.Torn ? story.poster.PeelProgress : 0;
            if (peel > .025f && previousPeel <= .025f) Play(Cue.Peel, At(story.poster.transform), .16f);
            previousPeel = peel;
            Loop(tension, peel > .02f ? .07f + peel * .17f : 0, At(story.poster ? story.poster.transform : null), .7f + peel * .75f);

            int hands = story.mirror ? story.mirror.AttachedHandCount : 0;
            Vector3 mirrorPosition = story.mirror ? story.mirror.SurfaceTransform.position : transform.position;
            if (hands > previousMirrorHands) { Play(Cue.MirrorTouch, mirrorPosition, .6f); Haptic(.13f, .045f); }
            if (hands < previousMirrorHands) { Play(Cue.MirrorSnap, mirrorPosition, .5f); Haptic(.19f, .06f); }
            previousMirrorHands = hands;
            float stretch = story.mirror ? Mathf.Clamp01(story.mirror.MaximumDisplacement / .65f) : 0;
            Loop(liquid, hands > 0 ? .07f + stretch * .17f : 0, mirrorPosition, .7f + stretch * .8f);

            bool held = story.chip && story.chip.IsHeld;
            if (held && !previousHeld)
            {
                Play(Cue.Chip, At(story.chip.HeldBy, 0), .52f); Haptic(.12f, .045f);
            }
            previousHeld = held;
            if (!endingPlayed && story.closingMessage && story.closingMessage.activeInHierarchy)
            {
                endingPlayed = true; Play(Cue.Ending, At(story.closingMessage.transform, 0), .62f);
            }
        }

        void OnStage(StoryStage stage)
        {
            switch (stage)
            {
                case StoryStage.Waking: if(!story.alarm || !story.alarm.OwnsAlarmAudio)Play(Cue.Awaken, At(story.alarm ? story.alarm.transform : null), .45f); break;
                case StoryStage.Alarm: nextAlarm = 2.5f; break;
                case StoryStage.Bedroom: Play(Cue.Confirm, At(story.alarm ? story.alarm.transform : null), .4f); Haptic(.08f, .035f); break;
                case StoryStage.Leaving: Play(Cue.Door, At(story.door ? story.door.transform : null), .6f); break;
                case StoryStage.RiftReady: Play(Cue.Rift, At(story.poster ? story.poster.transform : null), .8f); Haptic(.22f, .08f); break;
                case StoryStage.Encounter: Play(Cue.Rift, At(story.environment.liminalSpot), .7f); break;
                case StoryStage.Returning: Play(Cue.Secret, At(story.angel ? story.angel.transform : null), .4f); break;
                case StoryStage.Chip: Play(Cue.Wall, At(story.breach ? story.breach.transform : null), .8f); Haptic(.2f, .09f); break;
                case StoryStage.Revealing: Play(Cue.Unfilter, At(RealityPlayer.Head, 0), .82f); Haptic(.24f, .1f); break;
            }
        }

        static void Fade(AudioSource source, float volume, float step)
        {
            if (source) source.volume = Mathf.MoveTowards(source.volume, volume, step);
        }

        static void Loop(AudioSource source, float level, Vector3 position, float pitch)
        {
            if (!source) return;
            source.transform.position = position;
            source.volume = Mathf.MoveTowards(source.volume, level, Time.deltaTime * .8f);
            source.pitch = Mathf.Lerp(source.pitch, pitch, Time.deltaTime * 8);
            if (source.volume > .002f && source.clip && !source.isPlaying) source.Play();
            else if (source.volume <= .002f && source.isPlaying) source.Stop();
        }

        static void Haptic(float amplitude, float duration)
        {
            if (!RealityPlayer.IsXR) return;
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            var right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (left.isValid) left.SendHapticImpulse(0, amplitude, duration);
            if (right.isValid) right.SendHapticImpulse(0, amplitude, duration);
        }

        void OnDisable()
        {
            if (beds != null) foreach (var source in beds) if (source) source.Stop();
            if (accents != null) foreach (var source in accents) if (source) source.Stop();
            if (tension) tension.Stop(); if (liquid) liquid.Stop();
        }

        void OnEnable()
        {
            if (started && beds != null) foreach (var source in beds) if (source && source.clip) source.Play();
        }

        void OnDestroy()
        {
            foreach (var subscription in subscriptions) subscription.Key.RemoveListener(subscription.Value);
        }
    }
}
