using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

// Reduced copy of one screen, read straight from a surface that lives as long as the monitor.
sealed class ScreenGrabber : IDisposable {
    public const int W=192,H=108;
    IntPtr dc,bitmap,bits,old;
    public ScreenGrabber(){
        IntPtr screen=Native.GetDC(IntPtr.Zero);
        try{
            dc=Native.CreateCompatibleDC(screen);var info=new Native.BITMAPINFO{Size=40,Width=W,Height=-H,Planes=1,BitCount=32};
            bitmap=Native.CreateDIBSection(screen,ref info,0,out bits,IntPtr.Zero,0);
            if(dc==IntPtr.Zero||bitmap==IntPtr.Zero){int error=Marshal.GetLastWin32Error();if(dc!=IntPtr.Zero)Native.DeleteDC(dc);dc=IntPtr.Zero;throw new System.ComponentModel.Win32Exception(error);}
            old=Native.SelectObject(dc,bitmap);Native.SetStretchBltMode(dc,4);
        }finally{Native.ReleaseDC(IntPtr.Zero,screen);}
    }
    public void Grab(Rectangle r,int[] target){
        IntPtr screen=Native.GetDC(IntPtr.Zero);
        // SRCCOPY only: CAPTUREBLT can disrupt the hardware pointer during each sample.
        try{if(!Native.StretchBlt(dc,0,0,W,H,screen,r.X,r.Y,r.Width,r.Height,0x00CC0020))throw new System.ComponentModel.Win32Exception();}
        finally{Native.ReleaseDC(IntPtr.Zero,screen);}
        Marshal.Copy(bits,target,0,W*H);
    }
    public void Dispose(){if(dc==IntPtr.Zero)return;Native.SelectObject(dc,old);Native.DeleteObject(bitmap);Native.DeleteDC(dc);dc=IntPtr.Zero;}
}

