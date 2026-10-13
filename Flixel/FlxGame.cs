using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoFlixel;

/// <summary>How the fixed-size game screen is fitted into the application window.</summary>
public enum FlxScaleMode
{
    /// <summary>Largest whole-number scale that fits (crisp pixels), letterboxed. Falls back to fractional if the window is smaller than the game.</summary>
    Integer,
    /// <summary>Largest scale that fits, keeping the aspect ratio (fractional, letterboxed).</summary>
    Fit,
    /// <summary>Fill the whole window, ignoring the aspect ratio.</summary>
    Stretch
}

/// <summary>
/// The MonoGame Game subclass that runs everything. Renders to a fixed-size target
/// (GameWidth x GameHeight) and scales it to the window with letterboxing. The resolution can
/// change between states (see FlxG.SwitchState).
/// </summary>
public class FlxGame : Game
{
    static readonly RasterizerState ScissorRasterizer = new() { ScissorTestEnable = true, CullMode = CullMode.None };

    readonly GraphicsDeviceManager _graphics;
    readonly Func<FlxState> _initialState;
    RenderTarget2D _target = null!;
    FlxState? _pending;
    (int w, int h, int zoom)? _pendingResolution;
    Rectangle _dest;
    readonly RenderTarget2D[] _ping = new RenderTarget2D[2], _pingWindow = new RenderTarget2D[2];
    RenderTarget2D? _overlay;

    /// <summary>
    /// Normal alpha blending, but the alpha channel accumulates properly: drawing into a transparent target gives
    /// premultiplied colors that can be laid over another picture with BlendState.AlphaBlend.
    /// </summary>
    static readonly BlendState OverlayBlend = new()
    {
        ColorSourceBlend = Blend.SourceAlpha,
        ColorDestinationBlend = Blend.InverseSourceAlpha,
        AlphaSourceBlend = Blend.One,
        AlphaDestinationBlend = Blend.InverseSourceAlpha
    };
    bool _resizing;
    bool _fullscreen;
    int _windowedW, _windowedH;

    public int GameWidth { get; private set; }
    public int GameHeight { get; private set; }
    public FlxState State { get; private set; } = null!;
    public SpriteBatch SpriteBatch { get; private set; } = null!;
    public Texture2D Pixel { get; private set; } = null!;

    /// <summary>How the game screen is fitted into the window (default Integer).</summary>
    public FlxScaleMode ScaleMode { get; set; } = FlxScaleMode.Integer;

    /// <summary>Borderless fullscreen at the desktop resolution. The game screen keeps its own resolution and is scaled per ScaleMode.</summary>
    public bool Fullscreen
    {
        get => _fullscreen;
        set
        {
            if (value == _fullscreen) return;
            _fullscreen = value;
            _resizing = true;
            if (value)
            {
                _windowedW = _graphics.PreferredBackBufferWidth;
                _windowedH = _graphics.PreferredBackBufferHeight;
                var mode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
                _graphics.PreferredBackBufferWidth = mode.Width;
                _graphics.PreferredBackBufferHeight = mode.Height;
                _graphics.HardwareModeSwitch = false;   // borderless: no video mode change
                _graphics.IsFullScreen = true;
            }
            else
            {
                _graphics.IsFullScreen = false;
                _graphics.PreferredBackBufferWidth = _windowedW;
                _graphics.PreferredBackBufferHeight = _windowedH;
            }
            _graphics.ApplyChanges();
            _resizing = false;
        }
    }

