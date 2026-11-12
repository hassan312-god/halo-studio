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
    [StructLayout(LayoutKind.Sequential)] public struct BITMAPINFO {public int Size,Width,Height;public short Planes,BitCount;public int Compression,SizeImage,XPels,YPels,Used,Important,Colors;}
    [DllImport("gdi32.dll",SetLastError=true)] public static extern IntPtr CreateDIBSection(IntPtr dc,ref BITMAPINFO info,uint usage,out IntPtr bits,IntPtr section,uint offset);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h,System.Text.StringBuilder name,int max);
    [DllImport("winmm.dll")] public static extern uint timeBeginPeriod(uint ms);
    [DllImport("winmm.dll")] public static extern uint timeEndPeriod(uint ms);
    [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr h,int attribute,ref int value,int size);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr h,int message,IntPtr w,string l);
    [DllImport("user32.dll")] public static extern bool DrawIconEx(IntPtr dc,int x,int y,IntPtr icon,int width,int height,uint step,IntPtr brush,uint flags);
}

sealed class Layer : Form {
    public bool Excluded;
    public int ShowTransitions;
    int lastX=int.MinValue,lastY=int.MinValue,surfaceW,surfaceH;
    IntPtr surfaceDc,surfaceBitmap,surfaceBits,surfaceOld;
    public Layer(){ FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=false; StartPosition=FormStartPosition.Manual; Text="Halo · lumière"; }
    protected override bool ShowWithoutActivation {get{return true;}}
    protected override CreateParams CreateParams {get{var p=base.CreateParams; p.ExStyle |= 0x80000|0x20|0x80|0x08000000|0x8; return p;}}
    protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e); Excluded=Native.SetWindowDisplayAffinity(Handle,0x11);}
    protected override void WndProc(ref Message m){if(m.Msg==0x84){m.Result=new IntPtr(-1);return;} if(m.Msg==0x21){m.Result=new IntPtr(3);return;}base.WndProc(ref m);}
    void ReleaseSurface(){
        if(surfaceDc!=IntPtr.Zero){Native.SelectObject(surfaceDc,surfaceOld);Native.DeleteObject(surfaceBitmap);Native.DeleteDC(surfaceDc);}
        surfaceDc=surfaceBitmap=surfaceBits=surfaceOld=IntPtr.Zero;surfaceW=surfaceH=0;
    }
    void EnsureSurface(int w,int h){
        if(surfaceDc!=IntPtr.Zero&&surfaceW==w&&surfaceH==h)return;
        ReleaseSurface();IntPtr screen=Native.GetDC(IntPtr.Zero);
        try{
            surfaceDc=Native.CreateCompatibleDC(screen);var info=new Native.BITMAPINFO{Size=40,Width=w,Height=-h,Planes=1,BitCount=32};
            surfaceBitmap=Native.CreateDIBSection(screen,ref info,0,out surfaceBits,IntPtr.Zero,0);
            if(surfaceDc==IntPtr.Zero||surfaceBitmap==IntPtr.Zero){int error=Marshal.GetLastWin32Error();if(surfaceDc!=IntPtr.Zero)Native.DeleteDC(surfaceDc);surfaceDc=IntPtr.Zero;throw new System.ComponentModel.Win32Exception(error);}
            surfaceOld=Native.SelectObject(surfaceDc,surfaceBitmap);surfaceW=w;surfaceH=h;
        }finally{Native.ReleaseDC(IntPtr.Zero,screen);}
    }
    // Premultiplied BGRA pixels go straight into a surface kept between frames: no bitmap is allocated per frame.
    public void Present(int[] pixels,int w,int h,int x,int y){
        EnsureSurface(w,h);Marshal.Copy(pixels,0,surfaceBits,w*h);
        IntPtr screen=Native.GetDC(IntPtr.Zero);
        try{
            var p=new Native.POINT(x,y);var sp=new Native.POINT(0,0);var size=new Native.SIZE(w,h);
            var blend=new Native.BLEND{Alpha=255,Format=1};
            if(!Native.UpdateLayeredWindow(Handle,screen,ref p,ref size,surfaceDc,ref sp,0,ref blend,2))throw new System.ComponentModel.Win32Exception();
            if(!Visible){Show();Native.ShowWindow(Handle,4);Native.SetWindowPos(Handle,new IntPtr(-1),x,y,w,h,0x50);ShowTransitions++;}
            lastX=x;lastY=y;
        }finally{Native.ReleaseDC(IntPtr.Zero,screen);}
    }
    public void Draw(Bitmap b,int x,int y){Present(Colors.ToPixels(b,true),b.Width,b.Height,x,y);}
    public void MoveTo(int x,int y){if(x==lastX&&y==lastY)return;Native.SetWindowPos(Handle,IntPtr.Zero,x,y,0,0,0x15);lastX=x;lastY=y;}
    protected override void Dispose(bool disposing){ReleaseSurface();base.Dispose(disposing);}
}

