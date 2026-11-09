using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Drawing2D;

static class Native {
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X,Y; public POINT(int x,int y){X=x;Y=y;} }
    [StructLayout(LayoutKind.Sequential)] public struct SIZE { public int X,Y; public SIZE(int x,int y){X=x;Y=y;} }
    [StructLayout(LayoutKind.Sequential, Pack=1)] public struct BLEND { public byte Op,Flags,Alpha,Format; }
    [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr h,IntPtr dc);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr dc,IntPtr obj);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] public static extern bool StretchBlt(IntPtr dst,int x,int y,int w,int h,IntPtr src,int sx,int sy,int sw,int sh,uint rop);
    [DllImport("gdi32.dll")] public static extern int SetStretchBltMode(IntPtr dc,int mode);
    [DllImport("user32.dll",SetLastError=true)] public static extern bool UpdateLayeredWindow(IntPtr h,IntPtr dst,ref POINT p,ref SIZE size,IntPtr src,ref POINT sp,int key,ref BLEND blend,int flags);
    [DllImport("user32.dll")] public static extern bool SetWindowDisplayAffinity(IntPtr h,uint value);
    [DllImport("user32.dll")] public static extern bool GetWindowDisplayAffinity(IntPtr h,out uint value);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int ht,uint flags);
    [DllImport("user32.dll",SetLastError=true)] public static extern bool RegisterHotKey(IntPtr h,int id,uint mods,uint key);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h,int id);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr ctx);
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h,int index);
    [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h,int command);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr LoadCursor(IntPtr module,int id);
    [DllImport("user32.dll")] public static extern IntPtr CopyIcon(IntPtr icon);
    [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr icon);
    [DllImport("user32.dll",SetLastError=true)] public static extern bool SetSystemCursor(IntPtr cursor,uint id);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] public static extern IntPtr LoadCursorFromFile(string file);
    [StructLayout(LayoutKind.Sequential)] public struct RECT {public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h,out RECT rect);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,out uint process);
    [StructLayout(LayoutKind.Sequential)] public struct CURSORINFO {public int Size;public int Flags;public IntPtr Cursor;public POINT Position;}
    [DllImport("user32.dll")] public static extern bool GetCursorInfo(ref CURSORINFO info);
    [DllImport("user32.dll")] public static extern bool DrawIconEx(IntPtr dc,int x,int y,IntPtr icon,int width,int height,uint step,IntPtr brush,uint flags);
}

sealed class Layer : Form {
    public bool Excluded;
    public int ShowTransitions;
    int lastX=int.MinValue,lastY=int.MinValue;
    public Layer(){ FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false; StartPosition=FormStartPosition.Manual; Text="Halo · lumière"; }
    protected override bool ShowWithoutActivation {get{return true;}}
    protected override CreateParams CreateParams {get{var p=base.CreateParams; p.ExStyle |= 0x80000|0x20|0x80|0x08000000|0x8; return p;}}
    protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e); Excluded=Native.SetWindowDisplayAffinity(Handle,0x11);}
    protected override void WndProc(ref Message m){if(m.Msg==0x84){m.Result=new IntPtr(-1);return;} if(m.Msg==0x21){m.Result=new IntPtr(3);return;}base.WndProc(ref m);}
    public void Draw(Bitmap b,int x,int y){
        IntPtr screen=Native.GetDC(IntPtr.Zero), dc=Native.CreateCompatibleDC(screen), hb=IntPtr.Zero, old=IntPtr.Zero;
        try {
            hb=b.GetHbitmap(Color.FromArgb(0)); old=Native.SelectObject(dc,hb);
            var p=new Native.POINT(x,y); var sp=new Native.POINT(0,0);var size=new Native.SIZE(b.Width,b.Height);
            var blend=new Native.BLEND{Alpha=255,Format=1};
            if(!Native.UpdateLayeredWindow(Handle,screen,ref p,ref size,dc,ref sp,0,ref blend,2)) throw new System.ComponentModel.Win32Exception();
            if(!Visible){Show();Native.ShowWindow(Handle,4);Native.SetWindowPos(Handle,new IntPtr(-1),x,y,b.Width,b.Height,0x50);ShowTransitions++;}
            lastX=x;lastY=y;
        } finally {if(old!=IntPtr.Zero)Native.SelectObject(dc,old);if(hb!=IntPtr.Zero)Native.DeleteObject(hb);Native.DeleteDC(dc);Native.ReleaseDC(IntPtr.Zero,screen);}
    }
    public void MoveTo(int x,int y){if(x==lastX&&y==lastY)return;Native.SetWindowPos(Handle,IntPtr.Zero,x,y,0,0,0x15);lastX=x;lastY=y;}
}