// Finds the part of the screen that behaves like a video: a compact area that keeps changing, as opposed to
// a page being scrolled (the same picture moving up or down) or a short animation.
sealed class VideoDetector {
    public const int GW=48,GH=27,Cell=4;
    const int W=ScreenGrabber.W,H=ScreenGrabber.H;
    readonly int[] cells=new int[GW*GH*3],previousCells=new int[GW*GH*3],previousFrame=new int[W*H];
    readonly float[] activity=new float[GW*GH];readonly bool[] changed=new bool[GW*GH],colorful=new bool[GW*GH];
    bool primed;double seen,hold;
    public double Boost;public bool Detected,Suspect,SceneCut,Scrolling;public Rectangle Region;
    static int Luma(int p){return ((p>>16)&255)+2*((p>>8)&255)+(p&255);}
    double Difference(int[] frame,int x0,int x1,int y0,int y1,int dy){
        long sum=0;int n=0;
        for(int y=y0;y<y1;y+=2)for(int x=x0;x<x1;x+=3){sum+=Math.Abs(Luma(frame[y*W+x])-Luma(previousFrame[(y+dy)*W+x]));n++;}
        return sum/(double)Math.Max(1,n);
    }
    bool IsScroll(int[] frame,Rectangle c){
        int x0=c.Left+2,x1=c.Right-2,y0=c.Top+2,y1=c.Bottom-2;if(x1-x0<24||y1-y0<30)return false;
        int range=Math.Min(20,(y1-y0)/3);double still=Difference(frame,x0,x1,y0+range,y1-range,0);if(still<4)return false;
        double best=double.MaxValue;
        for(int dy=-range;dy<=range;dy++)if(dy!=0)best=Math.Min(best,Difference(frame,x0,x1,y0+range,y1-range,dy));
        return best<still*.35;
    }
    public void Update(int[] frame,Rectangle content,double dt,bool fullscreen){
        for(int gy=0;gy<GH;gy++)for(int gx=0;gx<GW;gx++){
            int r=0,g=0,b=0;
            for(int y=gy*Cell;y<gy*Cell+Cell;y++)for(int x=gx*Cell;x<gx*Cell+Cell;x++){int p=frame[y*W+x];r+=(p>>16)&255;g+=(p>>8)&255;b+=p&255;}
            int i=gy*GW+gx;r/=Cell*Cell;g/=Cell*Cell;b/=Cell*Cell;cells[i*3]=r;cells[i*3+1]=g;cells[i*3+2]=b;
            int hi=Math.Max(r,Math.Max(g,b)),lo=Math.Min(r,Math.Min(g,b));colorful[i]=hi-lo>30&&hi>55;
        }
        int left=Math.Max(0,content.Left/Cell),top=Math.Max(0,content.Top/Cell),right=Math.Min(GW,(content.Right+Cell-1)/Cell),bottom=Math.Min(GH,(content.Bottom+Cell-1)/Cell);
        int total=Math.Max(1,(right-left)*(bottom-top)),moved=0,jumped=0;
        Array.Clear(changed,0,changed.Length);
        if(primed)for(int gy=top;gy<bottom;gy++)for(int gx=left;gx<right;gx++){
            int i=gy*GW+gx,d=Math.Abs(cells[i*3]-previousCells[i*3])+Math.Abs(cells[i*3+1]-previousCells[i*3+1])+Math.Abs(cells[i*3+2]-previousCells[i*3+2]);
            if(d>30){changed[i]=true;moved++;}if(d>150)jumped++;
        }
        double fraction=moved/(double)total;Suspect=fraction>.04;
        Scrolling=primed&&fraction>.2&&IsScroll(frame,content);
        if(Scrolling)Array.Clear(changed,0,changed.Length);
        float decay=(float)Math.Exp(-dt/350);int[] columns=new int[GW],rows=new int[GH];int widest=0,tallest=0;
        for(int i=0;i<activity.Length;i++)activity[i]=activity[i]*decay+(changed[i]?1-decay:0);
        for(int gy=top;gy<bottom;gy++)for(int gx=left;gx<right;gx++)if(activity[gy*GW+gx]>.3f){columns[gx]++;rows[gy]++;}
        for(int gx=left;gx<right;gx++)widest=Math.Max(widest,columns[gx]);for(int gy=top;gy<bottom;gy++)tallest=Math.Max(tallest,rows[gy]);
        // Rows and columns that only hold a few stray cells (a spinner, a blinking caret) stay outside the box.
        int x0=left,x1=right,y0=top,y1=bottom,needX=Math.Max(2,widest*35/100),needY=Math.Max(2,tallest*35/100);
        while(x0<x1&&columns[x0]<needX)x0++;while(x1>x0&&columns[x1-1]<needX)x1--;
        while(y0<y1&&rows[y0]<needY)y0++;while(y1>y0&&rows[y1-1]<needY)y1--;
        int active=0,chroma=0,area=Math.Max(1,(x1-x0)*(y1-y0));
        for(int gy=y0;gy<y1;gy++)for(int gx=x0;gx<x1;gx++){if(activity[gy*GW+gx]>.3f)active++;if(colorful[gy*GW+gx])chroma++;}
        bool candidate=x1-x0>=8&&y1-y0>=5&&active>=area*4/10&&(fullscreen||chroma>=area*9/100);
        if(candidate)seen+=dt;else seen=Math.Max(0,seen-2*dt);
        if(candidate&&seen>=(fullscreen?60:200)){
            Rectangle box=Rectangle.FromLTRB(x0*Cell,y0*Cell,x1*Cell,y1*Cell);
            // A video with a still background only moves in part of its frame: the area grows as more of it is seen.
            Region=hold>0&&!Region.IsEmpty?Rectangle.Union(Region,box):box;hold=fullscreen?12000:4000;
        }else if(hold>0)hold-=dt;
        Detected=hold>0;if(!Detected)Region=Rectangle.Empty;
        SceneCut=Detected&&!Scrolling&&jumped>=total/2;
        double target=Detected?1:0;Boost+=(target-Boost)*(1-Math.Exp(-dt/(target>Boost?150:1500)));
        Array.Copy(cells,previousCells,cells.Length);Array.Copy(frame,previousFrame,frame.Length);primed=true;
    }
    // Black bars around a film carry no light: zones are read on the picture itself.
    public static Rectangle TrimBlack(int[] frame,Rectangle r){
        int top=r.Top,bottom=r.Bottom,left=r.Left,right=r.Right,maxY=r.Height*30/100,maxX=r.Width*30/100;
        while(top-r.Top<maxY&&Dark(frame,r.Left,r.Right,top,top+1))top++;
        while(r.Bottom-bottom<maxY&&Dark(frame,r.Left,r.Right,bottom-1,bottom))bottom--;
        while(left-r.Left<maxX&&Dark(frame,left,left+1,top,bottom))left++;
        while(r.Right-right<maxX&&Dark(frame,right-1,right,top,bottom))right--;
        return right-left<12||bottom-top<8?r:Rectangle.FromLTRB(left,top,right,bottom);
    }
    static bool Dark(int[] frame,int x0,int x1,int y0,int y1){
        int lit=0,n=0;
        for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++){int p=frame[y*W+x];if(Math.Max((p>>16)&255,Math.Max((p>>8)&255,p&255))>24)lit++;n++;}
        return lit*100<=n*3;
    }
}

