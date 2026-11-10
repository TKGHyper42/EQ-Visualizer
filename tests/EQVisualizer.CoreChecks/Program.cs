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
    int expected=baseVertices+64*6*(mode==VisualizerMode.Both?2:1);
    Check(scene.Vertices.Length==expected,"Independent visualizer geometry: "+mode.Label());
    var bars=scene.Vertices[baseVertices..].ToArray();
    if(mode==VisualizerMode.Bottom)Check(bars.All(v=>v.Position.Y<-.45f),"Bottom-only mode contains no circle bars");
    if(mode==VisualizerMode.Circle)Check(bars.All(v=>MathF.Abs(v.Position.X)<.46f),"Circle-only mode contains no wide taskbar strip");
    Check(System.Text.Json.JsonSerializer.Deserialize<VisualizerMode>(((int)mode).ToString())==mode,"Existing numeric settings remain compatible");
}
