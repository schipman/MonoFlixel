using MonoFlixel;
using MonoFlixel.Demo;

// The launcher runs at 640x360 shown at 2x (1280x720 window).
// Each demo can switch the game's resolution when it starts (Mode: 320x240 at 2x, Mega Man: 480x270 at 2x).
using var game = new FlxGame(640, 360, () => new LauncherState(), fullscreen:true, zoom: 2, scaleMode: FlxScaleMode.Integer, framerate: 60, title: "MonoFlixel");
game.Run();