// Draws one halo edge into a pixel buffer. Tables are kept between frames; the inner loop is integer only.
sealed class EdgeRenderer {
    int[] fall=new int[0],red=new int[0],green=new int[0],blue=new int[0],gain=new int[0];
    public void Render(int[] target,int w,int h,int side,Color[] palette,double strength,double phase){
        int depth=side<2?h:w,length=side<2?w:h;
        if(fall.Length<depth)fall=new int[depth];
        if(red.Length<length){red=new int[length];green=new int[length];blue=new int[length];gain=new int[length];}
        for(int d=0;d<depth;d++)fall[d]=d<5?255:(int)(255*Math.Pow(1-(double)d/Math.Max(1,depth-1),.92));
        for(int i=0;i<length;i++){
            double position=(double)i/Math.Max(1,length-1),at=position*(palette.Length-1);int k=Math.Min(palette.Length-2,(int)at);
            Color c=Colors.Mix(palette[k],palette[k+1],at-k);red[i]=c.R;green[i]=c.G;blue[i]=c.B;
            double delta=Math.Abs(position-phase);delta=Math.Min(delta,1-delta);
            double gleam=delta<.2?1+.16*Math.Exp(-delta*delta/.0018):1;
            gain[i]=(int)(strength*gleam*c.A/255*256);
        }
        for(int y=0;y<h;y++){
            int row=y*w,edge=side==0?y:h-1-y;
            for(int x=0;x<w;x++){
                int d=side<2?Math.Min(edge,Math.Min(x,w-1-x)):side==2?x:w-1-x,axis=side<2?x:y;
                int a=fall[d]*gain[axis]>>8;if(a>255)a=255;
                // (v*a)/255 without a division.
                target[row+x]=(a<<24)|((red[axis]*a*32897>>23)<<16)|((green[axis]*a*32897>>23)<<8)|(blue[axis]*a*32897>>23);
            }
        }
    }
}

sealed class EngineConfig {
    public bool Enabled,VideoOnly,AutoColor=true,Cinema,Paused;
    public double Strength=1,Width=1;public Color Fixed=Color.FromArgb(75,95,245);
}

sealed class EngineState {
    public Color Color=Color.FromArgb(75,145,240);public bool Video;public double FrameMs;public int[] Preview;public string Error;
}

