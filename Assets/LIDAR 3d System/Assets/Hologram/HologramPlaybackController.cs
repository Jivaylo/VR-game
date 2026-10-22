using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;

/// <summary>
/// Plays a Record3D recording as a hologram with a chosen play range, speed and loop mode, powers the
/// hologram up when it starts, and powers it down when it finishes: the last frame and every projected
/// element collapse into the projector, which flares and shuts off. While enabled it drives
/// <see cref="Record3DPlayback"/> itself and pauses the Timeline that used to, so loops are frame-exact
/// with no pause in between.
/// </summary>
[RequireComponent(typeof(Record3DPlayback))]
public class HologramPlaybackController : MonoBehaviour
{
    public enum LoopMode { InfiniteLoop, LoopCount, PlayOnce }
    public enum FinishedAction { HideHologram, DisableGameObject, DestroyGameObject }
    private enum State { Stopped, PoweringUp, Playing, PoweringDown, Finished }

    [SerializeField] private Record3DPlayback playback;
    [SerializeField] private VolumetricHologramController hologram;

    [Header("Play Time (seconds into the recording)")]
    [Tooltip("Where playback starts, and where every loop starts again.")]
    [Min(0f)] public float startTime = 0f;
    [Tooltip("Where playback ends and loops back to Start Time. 0 = the end of the recording.")]
    [Min(0f)] public float endTime = 0f;
    [Tooltip("1 = real time.")]
    [Range(0.1f, 3f)] public float playbackSpeed = 1f;

    [Header("Looping")]
    [Tooltip("Infinite Loop: plays the range forever. Loop Count: plays it Loop Count times, then powers down. Play Once: plays it once, then powers down.")]
    public LoopMode loopMode = LoopMode.InfiniteLoop;
    [Tooltip("How many times the range plays before the hologram powers down (Loop Count mode).")]
    [Min(1)] public int loopCount = 3;
    [Tooltip("Seconds of a brief resync shimmer around the loop point, which softens the jump from the last frame back to the first. The loop itself never waits or pauses. 0 = hard cut.")]
    [Range(0f, 1f)] public float loopBlend = 0.3f;

    [Header("Power")]
    [Tooltip("Start playing when this object is enabled (for example when the scene starts).")]
    public bool playOnEnable = true;
    [Tooltip("Build the hologram up out of the projector when playback starts.")]
    public bool powerUpOnStart = true;
    [Range(0.2f, 4f)] public float powerUpDuration = 1.2f;
    [Tooltip("Length of the power-down: the last frame and the projection collapse into the emitter, which flares, and the projector shuts off.")]
    [Range(0.3f, 5f)] public float powerDownDuration = 2f;
    [Tooltip("What happens once the hologram has powered down.")]
    public FinishedAction whenFinished = FinishedAction.HideHologram;
    public UnityEvent onFinished = new UnityEvent();

    [Header("Timeline")]
    [Tooltip("Pause any Timeline (PlayableDirector) that drives this recording while this component plays it, so the two do not fight over the frame. It resumes when this component is disabled.")]
    public bool takeOverFromTimeline = true;

    /// <summary>True while the hologram is powering up or playing.</summary>
    public bool isPlaying { get { return state_ == State.PoweringUp || state_ == State.Playing; } }
    /// <summary>True once the hologram has powered down.</summary>
    public bool isFinished { get { return state_ == State.Finished; } }
    /// <summary>Current position in the recording, in seconds.</summary>
    public float currentTime { get; private set; }
    /// <summary>How many times the play range has completed since Play.</summary>
    public int completedLoops { get; private set; }

    private State state_ = State.Stopped;
    private float playTime_;   // seconds into the play range
    private float powerTimer_; // seconds into the current power-up or power-down
    private readonly List<PlayableDirector> pausedDirectors_ = new List<PlayableDirector>();
    private readonly List<bool> pausedDirectorsWerePlaying_ = new List<bool>();

