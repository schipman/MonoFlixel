# MonoFlixel starter

A C# / MonoGame port of the core of HaxeFlixel, plus three demos you can launch from a main menu:
a port of **Mode** (the HaxeFlixel platformer demo), a **Mega Man** tilemap/animation test, and the
**Tiled editor** demo (a level made in Tiled and loaded from a `.tmx` file).

<img width="2558" height="1440" alt="mPxCDAjhQu" src="https://github.com/user-attachments/assets/e1f832b4-b8c7-4e9c-a8e2-89fe04fe4e11" />
<img width="2560" height="1440" alt="Vw3qTSzFKS" src="https://github.com/user-attachments/assets/999b713b-cfbf-47c3-8c29-1ef5cb482d3d" />
<img width="2555" height="1440" alt="QEE3mXYXTY" src="https://github.com/user-attachments/assets/f1da691b-c275-4200-bd19-ef89b0ed3826" />


## Run it
1. Install the .NET 8 SDK.
2. `dotnet run` in this folder (or F5 in Visual Studio). NuGet restores `MonoGame.Framework.DesktopGL`.

## Controls
| Where | Keys |
|---|---|
| Launcher (640x360) | tiles over an animated gradient with an aurora, rising glow dust and a wavy rainbow title. Click a tile, or arrows + ENTER, or 1-9; **F** = effects menu (post-processing for every demo); ESC = quit. Demos go back with `LauncherState.Return()` |
| Mode menu | click the buttons, or X + C to play, ESC = back to the launcher |
| Mode game | arrows / WASD move + aim, **X** jump, **C** shoot (gamepad: D-pad/stick, A, X), ESC = Mode menu |
| Mega Man | arrows / WASD, Space/Up/W jump, **T** swap AUTO <-> ALT tileset, ESC = launcher |
| Tiled demo | arrows / WASD move, Space/Up/W jump. Collect the coins, then find the exit. ESC = launcher |

## What is in the engine (`Flixel/`)
`FlxGame` (fixed-resolution render target, integer scaling, resolution can change per state),
`FlxState`, `FlxBasic`, `FlxObject` (motion, angular motion, Flixel separation, moving-platform carry),
`FlxSprite` (animation, facing flips, origin/rotation), `FlxGroup`/`FlxTypedGroup` (Flixel-style `Recycle`),
`FlxSpriteGroup`, `FlxTiledSprite`, `FlxTilemap` (AUTO / ALT auto-tiling, collision, tile callbacks, rays, animated and flipped tiles),
`MonoFlixel.Tiled` (`.tmx` loader, see below),
`FlxText` (bitmap font, wrap, align, outline, rotation), `FlxButton`, `FlxEmitter` (particles that collide),
`FlxCamera` (multiple cameras, viewports, zoom, dead zones, shake/flash/fade), `FlxG.Sound` (effects + music),
`FlxTimer`, `FlxTween`, `FlxFlicker`, keyboard / mouse (custom cursor) / gamepad input, `FlxG.TimeScale`.

## Haxe -> C# cheat sheet
| HaxeFlixel | MonoFlixel |
|---|---|
| `new FlxGame(320, 240, PlayState)` | `new FlxGame(320, 240, () => new PlayState())` |
| `override function create()` / `update(elapsed)` | `public override void Create()` / `Update(float elapsed)` |
| `FlxG.keys.pressed.LEFT` | `FlxG.Keys.Pressed(Keys.Left)` |
| `sprite.velocity.x = 50` | `sprite.Velocity.X = 50` |
| `FlxG.collide(a, b)` / `FlxG.overlap(a, b, cb)` | `FlxG.Collide(a, b)` / `FlxG.Overlap(a, b, cb)` |
| `FlxG.camera.follow(p, PLATFORMER)` | `FlxG.Camera.Follow(p, FlxCameraFollowStyle.Platformer)` |
| `FlxG.cameras.flash(0xff131c1b)` | `FlxG.Cameras.Flash(FlxColor.Rgb(0x131c1b))` |
| `FlxG.sound.play(AssetPaths.sounds.jump)` | `FlxG.Sound.Play("mode/sounds/jump.wav")` |
| `FlxG.random.int(1, 6)` (inclusive) | `FlxG.Random.Int(1, 6)` |
| `group.recycle(Bullet.new)` | `group.Recycle(() => new Bullet())` |
| `emitter.start(true, 0, 20)` | `emitter.Start(true, 0, 20)` |
| `FlxG.switchState(MenuState.new)` | `FlxG.SwitchState(new MenuState())` (optionally with a new resolution) |
| `FlxTween.tween(s, {x: 100}, 1)` | `FlxTween.Tween(s, new { X = 100f }, 1f)` |

Reading `Touching` / `IsTouching()` must happen **before** `base.Update()` in your own `Update` (Flixel resets it there).