sealed class MonitorGlow : IDisposable {
    const int W=ScreenGrabber.W,H=ScreenGrabber.H;
    static readonly int[] Zones={16,16,9,9};
    public Rectangle Bounds;public Layer[] Edges=new Layer[4];public Color Global=Color.FromArgb(75,145,240);
    public double Lightness;public readonly int[] Frame=new int[W*H];public readonly VideoDetector Video=new VideoDetector();
    public bool Visible;
    readonly ScreenGrabber grabber=new ScreenGrabber();readonly EdgeRenderer[] renderers=new EdgeRenderer[4];
    readonly double[][] light=new double[4][];readonly Color[][] palettes=new Color[4][];readonly long[] drawn=new long[4];
    readonly double[] global=new double[3];int[] buffer=new int[0];bool primed;double fade;
    public double VideoBoost {get{return Video.Boost;}}
    public MonitorGlow(Rectangle r){
        Bounds=r;
        for(int s=0;s<4;s++){Edges[s]=new Layer();renderers[s]=new EdgeRenderer();light[s]=new double[Zones[s]*4];palettes[s]=new Color[Zones[s]];}
    }
    static double Blend(double dt,double time){return 1-Math.Exp(-dt/time);}
    static void Approach(double[] state,int at,Color target,double t){
        state[at]+=(Colors.ToLinear(target.R)-state[at])*t;state[at+1]+=(Colors.ToLinear(target.G)-state[at+1])*t;
        state[at+2]+=(Colors.ToLinear(target.B)-state[at+2])*t;if(state.Length>at+3)state[at+3]+=(target.A/255.0-state[at+3])*t;
    }
    static Color Read(double[] state,int at,bool alpha){
        return Color.FromArgb(alpha?Math.Max(0,Math.Min(255,(int)(state[at+3]*255+.5))):255,Colors.FromLinear(state[at]),Colors.FromLinear(state[at+1]),Colors.FromLinear(state[at+2]));
    }
    static int Lerp(int a,int b,double t){return (int)(a+(b-a)*t);}
    public void Update(EngineConfig c,double dt){
        grabber.Grab(Bounds,Frame);
        bool fullscreen;Rectangle content=Colors.ActiveContent(Bounds,out fullscreen);
        Video.Update(Frame,content,dt,fullscreen);double boost=Video.Boost;
        // During a video the colors come from the picture itself, black bars excluded; otherwise from the active application.
        Rectangle picture=Video.Detected?VideoDetector.TrimBlack(Frame,fullscreen||Video.Region.IsEmpty?content:Rectangle.Intersect(Video.Region,new Rectangle(0,0,W,H))):content;
        if(picture.Width<4||picture.Height<4)picture=content;
        double first=primed?0:1;
        Color main=c.AutoColor?Colors.Dominant(Frame,W,picture,Color.FromArgb(55,120,245)):c.Fixed;
        Lightness+=(Colors.Brightness(Frame,W,picture)-Lightness)*Math.Max(first,Blend(dt,210));
        double contrast=Lightness*(1-.35*boost),follow=Math.Max(first,Blend(dt,Video.SceneCut?40:450-340*boost));
        if(c.AutoColor){Approach(global,0,Colors.Contrast(main,contrast),follow);Global=Read(global,0,false);}
        else{Global=c.Fixed;Approach(global,0,c.Fixed,1);}
        primed=true;
        Visible=c.Enabled&&!(c.VideoOnly&&boost<.18);
        if(!Visible){Hide();return;}
        for(int s=0;s<4;s++)for(int k=0;k<Zones[s];k++){
            Color target=c.Fixed;
            if(c.AutoColor){
                // Zones hug the edge of the picture more closely as the video takes over.
                int rw=Math.Max(2,picture.Width*(s<2?Lerp(25,12,boost):Lerp(40,18,boost))/100),rh=Math.Max(2,picture.Height*(s<2?Lerp(40,22,boost):Lerp(30,20,boost))/100);
                int x=picture.X+(s<2?(picture.Width-rw)*k/(Zones[s]-1):s==2?0:picture.Width-rw);
                int y=picture.Y+(s<2?(s==0?0:picture.Height-rh):(picture.Height-rh)*k/(Zones[s]-1));
                Rectangle zone=new Rectangle(x,y,rw,rh);
                target=Colors.Contrast(Colors.Mix(Colors.Dominant(Frame,W,zone,main),main,.4),contrast);
                if(boost>.02)target=Colors.Mix(target,Colors.Contrast(Colors.Ambient(Frame,W,zone),contrast),boost);
            }
            Approach(light[s],k*4,target,fade<=0||!c.AutoColor?1:follow);
        }
        for(int s=0;s<4;s++)for(int k=0;k<Zones[s];k++)palettes[s][k]=Read(light[s],k*4,true);
        // Neighbouring edges share their corner so the halo turns the corner without a seam.
        int l=Zones[0]-1,v=Zones[2]-1;
        palettes[0][0]=palettes[2][0]=Colors.Mix(palettes[0][0],palettes[2][0],.5);
        palettes[0][l]=palettes[3][0]=Colors.Mix(palettes[0][l],palettes[3][0],.5);
        palettes[1][0]=palettes[2][v]=Colors.Mix(palettes[1][0],palettes[2][v],.5);
        palettes[1][l]=palettes[3][v]=Colors.Mix(palettes[1][l],palettes[3][v],.5);
        fade=Math.Min(1,fade+dt/300);
        int baseDepth=Math.Max(68,Math.Min(118,Bounds.Height/11)),videoDepth=c.Cinema?Math.Min(210,Bounds.Height/6):Math.Min(160,Bounds.Height/8);
        int d=Math.Max(10,Math.Min(Bounds.Height/3,(int)((baseDepth+(videoDepth-baseDepth)*boost)*c.Width)));
        double strength=c.Strength*(1+boost*(.3+.2*Lightness))*(c.Cinema?1+.15*boost:1)*fade;
        // The gliding highlight advances in steps so a still picture costs a redraw only fifteen times a second.
        long step=(Environment.TickCount&0x7fffffff)/66;double phase=step*66%5000/5000.0;
        for(int s=0;s<4;s++){
            int w=s<2?Bounds.Width:d,h=s<2?d:Bounds.Height-2*d;if(w<1||h<1)continue;
            long signature=step*31+d;signature=signature*31+(long)(strength*512);
            foreach(Color p in palettes[s])signature=signature*31+p.ToArgb();
            if(signature==drawn[s]&&Edges[s].Visible)continue;
            if(buffer.Length<w*h)buffer=new int[w*h];
            renderers[s].Render(buffer,w,h,s,palettes[s],strength,phase);
            Edges[s].Present(buffer,w,h,Bounds.X+(s==3?Bounds.Width-d:0),Bounds.Y+(s==1?Bounds.Height-d:s>=2?d:0));drawn[s]=signature;
        }
    }
    public void Hide(){fade=0;Visible=false;foreach(var e in Edges)if(e.Visible)e.Hide();}
    public void Dispose(){foreach(var e in Edges)e.Dispose();grabber.Dispose();}
}

