using System.Collections.Generic;
using RealityPlayground;
using UnityEngine;

namespace Garden
{
    public sealed class Briefing : MonoBehaviour
    {
        public Loop loop;
        public Train train;
        public Assembly station;
        public Door door;
        public float delay = 1.8f;
        public float pause = 3f;
        public float reminder = 70f;
        public const string First = "Good morning. Your day is ready.";
        public const string Second = "Good morning. Your day is ready.";
        public const string Later = "Good morning. Your day is ready.";
        public const string Door = "Breakfast is waiting at Commons. Your route is prepared.";
        public const string Commons = "Commons is serving breakfast. Please follow the illuminated route.";
        public const string Food = "Your usual is ready. Please collect your breakfast from the counter.";
        public const string FoodAgain = "Your usual is ready. Please collect your breakfast from the counter.";
        public const string Metro = "Nutritional requirements met. Enjoyment profile applied. Your metro service to Assembly is ready.";
        public const string Doors = "Welcome aboard. Close the doors and confirm departure when you are ready.";
        public const string Depart = "The doors are secured. Your service to the next station is ready to depart.";
        public const string ArriveWork = "Assembly station. Please open the doors before leaving the carriage.";
        public const string ArriveHome = "Residence station. Please open the doors before leaving the carriage.";
        public const string WorkRoute = "Your station is ready at Assembly. Please follow the illuminated route.";
        public const string Work = "Confirm approved units. Reject discrepancies.";
        public const string Damaged = "A discrepancy has been identified. Please classify the component.";
        public const string Survey = "Returned module ready for classification.";
        public const string SurveyAgain = "Returned module ready for classification.";
        public const string Inspection = "This station is being made safe. Please acknowledge the service inspection.";
        public const string ShiftDone = "Your shift is complete. The pedestrian arcade will take you home through Commons.";
        public const string HomeDoor = "Welcome home. Your evening meal service is ready.";
        public const string Order = "Dinner has been selected to complement your day. Please request delivery at the receiver.";
        public const string Ordered = "Your evening meal is on its way.";
        public const string Dinner = "Your meal is ready at the receiver. Enjoy your evening.";
        public const string Evening = "No changes are required for tomorrow. Your rest period is ready.";
        public const string EveningAgain = "No changes are required for tomorrow. Your rest period is ready.";
        public const string BeforeWork = "Breakfast service is available at Commons. Assembly follows at nine.";
        public const string NeedDinner = "Your evening meal is available from the receiver.";
        public const string Sleep = "Tomorrow is prepared. Breakfast at Commons, Assembly at nine, and evening service at home.";
        readonly HashSet<int> spoken = new HashSet<int>();
        readonly HashSet<int> reminded = new HashSet<int>();
        int day = -1, stage = -1;
        float changed, next, active;
        Vector3 feet;
        public string Hint => Current(out _);

        void Awake()
        {
            if (!loop) loop = GetComponent<Loop>();
            if (!loop) loop = Loop.Instance;
            if (loop) loop.briefing = this;
        }

        void OnEnable() { if (loop) loop.Changed += Progress; Progress(); }
        void OnDisable() { if (loop) loop.Changed -= Progress; }
        void Progress() { active = Time.unscaledTime; }

        void Update()
        {
            if (!loop || !loop.talk || !RealityPlayer.Head) return;
            var position = RealityPlayer.FeetPosition;
            if ((position - feet).sqrMagnitude > .25f) { feet = position; Progress(); }
            if (day != loop.state.day)
            {
                day = loop.state.day;
                spoken.Clear();
                reminded.Clear();
                stage = -1;
            }
            if (loop.state.local || loop.state.outside || loop.InAngel || loop.Busy) return;
            string text = Current(out int key);
            if (key != stage)
            {
                stage = key;
                changed = Time.unscaledTime;
            }
            if (string.IsNullOrEmpty(text) || Time.unscaledTime < next || Time.unscaledTime - changed < delay) return;
            if (loop.talk.Waiting || Time.unscaledTime - loop.talk.FinishedAt < pause) return;
            bool repeat = spoken.Contains(key);
            if (repeat && (reminded.Contains(key) || Time.unscaledTime - Mathf.Max(changed, active) < reminder)) return;
            if (!loop.talk.TrySay("CIVIC", text)) return;
            spoken.Add(key);
            if (repeat) reminded.Add(key);
            next = Time.unscaledTime + 10;
        }

        public bool Handles(string text) => Current(out _) == text;

        public void Replay()
        {
            if (!loop || !loop.talk) return;
            string text = Current(out int key);
            if (string.IsNullOrEmpty(text) || loop.state.local || loop.state.outside || loop.InAngel)
            {
                loop.talk.Replay();
                return;
            }
            loop.talk.Close();
            loop.talk.Say("CIVIC", text);
            spoken.Add(key);
            changed = Time.unscaledTime;
            next = Time.unscaledTime + 10;
        }

        string Current(out int key)
        {
            key = -1;
            if (!loop || loop.state.local || loop.state.outside || loop.InAngel) return "";
            if (train && train.Inside)
            {
                if (train.Riding) return "";
                if (train.HasArrived)
                {
                    if (train.Open) return "";
                    key = train.AtWork ? 7 : 8;
                    return train.AtWork ? ArriveWork : ArriveHome;
                }
                key = train.Closed ? 6 : 5;
                return train.Closed ? Depart : Doors;
            }
            var state = loop.state;
            switch (loop.Phase)
            {
                case DayPhase.Morning:
                    key = 0;
                    return state.day == 1 ? First : state.day == 2 ? Second : Later;
                case DayPhase.Breakfast:
                    if (loop.Near(loop.home, 4.2f) && door && !door.open) { key = 1; return Door; }
                    if (loop.Near(loop.breakfast, 4.5f)) { key = 3; return state.day == 1 ? Food : FoodAgain; }
                    key = 2;
                    return Commons;
                case DayPhase.Commute:
                    if (loop.Near(loop.factory, 6))
                    {
                        key = state.casing ? 13 : station && station.Sample >= 2 ? 12 : 10;
                        return state.casing ? Inspection : station && station.Sample >= 2 ? SurveyAgain : Work;
                    }
                    if (train && loop.Near(train.work, 5)) { key = 9; return WorkRoute; }
                    key = 4;
                    return Metro;
                case DayPhase.Work:
                    if (!loop.Near(loop.factory, 6)) { key = 9; return WorkRoute; }
                    if (state.casing) { key = 13; return Inspection; }
                    if (station && station.Sample == 1) { key = 11; return Damaged; }
                    if (station && station.Sample >= 2) { key = 12; return state.day == 1 ? Survey : SurveyAgain; }
                    key = 10;
                    return Work;
                case DayPhase.Return:
                    if (loop.Near(loop.home, 6) && door && !door.open) { key = 15; return HomeDoor; }
                    key = 14;
                    return ShiftDone;
                case DayPhase.Dinner:
                    key = state.dinnerReady ? 18 : state.dinner ? 17 : 16;
                    return state.dinnerReady ? Dinner : state.dinner ? Ordered : Order;
                case DayPhase.Evening:
                    key = 19;
                    return state.day == 1 ? Evening : EveningAgain;
                default:
                    return "";
            }
        }
    }
}