static class Colors {
    public static Color Mix(Color a,Color b,double t){return Color.FromArgb((int)(a.R+(b.R-a.R)*t),(int)(a.G+(b.G-a.G)*t),(int)(a.B+(b.B-a.B)*t));}
    public static Color Dominant(Bitmap b,Rectangle r){return Dominant(b,r,Color.FromArgb(55,120,245));}
    public static Color Dominant(Bitmap b,Rectangle r,Color fallback){
        // Prefer chromatic content over browser chrome. Use hue bins so opposing colors do not turn gray.
        double[] weight=new double[12],rr=new double[12],gg=new double[12],bb=new double[12];
        double neutral=0;int count=0;
        for(int y=r.Top;y<r.Bottom;y+=2)for(int x=r.Left;x<r.Right;x+=2){
            Color c=b.GetPixel(x,y);double max=Math.Max(c.R,Math.Max(c.G,c.B))/255.0;
            double min=Math.Min(c.R,Math.Min(c.G,c.B))/255.0;double sat=max==0?0:(max-min)/max;
            neutral+=max;count++;if(sat<.16||max<.12)continue;
            int bin=Math.Min(11,(int)(c.GetHue()/30));double w=sat*sat*max;
            weight[bin]+=w;rr[bin]+=c.R*w;gg[bin]+=c.G*w;bb[bin]+=c.B*w;
        }
        int best=0;for(int i=1;i<12;i++)if(weight[i]>weight[best])best=i;
        if(weight[best]<.25){double light=neutral/Math.Max(1,count);if(light<.08)return Color.Black;return fallback;}
        double red=rr[best]/weight[best],green=gg[best]/weight[best],blue=bb[best]/weight[best];
        double gain=Math.Min(2.4,235/Math.Max(red,Math.Max(green,blue)));
        return Color.FromArgb((int)(red*gain),(int)(green*gain),(int)(blue*gain));
    }
    public static double Brightness(Bitmap b,Rectangle r){double total=0;int n=0;for(int y=r.Top;y<r.Bottom;y+=3)for(int x=r.Left;x<r.Right;x+=3){Color c=b.GetPixel(x,y);total+=c.R*.2126+c.G*.7152+c.B*.0722;n++;}return total/(Math.Max(1,n)*255);}
    public static Color Contrast(Color c,double brightness){
        if(c.R+c.G+c.B==0)return c;
        double t=Math.Max(0,Math.Min(1,(brightness-.35)/.45));
        double min=Math.Min(c.R,Math.Min(c.G,c.B));
        double r=c.R-min*.7*t,g=c.G-min*.7*t,b=c.B-min*.7*t;
        double max=Math.Max(r,Math.Max(g,b));if(max<1)return c;
        double gain=(235-65*t)/max;
        return Color.FromArgb((int)(r*gain),(int)(g*gain),(int)(b*gain));
    }
    public static Rectangle ActiveContent(Rectangle screen){
        Rectangle area=Rectangle.Intersect(Screen.FromRectangle(screen).WorkingArea,screen);
        IntPtr foreground=Native.GetForegroundWindow();uint process;Native.GetWindowThreadProcessId(foreground,out process);Native.RECT rect;
        if(process!=(uint)Process.GetCurrentProcess().Id&&Native.GetWindowRect(foreground,out rect)){
            Rectangle active=Rectangle.Intersect(screen,Rectangle.FromLTRB(rect.Left,rect.Top,rect.Right,rect.Bottom));
            if(active.Width>160&&active.Height>120)area=active;
        }
        // Screen pixels are sampled once; map the active application into that small image.
        int x=Math.Max(0,(area.X-screen.X)*192/screen.Width),y=Math.Max(0,(area.Y-screen.Y)*108/screen.Height);
        int right=Math.Min(192,(area.Right-screen.X)*192/screen.Width),bottom=Math.Min(108,(area.Bottom-screen.Y)*108/screen.Height);
        int insetX=Math.Max(1,(right-x)/35),insetY=Math.Max(1,(bottom-y)/25);
        return Rectangle.FromLTRB(x+insetX,y+insetY,Math.Max(x+insetX+1,right-insetX),Math.Max(y+insetY+1,bottom-insetY));
    }
    public static Bitmap Capture(Rectangle r){
        Bitmap b=new Bitmap(192,108,PixelFormat.Format32bppArgb);
        using(Graphics g=Graphics.FromImage(b)){
            IntPtr dst=g.GetHdc(),src=Native.GetDC(IntPtr.Zero);
            // SRCCOPY only: CAPTUREBLT can disrupt the hardware pointer during each sample.
            try{Native.SetStretchBltMode(dst,4);if(!Native.StretchBlt(dst,0,0,b.Width,b.Height,src,r.X,r.Y,r.Width,r.Height,0x00CC0020))throw new System.ComponentModel.Win32Exception();}
            finally{Native.ReleaseDC(IntPtr.Zero,src);g.ReleaseHdc(dst);}
        }return b;
    }
    public static Bitmap Pixels(int w,int h,Func<int,int,Color> pixel){
        Bitmap b=new Bitmap(w,h,PixelFormat.Format32bppPArgb);var data=b.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,b.PixelFormat);
        try{byte[] bytes=new byte[data.Stride*h];for(int y=0;y<h;y++)for(int x=0;x<w;x++){
            Color c=pixel(x,y);int i=y*data.Stride+x*4;bytes[i]=(byte)(c.B*c.A/255);bytes[i+1]=(byte)(c.G*c.A/255);bytes[i+2]=(byte)(c.R*c.A/255);bytes[i+3]=c.A;
        }Marshal.Copy(bytes,0,data.Scan0,bytes.Length);}finally{b.UnlockBits(data);}return b;
    }
    public static Bitmap Edge(int w,int h,int side,Color[] palette,double strength){
        int depth=(side<2?h:w),length=(side<2?w:h);Color[] along=new Color[length];
        for(int i=0;i<length;i++){double pos=(double)i/Math.Max(1,length-1)*(palette.Length-1);int k=Math.Min(palette.Length-2,(int)pos);along[i]=Mix(palette[k],palette[k+1],pos-k);}
        return Pixels(w,h,delegate(int x,int y){int d=side==0?y:side==1?h-1-y:side==2?x:w-1-x;if(side<2)d=Math.Min(d,Math.Min(x,w-1-x));double f=1-(double)d/(depth-1);
            int alpha=(int)Math.Min(255,(d<5?255:255*Math.Pow(f,.92))*strength);return Color.FromArgb(alpha,along[side<2?x:y]);});
    }
}

