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
var low=new AudioAnalyzer();low.Push(Tone(32,true,100),2,32,true,48000);
var high=new AudioAnalyzer();high.Push(Tone(32,true,6000),2,32,true,48000);
Check(Array.IndexOf(low.Spectrum,low.Spectrum.Max())<Array.IndexOf(high.Spectrum,high.Spectrum.Max()),"Frequency bands increase with pitch");
var scene=new VisualScene();
foreach(var resolution in new[]{(1280,720),(1920,1080),(3840,2160)})
foreach(var phase in new[]{0f,.3f,1f,2000f})
{
    var o=new SceneOptions(resolution.Item1,resolution.Item2,phase,new(.5f,.48f),.36f,1,1,true,true,true,.94f,8,Vector4.One,Vector4.One);
    scene.Build(o,high.Spectrum,[new(100,100,80,80)]);
    Check(scene.Vertices.Length%6==0 && scene.Vertices.Length<16384 && scene.Vertices.ToArray().All(v=>float.IsFinite(v.Position.X)&&float.IsFinite(v.Position.Y)),"Bounded finite geometry "+resolution+" phase "+phase);
}
Console.WriteLine("All core checks passed.");
var baseOptions=new SceneOptions(1280,720,0,new(.5f,.48f),.36f,1,1,false,false,false,.94f,8,Vector4.One,Vector4.One);
scene.Build(baseOptions,high.Spectrum,[]);
int baseVertices=scene.Vertices.Length;
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
