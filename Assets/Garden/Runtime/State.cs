using System;

namespace Garden
{
    [Serializable]
    public sealed class State
    {
        public int version = 2;
        public int day = 1;
        public int hour = 700;
        public bool casing, glove, calibrated, traced, hatch, bridge, installed, local, outside, ended;
        public bool label, metroClue, poster, angel, raw, dinnerSeen, mara, cup;
        public bool gateLocal, gateEqual, gateOpen, alarm, wash, meal, work, dinner;
        public bool leftHome, commute, workStarted, returnedHome, dinnerReady, dinnerEaten, news;
        public bool returnArcade, returnCommons;
        public bool maraLabel, noor;
        public bool bridgeLeft;
        public bool homeMirror;
    }
}