static class Colors {
    static readonly int ProcessId=Process.GetCurrentProcess().Id;
    static readonly double[] Linear=BuildLinear();
    static double[] BuildLinear(){var t=new double[256];for(int i=0;i<256;i++)t[i]=Math.Pow(i/255.0,2.2);return t;}
    public static double ToLinear(int channel){return Linear[channel];}
    public static int FromLinear(double value){return value<=0?0:value>=1?255:(int)(Math.Pow(value,1/2.2)*255+.5);}
    public static Color Mix(Color a,Color b,double t){return Color.FromArgb((int)(a.A+(b.A-a.A)*t),(int)(a.R+(b.R-a.R)*t),(int)(a.G+(b.G-a.G)*t),(int)(a.B+(b.B-a.B)*t));}
    public static int[] ToPixels(Bitmap b,bool premultiplied=false){
        int[] pixels=new int[b.Width*b.Height];
        var data=b.LockBits(new Rectangle(0,0,b.Width,b.Height),ImageLockMode.ReadOnly,premultiplied?PixelFormat.Format32bppPArgb:PixelFormat.Format32bppArgb);
        try{Marshal.Copy(data.Scan0,pixels,0,pixels.Length);}finally{b.UnlockBits(data);}return pixels;
    }
    public static Bitmap FromPixels(int[] pixels,int w,int h,PixelFormat format){
        Bitmap b=new Bitmap(w,h,format);var data=b.LockBits(new Rectangle(0,0,w,h),ImageLockMode.WriteOnly,format);
        try{Marshal.Copy(pixels,0,data.Scan0,w*h);}finally{b.UnlockBits(data);}return b;
    }
    public static Color Dominant(Bitmap b,Rectangle r){return Dominant(ToPixels(b),b.Width,r,Color.FromArgb(55,120,245));}
    public static Color Dominant(Bitmap b,Rectangle r,Color fallback){return Dominant(ToPixels(b),b.Width,r,fallback);}
    public static Color Dominant(int[] pixels,int stride,Rectangle r,Color fallback){
        // Prefer chromatic content over browser chrome. Use hue bins so opposing colors do not turn gray.
        double[] weight=new double[12],rr=new double[12],gg=new double[12],bb=new double[12];
        double neutral=0;int count=0;
        for(int y=r.Top;y<r.Bottom;y+=2)for(int x=r.Left;x<r.Right;x+=2){
            int p=pixels[y*stride+x],cr=(p>>16)&255,cg=(p>>8)&255,cb=p&255;
            int hi=Math.Max(cr,Math.Max(cg,cb)),lo=Math.Min(cr,Math.Min(cg,cb));
            double max=hi/255.0,sat=hi==0?0:(hi-lo)/(double)hi;
            neutral+=max;count++;if(sat<.16||max<.12)continue;
            double hue=hi==cr?(cg-cb)/(double)(hi-lo):hi==cg?2+(cb-cr)/(double)(hi-lo):4+(cr-cg)/(double)(hi-lo);
            if(hue<0)hue+=6;int bin=Math.Min(11,(int)(hue*2));double w=sat*sat*max;
            weight[bin]+=w;rr[bin]+=cr*w;gg[bin]+=cg*w;bb[bin]+=cb*w;
        }
        int best=0;for(int i=1;i<12;i++)if(weight[i]>weight[best])best=i;
        if(weight[best]<.25){double light=neutral/Math.Max(1,count);if(light<.08)return Color.Black;return fallback;}
        double red=rr[best]/weight[best],green=gg[best]/weight[best],blue=bb[best]/weight[best];
        double gain=Math.Min(2.4,235/Math.Max(red,Math.Max(green,blue)));
        return Color.FromArgb((int)(red*gain),(int)(green*gain),(int)(blue*gain));
    }
    public static double Brightness(Bitmap b,Rectangle r){return Brightness(ToPixels(b),b.Width,r);}
    public static double Brightness(int[] pixels,int stride,Rectangle r){
        double total=0;int n=0;
        for(int y=r.Top;y<r.Bottom;y+=3)for(int x=r.Left;x<r.Right;x+=3){int p=pixels[y*stride+x];total+=((p>>16)&255)*.2126+((p>>8)&255)*.7152+(p&255)*.0722;n++;}
        return total/(Math.Max(1,n)*255);
    }
    // Faithful light of a video zone: linear-light average, slightly more vivid, with its own level in the alpha channel
    // so that a dark part of the picture gives less halo instead of a dark veil.
    public static Color Ambient(int[] pixels,int stride,Rectangle r){
        double red=0,green=0,blue=0;int n=0;
        for(int y=r.Top;y<r.Bottom;y+=2)for(int x=r.Left;x<r.Right;x+=2){int p=pixels[y*stride+x];red+=Linear[(p>>16)&255];green+=Linear[(p>>8)&255];blue+=Linear[p&255];n++;}
        if(n==0)return Color.FromArgb(0,0,0,0);
        double cr=FromLinear(red/n),cg=FromLinear(green/n),cb=FromLinear(blue/n),max=Math.Max(cr,Math.Max(cg,cb));
        if(max<6)return Color.FromArgb(0,0,0,0);
        double level=Math.Min(1,Math.Pow(max/255,.55)*1.12),gain=235/max,gray=(cr+cg+cb)/3*gain;
        cr=gray+(cr*gain-gray)*1.3;cg=gray+(cg*gain-gray)*1.3;cb=gray+(cb*gain-gray)*1.3;
        return Color.FromArgb((int)(level*255),Clamp(cr),Clamp(cg),Clamp(cb));
    }
    static int Clamp(double v){return v<0?0:v>255?255:(int)v;}
    public static Color Contrast(Color c,double brightness){
        if(c.R+c.G+c.B==0)return c;
        double t=Math.Max(0,Math.Min(1,(brightness-.35)/.45));
        double min=Math.Min(c.R,Math.Min(c.G,c.B));
        double r=c.R-min*.7*t,g=c.G-min*.7*t,b=c.B-min*.7*t;
        double max=Math.Max(r,Math.Max(g,b));if(max<1)return c;
        double gain=(235-65*t)/max;
        return Color.FromArgb(c.A,(int)(r*gain),(int)(g*gain),(int)(b*gain));
    }
    public static Rectangle ActiveContent(Rectangle screen){bool fullscreen;return ActiveContent(screen,out fullscreen);}
    public static Rectangle ActiveContent(Rectangle screen,out bool fullscreen){
        fullscreen=false;
        Rectangle area=Rectangle.Intersect(Screen.FromRectangle(screen).WorkingArea,screen);
        IntPtr foreground=Native.GetForegroundWindow();uint process;Native.GetWindowThreadProcessId(foreground,out process);Native.RECT rect;
        if(process!=(uint)ProcessId&&Native.GetWindowRect(foreground,out rect)){
            Rectangle window=Rectangle.FromLTRB(rect.Left,rect.Top,rect.Right,rect.Bottom),active=Rectangle.Intersect(screen,window);
            if(active.Width>160&&active.Height>120)area=active;
            if(window.Contains(screen)){
                // The desktop itself also covers the whole screen: only a real application counts as full screen.
                var name=new System.Text.StringBuilder(64);Native.GetClassName(foreground,name,64);string shell=name.ToString();
                fullscreen=shell!="Progman"&&shell!="WorkerW"&&shell!="Shell_TrayWnd";
            }
        }
        // Screen pixels are sampled once; map the active application into that small image.
        int x=Math.Max(0,(area.X-screen.X)*192/screen.Width),y=Math.Max(0,(area.Y-screen.Y)*108/screen.Height);
        int right=Math.Min(192,(area.Right-screen.X)*192/screen.Width),bottom=Math.Min(108,(area.Bottom-screen.Y)*108/screen.Height);
        int insetX=Math.Max(1,(right-x)/35),insetY=Math.Max(1,(bottom-y)/25);
        return Rectangle.FromLTRB(x+insetX,y+insetY,Math.Max(x+insetX+1,right-insetX),Math.Max(y+insetY+1,bottom-insetY));
    }
    public static Bitmap Capture(Rectangle r){
        int[] pixels=new int[ScreenGrabber.W*ScreenGrabber.H];
        using(var grabber=new ScreenGrabber())grabber.Grab(r,pixels);
        return FromPixels(pixels,ScreenGrabber.W,ScreenGrabber.H,PixelFormat.Format32bppRgb);
    }
    public static Bitmap Pixels(int w,int h,Func<int,int,Color> pixel){
        int[] pixels=new int[w*h];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++){Color c=pixel(x,y);pixels[y*w+x]=(c.A<<24)|((c.R*c.A/255)<<16)|((c.G*c.A/255)<<8)|(c.B*c.A/255);}
        return FromPixels(pixels,w,h,PixelFormat.Format32bppPArgb);
    }
    public static Bitmap Edge(int w,int h,int side,Color[] palette,double strength,double phase=0){
        int[] pixels=new int[w*h];new EdgeRenderer().Render(pixels,w,h,side,palette,strength,phase);
        return FromPixels(pixels,w,h,PixelFormat.Format32bppPArgb);
    }
}