## Mode port: what differs from the original
Ported nearly line by line (`Mode/`): menu, play, victory, player, enemies, spawners, bullets, level generator, HUD,
spawner mini-cameras, slow motion, score countdown. Not ported: the idle **demo replay** (FlxVCR) and
`FlxG.save` play counter, the debugger watch, the mobile virtual pad. Gamepad input is read directly instead
of through FlxActions. The cursor image is drawn at 1x game pixels (= the original's 2x at zoom 2).

## Assets and licenses
* `Assets/mode/` : images and sounds from the HaxeFlixel demos (MIT, see `Assets/mode/LICENSE-flixel-demos.txt`
  and `NOTICE.txt`). Sounds were converted from OGG to WAV because MonoGame loads WAV at runtime. Music by dannyB.
* `Assets/flixel/` : stock button image and Nokia Cellphone FC font from the HaxeFlixel repo (MIT);
  `nokia8.png/.json` is that font baked into a bitmap atlas.
* `Assets/tiled/` : level and images from the HaxeFlixel TiledEditor demo (MIT, see `NOTICE.txt` there).
* `Assets/autotiles*.png`, `Assets/megaman_run.png` : the assets you supplied.
* The collision/separation code follows Flixel's algorithm (MIT). Keep its copyright notice if you copy more of it.

## Tiled maps (.tmx)
```csharp
using MonoFlixel.Tiled;

var map = TiledMap.Load(FlxAssets.Get("tiled/level.tmx"));
FlxG.Camera.Bounds = new Rectangle(0, 0, map.FullWidth, map.FullHeight);

foreach (var layer in map.LayersOfType<TiledTileLayer>())
    Add(map.CreateTilemap(layer));                       // tileset, margin/spacing, flips, offset, opacity, animations

foreach (var layer in map.LayersOfType<TiledObjectLayer>())
    foreach (var o in layer.Objects)                     // o.Type, o.Name, o.X/Y, o.Width/Height, o.Properties, o.Gid ...
        { /* create your sprites, triggers, spawn points */ }
```
* Paths inside the map are relative to the map file: use `map.ResolvePath("level.png")`.
* **Tile objects** (objects with a `Gid`, like coins or pictures) are positioned by their **bottom-left** corner in Tiled,
  so subtract the tile height from `Y` to get a top-left position. Pictures are scaled and rotated around that corner.
* Custom properties are text with typed getters: `o.Properties.GetFloat("depth", 1f)`, `layer.Properties.Contains("nocollide")`.
* A tilemap uses **one tileset**; `CreateTilemap` tells you which layer mixes several (put each tileset on its own layer).
  The tileset comes from the layer's `tileset` property if set, otherwise from its first tile.
* Tile value = Tiled global id (0 = empty). `FlxTilemap.LoadMapFromArray(..., startingIndex: firstgid)` does the
  offsetting; margin/spacing are `TilesetMargin` / `TilesetSpacing` (set before loading).
* Supported: orthogonal maps, tile layers (CSV, Base64 uncompressed / gzip / zlib, plain XML), tile flips,
  object layers (tile, rectangle, ellipse, point, polygon, polyline, text objects), image layers, group layers
  (flattened), image-collection and external `.tsx` tilesets, tile animations, custom properties everywhere.
  Not supported (you get a clear error): infinite maps, zstd compression. Isometric/hex maps are not rendered.
* The demo (`Demo/TiledLevel.cs`, `Demo/TiledDemoState.cs`) is a port of HaxeFlixel's TiledEditor demo.

## Tilemap notes
Auto/Alt: 16-frame sheet, frame = neighbour mask (up=1, right=2, down=4, left=8), edges count as filled.
Tile value v is hidden if `v < DrawIndex`, solid if `v >= CollideIndex`, drawn with frame `v - FrameOffset`
(auto-tiling sets these for you; for other maps pass `startingIndex` when loading). Not ported: `FULL` (47-tile) auto-tiling.

## Post-process shaders: a stack, and a global effects menu
`FlxPostProcess` runs a pixel-shader `Effect` over the finished game screen. `FlxG.PostProcess` is a **stack**: any number of effects can
be on at once, each works on the result of the one before (so the order changes the picture). There are two stacks that run one after the other:
- the **local** stack belongs to the current screen (`Add`/`Push`, `Pop`, `Remove`, `Clear`, `Set`): a game puts its own look there (Drown's water wobble);
- the **global** stack is the player's choice (`AddGlobal`, `RemoveGlobal`, `MoveGlobal`, `ClearGlobal`). It stays on in every demo and across state changes, is saved with `FlxG.Save` and comes back at the next start.
```csharp
FlxG.PostProcess.Add(FlxPostProcess.Load("Wiggle", ("Amplitude", 0.004f), ("Frequency", 20f), ("Speed", 3f)));   // this screen
FlxG.PostProcess.AddGlobal(FlxPostProcessLibrary.Create("CRT"));                                          // everything
FlxG.PostProcess.Clear();                                                                                 // local ones only
```
**The launcher's EFFECTS button (or F)** opens the menu: SPACE / click switches an effect on or off for all demos, SHIFT+UP/DOWN (or `[` `]`) moves it in the stack, C clears all.
The engine's effects (`FlxPostProcessLibrary.All`, shaders in `Content/Shaders/`):

| Effect | What it does |
|---|---|
| Bloom | bright parts glow, at window resolution (`FlxBloom`) |
| Desaturate | colors fade towards grey |
| GaussianBlur | a smooth separable gaussian blur (`FlxGaussianBlur`: `Radius` is its sigma in game pixels, `Passes`) |
| RadialBlur | zoom blur: smeared towards the middle (`Strength`, `Inner`, `Center`) |
| Wiggle | water wobble |
| ChromaticAberration | red and blue pushed apart towards the edges (`Amount`, `Falloff`) |
| Vignette | soft dark corners (`Intensity`, `Radius`, `Softness`, `Roundness`) |
| HeatVision | thermal camera palette (black, blue, red, yellow, white) with a little heat haze |
| NightVision | green image intensifier: gain, grain, flicker, round lens |
| VHS | tape: color bleed and split, wobble, a rolling tracking band, tearing at the bottom, noise streaks |
| CRT Flat | the same CRT with `Curvature` 0: scanlines and mask, but the picture is not warped |
| CRT | barrel curvature with rounded corners, sharp-bilinear sampling, brightness dependent scanline beams, an aperture-grille mask at window resolution, vignette, in linear light. Written in the spirit of the RetroArch crt-easymode / crt-lottes / crt-geom shaders (not copied from them) |

Wiggle, HeatVision and RadialBlur move the picture by less than a pixel, so they sample with a linear filter and a "sharp bilinear" mapping (`SharpUV` in the shaders): texels stay crisp but their edges are blended over one output pixel, so the motion is smooth, also when they are not the last effect. Put the CRT last in the stack. Effects set `WindowResolution = true` when they need to work at the size of the window instead of the game screen (the CRT does; everything after such an effect does too).
Drawing after the effects (`DrawAfterPostProcess`: text, the Drown HUD) is unchanged and applies to the whole stack. A substate's backdrop is drawn after them too, over the parent's text and buttons.

To add a shader, put the `.fx` in `Content/Shaders/` and add a `#begin Shaders/X.fx` block to `Content/Content.mgcb`
(copy an existing one); the MonoGame content builder compiles it on build; list it in `FlxPostProcessLibrary` to get it in the menu. Parameters `fTimer`/`Time`, `TexelSize`,
`Resolution` (the picture it reads) and `OutputSize` (what it draws to) are filled in automatically; anything else via `.Set("Name", value)`. Shaders use `sampler : register(s0)` for the screen.
Note: default values written in a `.fx` (`float Amount = 1.0;`) are not compiled in, so every parameter starts at 0 until you set it
(`Load("Desaturate", ("Amount", 1f))`); the library sets sensible values.
The demos no longer have their own shader keys (Mega Man, Big Cave, Bitmap Text): use the EFFECTS menu.

## Quad tree
`FlxQuadTree` (port of Flixel's) finds overlapping pairs without comparing everything with everything.
```csharp
var tree = new FlxQuadTree(0, 0, FlxG.Width, FlxG.Height, divisions: 3);
tree.Add(bullets, FlxQuadTree.AList);      // objects or whole groups
tree.Add(enemies, FlxQuadTree.BList);      // optional second list
tree.Overlap(bothLists: true, (bullet, enemy) => { enemy.Kill(); return true; });   // false = A against itself
```
`FlxG.Collide` / `FlxG.Overlap` use it automatically when `objectsA * objectsB` is above `FlxG.QuadTreeMinPairs` (400);
turn it off with `FlxG.UseQuadTree = false`. `tree.ForEachNode(...)` exposes the cells for debug drawing.
Demo: launcher button 4 / key 4. UP/DOWN change the object count, Q compares with brute force, G toggles the grid, LEFT/RIGHT change divisions.

## Procedural level demos (launcher buttons 5 and 6, 640x480)
* **Cave gen** (`Demo/CaveDemoState.cs`, engine class `Flixel/FlxCaveGenerator.cs`): cellular-automata caves, auto-tiled, with a platformer
  spaceman. Drag the sliders (smoothing iterations, wall ratio), **R** regenerates, arrows/WASD + Space to move.
  `FlxCaveGenerator.GenerateCaveString(cols, rows, smoothing, wallRatio)` goes straight into `FlxTilemap.LoadMapFromCsv`
  (or use `GenerateCaveArray` with `LoadMapFromArray`). It uses `FlxG.Random`, so seeding makes caves repeatable.
* **BSP map** (`Demo/BspGenerateState.cs`, `BspPlayState.cs`, `BspLeaf.cs`): the BSPMapGen demo. **G** generates rooms + hallways,
  **Space** plays the map with a top-down player, **Space** again goes back. ESC returns to the launcher.
* Assets and licenses: `Assets/cave/`, `Assets/bsp/` (HaxeFlixel demos, MIT). `DemoSlider` stands in for flixel-addons' FlxSlider.

## Substates, tweens, bars, saves, math and pathfinding (launcher: ENGINE and PATHFIND)
**Substates** (`FlxSubState`): `OpenSubState(new PauseSubState())` from a state, `Close()` from the substate. While open the state below is
not updated (set `PersistentUpdate = true` to keep it running) but is still drawn (`PersistentDraw`). `BgColor` dims what is below.
Open/close take effect at the start of the next update, like Flixel. Tweens and timers belong to the game, so freeze them with `FlxG.TimeScale = 0`.

**Tweens** (`FlxTween`, `FlxEase`): `FlxTween.Tween(sprite, new { X = 200, Alpha = 0.5f }, 1f, new TweenOptions { Ease = FlxEase.BounceOut, Type = FlxTweenType.PingPong })`.
Member names go in an anonymous object (any case), a nested object reaches into structs (`new { Scale = new { X = 2f } }`), or pass a dictionary with
paths (`["Scale.X"] = 2f`). Also `Num`, `Color`, `Angle`, `Shake`, and the motion tweens `LinearMotion`, `QuadMotion`, `CubicMotion`, `CircularMotion`,
`LinearPath`, `QuadPath`. Types: OneShot, Persist, Looping, PingPong, Backward; options: StartDelay, LoopDelay, OnStart/OnUpdate/OnComplete.
`FlxTween.CancelTweensOf(obj)`, `CompleteTweensOf`, `IsTweening`. All 37 easings are in `FlxEase`.

**FlxBar**: `new FlxBar(x, y, FlxBarFillDirection.LeftToRight, 100, 8, () => hp, 0, 100, showBorder: true).CreateFilledBar(back, fill, true, border)`;
`CreateGradientBar`, `CreateImageBar`, `TrackParent(obj, dx, dy)`, `OnEmpty` / `OnFilled`, 8 fill directions. Set `Value` yourself or give it a function.

**FlxSave**: `FlxG.Save.Get("highScore", 0)`, `FlxG.Save.Set("highScore", 99)`, `Flush()` (also runs when the game exits). JSON file in
`%AppData%/MonoFlixel/<window title>.json`; any value System.Text.Json can handle. `new FlxSave().Bind("name", folder)` for extra files.

**Math and helpers**: `FlxMath` (Bound, Lerp, SmoothLerp, Wrap, Remap, RoundDecimal, Distance...), `FlxAngle`, `FlxColor` (FromHsb, ToHsb, FromString,
ToHex, Gradient...), `FlxVelocity` (MoveTowardsPoint/Object/Mouse, AccelerateTowards..., DistanceBetween), `FlxSort` + `group.Sort(FlxSort.ByY)`,
`FlxPool<T>`, `FlxSignal` / `FlxG.Signals` (state switch, pre/post update and draw, resize), `FlxSpriteUtil` (ScreenWrap, Bound, FadeIn/Out, `FlxCanvas`
for drawing onto a sprite), `FlxGradient`, `FlxG.DrawLine`.

**Pathfinding**: `List<Vector2>? path = tilemap.FindPath(from, to, FlxDiagonalPolicy.Wide, FlxPathSimplify.LineOfSight)` is A* over the non-solid tiles
(`FlxPathfinder.FindCells` works on any grid and takes a per-cell cost). `enemy.FollowPath(path, speed, FlxPathMode.Yoyo)` moves the object's middle along it
(Forward, Backward, LoopForward, LoopBackward, Yoyo; `AutoRotate`, `OnNodeReached`, `OnComplete`); `path.DebugDraw(color)` shows it.
Demo keys: click to walk, D diagonals, S simplify, C chaser, R new cave.

## Transitions (launcher: TRANSITIONS, 640x480)
Port of flixel-addons' transitions (`Flixel/Transition/`). Derive a state from `FlxTransitionableState` (and call `base.Create()` first in your `Create()`):
```csharp
FlxTransitionableState.DefaultTransIn  = new TransitionData(TransitionType.Fade, Color.Black, 0.5f);
FlxTransitionableState.DefaultTransOut = new TransitionData(TransitionType.Tiles, Color.Black, 0.8f, new Vector2(1, 1),
                                             new TransitionTileData(FlxTransitionAssets.Circle, 32, 32));
FlxG.SwitchState(new GameState());      // GameState : FlxTransitionableState. The old state plays its OUT, the new one its IN.
```
* `TransitionType.Fade` (solid color, or a soft wipe when `Direction` is set; diagonal wipes use `diagonal_gradient.png`) and `TransitionType.Tiles`
  (a grid of animated tiles that ripples in the `Direction`; built-in `FlxTransitionAssets.Diamond/Circle/Square`, or your own strip of square frames
  from empty to full). `Direction` is -1/0/1 per axis; (0, 0) means everything at once.
* Per state: `new MyState(transIn, transOut)`, or set `TransIn` / `TransOut`. `FlxTransitionableState.SkipNextTransIn/Out` skip one.
* `TransitionData.CameraMode`: `Top` (default, the top camera), `New` (its own see-through camera) or `Default`; `Region` limits the covered area.
* How it works: the transition is a substate (`Transition`), so the state is paused while it plays. `FlxG.SwitchState` now asks the current state to
  `StartOutro(onComplete)` first and only switches when it calls `onComplete`; the default does that at once, so other states are unaffected.
  You can use the same hook for your own outros (a fade, an animation...).
* The demo ports the original Transitions demo: IN / OUT duration, type, tile, color and direction, and STATE A / STATE B to switch between.

## Bitmap fonts and text (launcher: BITMAP TEXT) and drawing after post-processing
**`FlxBitmapFont`** (`FlxFont` is the old name and still works) is a font made of pictures. Loaders:
`FlxBitmapFont.Default` (Nokia 8px), `FromBMFont(path)` (AngelCode .fnt in text or XML format, any number of page images, offsets and kerning pairs),
`FromMonospace(texture, letters, charWidth, charHeight, spacing, gap, region)` (a grid of equal cells) and `FromXna(texture, letters)`
(cells separated by lines of the top-left pixel's color, which becomes transparent). Fonts with only capitals map lower case to them.

**`FlxBitmapText`** draws text with any of those fonts: `new FlxBitmapText(x, y, "text", font, fieldWidth)` with `Alignment`
(Left / Center / Right / Justify), word wrap (over-long words are broken), `LetterSpacing`, `LineSpacing`, `TextScale`, kerning, `Tint`, `Angle`,
`Background`, and borders `Outline` / `OutlineFast` / `Shadow`. Layout runs as soon as a property changes, so `Width`, `Height`, `NumLines`
and `TextWidth` are always current. **`FlxText`** is now a thin subclass (Size 8 = the font's native size), so everything above works on it too;
set `Font` to give it another font.

**Drawing after post-processing:** with an active `FlxG.PostProcess` effect, every object with `DrawAfterPostProcess = true` (text and buttons by default,
change it with `FlxBitmapText.DefaultDrawAfterPostProcess`; works on any object or group) is drawn in a second pass on a transparent target and laid over
the shaded picture, so Wiggle / Bloom / Desaturate change the game but not the text. Without an active effect nothing changes: all objects are drawn in
their normal order. Camera fades and flashes only affect the scene pass. In the demo, E cycles the shader and T moves the text before or after it.

## Sprites, atlases, animation and blend modes (launcher: SPRITES)
**Frames and atlases.** Every sprite now draws from an `FlxFramesCollection` (grids, whole images, atlases). `FlxAtlasFrames` loads Sparrow / Starling XML
(`FromSparrow`), TexturePacker XML and JSON (hash and array layouts), Aseprite JSON (`FromAseprite`, keeps frame tags and durations) and SpriteSheetPacker text.
Trimmed frames work: the sprite measures the full untrimmed frame and the picture sits where it belongs, also when flipped or clipped. Rotated atlas frames
give a clear error (export without rotation). `sprite.LoadFrames(frames)`, `sprite.LoadAseprite(png, json)` (adds an animation per tag),
`sprite.LoadTiles(tex, w, h, spacing, margin, region)` and `FlxFramesCollection.FromTiles` for sheets with gaps and borders.

**Animation** (`sprite.Animation`): `Add`, `AddByPrefix("run", "run_")`, `AddByNames`, `AddByIndices`, `AddFromAtlas()`; `Play(name, force, reversed, startFrame)`, `Reverse()`,
`Stop()`, `Finish()`, `Paused`, `SpeedScale`; looping, `pingPong`, per-animation `flipX/flipY`, per-frame durations (Aseprite); callbacks `OnFrame(name, step, frame)`,
`OnLoop`, `OnFinish`.

**Drawing.** `sprite.Blend` = `Alpha`, `Add`, `Multiply`, `Screen`, `Subtract`, `Lighten`, `Darken` (works with groups, text, the post-process overlay pass).
`sprite.ClipRect` shows only part of the frame (progress bars, reveals). `LoadRotatedGraphic(path, 16)` bakes pre-rotated pictures that the sprite picks by `Angle`.
`FlxStrip` is a sprite made of textured triangles (`FlxStrip.CreateGrid(tex, cols, rows, w, h)`, then move `Vertices` each frame for waves and flags).
`FlxCanvas.Stamp(sprite, x, y)` copies a sprite's frame into another sprite. `FlxTileblock` is a solid block filled with random tiles from a sheet.

**Tilemaps.** `FlxAutoTiling.Full` is Flixel's 47-tile blob auto-tiling (needs a 47 frame tileset in its order; `Assets/atlas/full47.png` is a sample). The map edge counts as filled.

## Big cave demo and FlxBackdrop (launcher: BIG CAVE)
`Demo/BigCaveDemoState.cs`: a 400 x 140 tile cave (12000 x 4200 pixels) made with `FlxCaveGenerator`, tiled with `Assets/rock_autotile.png` (30 pixel tiles, `FlxAutoTiling.Auto`),
the border closed and every sealed pocket filled in so only the biggest connected cavern stays open. The spaceman is drawn 3x bigger; A/D run, SPACE jumps and holding it in the air
fires a jetpack (a fuel `FlxBar` refills on the ground; the flame is an additive-blend glow sprite). M toggles the whole-cave map, R builds a new cave.

**`FlxBackdrop`** (port of flixel-addons') is a picture that repeats across the screen: `new FlxBackdrop(texture, repeatX, repeatY, spacingX, spacingY)`. Give each layer a
`ScrollFactor` (0 = fixed to the screen, 0.2 = far away and slow, 1 = moves with the world), add layers back to front before the world, and `AutoScroll` makes one drift by itself.
The demo builds five layers this way: a fixed gradient sky, drifting additive spores, and three procedurally drawn ridges that fade into mist.

## FlxNoise: Perlin, Simplex and sine noise (used by the BIG CAVE demo)
`new FlxNoise(seed)` makes smooth random values that change gradually from place to place; the same seed always gives the same noise (leave the seed out for a random one).
Everything returns about -1 to 1 (measured over millions of samples):
```csharp
var noise = new FlxNoise(1234);
float a = noise.Perlin(x * 0.05f, y * 0.05f);          // 1D, 2D and 3D versions (use the third value as time)
float b = noise.Simplex(x * 0.05f, y * 0.05f, time);   // smoother and more even in every direction
float c = noise.Sine(x * 0.1f);                        // a few sine waves added up: soft, regular waves (1 to 8 of them)
float d = noise.Fractal(FlxNoiseType.Simplex, x * 0.02f, y * 0.02f, octaves: 5, mode: FlxFractalMode.Ridged);   // layered detail
```
Multiply coordinates by a small "scale" to stretch features out (0.05 gives features about 20 units wide).
* **Layers:** `Fractal` adds octaves (each with `lacunarity` times the frequency and `gain` times the strength): `Fbm` (default), `Ridged` (sharp ridges) or `Turbulence`.
* **Maps and lines:** `GenerateMap(w, h, type, scale, octaves ...)` gives a grid of 0..1 values (for heightmaps, clouds, textures), `GenerateLine(count ...)` heights for a side-scrolling landscape.
* **Caves:** `noise.GenerateCave(cols, rows, type, scale, octaves, wallRatio, style, widen)` returns 1 = wall / 0 = open for `FlxTilemap.LoadMapFromArray`, like `FlxCaveGenerator`.
  The wall share is exact (the threshold is picked from the values, so every seed is equally open). `Blobs` are rounded caverns; `Tunnels` follow the lines where the noise is zero
  (use a high `wallRatio` like 0.8 and `widen: 1`, which also joins diagonal gaps; `FlxNoise.Widen` works on any cave array).
* **Moving things:** `noise.Wander(time, frequency, amplitude, id)` is a smooth wandering offset that never repeats (different `id`s are different paths); `noise.Smooth(time, ...)` is a single smooth number for flicker, bobbing, swaying.

**BIG CAVE demo options:** **G** cycles the generator (cellular automaton, Perlin or Simplex caverns, Perlin or Simplex tunnels, sine waves), **L** switches between Large (400 x 140 tiles) and Huge
(800 x 280 tiles = 24000 x 8400 pixels), **R** builds a new cave with the same settings. The fireflies drift on `FlxNoise.Wander` and pulse on sine noise.

### FlxNoise additions: fractal Brownian motion and random walk
* **`FlxNoiseType.Fbm`** is now a noise type like the others (`Get`, `GenerateMap`, `GenerateCave`, ... take it). It layers `FbmBase` (Simplex, Perlin or Sine) over `FbmOctaves` octaves with `FbmLacunarity` and
  `FbmGain`, in `FbmMode` (smooth, Ridged, Turbulence). **`FbmWarp`** adds domain warping (2D and 3D): the place being sampled is pushed by two more layers of the same noise, giving swirls, folds and veins.
  Octaves are shifted against each other, so layered Perlin no longer has flat spots on the grid.
* **Random walk:** `noise.RandomWalk(t, id)` is a walk of random steps as a function of time (-1 to 1, 256 steps long, bouncing back at the ends): it drifts and can linger high or low, unlike Perlin, which keeps returning to the
  average. `noise.WalkWander(time, frequency, amplitude, id)` is two walks as an offset. `GenerateWalkLine(count, persistence)` makes a 0..1 skyline, and `GenerateWalkPath(steps, stepLength, persistence)` a 2D Brownian path
  (feed it to `FlxObject.FollowPath`).
* **`GenerateWalkCave(cols, rows, openRatio, persistence, brush, branchEvery)`** is a drunkard's-walk cave: one walker steers with a smoothly changing heading and opens every cell it passes, jumping back to a random
  opened spot every `branchEvery` steps. The cave is always one connected piece. `persistence` 0.8 gives caverns, 0.95 long winding tunnels; `brush` is the radius of the hole.

**BIG CAVE demo:** the generator list (**G**) now also has Fractal Brownian motion, Domain warped fBm, Random walk caverns and Random walk tunnels. **F** switches the fireflies between simplex `Wander` and a random walk.
Shaders come from the global EFFECTS menu; the HUD text is drawn after them, so it stays sharp.

## FlxBloom: bloom at window resolution
The single-pass `Bloom` shader runs on the low-resolution game screen, so its glow is built from big pixels. **`FlxBloom`** (`Flixel/FlxBloom.cs`, shaders `BloomBright.fx` and `BloomBlur.fx`) works on the *window*:
the pixel-art scene is scaled up with sharp pixels as before, while the glow is cut from the bright parts, blurred with a separable gaussian on a texture of the window's size divided by `Downsample`
(each of the `Passes` blur levels is wider than the last, and all levels are added, giving a tight core and a wide halo), and laid on top.
```csharp
FlxG.PostProcess.Set(FlxBloom.Create(threshold: 0.5f, intensity: 1.3f));
// also: spread (tap distance in glow pixels, doubles per pass), passes (default 3), downsample (default 2; 4 = very soft and cheap)
```
Threshold is judged on the brightest colour channel and the glow keeps the hue of the highlight. Put it last in the chain (effects before it run at game-screen size). Multi-pass effects are possible because
`FlxPostProcess` is no longer sealed: override `Draw(batch, source, dest)`, and restore the render target you were given (see FlxBloom). The BIG CAVE, MEGA MAN and BITMAP TEXT demos use it.

## Sound (launcher: SOUND)
`FlxG.Sound` (`FlxSoundFrontEnd`), `FlxSound` and `FlxSoundGroup` are ports of Flixel's. Sound files are **.wav** (PCM 8/16/24/32 bit or float, mono or stereo), **.mp3** (decoded into memory with NLayer, so about 10 MB per minute) and **.ogg** (Vorbis, decoded with NVorbis: short files
are decoded once, long ones such as songs are streamed while they play). Names are relative to `Assets` and the extension may be left out (`"sound/hit"`).
```csharp
FlxG.Sound.Play("sound/hit.ogg", onComplete: () => score++);          // fire and forget; recycled when it ends
FlxG.Sound.PlayMusic("sound/mode.ogg", 0.7f);                         // loops, replaces the old music, survives state switches
var engine = FlxG.Sound.Load("sound/jet.ogg", 0.9f, looped: true)
                       .Proximity(x, y, player, radius: 300);          // quieter and panned with distance to the player
engine.Play(); engine.FadeOut(2f); engine.Pitch = 1.5f; engine.Time = 4000;   // fades, pitch (0.5-2), seeking (milliseconds)
engine.LoopTime = 2000; engine.Play(true, startTime: 0, endTime: 5000);        // loop points and an end time
FlxG.Sound.DefaultMusicGroup.Volume = 0.5f;                            // groups share a volume (make your own FlxSoundGroup too)
```
* **How it works:** MonoGame's `SoundEffect` can't seek, loop between points or report a position, so every sound feeds a streaming voice (`DynamicSoundEffectInstance`) a little ahead of time. That gives
  `Time` (get and set), `Length`, `LoopTime`, `EndTime`, `LoopUntil`, `LoopCount`, `Pause/Resume/Stop`, and an `OnComplete` that (like Flixel's) also fires at every loop. With no audio device, or with
  `FlxG.Sound.Silent = true`, sounds play on a silent virtual voice with the same timing, so callbacks still work.
* **Persistence:** like Flixel, sounds stop on a state switch unless `Persist` is set. Music and one-shot effects from `Play` persist; looped sounds and ones you `Load` don't. `FlxG.Sound.Destroy(true)` stops everything.
* **Master volume:** `FlxG.Sound.Volume`, `Muted`, `OnVolumeChange`, `SoundCurve`. The keys + - (and the numpad ones) change the volume and 0 mutes, with an on-screen tray (`FlxG.Sound.Tray`,
  drawn above everything, also above post-processing); change `VolumeUpKeys / VolumeDownKeys / MuteKeys`, or switch them off with `VolumeKeysEnabled`. (The launcher no longer uses 0 as a shortcut because of this.)
  Sounds pause while the window is in the background (`AutoPause`).
* **Limits:** only WAV and OGG (convert MP3/ADPCM files), pitch is limited to an octave either way by the sound card, and there is no `amplitude` / peak meter or URL loading.

## Input (launcher: INPUT, pages F1-F5)

Everything lives under `Flixel/Input/`. All sources are replaceable (`FlxG.Keys.Source`, `FlxG.Mouse.Source`, `FlxG.Gamepads.Source`, `FlxG.Touches.Source`), which is how the tests feed in fake input.

- **Keyboard** (`FlxG.Keys`): `Pressed / JustPressed / JustReleased(Keys)`, `Any*`, `PressedKeys`, `FirstJustPressed` ("press the key to bind"), name lookups (`Pressed("ONE")`), `TypedText` and a `TextInput` event for text fields, `Enabled`. `FlxKey.Name / Parse` turn keys into names and back.
- **Mouse** (`FlxG.Mouse`, a `FlxPointer`): `Left / Middle / Right` buttons (each with `JustDoubleClicked` and `ClickCount`), `Delta`, `Wheel`, camera aware `GetWorldPosition(camera)`, `Overlaps(object)`. The older `Pressed / RightPressed / WheelDelta...` members still work.
- **FlxMouseEvent**: `FlxMouseEvent.Add(sprite, onDown, onUp, onOver, onOut)` plus `SetClickCallback`, `SetDoubleClickCallback`, `SetMoveCallback`, `SetWheelCallback`. Only the top-most object under the mouse gets events (`SetPriority`, `Reorder`), sprites are hit by their pixels (`pixelPerfect: false` for the box), groups work with `mouseChildren: true`. Registrations are cleared when the state changes.
- **Touch** (`FlxG.Touches`): fingers as `FlxTouch` pointers, `Swipes` finished this frame (direction, distance, speed), and `SimulateWithMouse` so you can try it on a desktop.
- **Gamepads** (`FlxG.Gamepads[0..3]`, `FlxG.Gamepad` = the first connected one, never null): buttons by position (`FlxGamepadInputID.A`) or MonoGame `Buttons`; sticks with `DeadZone` / `DeadZoneMode` (positive Y is down); sticks and triggers also work as buttons; `Model` and `GetInputLabel(id)` give "Cross" on a PlayStation pad and "B" for the bottom button of a Switch pad; `Rumble(seconds, left, right)`; `DeviceConnected / DeviceDisconnected` signals.
- **Actions** (`FlxG.Actions`): name what the player does and bind it to anything.

```csharp
var jump = new FlxActionDigital("jump", a => player.Jump()).AddKey(Keys.Space).AddGamepad(FlxGamepadInputID.A);
var move = new FlxActionAnalog("move").AddKeys(Keys.W, Keys.S, Keys.A, Keys.D).AddGamepad(FlxGamepadInputID.LeftAnalogStick);
var set = new FlxActionSet("player", jump, move);
FlxG.Actions.Activate(set);                           // or Activate(set, FlxInputDevice.Gamepad, 1) for player two's pad
player.Velocity = move.Value * 100;                   // jump.Triggered is true on the frames it fires

FlxG.Save.Set("bindings", FlxG.Actions.ExportBindings());   // rebinds as JSON ...
FlxG.Actions.ApplyBindings(FlxG.Save.Get<string>("bindings"));   // ... and back, matched by set and action name
```

Sets are removed when the state changes (`FlxG.Actions.ResetOnStateSwitch` can keep or deactivate them instead). Not ported: virtual pad and analog widgets, accelerometer, FlxInputText.

## DROWN (launcher: DROWN, 640x480)

A port of the old XFlixel game "Drown" (`D:\PROJECTS\XFlixel\XFlixel\XFlixel\Drown`), in `Demo/Drown/`, with its art and sounds in `Assets/drown/`. Title screen (the original main menu: the logo rises and bounces into place over ten seconds to the title theme, with the water wobble; Z / ENTER / pad A starts, N twice erases the save), a map of islands to sail around, and dives: mine rock, harpoon creatures, collect shards, craft lanterns, and watch the water get darker as you go down.

**Controls.** Title: Z, ENTER, SPACE or A start. Sea: WASD / arrows / left stick to sail, 1 2 3 choose Shallows / Deep / Abyss, ENTER (Z, pad A) to dive. Dive: A D move, SPACE or W hold to swim up, TAB lets air into the suit to float (costs air), mouse aims (right stick on a pad), left button fires, 1 harpoon, 2 mining laser, Z suit drill, I inventory, L put down a lantern, R go up (near the top), P water wobble and glow on/off, N night vision.

**What changed from the original** (it used a class for every kind of thing and its own map files):
- **One site per place on the map.** A dive site comes from a seed made of its position and depth, so the same place gives the same site. Only what you changed (mined tiles, things taken, lanterns) is saved, in `FlxG.Save`, instead of map and XML files. `X` on the sea forgets a site.
- **Caves.** The shallows use a seeded cellular automaton (what FlxCaveGenerator does); Deep and Abyss use `FlxNoise` fractal Brownian motion (warped in the abyss). Only the cavern reachable from the surface is kept.
- **One class per idea**: `Deco` (kelp, coral and the three resource crystals), `Mob` (predator, shark, jellyfish) with one `MobSpawner`, `Pickup`, `Harpoon`; items are a table (`DrownData.Items`) and crafting is data (`DrownData.Recipes`). Mobs far from the player are switched off.
- **Controls are `FlxActionSet` actions** (`DrownInput`), so keyboard, mouse and gamepad all work.
- **The laser** is a ray (`FlxTilemap.Ray`) that ends on the first rock, instead of a fixed short stub.
- **The inventory** windows are sprites using `FlxMouseEvent` for hover and clicks (click moves an item, shift moves a stack, right click throws one away; with the crafting window open, clicking moves items to the components and the result row crafts).
- **Darkness** is `DarknessLayer`: a half-size picture multiplied over the world (`FlxBlend.Multiply`) with the diver's light and the lanterns cut out of it, replacing the lighting shader and its render targets. The water wobble and a glow (Wiggle and Bloom, **P**) and **N** night vision are on the dive screen's own effect stack, on top of the player's global effects; the HUD is drawn after them.
- **Bubbles** come from one pool (`BubblePool`) in many sizes: a steady stream from the diver's helmet (more when moving, a big burst when the suit lets air out), a burst and a trail from every harpoon, and from the geysers. Bigger bubbles rise faster.
- **Bubble geysers**: a few vents in the floor (3 in the shallows, 4 deep, 6 in the abyss, spread out, made from the site's seed). Standing in the pale column of bubbles refills your air (30 per second); each is a light in the dark, and the rock under a vent cannot be mined.
- Sharks now drop Territe (they dropped Magmite like the jellyfish), so every creature feeds a different component.
- The mouse cursor (and an item carried on it) is drawn last, above everything: text, windows and the other overlay, and it is not changed by post-process effects. (It used to be drawn with the scene.)
- `FlxAssets.GetTexture` (and so `LoadGraphic` and `LoadParticles`) now accepts a path below the Assets folder.

Not ported: the unused swarm/boid fish and the refraction shader (its texture was not in the content folder).

## POST APOC (launcher: POST APOC, 640x360)

A port of the old XNA/Flixel project `PostApoc` (`D:\PROJECTS\PostApoc\...`), in `Demo/PostApoc/` with its art, sounds and maps in `Assets/postapoc/`. A survivor walks right along a road through rooms of city and seaside (the 80x10 tile CSV maps of the original, 32 pixel tiles, a backdrop that scrolls at 0.2), picks up food, drink and firewood, shoots bandits and dogs, and has to eat and drink.

**Controls.** Arrows / WASD or the left stick walk (up and down too: the road has depth), **Z** / Space / pad X fires the pistol; with no bullets left it is a **melee swing** instead (the fourth row of the sheet: it hits whatever is just in front, in your lane) (the pistol has limited ammo: 10 to start, and it counts in the status strip), **I** the bag (a 4x4 grid), **U** the equipment, **P** saves, **ESC** closes windows, then goes back to the menu. In a window: click an item to pick it up (it becomes the cursor), click a slot to put it down or swap, the arrow slot drops it on the road, right click uses it. The five **hotbar** slots at the bottom right are real slots: drag food or drink onto them (or into the first five slots of the equipment window), then press **1-5** to use them. A hit (a bullet, a bite, a blow) sprays a little blood. Walk off the right end of a room and the next one starts.

**Survival.** Four meters: health, hunger, thirst and **fatigue**. All grow with every step (a room is 2560 pixels: about 20 hunger, 30 thirst and 30 fatigue per room, plus a little hunger and thirst with time), so you have to eat and drink every few rooms. Full hunger or thirst hurts; **full fatigue kills** ("you collapsed from exhaustion"). **Open doors** (the black ones, only in the city) are the other way to rest: walk up to the top edge of the road in front of one and press **UP**; the survivor slips inside for a second (nothing can hurt them) and comes out 25 less tired. Each door is good for one rest, and most of the time (70%) there are 4-6 bullets inside. **Both ways of resting refill your health.** Using **firewood** (right click in the bag, or from a quick slot) **makes camp**: a fire, the screen darkens for four seconds, fatigue drops by 55 and you are a bit hungrier and thirstier. It is refused (and the firewood kept) if an enemy is near. Firewood is the most common find; food and drink are rare.

**The end.** When the survivor dies the road closes over with dark red diamond tiles (the engine's tiles transition) and the game over screen comes in: "You Failed to Survive.", how it ended, then, counted up one line at a time, **how far you travelled** (a room is a mile), **how many food items you ate, how many times you drank, how many enemies you killed and how many times you rested** (camps and doorways), and the **score**: 100 per mile, 25 per kill, 10 per rest, 5 per meal or drink. The best score is kept (it survives new games) and a new one flashes. ENTER / Z skips the counting, then starts again; ESC goes to the menu. The numbers are saved with the run, so Continue keeps counting.

**Enemies.** Bandits (melee: they notice you from far off and run you down, much faster than the shooters), dogs (fast, melee) and **raiders** (the other survivor's sprite in dark clothes) who keep their distance, line up with you and shoot: step out of their lane (up or down the road) to make them miss. Bandits (40% of the time) and raiders (70%) drop three bullets when they die, and a rare box lies about: about 12 bullets a room, so pick your fights (dogs drop none).

**What changed from the original** (it was an unfinished prototype):
- **Things now work.** Bullets travel along their lane and hurt what they hit (two bring a bandit or a dog down), bandits and dogs hit back after a short warning, the survivor gets hungry and thirsty over time (and is hurt when either is full), food and drink can be used, and dying ends the run. The original only played the effects of an attack.
- **One class per idea:** `PostApocEnemy` is both the bandit and the dog (the originals differed only in numbers and pictures); items are a table (`PostApocData`, replacing items.xml and a class per item); `SlotWindow` is both the bag and the equipment window (the original had two copies with 16 and 12 hand-written buttons each) and its slots are sprites clicked through `FlxMouseEvent`; the controls are `FlxActionSet` actions (keyboard and gamepad).
- **Saving** is `FlxG.Save` (JSON) instead of hand-written XML in `save0.dat`; the original wrote it and read it back only in part. Continue loads the room count, stats and every item.
- **Y sorting** is a small `YSortedGroup` (the original's `GameLayer` + `YComparer`); the survivor and the enemies have footprint hitboxes so whoever stands lower on the road is drawn in front.
- The equipment window keeps the original's weapon and armor slots, but nothing fits in them yet; the five quick-use slots take food and drink.
- Walking is a little faster than the original (80 instead of 50 pixels a second); things are a spawn roll per spawner tile (mostly nothing; about 1 in 14 an item, 1 in 11 a bandit, 1 in 11 a dog) instead of a random pick from every item in items.xml, which also contained placeholder entries. Firewood is common, food and drink are rare (a few per room).

## FlxTypeText (typewriter text)

`FlxTypeText` (port of flixel-addons' FlxTypeText) is an `FlxText` that types itself: `Start(delay, forceRestart, autoErase, skipKeys, callback)`, `Erase(...)`, `ResetText(...)`, `Skip()`, `SetTypingVariation(...)`, plus `ShowCursor` / `CursorCharacter`, `Prefix`, `AutoErase` / `WaitTime`, `Sounds` or `UseDefaultSound` (a tick made in code), `SkipKeys`, `SkipOnClick` and `CompleteCallback` / `EraseCallback`. The final text is wrapped first (`FlxBitmapText.WrapText`), so a word never jumps to the next line while it is typed. Typed text is left aligned (put the box where the finished line is centered). Not ported: `applyMarkup`. `FlxG.Sound.Load` also takes an `FlxAudioClip` now.
Used by: the launcher subtitle, the Bitmap Text demo (R retypes, SPACE skips), the PostApoc tagline, game over title and messages, and Drown's HUD messages.

## Roguelike toolkit (`Flixel/Roguelike/`) and the ROGUELIKE demo (launcher: ROGUELIKE, 640x360)

- `FlxGridMap` (implements `IFlxGrid`): per cell transparent / walkable / in view / explored flags. `FromWalls(data, w, h)`, `FromTilemap`, `ComputeFov(x, y, radius, algorithm, lightWalls, shape, permissiveLevel)`, `IsInFov`, `IsExplored`, `ExploreAll`, `FindPath(...)`, `HasLineOfSight`.
- `FlxFov` (static): `Basic` (rays), `Shadowcast` (recursive, libtcod style), `SymmetricShadowcast` (Albert Ford: if A sees B, B sees A) and `Permissive` (levels 0-4: a cell is seen if any part of it is). Shapes: Circle, Square, Diamond.
- `FlxBresenham`: `Line`, `LineList`, `Cast` (stops at a blocker), `HasLineOfSight`, `Trace`, `Circle`, `FilledCircle`. `FlxGridUtil`: neighbours, cells in radius, borders, Manhattan / Chebyshev / Euclidean distance.
- `FlxDice`: `FlxDice.Roll("3d6+2")`, `Parse`, `Min`, `Max`, `Average`.
- `FlxDijkstraMap`: distance maps with many goals, `maxDistance`, per cell cost, `NextStep(x, y, isBlocked)`, `PathFrom`, `CreateFleeMap()`. One map serves every monster.
- `FlxBspGenerator.Generate(w, h, minLeaf, maxLeaf, minRoom, seed)` returns an `FlxBspMap` (`Data`, `Rooms`, `ToGridMap()`); the same seed makes the same dungeon.
- `FlxPathfinder` and tilemap `FindPath` take an `FlxPathAlgorithm`: `AStar`, `Dijkstra`, `BreadthFirst`, `GreedyBestFirst`; `Reachable(...)` too.

Demo: turn based. Move with arrows / WASD / numpad / Q E Z C (diagonals), SPACE or `.` waits, bump to attack (dice), walk onto potions (!) and the blue stairs. Rats and ogres follow a Dijkstra map, goblins use A*. Keys: F cycle FOV algorithm, `[` `]` radius, T light walls, M BSP or cave level, V show the distance map, X reveal map, R new level / restart, click to travel along the A* route, ESC back.

## Building an installer

`installer/build-installer.ps1 -Version 0.1.0` (needs the .NET 8 SDK and Inno Setup 6) publishes a self-contained win-x64 build, so the .NET runtime is bundled, and writes `dist/MonoFlixel-Setup-<version>.exe`.
