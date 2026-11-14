using System.Buffers.Binary;
using System.Numerics;
using EQVisualizer;

static void Check(bool condition,string message){if(!condition)throw new Exception(message);Console.WriteLine("PASS "+message);}
static byte[] Tone(int bits,bool floating,float frequency,int channels=2)
{
    var bytes=new byte[8192*channels*(bits/8)];
    for(int i=0;i<8192;i++)for(int c=0;c<channels;c++)
    {
        float v=c==channels-1?.5f*MathF.Sin(MathF.Tau*frequency*i/48000):0;
        var b=bytes.AsSpan((i*channels+c)*(bits/8),bits/8);
        if(floating)BinaryPrimitives.WriteInt32LittleEndian(b,BitConverter.SingleToInt32Bits(v));
        else if(bits==16)BinaryPrimitives.WriteInt16LittleEndian(b,(short)(v*32767));
        else if(bits==32)BinaryPrimitives.WriteInt32LittleEndian(b,(int)(v*2147483647));
        else{int n=(int)(v*8388607);b[0]=(byte)n;b[1]=(byte)(n>>8);b[2]=(byte)(n>>16);}
    }
    return bytes;
}
var peaks=new List<int>();
Check(WallpaperRecovery.Choose("eq.png", "eq.png", null, ["eq.png", "missing.png", "original.png"], p => p != "missing.png") == "original.png", "Recover previous wallpaper from history if backup is missing");
Check(WallpaperRecovery.Choose("user.png", "eq.png", "original.png", ["original.png"], _ => true) is null, "Preserve a wallpaper changed by the user");
Check(WallpaperRecovery.Choose("eq.png", "eq.png", "original.png", ["other.png"], _ => true) == "original.png", "Prefer saved wallpaper over history");
Check(WallpaperRecovery.Choose("eq.png", "eq.png", "", ["other.png"], _ => true) == "", "Restore an explicitly saved solid background");
Check(MediaSourceIdentity.Matches("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!SpotifyLauncher", "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify"), "Spotify keeps its selection when the player publishes a launcher identity");
Check(!MediaSourceIdentity.Matches("AnotherPackage!Spotify", "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify"), "A different package cannot replace the selected music player");
Check(!IdleMode.Clock.ShowTrack(PlaybackActivity.Paused, pauseGrace: true), "Pausing immediately shows the idle scene");
Check(!IdleMode.Clock.ShowTrack(PlaybackActivity.Paused, pauseGrace: false), "A retained paused session gives way to the clock");
Check(IdleMode.Clock.ShowTrack(PlaybackActivity.Playing, pauseGrace: false), "Resume replaces the clock with the track");
Check(!IdleMode.Clock.ShowTrack(PlaybackActivity.Stopped, pauseGrace: true), "Stop shows the clock immediately");
Check(IdleMode.Off.ShowTrack(PlaybackActivity.Paused, pauseGrace: false), "Pause still keeps the cover when the idle scene is off");
foreach(var format in new[]{(16,false),(24,false),(32,false),(32,true)})
{
    var a=new AudioAnalyzer();var tone=Tone(format.Item1,format.Item2,1000);
    int split=320*(format.Item1/8)*2;
    for(int i=0;i<tone.Length;i+=split)a.Push(tone.AsSpan(i,Math.Min(split,tone.Length-i)),2,format.Item1,format.Item2,48000);
    var s=a.Spectrum;int peak=Array.IndexOf(s,s.Max());peaks.Add(peak);
    Check(s.Max()>.2f && s.All(v=>float.IsFinite(v)&&v>=0&&v<=1),$"{format} stereo right-channel tone survives packet boundaries");
    a.Reset();a.Push(new byte[16384],2,16,false,48000);Check(a.Spectrum.All(v=>v==0),"Silence clears the spectrum");
}
Check(peaks.Max()-peaks.Min()<=1,"PCM and float formats agree on frequency");
var antiphase = new AudioAnalyzer();
var opposite = new byte[8192 * 2 * 2];
for (int i = 0; i < 8192; i++)
{
    short value = (short)(.7f * MathF.Sin(MathF.Tau * 1000 * i / 48000) * short.MaxValue);
    BinaryPrimitives.WriteInt16LittleEndian(opposite.AsSpan((i * 2) * 2, 2), value);
    BinaryPrimitives.WriteInt16LittleEndian(opposite.AsSpan((i * 2 + 1) * 2, 2), (short)-value);
}
antiphase.Push(opposite, 2, 16, false, 48000);
Check(antiphase.Spectrum.Max() > .2f, "Antiphase stereo energy remains visible");
int antiphasePeak = Array.IndexOf(antiphase.Spectrum, antiphase.Spectrum.Max());
Check(Math.Abs(antiphasePeak - peaks[0]) <= 1,
    "Antiphase stereo keeps the fundamental frequency without rectifying it");