    /// <param name="zoom">Initial window size = game size * zoom (ignored when windowWidth/windowHeight are given).</param>
    /// <param name="windowWidth">Initial window width, independent of the game resolution (0 = use zoom).</param>
    /// <param name="windowHeight">Initial window height, independent of the game resolution (0 = use zoom).</param>
    public FlxGame(int width, int height, Func<FlxState> initialState, int zoom = 1, int framerate = 60, string title = "MonoFlixel",
        int windowWidth = 0, int windowHeight = 0, bool fullscreen = false, FlxScaleMode scaleMode = FlxScaleMode.Integer)
    {
        GameWidth = width; GameHeight = height; _initialState = initialState;
        ScaleMode = scaleMode;
        int ww = windowWidth > 0 ? windowWidth : width * Math.Max(1, zoom);
        int wh = windowHeight > 0 ? windowHeight : height * Math.Max(1, zoom);

        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = ww,
            PreferredBackBufferHeight = wh
        };
        _windowedW = ww; _windowedH = wh;
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        Window.Title = title;
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / framerate);

        Window.ClientSizeChanged += OnResize;
        Window.TextInput += (_, e) => FlxG.Keys.OnTextInput(e.Character, e.Key);
        FlxG.Game = this;
        FlxG.Save.Bind(title);            // the game-wide save, named after the window title
        if (fullscreen) Fullscreen = true;
    }

    /// <summary>Writes FlxG.Save to disk when the game closes.</summary>
    protected override void OnDeactivated(object sender, EventArgs args)
    {
        FlxG.Sound.FocusLost();
        base.OnDeactivated(sender, args);
    }

    protected override void OnActivated(object sender, EventArgs args)
    {
        FlxG.Sound.FocusGained();
        base.OnActivated(sender, args);
    }

    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        FlxG.Sound.Shutdown();
        FlxG.Save.Flush();
        base.OnExiting(sender, args);
    }

    void OnResize(object? sender, EventArgs e)
    {
        if (_resizing) return;
        _resizing = true;
        _graphics.PreferredBackBufferWidth = Math.Max(1, Window.ClientBounds.Width);
        _graphics.PreferredBackBufferHeight = Math.Max(1, Window.ClientBounds.Height);
        _graphics.ApplyChanges();
        if (!_fullscreen) { _windowedW = _graphics.PreferredBackBufferWidth; _windowedH = _graphics.PreferredBackBufferHeight; }
        _resizing = false;
        FlxG.Signals.GameResized.Dispatch(new Point(_graphics.PreferredBackBufferWidth, _graphics.PreferredBackBufferHeight));
    }

    internal void RequestState(FlxState s, int width, int height, int zoom)
    {
        void Commit()
        {
            _pending = s;
            _pendingResolution = width > 0 && height > 0 ? (width, height, zoom) : null;
        }

        // the current state may want to play an outro first (see FlxState.StartOutro); the switch happens when it finishes
        if (State == null) Commit();
        else State.StartOutro(Commit);
    }

    protected override void LoadContent()
    {
        SpriteBatch = new SpriteBatch(GraphicsDevice);
        Pixel = new Texture2D(GraphicsDevice, 1, 1);
        Pixel.SetData(new[] { Color.White });
        _target = new RenderTarget2D(GraphicsDevice, GameWidth, GameHeight);
        FlxG.Cameras.Init(GameWidth, GameHeight);
        FlxG.PostProcess.RestoreGlobal();          // the effects the player chose last time

        _pending = _initialState();
        SwitchNow();
    }

    void ApplyResolution(int width, int height, int zoom)
    {
        if (width != GameWidth || height != GameHeight)
        {
            GameWidth = width; GameHeight = height;
            _target?.Dispose();
            _target = new RenderTarget2D(GraphicsDevice, width, height);
        }

        if (zoom > 0 && !_fullscreen)   // fullscreen keeps the desktop size; the game screen is just rescaled
        {
            _resizing = true;
            _graphics.PreferredBackBufferWidth = width * zoom;
            _graphics.PreferredBackBufferHeight = height * zoom;
            _graphics.ApplyChanges();
            _windowedW = _graphics.PreferredBackBufferWidth; _windowedH = _graphics.PreferredBackBufferHeight;
            _resizing = false;
        }
    }

    void SwitchNow()
    {
        if (_pending == null) return;

        FlxG.Signals.PreStateSwitch.Dispatch();
        FlxG.Sound.OnStateSwitch();            // sounds that don't persist stop
        State?.Destroy();
        FlxG.Timers.Clear();
        FlxG.Tweens.Clear();
        FlxMouseEvent.RemoveAll();
        FlxG.Gamepads.StopAllRumble();
        FlxG.Actions.OnStateSwitch();
        FlxG.TimeScale = 1f;

        if (_pendingResolution is { } r) { ApplyResolution(r.w, r.h, r.zoom); _pendingResolution = null; }
        FlxG.Cameras.Reset(GameWidth, GameHeight);

        State = _pending;
        _pending = null;
        State.Create();
        FlxG.Signals.PostStateSwitch.Dispatch();
    }

    void EnsureOverlayTarget()
    {
        if (_overlay != null && _overlay.Width == GameWidth && _overlay.Height == GameHeight) return;
        _overlay?.Dispose();
        _overlay = new RenderTarget2D(GraphicsDevice, GameWidth, GameHeight);
    }

    void EnsurePostTargets()
    {
        if (_ping[0] != null && _ping[0].Width == GameWidth && _ping[0].Height == GameHeight) return;
        _ping[0]?.Dispose(); _ping[1]?.Dispose();
        _ping[0] = new RenderTarget2D(GraphicsDevice, GameWidth, GameHeight);
        _ping[1] = new RenderTarget2D(GraphicsDevice, GameWidth, GameHeight);
    }

    void EnsureWindowTargets(int w, int h)
    {
        if (_pingWindow[0] != null && _pingWindow[0].Width == w && _pingWindow[0].Height == h) return;
        _pingWindow[0]?.Dispose(); _pingWindow[1]?.Dispose();
        _pingWindow[0] = new RenderTarget2D(GraphicsDevice, w, h);
        _pingWindow[1] = new RenderTarget2D(GraphicsDevice, w, h);
    }

    void UpdateLayout()
    {
        int bw = GraphicsDevice.PresentationParameters.BackBufferWidth;
        int bh = GraphicsDevice.PresentationParameters.BackBufferHeight;
        int w, h;
        if (ScaleMode == FlxScaleMode.Stretch) { w = bw; h = bh; }
        else
        {
            float s = MathF.Min(bw / (float)GameWidth, bh / (float)GameHeight);
            if (ScaleMode == FlxScaleMode.Integer && s >= 1f) s = MathF.Floor(s);   // crisp pixels when possible
            w = Math.Max(1, (int)(GameWidth * s)); h = Math.Max(1, (int)(GameHeight * s));
        }
        _dest = new Rectangle((bw - w) / 2, (bh - h) / 2, w, h);
    }

    protected override void Update(GameTime gameTime)
    {
        SwitchNow();
        UpdateLayout();
        FlxG.Keys.Update();
        if (FlxG.Keys.JustPressed(Microsoft.Xna.Framework.Input.Keys.F11)) Fullscreen = !Fullscreen;
        FlxG.RealTime += gameTime.ElapsedGameTime.TotalSeconds;
        FlxG.Gamepads.Update((float)gameTime.ElapsedGameTime.TotalSeconds);
        FlxG.Mouse.Update(_dest, GameWidth, GameHeight);
        FlxG.Touches.Update(_dest, GameWidth, GameHeight);
        FlxMouseEvent.Update();
        FlxG.Actions.Update();
        IsMouseVisible = FlxG.Mouse.Visible && FlxG.Mouse.Cursor == null;

        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds * FlxG.TimeScale;
        FlxG.Elapsed = dt;

        FlxG.Signals.PreUpdate.Dispatch();
        FlxG.Sound.Update(gameTime.ElapsedGameTime.TotalSeconds, dt);
        FlxG.Timers.Update(dt);
        FlxG.Tweens.Update(dt);
        FlxG.PostProcess.Update(dt);
        State.TryUpdate(dt);
        FlxG.Cameras.Update(dt);   // after the state, so cameras follow final positions
        FlxG.Signals.PostUpdate.Dispatch();

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        FlxG.Signals.PreDraw.Dispatch();
        GraphicsDevice.SetRenderTarget(_target);
        GraphicsDevice.Clear(Color.Black);

        // With an active post-process, objects flagged DrawAfterPostProcess (text by default) are drawn in a separate pass
        // onto a transparent target and laid over the finished picture, so the shaders don't touch them.
        var passes = FlxG.PostProcess.Active ? FlxG.PostProcess.List : null;
        int last = -1;
        if (passes != null) for (int i = 0; i < passes.Count; i++) if (passes[i].Enabled) last = i;
        bool overlay = last >= 0 && FlxBasic.AnyDrawAfterPostProcess;

        FlxG.DrawLayer = overlay ? FlxG.DrawLayerKind.Scene : FlxG.DrawLayerKind.All;
        foreach (var cam in FlxG.Cameras.List) DrawCamera(cam);

        if (overlay)
        {
            EnsureOverlayTarget();
            GraphicsDevice.SetRenderTarget(_overlay);
            GraphicsDevice.Clear(Color.Transparent);

            FlxG.DrawLayer = FlxG.DrawLayerKind.Overlay;
            foreach (var cam in FlxG.Cameras.List) DrawCamera(cam, overlayPass: true);
        }
        FlxG.DrawLayer = FlxG.DrawLayerKind.All;

        // Post-processes run at game resolution (ping-pong between two targets); the last pass scales to the window.
        Texture2D screen = _target;

        if (last >= 0)
        {
            EnsurePostTargets();
            var full = new Rectangle(0, 0, GameWidth, GameHeight);
            var window = new Rectangle(0, 0, _dest.Width, _dest.Height);
            int flip = 0;
            bool atWindow = false;       // once an effect asks for the window's size, so does everything after it
            for (int i = 0; i < last; i++)
            {
                if (!passes![i].Enabled) continue;
                if (passes[i].WindowResolution) { atWindow = true; EnsureWindowTargets(window.Width, window.Height); }
                var rt = (atWindow ? _pingWindow : _ping)[flip ^= 1];
                GraphicsDevice.SetRenderTarget(rt);
                passes[i].Draw(SpriteBatch, screen, atWindow ? window : full);
                screen = rt;
            }
            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.Clear(Color.Black);
            passes![last].Draw(SpriteBatch, screen, _dest);
        }
        else
        {
            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.Clear(Color.Black);
            SpriteBatch.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.Opaque);
            SpriteBatch.Draw(screen, _dest, Color.White);
            SpriteBatch.End();
        }

        if (overlay)
        {
            // the overlay target holds premultiplied colors (see OverlayBlend)
            SpriteBatch.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.AlphaBlend);
            SpriteBatch.Draw(_overlay, _dest, Color.White);
            SpriteBatch.End();
        }

        // the mouse cursor (and an item carried on it) is the very top: above the overlay (text, windows) and not changed by effects
        if (FlxG.Mouse.Visible && FlxG.Mouse.Cursor is { } cursor)
        {
            float scale = _dest.Width / (float)GameWidth;
            var pos = new Vector2(_dest.X + MathF.Round(FlxG.Mouse.X) * scale, _dest.Y + MathF.Round(FlxG.Mouse.Y) * scale) + FlxG.Mouse.CursorOffset * scale;
            SpriteBatch.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.NonPremultiplied);
            SpriteBatch.Draw(cursor, pos, null, Color.White, 0f, Vector2.Zero, FlxG.Mouse.CursorScale * scale, SpriteEffects.None, 0f);
            SpriteBatch.End();
        }

        FlxG.Sound.Tray.Draw(SpriteBatch, _dest, GameWidth, GameHeight);     // above everything, also above post-processing

        FlxG.Signals.PostDraw.Dispatch();
        base.Draw(gameTime);
    }

    /// <summary>Draws the whole state through one camera: background, world (clipped to the viewport, zoomed), effects.</summary>
    void DrawCamera(FlxCamera cam, bool overlayPass = false)
    {
        var view = new Rectangle(cam.X, cam.Y, cam.Width, cam.Height);

        // a transparent (or partly transparent) background lets what was drawn before show through, e.g. overlay cameras
        if (cam.BgColor.A > 0 && !overlayPass)
        {
            SpriteBatch.Begin(samplerState: SamplerState.PointClamp,
                blendState: cam.BgColor.A == 255 ? BlendState.Opaque : BlendState.NonPremultiplied);
            SpriteBatch.Draw(Pixel, view, cam.BgColor);
            SpriteBatch.End();
        }

        GraphicsDevice.ScissorRectangle = view;
        var transform = Matrix.CreateScale(cam.Zoom, cam.Zoom, 1f) * Matrix.CreateTranslation(cam.X, cam.Y, 0f);

        FlxG.BeginDraw(cam);
        FlxG.BeginBatch(overlayPass ? OverlayBlend : BlendState.NonPremultiplied, ScissorRasterizer, transform);
        State.TryDraw();
        FlxG.EndBatch();
        FlxG.EndDraw();

        if (overlayPass) return;      // flash / fade / shake belong to the scene

        SpriteBatch.Begin(samplerState: SamplerState.PointClamp, blendState: BlendState.NonPremultiplied);
        cam.DrawFx(SpriteBatch, Pixel);
        SpriteBatch.End();
    }
}
