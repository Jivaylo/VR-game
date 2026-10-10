using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using RealityPlayground;

namespace Garden
{
    public sealed class Talk : MonoBehaviour
    {
        [Serializable] public struct Line { public string speaker, text; public AudioClip clip; }
        public TMP_Text caption;
        public TMP_Text speakerLabel;
        public Transform board;
        public AudioSource voice;
        public Wrist wrist;
        public Line[] lines;
        public float hold = 3;
        string lastSpeaker, lastText;
        string[] pages = Array.Empty<string>();
        float[] ends = Array.Empty<float>();
        float started, until;
        int page;
        Panel panel;
        struct Pending { public string speaker, text; public float pause; }
        readonly List<Pending> pending = new List<Pending>();
        public bool Speaking => until > Time.unscaledTime || voice && voice.isPlaying;
        public bool Waiting => Speaking || pending.Count > 0;
        public float FinishedAt { get; private set; }
        public string Speaker => lastSpeaker;
        public int PageIndex => page;
        public int PageCount => pages.Length;

        public void Say(string speaker, string text)
        {
            SayAfter(speaker, text, .6f);
        }

        public void SayAfter(string speaker, string text, float pause)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (Waiting)
            {
                if (lastSpeaker == speaker && lastText == text) return;
                foreach (var item in pending) if (item.speaker == speaker && item.text == text) return;
                if (pending.Count >= 8) pending.RemoveAt(0);
                pending.Add(new Pending { speaker = speaker, text = text, pause = Mathf.Clamp(pause, .3f, 6) });
                return;
            }
            Speak(speaker, text);
        }

        public bool TrySay(string speaker, string text)
        {
            if (Waiting || string.IsNullOrWhiteSpace(text)) return false;
            Speak(speaker, text);
            return true;
        }

        void Speak(string speaker, string text)
        {
            lastSpeaker = speaker;
            lastText = text;
            pages = SplitPages(text, Loop.Instance && Loop.Instance.state.local && Wrist.Supports(speaker) ? 58 : 100);
            page = 0;
            if (speakerLabel) speakerLabel.text = speaker;
            if (voice)
            {
                voice.Stop();
                voice.clip = null;
                if (lines != null) foreach (var line in lines)
                    if (line.text == text && (string.IsNullOrEmpty(line.speaker) || line.speaker == speaker) && line.clip) { voice.clip = line.clip; voice.Play(); break; }
            }
            float duration = voice && voice.clip ? voice.clip.length : Mathf.Max(3, (text?.Length ?? 0) * .06f);
            ends = new float[pages.Length];
            float weight = 0;
            for (int i = 0; i < pages.Length; i++) weight += Weight(pages[i]);
            float elapsed = 0;
            for (int i = 0; i < pages.Length; i++)
            {
                elapsed += Mathf.Max(2.2f, duration * Weight(pages[i]) / Mathf.Max(1, weight));
                ends[i] = elapsed;
            }
            started = Time.unscaledTime;
            until = started + Mathf.Max(Mathf.Clamp(hold, 2.5f, 5), elapsed + 1.5f);
            ShowPage();
            if (!board) return;
            if (!panel || panel.transform != board) panel = board.GetComponent<Panel>();
            if (!panel)
            {
                panel = board.gameObject.AddComponent<Panel>();
                panel.fitChildren = true;
                panel.RefreshBounds();
            }
            bool attached = wrist && Wrist.Supports(speaker);
            panel.enabled = !attached;
            if (wrist) wrist.Present(attached);
            if (attached) { board.gameObject.SetActive(true); wrist.Follow(); }
            else panel.Show();
        }

        public static string[] SplitPages(string text, int limit = 100)
        {
            if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();
            limit = Mathf.Max(24, limit);
            var result = new List<string>();
            int from = 0;
            while (from < text.Length)
            {
                while (from < text.Length && char.IsWhiteSpace(text[from])) from++;
                if (from >= text.Length) break;
                int end = Mathf.Min(text.Length, from + limit);
                if (end < text.Length)
                {
                    int boundary = end;
                    while (boundary > from && !char.IsWhiteSpace(text[boundary])) boundary--;
                    if (boundary > from) end = boundary;
                    for (int i = end - 1; i > from + limit / 2; i--)
                        if ((text[i] == '.' || text[i] == '?' || text[i] == '!' || text[i] == ';') && i + 1 < text.Length && char.IsWhiteSpace(text[i + 1])) { end = i + 1; break; }
                }
                result.Add(text.Substring(from, end - from).Trim());
                from = end;
            }
            return result.ToArray();
        }

        static float Weight(string text)
        {
            float weight = text.Length;
            foreach (char c in text)
                if (c == '.' || c == '?' || c == '!') weight += 5;
            return weight;
        }

        void ShowPage()
        {
            if (caption) caption.text = pages.Length == 0 ? "" : (speakerLabel ? "" : lastSpeaker + "\n") + pages[page];
        }

        public void Replay() { if (!string.IsNullOrEmpty(lastText)) { pending.Clear(); Speak(lastSpeaker, lastText); } }
        public void CancelSpeaker(string speaker)
        {
            pending.RemoveAll(item => item.speaker == speaker);
            if (lastSpeaker == speaker && Speaking) Hide();
        }
        public void Close() { pending.Clear(); Hide(); }
        void Hide() { until = 0; FinishedAt = Time.unscaledTime; if (wrist) wrist.Present(false); if (board) board.gameObject.SetActive(false); if (voice) voice.Stop(); }
        void Update()
        {
            if (until > 0 && !Speaking) Hide();
            if (until <= 0)
            {
                if (pending.Count > 0 && Time.unscaledTime - FinishedAt >= pending[0].pause)
                {
                    var item = pending[0];
                    pending.RemoveAt(0);
                    Speak(item.speaker, item.text);
                }
                return;
            }
            while (page + 1 < pages.Length && Time.unscaledTime - started >= ends[page]) { page++; ShowPage(); }
        }
    }
}