sealed class PointerTheme : IDisposable {
    IntPtr original;bool enabled;string file;Process guard;public int ApplyCount;
    int style=0,intensity=85,size=100;Color tint=Color.FromArgb(75,95,245);DateTime applied=DateTime.MinValue;
    public void Configure(int nextStyle,Color color,int nextIntensity,int nextSize,bool immediate){
        bool geometry=style!=nextStyle||intensity!=nextIntensity||size!=nextSize;
        int delta=Math.Max(Math.Abs(tint.R-color.R),Math.Max(Math.Abs(tint.G-color.G),Math.Abs(tint.B-color.B)));
        if(!geometry&&delta==0)return;
        if(!immediate&&!geometry&&(delta<24||(DateTime.UtcNow-applied).TotalMilliseconds<400))return;
        if(enabled){IntPtr h=PointerArt.CreateCursor(nextStyle,color,nextIntensity,nextSize);if(!Native.SetSystemCursor(h,32512))throw new System.ComponentModel.Win32Exception();ApplyCount++;applied=DateTime.UtcNow;}
        style=nextStyle;tint=color;intensity=nextIntensity;size=nextSize;
    }
    public static Bitmap DrawPointer(){return PointerArt.Render(0,Color.FromArgb(75,95,245),85,100);}
    public static void WriteCursor(string file){using(var b=DrawPointer())using(var writer=new BinaryWriter(File.Create(file))){
        int maskStride=((b.Width+31)/32)*4, imageBytes=40+b.Width*b.Height*4+maskStride*b.Height;
        writer.Write((ushort)0);writer.Write((ushort)2);writer.Write((ushort)1);
        writer.Write((byte)b.Width);writer.Write((byte)b.Height);writer.Write((byte)0);writer.Write((byte)0);
        writer.Write((ushort)17);writer.Write((ushort)14);writer.Write(imageBytes);writer.Write(22);
        writer.Write(40);writer.Write(b.Width);writer.Write(b.Height*2);writer.Write((ushort)1);writer.Write((ushort)32);
        writer.Write(0);writer.Write(b.Width*b.Height*4+maskStride*b.Height);writer.Write(0);writer.Write(0);writer.Write(0);writer.Write(0);
        for(int y=b.Height-1;y>=0;y--)for(int x=0;x<b.Width;x++){Color c=b.GetPixel(x,y);writer.Write(c.B);writer.Write(c.G);writer.Write(c.R);writer.Write(c.A);}
        for(int y=b.Height-1;y>=0;y--){byte[] mask=new byte[maskStride];for(int x=0;x<b.Width;x++)if(b.GetPixel(x,y).A==0)mask[x/8]|=(byte)(0x80>>(x%8));writer.Write(mask);}
    }}
    public PointerTheme(){
        original=Native.CopyIcon(Native.LoadCursor(IntPtr.Zero,32512));
        file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Pointeur-Halo.cur");WriteCursor(file);
        string ready="Local\\HaloPointerGuard_"+Process.GetCurrentProcess().Id;
        using(var signal=new EventWaitHandle(false,EventResetMode.ManualReset,ready)){
            guard=Process.Start(new ProcessStartInfo(Application.ExecutablePath,"--cursor-guard "+Process.GetCurrentProcess().Id+" "+ready){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});
            if(!signal.WaitOne(5000))throw new Exception("La protection de restauration du pointeur ne répond pas.");
        }
    }
    public void SetEnabled(bool value){if(enabled==value)return;IntPtr h=value?PointerArt.CreateCursor(style,tint,intensity,size):Native.CopyIcon(original);
        if(h==IntPtr.Zero||!Native.SetSystemCursor(h,32512))throw new System.ComponentModel.Win32Exception();enabled=value;ApplyCount++;
    }
    public void Dispose(){SetEnabled(false);if(original!=IntPtr.Zero)Native.DestroyIcon(original);if(guard!=null)guard.Dispose();}
    public static void Guard(int parent,string ready){
        IntPtr saved=Native.CopyIcon(Native.LoadCursor(IntPtr.Zero,32512));
        try{using(var p=Process.GetProcessById(parent)){using(var signal=EventWaitHandle.OpenExisting(ready))signal.Set();p.WaitForExit();}}
        catch(ArgumentException){}finally{if(saved!=IntPtr.Zero)Native.SetSystemCursor(saved,32512);}
    }
}