    private void OnEnable()
    {
        if (playback == null)
        {
            playback = GetComponent<Record3DPlayback>();
        }
        if (hologram == null)
        {
            hologram = GetComponent<VolumetricHologramController>();
        }
        if (takeOverFromTimeline)
        {
            PauseTimelines();
        }
        if (playOnEnable)
        {
            Play();
        }
    }

    private void OnDisable()
    {
        ResumeTimelines();
        if (hologram != null)
        {
            hologram.SetPowerState(0f, 1f, 1f, 0f, 0f);
        }
        state_ = State.Stopped;
    }

    /// <summary>Plays the range from Start Time, powering up first if Power Up On Start is set.</summary>
    public void Play()
    {
        completedLoops = 0;
        playTime_ = 0f;
        powerTimer_ = 0f;
        state_ = powerUpOnStart ? State.PoweringUp : State.Playing;
        Step(0f);
    }

    /// <summary>Starts the power-down now, whatever the loop mode.</summary>
    public void Stop()
    {
        if (isPlaying)
        {
            state_ = State.PoweringDown;
            powerTimer_ = 0f;
        }
    }

    private void Update()
    {
        Step(Time.deltaTime);
    }

    private void Step(float deltaTime)
    {
        if (playback == null || state_ == State.Stopped || state_ == State.Finished)
        {
            return;
        }

        float framesPerSecond = Mathf.Max(1, playback.fps);
        GetRange(framesPerSecond, out int firstFrame, out int frameCount);
        float rangeLength = frameCount / framesPerSecond;

        switch (state_)
        {
            case State.PoweringUp:
                // The recording already plays while it assembles.
                powerTimer_ += deltaTime;
                if (powerTimer_ >= powerUpDuration)
                {
                    state_ = State.Playing;
                }
                Advance(deltaTime * playbackSpeed, rangeLength, framesPerSecond);
                break;
            case State.Playing:
                Advance(deltaTime * playbackSpeed, rangeLength, framesPerSecond);
                break;
            case State.PoweringDown:
                powerTimer_ += deltaTime;
                if (powerTimer_ >= powerDownDuration)
                {
                    Finish();
                    return;
                }
                break;
        }

        int frame = firstFrame + Mathf.Clamp(Mathf.FloorToInt(playTime_ * framesPerSecond), 0, frameCount - 1);
        playback.LoadFrame(frame);
        currentTime = frame / framesPerSecond;
        ApplyPower(rangeLength);
    }

    /// <summary>Moves through the range, wrapping straight back to its start (no hold, no gap) while looping.</summary>
    private void Advance(float deltaTime, float rangeLength, float framesPerSecond)
    {
        playTime_ += deltaTime;
        while (playTime_ >= rangeLength)
        {
            completedLoops++;
            if (LoopsAgain())
            {
                playTime_ -= rangeLength;
            }
            else
            {
                playTime_ = rangeLength - 0.5f / framesPerSecond; // hold the last frame while it collapses
                state_ = State.PoweringDown;
                powerTimer_ = 0f;
                break;
            }
        }
    }

    private bool LoopsAgain()
    {
        switch (loopMode)
        {
            case LoopMode.InfiniteLoop: return true;
            case LoopMode.LoopCount: return completedLoops < Mathf.Max(1, loopCount);
            default: return false;
        }
    }

    private void GetRange(float framesPerSecond, out int firstFrame, out int frameCount)
    {
        int total = Mathf.Max(1, playback.numberOfFrames);
        firstFrame = Mathf.Clamp(Mathf.FloorToInt(startTime * framesPerSecond), 0, total - 1);
        int endFrame = endTime > 0f ? Mathf.Clamp(Mathf.CeilToInt(endTime * framesPerSecond), firstFrame + 1, total) : total;
        frameCount = Mathf.Max(1, endFrame - firstFrame);
    }

