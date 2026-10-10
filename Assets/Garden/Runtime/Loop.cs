using System;
using System.Collections;
using System.IO;
using RealityPlayground;
using RealityPlayground.Story;
using TMPro;
using UnityEngine;

namespace Garden
{
    public enum DayPhase { Morning, Breakfast, Commute, Work, Return, Dinner, Evening, Local, Outside }

    [DefaultExecutionOrder(-4000)]
    public sealed class Loop : MonoBehaviour
    {
        public State state = new State();
        public static Loop Instance { get; private set; }
        public RealityPlayer player;
        public Talk talk;
        public Briefing briefing;
        public StoryTransition transition;
        public Transform home, checkpoint, outside, angelRoom, alcove, threshold;
        public Transform breakfast, factory, concourse, dinnerPoint, bed;
        public GameObject overlays, localChoice, gardenChoice, gardenView, endCard, morning;
        public GameObject alarmPrefab;
        public Transform alarmPoint;
        public TMP_Text clock, evidence, receiver;
        public AudioSource cue, ambience;
        public AudioClip clickSound, wind, city;
        public Transform gloveModel, bridgeModel;
        public bool saveEnabled = true;
        public event Action Changed;
        public event Action WrapperOpened;
        public event Action Watered;
        public event Action Washed;
        public bool Busy { get; private set; }
        public bool InAngel { get; private set; }
        StoryAlarm alarm;
        string SavePath => Path.Combine(Application.persistentDataPath, "Garden.json");
        Vector3 returnPoint;
        Vector3 morningPosition, morningScale;
        Quaternion morningRotation;
        int question;
        float nextPlaceCheck, nextRefusal;
        bool placed;
        bool newsAuto;
        public string AngelQuestion => (question % 4) switch
        {
            0 => "Who are you?",
            1 => "Are you another system?",
            2 => "Can I trust you?",
            _ => "Is it safe outside?"
        };
        public DayPhase Phase => state.outside ? DayPhase.Outside : state.local ? DayPhase.Local :
            state.dinnerEaten ? DayPhase.Evening : state.work && state.returnedHome ? DayPhase.Dinner :
            state.work ? DayPhase.Return : state.workStarted ? DayPhase.Work : state.meal ? DayPhase.Commute :
            state.leftHome || state.alarm ? DayPhase.Breakfast : DayPhase.Morning;
        public string Objective => Phase switch
        {
            DayPhase.Morning => "Wake up",
            DayPhase.Breakfast => "Breakfast at Commons",
            DayPhase.Commute => "Take the metro to Assembly",
            DayPhase.Work => "Classify the batch",
            DayPhase.Return => "Return through the arcade",
            DayPhase.Dinner => state.dinnerReady ? "Your meal has arrived" : state.dinner ? "Delivery approaching" : "Order your evening meal",
            DayPhase.Evening => "Tomorrow is prepared",
            DayPhase.Local => "Find the service passage",
            _ => "Take your time"
        };
        void Awake()
        {
            Instance = this;
            if (morning) { morningPosition=morning.transform.localPosition; morningScale=morning.transform.localScale; morningRotation=morning.transform.localRotation; }
            if (saveEnabled) Load();
            Normalize();
            if (localChoice) localChoice.SetActive(false);
            if (gardenChoice) gardenChoice.SetActive(false);
            if (gardenView) gardenView.SetActive(false);
            if (endCard) endCard.SetActive(false);
        }
        IEnumerator Start()
        {
            yield return null;
            Apply();
            if (!state.outside) ResetAlarm();
            var point = ResumePoint();
            if (player && point) RealityPlayer.TeleportTo(point.position);
            placed = true;
            if (state.outside) Announce("NOOR", "There's water here. Take your time.");
            else if (state.local) Announce("SERVICE", "Local maintenance mode. Network connection unavailable.");
            else if (!briefing) Announce("CIVIC", "Good morning. Your day is ready.");
        }
        public Transform ResumePoint()
        {
            if (state.outside && outside) return outside;
            if (state.local && checkpoint) return checkpoint;
            if (state.workStarted && !state.returnedHome && factory) return factory;
            if (state.commute && !state.workStarted)
            {
                var train = briefing && briefing.train ? briefing.train : GetComponentInChildren<Train>(true);
                if (train && train.work) return train.work;
                if (factory) return factory;
            }
            if (state.leftHome && !state.work && breakfast) return breakfast;
            return home;
        }
        void LateUpdate()
        {
            if (placed && Time.unscaledTime >= nextPlaceCheck && !Busy && !InAngel)
            {
                nextPlaceCheck = Time.unscaledTime + .35f;
                CheckPlace();
            }
            var hand = RealityPlayer.LeftHand;
            if (hand && gloveModel)
            {
                gloveModel.gameObject.SetActive(state.glove);
                gloveModel.SetPositionAndRotation(hand.position + hand.rotation * new Vector3(0,.02f,-.07f), hand.rotation);
            }
            hand = RealityPlayer.RightHand;
            if (hand && bridgeModel)
            {
                bridgeModel.gameObject.SetActive(state.installed);
                bridgeModel.SetPositionAndRotation(hand.position + hand.rotation * new Vector3(0,.03f,-.10f), hand.rotation);
            }
        }
        public void Notify() { StopNews(); Apply(); Save(); Changed?.Invoke(); }
        public bool Near(Transform point, float radius = 4f)
        {
            if (!point || !RealityPlayer.Head) return false;
            var offset = RealityPlayer.FeetPosition - point.position;
            return Mathf.Abs(offset.y) < 1.6f && new Vector2(offset.x, offset.z).sqrMagnitude <= radius * radius;
        }
        void CheckPlace()
        {
            if (!RealityPlayer.Head) return;
            if (state.local)
            {
                if (state.gateOpen && !state.outside && threshold && outside)
                {
                    var direction = outside.position - threshold.position;
                    direction.y = 0;
                    if (direction.sqrMagnitude > .01f && Vector3.Dot(RealityPlayer.FeetPosition - threshold.position, direction.normalized) >= -.1f
                        && Near(outside, direction.magnitude + .5f)) ReachOutside();
                }
                return;
            }
            if (!home) return;
            if (!state.leftHome && !Near(home, 4.2f))
            {
                StopNews();
                state.leftHome = true;
                if (alarm) { alarm.gameObject.SetActive(false); Destroy(alarm.gameObject); }
                if (morning) morning.SetActive(false);
                Notify();
                if (!state.meal && !briefing) Announce("CIVIC", "Breakfast is ready at Commons. Follow the lighted route.");
            }
            if (state.work && !state.returnedHome && Near(home, 3.1f))
            {
                state.returnedHome = true;
                Advance(1900);
                if (!briefing) Announce("CIVIC", "Welcome home. Order your evening meal at the receiver.");
            }
            if (state.work && !state.returnArcade && Near(concourse, 7)) { state.returnArcade = true; Notify(); }
            if (state.work && !state.returnCommons && Near(breakfast, 5)) { state.returnCommons = true; Notify(); }
        }
        public bool CanUse(string action)
        {
            if (Busy || Session.IsOpen) return false;
            switch (action)
            {
                case "Snooze": case "Swat": return !state.local && !state.alarm && Near(home);
                case "Wash": case "News": return !state.local && Near(home);
                case "Meal": return !state.local && !state.meal && Near(breakfast, 5);
                case "BeginWork": return !state.local && state.meal && !state.work && Near(factory, 6);
                case "Dinner": return !state.local && state.work && state.returnedHome && !state.dinner && Near(home);
                case "EatDinner": return !state.local && state.dinner && state.dinnerReady && !state.dinnerEaten && Near(home);
                case "Sleep": case "SleepOffer": case "RestNight": return !state.local && state.work && state.dinnerEaten && Near(home);
                case "Glove": return state.casing && !state.glove && Near(factory, 6);
                case "Mara": case "Cup": return Near(breakfast, 5);
                case "Peel": return Near(breakfast, 5);
                case "OfferLocal": case "ConfirmLocal": case "Disconnect": return state.installed && !state.local && !InAngel;
                case "EnterAngel": return state.poster && !state.local && !InAngel && Near(alcove, 5);
                case "LeaveAngel": case "AskAngel": return InAngel;
                case "Noor": case "Water": case "Rest": return state.outside && Near(outside, 12);
                case "ReachOutside": return state.local && state.gateOpen;
                case "RequestDeparture": return state.local;
                case "Evidence": return state.glove;
                default: return true;
            }
        }
        public bool TryUse(string action)
        {
            if (CanUse(action)) return true;
            if (Busy || Session.IsOpen || Time.unscaledTime < nextRefusal) return false;
            nextRefusal = Time.unscaledTime + 5;
            if (action == "Dinner" || action == "EatDinner" || action == "Sleep" || action == "RestNight" || action == "SleepOffer")
                Announce("CIVIC", state.local ? "Scheduled services are unavailable." : !state.work ? state.meal ? Briefing.WorkRoute : Briefing.BeforeWork : !state.returnedHome ? Briefing.ShiftDone : !state.dinnerEaten ? Briefing.NeedDinner : Briefing.Evening);
            else if (action == "BeginWork") Announce("CIVIC", Briefing.BeforeWork);
            return false;
        }
        public void BeginCommute()
        {
            if (state.local || !state.meal || state.workStarted || state.commute) return;
            state.commute = true;
            Advance(800);
        }
        public void BeginWork()
        {
            if (state.workStarted || state.local || !state.meal || state.work || !Near(factory, 6)) return;
            state.commute = state.workStarted = true;
            Advance(900);
        }
        public void CompleteWork()
        {
            if (state.local || !state.workStarted || state.work || !Near(factory, 6)) return;
            state.work = true;
            Advance(1700);
        }
        public void DinnerArrived()
        {
            if (!state.dinner || state.dinnerReady) return;
            state.dinnerReady = true;
            Notify();
        }
        public void EatDinner()
        {
            if (!TryUse("EatDinner")) return;
            state.dinnerEaten = true;
            Advance(2200);
            Cue(1.1f);
            if (!briefing) Announce("CIVIC", Briefing.Evening);
        }
        void Apply()
        {
            if (clock) clock.text = (state.hour / 100).ToString("00") + ":" + (state.hour % 100).ToString("00") + "\n" + new DateTime(2056,10,7).AddDays(state.day-1).ToString("dd MMM yyyy").ToUpperInvariant();
            if (overlays) overlays.SetActive(!state.local);
            if (morning) morning.SetActive(!state.local && !state.leftHome && (!state.alarm || alarm && !alarm.MorningDismissed));
            if (endCard) endCard.SetActive(state.ended);
            if (evidence) evidence.text = state.local ? "LOCAL" : state.calibrated ? "TRACE READY" : "CALIBRATE";
            if (ambience && ambience.clip != (state.local ? wind : city)) { ambience.clip = state.local ? wind : city; ambience.Play(); }
        }
        public void Advance(int hour) { state.hour = Mathf.Max(state.hour, hour); Notify(); }
        public void Announce(string speaker, string text)
        {
            if (briefing && briefing.isActiveAndEnabled && speaker == "CIVIC" && briefing.Handles(text)) return;
            if (talk) talk.Say(speaker, text);
        }
        public void Cue(float pitch = 1) { if (cue && clickSound) { cue.pitch = pitch; cue.PlayOneShot(clickSound, .5f); } }
        public void Snooze() { if (!CanUse("Snooze")) return; state.alarm = true; Notify(); Cue(1.3f); StartCoroutine(MorningNews()); }
        public void Swat() { if (alarm) alarm.ActivateWithSwipe(new Vector3(2,1,3)); else Snooze(); }
        public void Wash() { if (!CanUse("Wash")) return; StopNews(); bool first = !state.wash; state.wash = true; Washed?.Invoke(); Notify(); Cue(); if (first) Announce("CIVIC", "Your appearance is within your preferred range."); }
        public void News()
        {
            if (!CanUse("News")) return;
            if (talk && talk.Speaker == "NEWS" && talk.Speaking) { talk.CancelSpeaker("NEWS"); newsAuto = false; return; }
            state.news = true;
            Notify();
            Announce("NEWS", state.day == 1 ? "More room to be yourself. This week, residents can choose from twelve new horizon experiences. Perimeter access remains unnecessary while exterior services are unavailable." : "More room to be yourself. This week, residents can choose from twelve new horizon experiences.");
        }
        IEnumerator MorningNews()
        {
            int day = state.day;
            float until = Time.unscaledTime + 25;
            yield return new WaitForSeconds(.5f);
            while (Time.unscaledTime < until && day == state.day && !state.news && !state.wash && !state.local && !state.leftHome && Near(home, 4.2f))
            {
                if (!talk || !talk.Waiting)
                {
                    News();
                    newsAuto = true;
                    yield break;
                }
                yield return null;
            }
        }
        void StopNews()
        {
            if (!newsAuto) return;
            if (talk) talk.CancelSpeaker("NEWS");
            newsAuto = false;
        }
        public void Meal() { if (!TryUse("Meal")) return; state.meal = true; state.leftHome = true; Advance(730); Cue(1.2f); if (!briefing) Announce("CIVIC", Briefing.Metro); }
        public void Peel() { if (!CanUse("Peel")) return; bool first = !state.label; state.label = true; Notify(); WrapperOpened?.Invoke(); if (first) Announce("SIGNAL", "Same batch. Different picture."); }
        public void Mara()
        {
            if (!CanUse("Mara")) return;
            bool met = state.mara;
            bool label = state.label && !state.maraLabel;
            state.mara = true;
            if (label) state.maraLabel = true;
            Notify();
            if (state.local) Announce("MARA", "My display says everything is fine. Are you all right?");
            else if (label) Announce("MARA", "Oh. I thought the wrapper was wrong.");
            else if (met && state.glove) Announce("MARA", "Did you find anything else?");
            else if (met) Announce("MARA", "There's room here, if you want.");
            else
            {
                Announce("MARA", "You can put that here. The table's less wobbly on this side.");
                if (talk) talk.SayAfter("MARA", "They changed the steam again. Mine used to make a little heart.", 2);
            }
        }
        public void Cup() { if(state.cup || !CanUse("Cup")) return; state.cup = true; Notify(); Cue(); Announce("MARA", "Keep it. Bring it back sometime, if you want."); }
        public void MetroClue() { bool first = !state.metroClue; state.metroClue = true; Notify(); if (first) Announce("SIGNAL", "At work: reject, accept, reject. Watch the arms. Not the score."); }
        public void Glove()
        {
            if (!CanUse("Glove")) return;
            state.glove = true; Notify(); Cue(1.2f); Announce("SIGNAL", "Keep the glove. There's a service face behind every approved one.");
        }
        public void Dinner()
        {
            CheckPlace();
            if (!TryUse("Dinner")) return;
            state.dinner = true; Advance(1900);
            if (receiver) receiver.text = "EVENING SERVICE\nYour meal is on its way";
            Announce("CIVIC", Briefing.Ordered);
        }
        public void SleepOffer() { if (!TryUse("SleepOffer")) return; Announce("CIVIC", Briefing.Sleep); sleepUntil = Time.unscaledTime + 12; }
        float sleepUntil;
        public void RestNight()
        {
            if(!TryUse("RestNight")) return;
            sleepUntil=Time.unscaledTime+1;
            Sleep();
        }
        public void Sleep()
        {
            if (!TryUse("Sleep")) return;
            if (sleepUntil < Time.unscaledTime) { SleepOffer(); return; }
            sleepUntil = 0;
            if (talk) talk.Close();
            Travel(home.position, () => { state.day++; ResetRoutine(); Notify(); ResetAlarm(); if (!briefing) Announce("CIVIC", "Good morning. Your day is ready."); });
        }
        void ResetAlarm()
        {
            if (alarm) { alarm.gameObject.SetActive(false); Destroy(alarm.gameObject); }
            if (!alarmPrefab || !alarmPoint || state.local || state.alarm || state.leftHome) return;
            if (morning) { morning.transform.localPosition=morningPosition; morning.transform.localScale=morningScale; morning.transform.localRotation=morningRotation; morning.SetActive(true); }
            var obj = Instantiate(alarmPrefab, alarmPoint.position, alarmPoint.rotation, alarmPoint);
            obj.name = ObjectNames.Short(alarmPrefab.name);
            alarm = obj.GetComponent<StoryAlarm>();
            if (alarm) { alarm.floorHeight = home.position.y; alarm.ConfigureMorningDisplay(morning ? morning.transform : null); alarm.snoozed.AddListener(Snooze); }
        }
        public void OfferLocal() { OfferLocal(true); }
        public void OfferLocal(bool showChoice)
        {
            if (state.installed && !CanUse("OfferLocal")) return;
            if (!state.installed) { Announce("SERVICE", "Connect the bridge to the forearm port first."); return; }
            if (state.local) { Announce("SERVICE", "Local maintenance mode active."); return; }
            if (showChoice) PlaceChoice(localChoice);
            Announce("CIVIC", "Presentation services will end. Navigation assistance and personalized environments will be unavailable.");
        }
        void PlaceChoice(GameObject panel)
        {
            if (!panel || !RealityPlayer.Head) return;
            var placement = panel.GetComponent<Panel>();
            if (!placement)
            {
                placement = panel.AddComponent<Panel>();
                placement.fitChildren = true;
                placement.height = -.32f;
                placement.RefreshBounds();
            }
            placement.Show();
        }
        public void KeepNetwork() { if (localChoice) localChoice.SetActive(false); if (gardenChoice) gardenChoice.SetActive(false); if (gardenView) gardenView.SetActive(false); if (talk) talk.Close(); Announce("CIVIC", "Your presentation preferences have been retained."); }
        public void ConfirmLocal() { ConfirmLocal(true); }
        public void ConfirmLocal(bool showChoice)
        {
            if (!CanUse("ConfirmLocal")) return;
            if (localChoice) localChoice.SetActive(false);
            if (showChoice) PlaceChoice(gardenChoice);
            if (gardenView) gardenView.SetActive(true);
            Announce("CIVIC", "You don't need to leave to see it.");
            if (talk) talk.SayAfter("SIGNAL", "You can stop here.", 1.5f);
        }
        public void Disconnect()
        {
            if (!CanUse("Disconnect")) return;
            if (gardenChoice) gardenChoice.SetActive(false);
            if (gardenView) gardenView.SetActive(false);
            if (talk) talk.Close();
            StartCoroutine(RemovePresentation());
        }
        IEnumerator RemovePresentation()
        {
            Busy = true;
            var reveal = GetComponent<Reveal>();
            if (reveal) yield return reveal.Play();
            else if (overlays)
            {
                var children = new Transform[overlays.transform.childCount];
                for (int i=0;i<children.Length;i++) children[i]=overlays.transform.GetChild(i);
                for (int i=0;i<children.Length;i++) { if (children[i]) children[i].gameObject.SetActive(false); if(i%3==0) { Cue(.7f+i*.005f); yield return new WaitForSeconds(.09f); } }
            }
            state.local = true;
            Notify();
            Busy = false;
            Announce("CIVIC", "Local connection lost. Please remain at a registered point. A recovery unit is approaching.");
        }
        public void PosterDone() { state.poster = true; Notify(); }
        public void EnterAngel()
        {
            if (!CanUse("EnterAngel")) return;
            if (talk) talk.Close();
            returnPoint = RealityPlayer.FeetPosition;
            InAngel = true;
            bool seen = state.angel;
            Travel(angelRoom.position, () =>
            {
                state.angel = true;
                Notify();
                string next = state.hatch ? "You found it. The bridge gives you local control. What you do with that is yours." : state.glove ? "Put that against the mirror. Then follow what the reflection keeps." : "There's a service glove at your station. Reject, accept, reject. Watch what moves.";
                Announce("SIGNAL", (seen ? "" : "There you are. Don't be afraid of reality. This isn't it, either. This is a way to reach you. The mirror remembers. The wall holds your way out. Don't take my word for it. Look. ") + next);
            }, true);
        }
        public void LeaveAngel() { if (!InAngel) return; if (talk) talk.Close(); Travel(returnPoint, () => InAngel = false, true); }
        public void AskAngel()
        {
            if (!CanUse("AskAngel")) return;
            string[] lines = { "I could give you a name. You couldn't check it.", "I'm using one. Yes. I know that isn't an answer.", "Start with the mirror. That's something you can test.", "I don't know enough to promise that." };
            Announce("SIGNAL", lines[question++ % lines.Length]);
        }
        public void Recover() { if (Busy || !state.local || state.outside || InAngel) return; Travel(checkpoint.position, () => Announce("CONTROLS", "Checkpoint. Stay behind cover when a camera is facing you.")); }
        public void RequestDeparture() { if (CanUse("RequestDeparture")) Announce("CIVIC", "No supported destination is available."); }
        public void ReachOutside() { if (!state.local || !state.gateOpen || state.outside) return; state.outside = true; Notify(); }
        public void Noor()
        {
            if (!CanUse("Noor")) return;
            if (!state.noor)
            {
                state.noor = true;
                Notify();
                Announce("NOOR", "Careful. That slab moves. You came through the gate?");
                if (talk)
                {
                    talk.SayAfter("NOOR", "All right. You don't have to explain yet.", 2.5f);
                    talk.SayAfter("NOOR", "There's food, if you're hungry. Or have a look around. We'll be here.", 2);
                }
            }
            else Announce("NOOR", state.cup ? "You brought a cup. There's water here." : "There's food, if you're hungry. Or have a look around. We'll be here.");
        }
        public void Water() { if (!CanUse("Water")) return; Watered?.Invoke(); Cue(.8f); Announce("NOOR", "A little is enough. That one came up by itself."); }
        public void Rest() { if (!CanUse("Rest")) return; state.ended = true; Notify(); Announce("NOOR", "Stay as long as you like."); }
        public void Replay() { if (briefing && talk && talk.Speaker == "CIVIC") briefing.Replay(); else if (talk) talk.Replay(); }
        public void Evidence() { if (state.glove) Announce("SERVICE", (state.raw ? "Current exterior survey: vegetation, a gate, habitation. " : "No exterior sample recorded. ") + (state.calibrated ? "Trace calibrated. The concourse hatch is a physical door." : "Mirror contact trace requires calibration.")); }
        public void Travel(Vector3 floor, Action action = null, bool rift = false)
        {
            if (Busy || Session.IsOpen || !player) return;
            StartCoroutine(Move(floor, action, rift));
        }
        IEnumerator Move(Vector3 floor, Action action, bool rift)
        {
            Busy = true;
            Action midpoint = () => { RealityPlayer.TeleportTo(floor); action?.Invoke(); };
            if (transition && RealityPlayer.Head)
            {
                if (rift) yield return transition.Play(1.15f, midpoint);
                else yield return transition.PlayBlink(.8f, midpoint);
            }
            else midpoint();
            Busy = false;
        }
        public void Run(string action)
        {
            if (!TryUse(action)) return;
            switch(action)
            {
                case "Snooze": Snooze(); break;
                case "Swat": Swat(); break;
                case "Wash": Wash(); break;
                case "News": News(); break;
                case "Meal": Meal(); break;
                case "Peel": Peel(); break;
                case "Mara": Mara(); break;
                case "Cup": Cup(); break;
                case "MetroClue": MetroClue(); break;
                case "Glove": Glove(); break;
                case "Dinner": Dinner(); break;
                case "SleepOffer": SleepOffer(); break;
                case "Sleep": Sleep(); break;
                case "RestNight": RestNight(); break;
                case "OfferLocal": OfferLocal(); break;
                case "KeepNetwork": KeepNetwork(); break;
                case "ConfirmLocal": ConfirmLocal(); break;
                case "Disconnect": Disconnect(); break;
                case "PosterDone": PosterDone(); break;
                case "EnterAngel": EnterAngel(); break;
                case "LeaveAngel": LeaveAngel(); break;
                case "AskAngel": AskAngel(); break;
                case "RequestDeparture": RequestDeparture(); break;
                case "ReachOutside": ReachOutside(); break;
                case "Noor": Noor(); break;
                case "Water": Water(); break;
                case "Rest": Rest(); break;
                case "Replay": Replay(); break;
                case "Evidence": Evidence(); break;
            }
        }
        public void Save()
        {
            if (!saveEnabled) return;
            try { var json = JsonUtility.ToJson(state,true); File.WriteAllText(SavePath+".tmp",json); if (File.Exists(SavePath)) File.Copy(SavePath,SavePath+".bak",true); File.Copy(SavePath+".tmp",SavePath,true); File.Delete(SavePath+".tmp"); }
            catch (Exception e) { Debug.LogWarning("Garden save: " + e.Message); }
        }
        void Load()
        {
            foreach (var path in new[]{ SavePath, SavePath+".bak" })
            {
                    try { if (!File.Exists(path)) continue; var value=JsonUtility.FromJson<State>(File.ReadAllText(path)); if(value != null && value.version>=1 && value.version<=2 && value.day>0) { state=value; break; } }
                catch (Exception e) { Debug.LogWarning("Garden load: " + e.Message); }
            }
        }
        void OnApplicationPause(bool pause) { if (pause) Save(); }
        void OnApplicationQuit() { Save(); }
        void ResetRoutine()
        {
            state.hour = 700;
            state.alarm = state.wash = state.meal = state.work = state.dinner = false;
            state.leftHome = state.commute = state.workStarted = state.returnedHome = false;
            state.dinnerReady = state.dinnerEaten = state.news = false;
            state.returnArcade = state.returnCommons = false;
        }
        public void Normalize()
        {
            if (state == null) state = new State();
            if (state.version < 2)
            {
                state.workStarted |= state.work;
                state.returnedHome |= state.work && state.dinner;
                state.dinnerReady |= state.dinner;
            }
            state.version = 2;
            state.day = Mathf.Max(1, state.day);
            if (state.dinnerEaten) state.dinnerReady = true;
            if (state.dinnerReady) state.dinner = true;
            if (state.dinner) state.work = state.returnedHome = true;
            if (state.work) state.workStarted = state.meal = state.commute = true;
            if (state.workStarted) state.meal = state.commute = true;
            if (state.commute) state.meal = true;
            if (state.meal) state.leftHome = state.alarm = true;
            state.hour = state.dinnerEaten ? 2200 : state.returnedHome && state.work ? 1900 : state.work ? 1700 : state.workStarted ? 900 : state.commute ? 800 : state.meal ? 730 : 700;
        }
    }
}