var low=new AudioAnalyzer();low.Push(Tone(32,true,100),2,32,true,48000);
var high=new AudioAnalyzer();high.Push(Tone(32,true,6000),2,32,true,48000);
Check(Array.IndexOf(low.Spectrum,low.Spectrum.Max())<Array.IndexOf(high.Spectrum,high.Spectrum.Max()),"Frequency bands increase with pitch");
var scene=new VisualScene();
scene.Build(new(1366, 768, 0, new(.5f, .44f), .33f, .7f, 1, false,
    false, true, .92f, 3, new(1, .25f, .15f, 1), new(.7f, .3f, .1f, 1), .75f, true),
    Enumerable.Repeat(1f,64).ToArray(), []);
var discreet = scene.Vertices.ToArray();
var tightRing = new VisualScene();
tightRing.Build(new(1366,768,0,new(.5f,.44f),.4f,3,5,false,true,false,.92f,3,
    new(1,.25f,.15f,1),new(.7f,.3f,.1f,1),.9f,true),Enumerable.Repeat(1f,64).ToArray(),[]);
var ringVertices=tightRing.Vertices.ToArray();
Check(ringVertices.Count(v=>v.Textured==4)==6,"Circle mode crops the main artwork to a circle");
Vector2 ToPixel(Vector2 p)=>new((p.X+1)*1366/2,(1-p.Y)*768/2);
var ringStart=(ToPixel(ringVertices[24].Position)+ToPixel(ringVertices[25].Position))/2;
var ringEnd=(ToPixel(ringVertices[26].Position)+ToPixel(ringVertices[29].Position))/2;
Check(MathF.Abs(Vector2.Distance(ringStart,new(683,768*.44f))-(768*.4f/2+5))<.1f,"Circular bars leave a 5px gap to the artwork");
Check(Vector2.Distance(ringStart,ringEnd)<24,"Default circular bars stay close to the artwork even at 300% strength");
tightRing.Build(new(1366,768,0,new(.5f,.44f),.4f,1,5,false,true,false,.92f,3,
    new(1,.25f,.15f,1),new(.7f,.3f,.1f,1),.9f,true),Enumerable.Repeat(1f,64).ToArray(),[]);
var normalRing = tightRing.Vertices.ToArray();
var normalEnd = (ToPixel(normalRing[26].Position) + ToPixel(normalRing[29].Position)) / 2;
Check(Vector2.Distance(ringStart,ringEnd) > Vector2.Distance(ringStart,normalEnd),
    "The intensity slider still affects circular bars above 100%");
