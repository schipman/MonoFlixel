using System;
using Microsoft.Xna.Framework;

namespace MonoFlixel;

/// <summary>Base of the visuals a Transition plays (TransitionFade, TransitionTiles).</summary>
public abstract class TransitionEffect : FlxGroup
{
    /// <summary>Called once, when the effect has finished.</summary>
    public Action? FinishCallback;

    public bool Finished { get; private set; }

    protected readonly TransitionData Data;
    protected bool Started;
    protected TransitionStatus EndStatus;

    /// <summary>Wait this long after the last frame before finishing (lets one last frame render).</summary>
    protected float FinalDelayTime;

    FlxCamera? _customCamera;

    protected TransitionEffect(TransitionData data)
    {
        Data = data;
    }

    public override void Destroy()
    {
        FinishCallback = null;
        if (_customCamera != null)
        {
            FlxG.Cameras.Remove(_customCamera);   // may already be gone because the state switched
            _customCamera = null;
        }
        base.Destroy();
    }

    /// <summary>Starts moving to full (In) or to empty (Out), and picks the camera to draw on.</summary>
    public virtual void Start(TransitionStatus newStatus)
    {
        Started = true;
        EndStatus = newStatus == TransitionStatus.In ? TransitionStatus.Full : TransitionStatus.Empty;

        switch (Data.CameraMode)
        {
            case TransitionCameraMode.New:
                var region = Data.GetRegion();
                _customCamera = new FlxCamera(0, 0, region.Width, region.Height);
                FlxG.Cameras.Add(_customCamera);
                _customCamera.BgColor = Color.Transparent;     // see-through, so the game shows underneath
                Cameras = new[] { _customCamera };
                break;

            case TransitionCameraMode.Top:
                // the last camera is drawn last, so the transition is on top of everything
                Cameras = new[] { FlxG.Cameras.List[FlxG.Cameras.List.Count - 1] };
                break;
        }
    }

    /// <summary>Jumps to a status (Full or Empty) without animating.</summary>
    public virtual void SetStatus(TransitionStatus newStatus) { }

    protected void DelayThenFinish() => new FlxTimer().Start(FinalDelayTime, _ => OnFinish());

    void OnFinish()
    {
        Finished = true;

        if (FinishCallback != null)
        {
            var callback = FinishCallback;
            FinishCallback = null;
            callback();
        }
    }
}