sealed class SceneDynamics {
    Color[] previous;double motion;int hold;public double Boost;
    public void Update(Bitmap image,Rectangle region,bool force){
        Color[] pixels=new Color[24*14];int changed=0,colorful=0;
        for(int y=0;y<14;y++)for(int x=0;x<24;x++){
            int i=y*24+x;Color c=image.GetPixel(region.X+x*(region.Width-1)/23,region.Y+y*(region.Height-1)/13);pixels[i]=c;
            int hi=Math.Max(c.R,Math.Max(c.G,c.B)),lo=Math.Min(c.R,Math.Min(c.G,c.B));if(hi-lo>30&&hi>55)colorful++;
            if(previous!=null){Color p=previous[i];if(Math.Abs(c.R-p.R)+Math.Abs(c.G-p.G)+Math.Abs(c.B-p.B)>75)changed++;}
        }
        double chroma=colorful/(double)pixels.Length;motion=motion*.65+changed/(double)pixels.Length*.35;
        if(motion>.13&&chroma>.09)hold=40;else if(hold>0)hold--;
        double target=force||hold>0?1:0;Boost+=(target-Boost)*(target>Boost?.2:.035);previous=pixels;
    }
}

sealed class MonitorGlow : IDisposable {
    public Rectangle Bounds;public Layer[] Edges=new Layer[4];public Color Global=Color.FromArgb(75,145,240);
    public double Lightness;public Bitmap PreviewFrame;
    SceneDynamics scene=new SceneDynamics();public double VideoBoost {get{return scene.Boost;}}
    Color[][] palettes=new Color[4][];
    public MonitorGlow(Rectangle r){Bounds=r;for(int s=0;s<4;s++){Edges[s]=new Layer();palettes[s]=new Color[9];for(int i=0;i<9;i++)palettes[s][i]=Global;}}
    public void Update(bool enabled,double strength,bool forceVideo,bool autoColor=true,Color fixedColor=default(Color),double width=1){
        using(Bitmap small=Colors.Capture(Bounds)){
            if(PreviewFrame!=null)PreviewFrame.Dispose();PreviewFrame=(Bitmap)small.Clone();
            Rectangle content=Colors.ActiveContent(Bounds);
            Color main=autoColor?Colors.Dominant(small,content):fixedColor;Lightness=Lightness*.65+Colors.Brightness(small,content)*.35;
            scene.Update(small,content,forceVideo);
            Global=autoColor?Colors.Mix(Global,Colors.Contrast(main,Lightness*(1-.35*VideoBoost)),.35):fixedColor;
            if(!enabled){foreach(var edge in Edges)edge.Hide();return;}
            for(int s=0;s<4;s++)for(int k=0;k<9;k++){
                int rw=Math.Max(1,content.Width*(s<2?25:40)/100),rh=Math.Max(1,content.Height*(s<2?40:30)/100);
                int x=content.X+(s<2?(content.Width-rw)*k/8:s==2?0:content.Width-rw);
                int y=content.Y+(s<2?(s==0?0:content.Height-rh):(content.Height-rh)*k/8);
                Rectangle region=new Rectangle(x,y,rw,rh);
                Color target=autoColor?Colors.Contrast(Colors.Mix(Colors.Dominant(small,region,main),main,.4),Lightness*(1-.35*VideoBoost)):fixedColor;
                palettes[s][k]=autoColor?Colors.Mix(palettes[s][k],target,.32):target;
            }
            palettes[0][0]=palettes[2][0]=Colors.Mix(palettes[0][0],palettes[2][0],.5);
            palettes[0][8]=palettes[3][0]=Colors.Mix(palettes[0][8],palettes[3][0],.5);
            palettes[1][0]=palettes[2][8]=Colors.Mix(palettes[1][0],palettes[2][8],.5);
            palettes[1][8]=palettes[3][8]=Colors.Mix(palettes[1][8],palettes[3][8],.5);
        }
        int baseDepth=Math.Max(68,Math.Min(118,Bounds.Height/11));
        int d=Math.Max(10,Math.Min(Bounds.Height/3,(int)((baseDepth+(Math.Min(160,Bounds.Height/8)-baseDepth)*VideoBoost)*width)));
        for(int s=0;s<4;s++){
            int w=s<2?Bounds.Width:d,h=s<2?d:Bounds.Height-2*d;
            int x=Bounds.X+(s==3?Bounds.Width-d:0),y=Bounds.Y+(s==1?Bounds.Height-d:s>=2?d:0);
            using(Bitmap b=Colors.Edge(w,h,s,palettes[s],strength*(1+VideoBoost*(.3+.2*Lightness))))Edges[s].Draw(b,x,y);
        }
    }
    public void Hide(){foreach(var e in Edges)e.Hide();}
    public void Dispose(){foreach(var e in Edges)e.Dispose();if(PreviewFrame!=null)PreviewFrame.Dispose();}
}