for (int i=18;i<18+64*12;i+=12)
{
    float height = Math.Abs(discreet[i].Position.Y - discreet[i+2].Position.Y)*768/2;
    Check(height<=32,"Discreet strip stays below 32px at maximum signal");
    Check(discreet[i+6].Color != discreet[i+8].Color,"Bars retain a vertical gradient from the cover palette");
}
foreach(var resolution in new[]{(1280,720),(1920,1080),(3840,2160)})
foreach(var phase in new[]{0f,.3f,1f,2000f})
{
    var o=new SceneOptions(resolution.Item1,resolution.Item2,phase,new(.5f,.48f),.36f,1,1,true,true,true,.94f,8,Vector4.One,Vector4.One);
    scene.Build(o,high.Spectrum,[new(100,100,80,80)]);
    Check(scene.Vertices.Length%6==0 && scene.Vertices.Length<16384 && scene.Vertices.ToArray().All(v=>float.IsFinite(v.Position.X)&&float.IsFinite(v.Position.Y)),"Bounded finite geometry "+resolution+" phase "+phase);
}
var baseOptions=new SceneOptions(1280,720,0,new(.5f,.48f),.36f,1,1,false,false,false,.94f,8,Vector4.One,Vector4.One);
scene.Build(baseOptions with { Tunnel = true }, [], []);
var tunnel = scene.Vertices.ToArray();
Check(tunnel.Count(v => v.Textured == 1) == 6, "Tunnel leaves a single artwork quad");
scene.Build(baseOptions with { Tunnel = true, Phase = .45f }, [], []);
Check(!tunnel.Select(v => v.Position).SequenceEqual(scene.Vertices.ToArray().Select(v => v.Position)),
    "Tunnel perspective moves with phase");
scene.Build(baseOptions,high.Spectrum,[]);
int baseVertices=scene.Vertices.Length;
var bothStyles = baseOptions with { Ring = true, Bottom = true };
float[] fullSpectrum = Enumerable.Repeat(1f, 64).ToArray();
RunBarGeometryChecks();
(float Bottom, float Circle) MeasureBars(SceneOptions options)
{
    scene.Build(options, fullSpectrum, []);
    var bars = scene.Vertices[baseVertices..];
    return (MathF.Abs(bars[6].Position.Y - bars[8].Position.Y),
        MathF.Abs(bars[18].Position.Y - bars[20].Position.Y));
}
var regular = MeasureBars(bothStyles);
var tallerCircle = MeasureBars(bothStyles with { CircleBarHeight = 2 });
var tallerBottom = MeasureBars(bothStyles with { BottomBarHeight = 2 });
Check(tallerCircle.Circle > regular.Circle && tallerCircle.Bottom == regular.Bottom,
    "Circle height leaves the bottom strip unchanged");
Check(tallerBottom.Bottom > regular.Bottom && tallerBottom.Circle == regular.Circle,
    "Bottom height leaves the circle unchanged");
foreach(var mode in VisualizerModes.All)
{
    scene.Build(baseOptions with{Ring=mode.HasCircle(),Bottom=mode.HasBottom()},high.Spectrum,[]);
    int expected=baseVertices+64*12*(mode==VisualizerMode.Both?2:1);
    Check(scene.Vertices.Length==expected,"Independent visualizer geometry: "+mode.Label());
    var bars=scene.Vertices[baseVertices..].ToArray();
    if(mode==VisualizerMode.Bottom)Check(bars.All(v=>v.Position.Y<-.45f),"Bottom-only mode contains no circle bars");
    if(mode==VisualizerMode.Circle)Check(bars.All(v=>MathF.Abs(v.Position.X)<.46f),"Circle-only mode contains no wide taskbar strip");
    Check(System.Text.Json.JsonSerializer.Deserialize<VisualizerMode>(((int)mode).ToString())==mode,"Existing numeric settings remain compatible");
}
scene.Build(baseOptions with{Ring=true,BarThickness=.5f},high.Spectrum,[]);
float thin=Vector2.Distance(scene.Vertices[baseVertices+6].Position,scene.Vertices[baseVertices+7].Position);
int count=scene.Vertices.Length;
scene.Build(baseOptions with{Ring=true,BarThickness=3},high.Spectrum,[]);
float thick=Vector2.Distance(scene.Vertices[baseVertices+6].Position,scene.Vertices[baseVertices+7].Position);
Check(thick>thin*5.9f && scene.Vertices.Length==count,"Bar thickness changes width without increasing geometry cost");
scene.Build(baseOptions with{ShowTrackInfo=true},high.Spectrum,[]);
Check(scene.Vertices.Length==baseVertices+6 && scene.Vertices[^1].Textured==2,"Optional track information uses its own texture quad");
scene.Build(baseOptions with{ShowTrackInfo=false},high.Spectrum,[]);
Check(scene.Vertices.ToArray().All(v=>v.Textured!=2),"Track information is removed completely when disabled");
scene.BuildIdle(1280,720,.36f,new(.5f,.5f),false,true);
Check(scene.Vertices.ToArray().Count(v=>v.Textured==2)==6 && scene.Vertices.ToArray().All(v=>v.Textured!=1 && v.Textured!=3),"Clock-only fallback has no artwork or audio bars");
scene.BuildIdle(1280,720,.36f,new(.5f,.5f),true,false);
Check(scene.Vertices.ToArray().Count(v=>v.Textured==1)==6 && scene.Vertices.ToArray().All(v=>v.Textured!=2),"Image-only fallback has no clock or audio bars");
scene.BuildIdle(1280,720,.36f,new(.5f,.5f),true,true);
Check(scene.Vertices.ToArray().Count(v=>v.Textured==1)==6 && scene.Vertices.ToArray().Count(v=>v.Textured==2)==6,"Combined fallback renders image and clock");
Check(IdleMode.ImageAndClock.HasImage() && IdleMode.ImageAndClock.HasClock() && !IdleMode.Off.HasClock(),"Fallback choices remain independent");
scene.BuildIdle(960,540,.36f,new(.5f,.48f),false,true,IdleClockStyle.Analog);
var analogClock=scene.Vertices.ToArray().Where(v=>v.Textured==2).ToArray();
Check(analogClock.Length==6 && MathF.Abs((analogClock.Max(v=>v.Position.X)-analogClock.Min(v=>v.Position.X))*960/2 -
    (analogClock.Max(v=>v.Position.Y)-analogClock.Min(v=>v.Position.Y))*540/2)<2,"Analog clock keeps a round aspect ratio");