// Capture, analysis and drawing run on their own thread, which owns the halo windows: the settings window never waits for them.
sealed class HaloEngine : IDisposable {
    volatile EngineConfig config=new EngineConfig();volatile EngineState state=new EngineState();
    readonly ManualResetEvent quit=new ManualResetEvent(false);Thread thread;
    public volatile bool PreviewWanted;
    public EngineState State {get{return state;}}
    public void Configure(EngineConfig next){config=next;}
    public void Start(){
        if(thread!=null)return;
        thread=new Thread(Run){IsBackground=true,Name="Halo engine"};thread.SetApartmentState(ApartmentState.STA);thread.Start();
    }
    void Run(){
        var monitors=new List<MonitorGlow>();string topology="";var clock=Stopwatch.StartNew();
        double last=clock.Elapsed.TotalMilliseconds,published=0,logged=-60000;bool precise=false;
        try{
            while(!quit.WaitOne(0)){
                double start=clock.Elapsed.TotalMilliseconds,dt=Math.Max(1,Math.Min(500,start-last));last=start;
                EngineConfig c=config;int interval=250;var next=new EngineState{Color=state.Color,Preview=state.Preview};
                try{
                    if(c.Paused)foreach(var m in monitors)m.Hide();
                    else{
                        string now="";foreach(var s in Screen.AllScreens)now+=s.Bounds.ToString();
                        if(now!=topology){foreach(var m in monitors)m.Dispose();monitors.Clear();foreach(var s in Screen.AllScreens)monitors.Add(new MonitorGlow(s.Bounds));topology=now;}
                        // Thirty frames a second while a video plays, fifteen while something moves, ten on a still screen.
                        interval=100;
                        foreach(var m in monitors){m.Update(c,dt);interval=Math.Min(interval,m.Video.Detected||m.VideoBoost>.02?33:m.Video.Suspect?66:100);}
                        Point cursor=Cursor.Position;MonitorGlow active=monitors.Find(m=>m.Bounds.Contains(cursor));
                        if(active==null&&monitors.Count>0)active=monitors[0];
                        if(active!=null){
                            next.Color=c.AutoColor?active.Global:c.Fixed;next.Video=active.VideoBoost>=.18;
                            if(PreviewWanted&&start-published>=60){next.Preview=(int[])active.Frame.Clone();published=start;}
                        }
                    }
                }catch(Exception ex){
                    next.Error=ex.Message;
                    if(start-logged>30000){logged=start;try{File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"erreurs.log"),DateTime.UtcNow.ToString("s")+" "+ex+Environment.NewLine);}catch{}}
                }
                next.FrameMs=clock.Elapsed.TotalMilliseconds-start;state=next;
                // Thirty frames a second need a finer system timer than the default 15.6 ms; it is released when idle.
                if(interval<50!=precise){precise=interval<50;try{if(precise)Native.timeBeginPeriod(1);else Native.timeEndPeriod(1);}catch{}}
                Application.DoEvents();
                quit.WaitOne(Math.Max(1,(int)(interval-(clock.Elapsed.TotalMilliseconds-start))));
            }
        }finally{
            foreach(var m in monitors)m.Dispose();
            if(precise)try{Native.timeEndPeriod(1);}catch{}
        }
    }
    public void Dispose(){quit.Set();if(thread!=null&&!thread.Join(3000))thread.Abort();thread=null;quit.Dispose();}
}