sealed class Hotkeys : NativeWindow,IDisposable {
    Action<int> action;public bool PauseRegistered,QuitRegistered;
    public int PauseError,QuitError;
    public Hotkeys(Action<int> a){action=a;CreateHandle(new CreateParams{Caption="Halo shortcuts",Parent=new IntPtr(-3)});PauseRegistered=Native.RegisterHotKey(Handle,1,0x4007,0x48);PauseError=Marshal.GetLastWin32Error();QuitRegistered=Native.RegisterHotKey(Handle,2,0x4007,0x51);QuitError=Marshal.GetLastWin32Error();}
    protected override void WndProc(ref Message m){if(m.Msg==0x312)action(m.WParam.ToInt32());base.WndProc(ref m);}
    public void Dispose(){Native.UnregisterHotKey(Handle,1);Native.UnregisterHotKey(Handle,2);DestroyHandle();}
}

static class Program {
    const string Instance="Local\\HaloAmbient_Je_20261108",Stop="Local\\HaloAmbientStop_Je_20261108";
    [STAThread] static void Main(string[] args){
        try{Native.SetProcessDpiAwarenessContext(new IntPtr(-4));}catch{Native.SetProcessDPIAware();}
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        if(args.Length>0&&args[0]=="--cursor-guard"){PointerTheme.Guard(int.Parse(args[1]),args[2]);return;}
        if(args.Length>0&&args[0]=="--stop"){try{using(var e=EventWaitHandle.OpenExisting(Stop))e.Set();}catch(WaitHandleCannotBeOpenedException){}return;}
        if(args.Length>0&&args[0]=="--self-test"){SelfTest(args[1]);return;}
        if(args.Length>0&&args[0]=="--update-check"){using(var updater=new AppUpdater()){var latest=updater.Latest().GetAwaiter().GetResult();File.WriteAllText(args[1],latest==null?"No published beta available":latest.Version+"\n"+latest.Url+"\n"+latest.Digest);}return;}
        bool created;using(var mutex=new Mutex(true,Instance,out created)){if(!created){try{using(var show=EventWaitHandle.OpenExisting("Local\\HaloStudioShow"))show.Set();}catch{}return;}using(var stop=new EventWaitHandle(false,EventResetMode.ManualReset,Stop))Application.Run(new HaloApp(stop));}
    }
    static void Check(bool ok,string name,List<string> log){log.Add((ok?"PASS ":"FAIL ")+name);if(!ok)throw new Exception(name);}
    static long PointerFingerprint(){using(var b=new Bitmap(48,48))using(var g=Graphics.FromImage(b)){
        g.Clear(Color.Fuchsia);IntPtr dc=g.GetHdc();try{if(!Native.DrawIconEx(dc,0,0,Native.LoadCursor(IntPtr.Zero,32512),48,48,0,IntPtr.Zero,3))throw new Exception("Pointer drawing failed");}finally{g.ReleaseHdc(dc);}
        long hash=17;unchecked{for(int y=0;y<48;y++)for(int x=0;x<48;x++)hash=hash*31+b.GetPixel(x,y).ToArgb();}return hash;
    }}
    static void SelfTest(string folder){
        Directory.CreateDirectory(folder);var log=new List<string>();
        try{
            using(var test=new Bitmap(192,108))using(var g=Graphics.FromImage(test)){
                g.Clear(Color.Red);Color red=Colors.Dominant(test,new Rectangle(0,0,192,108));Check(red.R>200&&red.B<10,"red content detection",log);
                g.Clear(Color.Blue);Color blue=Colors.Dominant(test,new Rectangle(0,0,192,108));Check(blue.B>200&&blue.R<10,"blue content detection",log);
                g.Clear(Color.FromArgb(25,25,25));g.FillRectangle(Brushes.LimeGreen,65,30,55,55);Color accent=Colors.Dominant(test,new Rectangle(0,0,192,108));Check(accent.G>accent.R*2,"website accent survives neutral chrome",log);
                g.Clear(Color.Black);Color black=Colors.Dominant(test,new Rectangle(0,0,192,108));Check(black.R==0&&black.G==0&&black.B==0,"black frame dims to black",log);
                g.Clear(Color.White);g.FillRectangle(Brushes.OrangeRed,60,30,55,40);Color lightAccent=Colors.Dominant(test,new Rectangle(0,0,192,108));Check(lightAccent.R>lightAccent.G*2&&lightAccent.B<30,"colored accent detected on a light page",log);
                Check(Colors.Brightness(test,new Rectangle(0,0,192,108))>.8,"light page brightness detected",log);
                Color light=Colors.Contrast(lightAccent,1),dark=Colors.Contrast(lightAccent,.1);Check(light.R<dark.R&&light.R>light.G*2,"light theme gets a deeper color while preserving hue",log);
                g.Clear(Color.White);Color inherited=Colors.Dominant(test,new Rectangle(0,0,192,108),Color.Red);Check(inherited.R==255&&inherited.B==0,"neutral page edges inherit the application's accent",log);
                using(var edge=Colors.Edge(240,64,0,new[]{light,light},1)){Color p=edge.GetPixel(120,16);double compositeBlue=(255-p.A)+p.B*p.A/255.0;Check(p.A>140&&compositeBlue<140,"broad halo remains visible against pure white",log);}
                var scene=new SceneDynamics();Rectangle all=new Rectangle(0,0,192,108);
                for(int i=0;i<20;i++){g.Clear(i%2==0?Color.Orange:Color.DodgerBlue);scene.Update(test,all,false);}
                Check(scene.Boost>.9,"moving colored frames progressively amplify the halo",log);
                g.Clear(Color.White);for(int i=0;i<8;i++)scene.Update(test,all,false);
                Check(scene.Boost>.9,"video amplification survives a bright white scene",log);
                for(int i=0;i<180;i++)scene.Update(test,all,false);
                Check(scene.Boost<.02,"static light page returns smoothly to page contrast",log);
                for(int i=0;i<25;i++)scene.Update(test,all,true);
                Check(scene.Boost>.99,"manual video amplification holds on a paused scene",log);
                using(var edge=Colors.Edge(240,120,0,new[]{light,light},1.5))Check(edge.GetPixel(120,35).A>180,"video amplification stays strong away from the edge",log);
            }
            using(var layer=new Layer())using(var glow=PointerArt.Render(0,Color.CornflowerBlue,100,100)){
                Point p=Cursor.Position;layer.Draw(glow,p.X-56,p.Y-56);Application.DoEvents();uint affinity;
                Check(Native.GetWindowDisplayAffinity(layer.Handle,out affinity)&&affinity==0x11,"overlay excluded from capture",log);
                int styles=Native.GetWindowLong(layer.Handle,-20);Check((styles&0x08080020)==0x08080020,"layered, click-through, no-activate styles",log);
                Check(Native.WindowFromPoint(new Native.POINT(p.X,p.Y))!=layer.Handle,"cursor target passes through overlay",log);
                Check(Native.IsWindowVisible(layer.Handle),"overlay window is actually visible",log);
                for(int i=0;i<45;i++){using(var sample=Colors.Capture(Screen.PrimaryScreen.Bounds)){}layer.Draw(glow,p.X-56,p.Y-56);layer.MoveTo(p.X-56,p.Y-56);Application.DoEvents();Thread.Sleep(16);}
                Check(layer.ShowTransitions==1,"45 capture and redraw cycles do not repeatedly show the pointer overlay",log);
            }
            using(var keys=new Hotkeys(delegate(int id){})){log.Add("Hotkey status: pause="+keys.PauseRegistered+" error="+keys.PauseError+" quit="+keys.QuitRegistered+" error="+keys.QuitError);if(keys.PauseRegistered&&keys.QuitRegistered)log.Add("PASS pause and exit global hotkeys available");else log.Add("INFO Windows has reserved one or both optional shortcuts");}
            using(var capture=Colors.Capture(Screen.PrimaryScreen.Bounds))Check(capture.Width==192,"desktop sampling succeeds",log);
            // Visible fixture proves the actual desktop capture follows changed frames; no user images are saved.
            using(var fixture=new Form{FormBorderStyle=FormBorderStyle.None,StartPosition=FormStartPosition.Manual,Bounds=new Rectangle(Screen.PrimaryScreen.Bounds.X+180,Screen.PrimaryScreen.Bounds.Y+180,360,240),TopMost=true,ShowInTaskbar=false}){
                fixture.BackColor=Color.Red;fixture.Show();fixture.Refresh();Application.DoEvents();Thread.Sleep(150);
                using(var sample=Colors.Capture(fixture.Bounds)){Color c=Colors.Dominant(sample,new Rectangle(0,0,192,108));Check(c.R>c.B*3,"live capture red frame",log);}
                fixture.BackColor=Color.Blue;fixture.Refresh();Application.DoEvents();Thread.Sleep(150);
                using(var sample=Colors.Capture(fixture.Bounds)){Color c=Colors.Dominant(sample,new Rectangle(0,0,192,108));Check(c.B>c.R*3,"live capture changed blue frame",log);}
                using(var layer=new Layer())using(var patch=Colors.Pixels(120,80,delegate(int x,int y){return Color.FromArgb(255,255,30,20);})){
                    layer.Draw(patch,fixture.Left+60,fixture.Top+60);Native.SetWindowDisplayAffinity(layer.Handle,0);Application.DoEvents();Thread.Sleep(100);
                    using(var sample=Colors.Capture(new Rectangle(fixture.Left+70,fixture.Top+70,80,40))){Color c=sample.GetPixel(96,54);Check(c.R>200&&c.B<80,"actual desktop composition contains rendered overlay",log);}
                }
                fixture.Close();
            }
            string cursorFile=Path.Combine(folder,"pointer-test.cur");PointerTheme.WriteCursor(cursorFile);IntPtr pointerHandle=Native.LoadCursorFromFile(cursorFile);Check(pointerHandle!=IntPtr.Zero,"Windows loads the custom outlined pointer",log);Native.DestroyIcon(pointerHandle);
            try{long originalPointer=PointerFingerprint();using(var theme=new PointerTheme()){
                theme.SetEnabled(true);long themedPointer=PointerFingerprint();Check(themedPointer!=originalPointer,"custom pointer actually replaces the system arrow",log);
                for(int i=0;i<30;i++){theme.SetEnabled(true);using(var sample=Colors.Capture(Screen.PrimaryScreen.Bounds)){}Check(PointerFingerprint()==themedPointer,"pointer image stable during sampling "+i,log);}
                Check(theme.ApplyCount==1,"enabling the pointer repeatedly applies its theme only once",log);
                theme.SetEnabled(false);Check(PointerFingerprint()==originalPointer,"disabling restores the original pointer exactly",log);
                theme.SetEnabled(true);theme.SetEnabled(false);Check(PointerFingerprint()==originalPointer,"second on/off cycle restores the pointer",log);
            }}catch(System.ComponentModel.Win32Exception ex){if(ex.NativeErrorCode!=5)throw;log.Add("INFO Windows refused system pointer replacement in this test session");}
            using(var preview=new Bitmap(960,540))using(var g=Graphics.FromImage(preview)){
                g.Clear(Color.White);Color[] palette={Colors.Contrast(Color.RoyalBlue,1),Colors.Contrast(Color.MediumPurple,1),Colors.Contrast(Color.DeepPink,1)};
                for(int side=0;side<4;side++)using(var edge=Colors.Edge(side<2?960:64,side<2?64:412,side,side<2?palette:side==2?new[]{palette[0],palette[0]}:new[]{palette[2],palette[2]},1))g.DrawImageUnscaled(edge,side==3?896:0,side==1?476:side>=2?64:0);
                using(var pointer=PointerTheme.DrawPointer())g.DrawImageUnscaled(pointer,475,266);
                preview.Save(Path.Combine(folder,"renderer-preview.png"));
            }
            StudioTests.Run(folder,log);
            log.Add("PASS all tests");
        }catch(Exception ex){log.Add("ERROR "+ex);Environment.ExitCode=1;}
        File.WriteAllLines(Path.Combine(folder,"self-test.txt"),log);
    }
}
