using System;
using System.IO;
using RealityPlayground.Story;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Garden
{
    [DefaultExecutionOrder(-3000)]
    public sealed class Session : MonoBehaviour
    {
        [Serializable] sealed class Options { public bool slow; }
        sealed class RecoveryError : IOException { public RecoveryError(string message, Exception inner) : base(message, inner) { } }
        public Loop loop;
        public GameObject panel;
        public GameObject menu;
        public GameObject confirmation;
        public TMP_Text checkpoint;
        public TMP_Text patrol;
        public TMP_Text status;
        public float timeout = 45f;
        public float confirmTimeout = 15f;
        public bool Slow { get; private set; }
        public bool Confirming { get; private set; }
        public static bool IsOpen => current && current.panel && current.panel.activeInHierarchy;
        static Session current;
        Drone[] drones = Array.Empty<Drone>();
        float until;
        float confirmAt;
        bool loading;
        StoryAlarm heldAlarm;
        bool alarmEnabled;
        string SettingsPath => Path.Combine(Application.persistentDataPath, "Garden.settings.json");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() { current = null; }

        void Awake()
        {
            if (!loop) loop = GetComponentInParent<Loop>();
            if (loop) drones = loop.GetComponentsInChildren<Drone>(true);
            if (loop && loop.saveEnabled)
            {
                try
                {
                    if (File.Exists(SettingsPath)) Slow = JsonUtility.FromJson<Options>(File.ReadAllText(SettingsPath))?.slow ?? false;
                }
                catch (Exception error) { Debug.LogWarning("Session settings: " + error.Message); }
            }
            ApplyPatrol();
            Resume();
        }

        void OnDisable() { Resume(); }

        void Update()
        {
            if (current != this || loading) return;
            HoldAlarm();
            if (Time.unscaledTime >= until) Resume();
        }

        public void Open()
        {
            if (!loop || !panel || loading) return;
            if (current == this && IsOpen) { Resume(); return; }
            if (current) current.Resume();
            current = this;
            panel.SetActive(true);
            HoldAlarm();
            Cancel();
            if (checkpoint) checkpoint.text = "CURRENT CHECKPOINT\nDay " + loop.state.day + "   " + (loop.state.hour / 100).ToString("00") + ":" + (loop.state.hour % 100).ToString("00");
            Message(loop.Busy ? "Transition in progress. Please wait." : "Game controls. Detection is held while this panel is open.");
        }

        public void Resume()
        {
            Confirming = false;
            if (heldAlarm) heldAlarm.interactionEnabled = alarmEnabled;
            heldAlarm = null;
            if (panel) panel.SetActive(false);
            if (current == this) current = null;
        }

        public void Cancel()
        {
            Confirming = false;
            until = Time.unscaledTime + Mathf.Max(5, timeout);
            if (menu) menu.SetActive(true);
            if (confirmation) confirmation.SetActive(false);
            Message("Game controls. Detection is held while this panel is open.");
        }

        public void StartAgain()
        {
            if (!Ready()) return;
            Confirming = true;
            confirmAt = Time.unscaledTime + .8f;
            until = Time.unscaledTime + Mathf.Max(3, confirmTimeout);
            if (menu) menu.SetActive(false);
            if (confirmation) confirmation.SetActive(true);
            Message("Choose START AGAIN to confirm, or KEEP PLAYING to cancel.");
        }

        public void Confirm()
        {
            if (!Confirming || Time.unscaledTime < confirmAt || Time.unscaledTime >= until || !Ready()) return;
            Reload(true);
        }

        public void Checkpoint()
        {
            if (!Ready() || Confirming) return;
            Reload(false);
        }

        public void TogglePatrol()
        {
            if (!loop || !IsOpen || current != this || Confirming || loading) return;
            bool next = !Slow;
            if (loop.saveEnabled)
            {
                try { WriteVerified(SettingsPath, JsonUtility.ToJson(new Options { slow = next }, true)); }
                catch (Exception error) { Message("The setting could not be verified. Using the previous choice for this session."); Debug.LogWarning("Session settings: " + error.Message); return; }
            }
            Slow = next;
            ApplyPatrol();
            until = Time.unscaledTime + Mathf.Max(5, timeout);
            Message(Slow ? "Slower patrols and camera sweeps. Twice as long to leave a sensor's view." : "Patrols and camera sweeps use their standard timing.");
        }

        void ApplyPatrol()
        {
            foreach (var drone in drones) if (drone) drone.patrolRate = Slow ? .5f : 1f;
            if (patrol) patrol.text = "SLOW PATROLS   " + (Slow ? "ON" : "OFF");
        }

        void HoldAlarm()
        {
            var alarm = loop && loop.alarmPoint ? loop.alarmPoint.GetComponentInChildren<StoryAlarm>(true) : null;
            if (alarm != heldAlarm)
            {
                if (heldAlarm) heldAlarm.interactionEnabled = alarmEnabled;
                heldAlarm = alarm;
                if (heldAlarm) alarmEnabled = heldAlarm.interactionEnabled;
            }
            if (heldAlarm) heldAlarm.interactionEnabled = false;
        }

        bool Ready()
        {
            if (!loop || loading || current != this || !IsOpen) return false;
            if (loop.Busy) { Message("Wait for the current transition to finish."); return false; }
            return true;
        }

        void Reload(bool restart)
        {
            if (!loop.saveEnabled) { Message("Reload is unavailable in this test session. Progress was not changed."); return; }
            var scene = loop.gameObject.scene;
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path) || !Application.CanStreamedLevelBeLoaded(scene.path))
            {
                Message("The current scene cannot be reloaded. Progress was not changed.");
                return;
            }
            loading = true;
            loop.saveEnabled = false;
            string path = Path.Combine(Application.persistentDataPath, "Garden.json");
            byte[] main = null, mirror = null;
            bool written = false;
            try
            {
                main = File.Exists(path) ? File.ReadAllBytes(path) : null;
                mirror = File.Exists(path + ".bak") ? File.ReadAllBytes(path + ".bak") : null;
                SaveRun(path, loop.state, restart);
                written = true;
                Message(restart ? "Starting again. Your previous progress has been backed up." : "Reloading the current checkpoint.");
                var operation = SceneManager.LoadSceneAsync(scene.path, LoadSceneMode.Single);
                if (operation == null) throw new IOException("The scene load could not start.");
                Confirming = false;
            }
            catch (Exception error)
            {
                string extra = error is RecoveryError ? " A recovery copy is retained." : "";
                if (written)
                {
                    string restored = RestorePair(path, main, mirror);
                    if (!string.IsNullOrEmpty(restored)) { extra = " A recovery copy is retained."; Debug.LogWarning("Session recovery: " + restored); }
                }
                loading = false;
                loop.saveEnabled = true;
                Cancel();
                Message("Could not reload. Your current game is still running." + extra);
                Debug.LogWarning("Session reload: " + error.Message);
            }
        }

        static void SaveRun(string path, State state, bool restart)
        {
            string currentState = JsonUtility.ToJson(state, true);
            Validate(currentState);
            byte[] original = File.Exists(path) ? File.ReadAllBytes(path) : null;
            byte[] mirror = File.Exists(path + ".bak") ? File.ReadAllBytes(path + ".bak") : null;
            string backup = path + (restart ? ".restart." : ".resume.") + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff") + "." + Guid.NewGuid().ToString("N").Substring(0, 6);
            WriteVerified(backup + ".json", currentState);
            if (original != null) WriteBytes(backup + ".save", original);
            if (mirror != null) WriteBytes(backup + ".bak", mirror);
            string next = restart ? JsonUtility.ToJson(new State(), true) : currentState;
            Validate(next);
            try
            {
                WriteVerified(path + ".bak", next);
                WriteVerified(path, next);
            }
            catch (Exception error)
            {
                string restored = RestorePair(path, original, mirror);
                if (!string.IsNullOrEmpty(restored)) throw new RecoveryError("Recovery copies are retained at " + backup + ".json. " + restored, error);
                throw;
            }
        }

        static string RestorePair(string path, byte[] original, byte[] mirror)
        {
            string errors = "";
            try { Restore(path, original); }
            catch (Exception error) { errors = "Main: " + error.Message; }
            try { Restore(path + ".bak", mirror); }
            catch (Exception error) { errors += (errors.Length > 0 ? " " : "") + "Mirror: " + error.Message; }
            return errors;
        }

        static void Validate(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            if (state == null || state.version < 1 || state.version > 2 || state.day < 1) throw new IOException("Invalid progress data.");
        }

        static void Restore(string path, byte[] bytes)
        {
            if (bytes == null) { if (File.Exists(path)) File.Delete(path); }
            else WriteBytes(path, bytes);
        }

        static void WriteVerified(string path, string json)
        {
            WriteBytes(path, System.Text.Encoding.UTF8.GetBytes(json));
        }

        static void WriteBytes(string path, byte[] bytes)
        {
            string staged = path + ".pending";
            try
            {
                File.WriteAllBytes(staged, bytes);
                if (!Equal(File.ReadAllBytes(staged), bytes)) throw new IOException("The staged save could not be verified.");
                File.Copy(staged, path, true);
                if (!Equal(File.ReadAllBytes(path), bytes)) throw new IOException("The saved copy could not be verified.");
            }
            finally { if (File.Exists(staged)) File.Delete(staged); }
        }

        static bool Equal(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        void Message(string text) { if (status) status.text = text; }
    }
}