foreach(var theme in Enum.GetValues<InterfaceTheme>())
{
    var palette=InterfacePalettes.For(theme);
    Check(InterfacePalettes.Contrast(palette.Text,palette.Surface)>=7 &&
        InterfacePalettes.Contrast(palette.Muted,palette.Surface)>=4.5 &&
        InterfacePalettes.Contrast(palette.Accent,palette.Surface)>=4.5 &&
        InterfacePalettes.Contrast(InterfacePalettes.TextOnAccent(palette.Accent),palette.Accent)>=4.5,
        "Readable interface palette: "+theme);
}
Check(InterfacePalettes.Contrast(InterfacePalettes.EnsureAccent(System.Drawing.Color.FromArgb(15,15,15),InterfacePalettes.For(InterfaceTheme.Carbon).Surface),InterfacePalettes.For(InterfaceTheme.Carbon).Surface)>=4.5,
    "Custom dark accent is corrected on a dark theme");
Check(InterfacePalettes.Contrast(InterfacePalettes.EnsureAccent(System.Drawing.Color.FromArgb(250,250,250),InterfacePalettes.For(InterfaceTheme.Pearl).Surface),InterfacePalettes.For(InterfaceTheme.Pearl).Surface)>=4.5,
    "Custom light accent is corrected on a light theme");
var warm=ArtworkPalette.Create([new(.8f,.7f,.6f),new(.7f,.6f,.5f)]);
Check(warm.Primary.X>warm.Primary.Z && warm.Primary.X>=.95f,"Warm cover keeps a bright warm spectrum");
var blue=ArtworkPalette.Create([new(.1f,.2f,.7f),new(.2f,.3f,.8f)]);
Check(blue.Primary.Z>blue.Primary.X && blue.Primary.Z>=.95f,"Blue cover keeps a bright blue spectrum");
var neutral=ArtworkPalette.Create([new(.3f,.3f,.3f)]);
Check(MathF.Abs(neutral.Primary.X-neutral.Primary.Y)<.1f,"Monochrome artwork keeps neutral contrast colors");
byte[] solid=Enumerable.Repeat((byte)128,9*9*4).ToArray();BackdropBlur.Apply(solid,9,9,3);
Check(solid.All(v=>v==128),"Blur preserves constant colors and handles clamped edges");
byte[] impulse=new byte[9*9*4];impulse[(4*9+4)*4]=255;
var original=impulse.ToArray();BackdropBlur.Apply(impulse,9,9,0);Check(impulse.SequenceEqual(original),"Zero blur leaves pixels unchanged");
BackdropBlur.Apply(impulse,9,9,1);
Check(impulse[(4*9+4)*4]<255 && impulse.Where((v,i)=>i%4==0).Count(v=>v>0)>1,"Blur spreads detail rather than replacing the image");
Console.WriteLine("All core checks passed.");