sealed class PointerTheme : IDisposable {
    // Arrow, link, text, busy, working in background, precision, help, unavailable, four resize directions, move, alternate.
    public static readonly uint[] CursorIds={32512,32649,32513,32514,32650,32515,32651,32648,32645,32644,32642,32643,32646,32516};
    readonly IntPtr[] originals=new IntPtr[CursorIds.Length];bool enabled;string file;Process guard;public int ApplyCount;
    int style=0,intensity=85,size=100;Color tint=Color.FromArgb(75,95,245);DateTime applied=DateTime.MinValue;
    public void Configure(int nextStyle,Color color,int nextIntensity,int nextSize,bool immediate){
        bool geometry=style!=nextStyle||intensity!=nextIntensity||size!=nextSize;
        int delta=Math.Max(Math.Abs(tint.R-color.R),Math.Max(Math.Abs(tint.G-color.G),Math.Abs(tint.B-color.B)));
        if(!geometry&&delta==0)return;
        if(!immediate&&!geometry&&(delta<24||(DateTime.UtcNow-applied).TotalMilliseconds<600))return;
        if(enabled){ApplySet(true,nextStyle,color,nextIntensity,nextSize);ApplyCount++;applied=DateTime.UtcNow;}
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
        for(int i=0;i<CursorIds.Length;i++)originals[i]=Native.CopyIcon(Native.LoadCursor(IntPtr.Zero,(int)CursorIds[i]));
        file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Pointeur-Halo.cur");WriteCursor(file);
        string ready="Local\\HaloPointerGuard_"+Process.GetCurrentProcess().Id;
        using(var signal=new EventWaitHandle(false,EventResetMode.ManualReset,ready)){
            guard=Process.Start(new ProcessStartInfo(Application.ExecutablePath,"--cursor-guard "+Process.GetCurrentProcess().Id+" "+ready){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});
            if(!signal.WaitOne(5000))throw new Exception("La protection de restauration du pointeur ne répond pas.");
        }
    }
    void ApplySet(bool custom,int nextStyle,Color color,int nextIntensity,int nextSize){
        for(int i=0;i<CursorIds.Length;i++){
            IntPtr h=custom?PointerArt.CreateCursor(nextStyle,color,nextIntensity,nextSize,i):Native.CopyIcon(originals[i]);
            if(h==IntPtr.Zero||!Native.SetSystemCursor(h,CursorIds[i])){if(h!=IntPtr.Zero)Native.DestroyIcon(h);throw new System.ComponentModel.Win32Exception();}
        }
    }
    public void SetEnabled(bool value){if(enabled==value)return;try{ApplySet(value,style,tint,intensity,size);enabled=value;ApplyCount++;}catch{if(value)try{ApplySet(false,style,tint,intensity,size);}catch{}throw;}}
    public void Dispose(){SetEnabled(false);foreach(IntPtr h in originals)if(h!=IntPtr.Zero)Native.DestroyIcon(h);if(guard!=null)guard.Dispose();}
    public static void Guard(int parent,string ready){
        IntPtr[] saved=new IntPtr[CursorIds.Length];for(int i=0;i<CursorIds.Length;i++)saved[i]=Native.CopyIcon(Native.LoadCursor(IntPtr.Zero,(int)CursorIds[i]));
        try{using(var p=Process.GetProcessById(parent)){using(var signal=EventWaitHandle.OpenExisting(ready))signal.Set();p.WaitForExit();}}
        catch(ArgumentException){}finally{for(int i=0;i<CursorIds.Length;i++)if(saved[i]!=IntPtr.Zero)Native.SetSystemCursor(saved[i],CursorIds[i]);}
    }
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
        // Checks that need no desktop at all: the analysis and drawing code alone.
        if(args.Length>0&&args[0]=="--logic-test"){var log=new List<string>();try{EngineTests.Run(log);log.Add("PASS all logic tests");}catch(Exception ex){log.Add("ERROR "+ex);Environment.ExitCode=1;}Directory.CreateDirectory(args[1]);File.WriteAllLines(Path.Combine(args[1],"logic-test.txt"),log);return;}
        try{Native.SetProcessDpiAwarenessContext(new IntPtr(-4));}catch{try{Native.SetProcessDPIAware();}catch{}}
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        if(args.Length>0&&args[0]=="--cursor-guard"){PointerTheme.Guard(int.Parse(args[1]),args[2]);return;}
        if(args.Length>0&&args[0]=="--stop"){try{using(var e=EventWaitHandle.OpenExisting(Stop))e.Set();}catch(WaitHandleCannotBeOpenedException){}return;}
        if(args.Length>0&&args[0]=="--self-test"){SelfTest(args[1]);return;}
        // Pictures of every page, for review.
        if(args.Length>0&&args[0]=="--ui-shots"){StudioTests.Shots(args[1],args.Length>2&&args[2]=="dark");return;}
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
            EngineTests.Run(log);
            using(var monitor=new MonitorGlow(Screen.PrimaryScreen.Bounds)){
                var always=new EngineConfig{Enabled=true,AutoColor=true,Cinema=true};var watch=Stopwatch.StartNew();
                for(int i=0;i<30;i++){monitor.Update(always,33);Application.DoEvents();}
                double perFrame=watch.Elapsed.TotalMilliseconds/30;
                Check(monitor.Visible&&Native.IsWindowVisible(monitor.Edges[0].Handle),"engine draws the permanent halo on the primary screen",log);
                log.Add("INFO capture, analysis and drawing: "+perFrame.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+" ms per frame on "+Screen.PrimaryScreen.Bounds.Width+"x"+Screen.PrimaryScreen.Bounds.Height);
                Check(perFrame<33,"a full frame fits the 30 images per second budget",log);
            }
            using(var engine=new HaloEngine()){
                engine.PreviewWanted=true;engine.Configure(new EngineConfig{Enabled=true,AutoColor=true});engine.Start();
                for(int i=0;i<40&&engine.State.Preview==null;i++)Thread.Sleep(50);
                Check(engine.State.Preview!=null&&engine.State.Error==null,"engine thread publishes live frames without blocking the window",log);
            }
            StudioTests.Run(folder,log);
            log.Add("PASS all tests");
        }catch(Exception ex){log.Add("ERROR "+ex);Environment.ExitCode=1;}
        File.WriteAllLines(Path.Combine(folder,"self-test.txt"),log);
    }
}