    private void ApplyPower(float rangeLength)
    {
        if (hologram == null)
        {
            return;
        }
        float collapse = 0f, visibility = 1f, projectorVisibility = 1f, flash = 0f, boost = 0f;
        if (state_ == State.PoweringUp)
        {
            // Projector flickers on and flares, then the form rises out of the emitter.
            float s = Mathf.Clamp01(powerTimer_ / powerUpDuration);
            projectorVisibility = Ramp(0f, 0.25f, s) * Flicker(s, 0f, 0.3f);
            flash = Bump(s, 0.22f, 0.08f);
            collapse = 1f - Ramp(0.2f, 0.95f, s);
            visibility = Ramp(0.15f, 0.5f, s);
            boost = 1.2f * (1f - s) * (1f - s);
        }
        else if (state_ == State.PoweringDown)
        {
            // A last glitch, then everything collapses into the emitter, which flares; the projector
            // flickers and shuts off.
            float s = Mathf.Clamp01(powerTimer_ / powerDownDuration);
            boost = 1.5f * Bump(s, 0.06f, 0.08f) + 0.8f * s;
            collapse = Ramp(0.1f, 0.75f, s);
            visibility = 1f - Ramp(0.55f, 0.85f, s);
            flash = 1.5f * Bump(s, 0.74f, 0.06f);
            projectorVisibility = (1f - Ramp(0.8f, 1f, s)) * Flicker(s, 0.8f, 1f);
        }
        else if (state_ == State.Playing && loopBlend > 0f)
        {
            // Resync shimmer centred on the loop point (only where a loop actually happens).
            float halfBlend = 0.5f * loopBlend;
            float distance = float.MaxValue;
            if (completedLoops > 0)
            {
                distance = playTime_;
            }
            if (loopMode == LoopMode.InfiniteLoop || (loopMode == LoopMode.LoopCount && completedLoops + 1 < loopCount))
            {
                distance = Mathf.Min(distance, rangeLength - playTime_);
            }
            float k = 1f - Mathf.Clamp01(distance / halfBlend);
            k = k * k * (3f - 2f * k);
            boost = 0.8f * k;
            visibility = 1f - 0.25f * k;
        }
        hologram.SetPowerState(collapse, visibility, projectorVisibility, flash, boost);
    }

    private void Finish()
    {
        state_ = State.Finished;
        if (hologram != null)
        {
            hologram.SetPowerState(1f, 0f, 0f, 0f, 0f);
        }
        onFinished.Invoke();
        switch (whenFinished)
        {
            case FinishedAction.DisableGameObject:
                gameObject.SetActive(false);
                break;
            case FinishedAction.DestroyGameObject:
                Destroy(gameObject);
                break;
        }
    }

    // Smooth 0 -> 1 between a and b.
    private static float Ramp(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    private static float Bump(float x, float centre, float width)
    {
        float d = (x - centre) / width;
        return Mathf.Exp(-d * d);
    }

    // Hard on/off flicker inside [from, to], like a failing light.
    private static float Flicker(float x, float from, float to)
    {
        if (x < from || x > to)
        {
            return 1f;
        }
        return Mathf.Repeat(x * 23f, 1f) > 0.5f ? 1f : 0.45f;
    }

    private void PauseTimelines()
    {
        pausedDirectors_.Clear();
        pausedDirectorsWerePlaying_.Clear();
        foreach (var director in FindObjectsByType<PlayableDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!DrivesThisRecording(director))
            {
                continue;
            }
            pausedDirectors_.Add(director);
            pausedDirectorsWerePlaying_.Add(director.state == PlayState.Playing || director.playOnAwake);
            director.Pause();
            director.enabled = false;
        }
    }

    private void ResumeTimelines()
    {
        for (int i = 0; i < pausedDirectors_.Count; i++)
        {
            var director = pausedDirectors_[i];
            if (director == null)
            {
                continue;
            }
            director.enabled = true;
            if (pausedDirectorsWerePlaying_[i])
            {
                director.Play();
            }
        }
        pausedDirectors_.Clear();
        pausedDirectorsWerePlaying_.Clear();
    }

    private bool DrivesThisRecording(PlayableDirector director)
    {
        if (director.playableAsset == null)
        {
            return false;
        }
        foreach (var output in director.playableAsset.outputs)
        {
            if (output.sourceObject != null && director.GetGenericBinding(output.sourceObject) == playback)
            {
                return true;
            }
        }
        return false;
    }
}