void RunBarGeometryChecks()
{
    const int sceneStart = 18;
    var failures = new List<string>();
    void Expect(bool condition, string message)
    {
        if (condition) Check(true, message);
        else
        {
            failures.Add(message);
            Console.Error.WriteLine("FAIL " + message);
        }
    }
    GpuVertex[] Build(SceneOptions options, float signal = 1)
    {
        scene.Build(options, Enumerable.Repeat(signal, 64).ToArray(), []);
        return scene.Vertices.ToArray();
    }
    Vector2 Pixel(GpuVertex v) => new((v.Position.X + 1) * 640, (1 - v.Position.Y) * 360);
    float BarLength(GpuVertex[] vertices, int first) => Vector2.Distance(Pixel(vertices[first + 6]), Pixel(vertices[first + 11]));
    var strip = baseOptions with { Bottom = true };
    var circle = baseOptions with { Ring = true };

    var heights = new[] { 1f, 2f, 4f, 6f }
        .Select(height => BarLength(Build(strip with { BottomBarHeight = height }), sceneStart)).ToArray();
    Expect(heights[0] < 32 && heights[1] >= 40 && heights[2] >= heights[1] + 35 && heights[3] >= heights[2] + 60,
        $"Bottom height grows clearly at 100%, 200%, 400% and 600% ({string.Join(", ", heights.Select(h => h.ToString("F1")))}px)");
    var highStrip = Build(strip with { BottomBarHeight = 6, Intensity = 3, BottomY = .2f })[sceneStart..];
    Expect(highStrip.All(v => MathF.Abs(v.Position.Y) <= 1.00001f),
        "The tallest bottom strip and its outline stay inside the viewport near a high anchor");

    foreach (var options in new[] { strip, circle })
    {
        var lengths = new[] { 0f, 1f, 2f, 3f }
            .Select(strength => BarLength(Build(options with { Intensity = strength }, .8f), sceneStart)).ToArray();
        string mode = options.Ring ? "Circle" : "Bottom";
        Expect(lengths[0] <= 1.01f && lengths[1] > 8 && lengths[2] > lengths[1] + 3 && lengths[3] > lengths[2] + 3,
            $"{mode} strength remains visibly responsive through 300% ({string.Join(", ", lengths.Select(h => h.ToString("F1")))}px)");
        var upper = options with { Intensity = 3, Sensitivity = 5 };
        Expect(BarLength(Build(upper, .9f), sceneStart) > BarLength(Build(upper, .6f), sceneStart) + 1,
            $"{mode} keeps loud bands distinct at high strength and sensitivity");
    }

    var lowArt = Build(circle with { Center = new(.5f, .88f), ShowTrackInfo = true, CircleBarHeight = 3, Intensity = 3 });
    var lowCaption = lowArt[^6..];
    var lowCircle = lowArt[sceneStart..^6];
    Expect(lowCaption.All(v => v.Textured == 2 && MathF.Abs(v.Position.Y) <= 1.00001f) &&
        lowCaption.Max(v => Pixel(v).Y) + 2 < lowCircle.Min(v => Pixel(v).Y),
        "Track information fits above a low circle with clearance for the full outline");

    const int nextBar = sceneStart + 12;
    float Thickness(GpuVertex[] vertices, int first) => Vector2.Distance(Pixel(vertices[first + 6]), Pixel(vertices[first + 7]));
    var square = new BarAppearance(1, 1.6f, 1, false);
    var rounded = new BarAppearance(2, 3, .4f, true);
    var defaultBars = Build(bothStyles);
    Expect(defaultBars.SequenceEqual(Build(bothStyles with { CircleAppearance = square, BottomAppearance = square })),
        "Omitted appearances fall back to legacy heights, thickness, full opacity and square bars");
    foreach (bool changeCircle in new[] { false, true })
    {
        string mode = changeCircle ? "Circle" : "Bottom";
        int changed = changeCircle ? nextBar : sceneStart;
        int other = changeCircle ? sceneStart : nextBar;
        SceneOptions WithStyle(BarAppearance style) => changeCircle
            ? bothStyles with { CircleAppearance = style }
            : bothStyles with { BottomAppearance = style };
        var styled = Build(WithStyle(rounded));
        Expect(styled[other..(other + 12)].SequenceEqual(defaultBars[other..(other + 12)]),
            $"{mode} appearance leaves the other style unchanged");
        Expect(BarLength(styled, changed) > BarLength(defaultBars, changed) &&
            Thickness(styled, changed) > Thickness(defaultBars, changed),
            $"{mode} appearance controls both height and thickness");
        Expect(styled.Length == defaultBars.Length &&
            styled[changed..(changed + 12)].All(v => v.Textured > 5) &&
            defaultBars[changed..(changed + 12)].All(v => v.Textured == 0),
            $"{mode} sends rounded fill and outline to the renderer with the same geometry cost as square bars");
        Expect(MathF.Abs(styled[changed].Color.W - .1f) < .00001f &&
            MathF.Abs(styled[changed + 6].Color.W - defaultBars[changed + 6].Color.W * .4f) < .00001f &&
            MathF.Abs(styled[changed + 8].Color.W - defaultBars[changed + 8].Color.W * .4f) < .00001f &&
            styled[changed + 6].Color != styled[changed + 8].Color,
            $"{mode} opacity scales the outline and both gradient endpoints");
        var visibleAlone = changeCircle
            ? defaultBars[..sceneStart].Concat(Build(strip)[sceneStart..]).ToArray()
            : Build(circle);
        Expect(Build(WithStyle(square with { Opacity = 0 })).SequenceEqual(visibleAlone),
            $"{mode} at zero opacity emits neither fill nor outline");
    }

    foreach (var (setting, expectedInset) in new[] { (.25f, 480f), (.52f, 307.2f), (1f, 0f) })
    {
        var stripVertices = Build(strip with { BottomWidth = setting })[sceneStart..];
        float left = stripVertices.Min(v => Pixel(v).X), right = stripVertices.Max(v => Pixel(v).X);
        Expect(left >= expectedInset - .001f && left <= expectedInset + 1280 * setting / 64 &&
            MathF.Abs(left + right - 1280) < .001f,
            $"Bottom strip is centered at {setting:P0} width and reaches within one bar pitch of its edges");
    }
    Expect(Build(strip with { BottomWidth = 0 }).SequenceEqual(Build(strip with { BottomWidth = .25f })) &&
        Build(strip with { BottomWidth = 2 }).SequenceEqual(Build(strip with { BottomWidth = 1 })),
        "Bottom width is bounded from 25% to 100%");
    Expect(Build(bothStyles with { BottomWidth = 1 })[nextBar..(nextBar + 12)]
        .SequenceEqual(defaultBars[nextBar..(nextBar + 12)]), "Bottom width leaves circle geometry unchanged");

    var extreme = square with { Height = 20, Thickness = 20, Opacity = 20 };
    Expect(Build(strip with { BottomAppearance = extreme }).SequenceEqual(
        Build(strip with { BottomAppearance = new(6, 3, 1, false) })) &&
        Build(circle with { CircleAppearance = extreme }).SequenceEqual(
        Build(circle with { CircleAppearance = new(3, 3, 1, false) })),
        "Circle and bottom appearance values clamp to their own limits");
    var hiddenRingCaption = Build(circle with { ShowTrackInfo = true, CircleAppearance = square with { Opacity = 0 } })[^6..];
    Expect(hiddenRingCaption.SequenceEqual(Build(baseOptions with { ShowTrackInfo = true })[^6..]),
        "An invisible circle reserves no space below the artwork for the caption");

    if (failures.Count != 0) throw new Exception($"{failures.Count} bar geometry checks failed.");
}
