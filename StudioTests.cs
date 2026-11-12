using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Runtime.InteropServices;

static class StudioTests {
    static void Check(bool ok,string message,List<string> log){if(!ok)throw new Exception(message);log.Add("PASS "+message);}
    static long Hash(Bitmap b){long h=23;unchecked{for(int y=0;y<b.Height;y++)for(int x=0;x<b.Width;x++)h=h*31+b.GetPixel(x,y).ToArgb();}return h;}
    static Bitmap Snapshot(Control c){Bitmap b=new Bitmap(c.Width,c.Height);c.DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height));return b;}
    // Where DrawToBitmap leaves children out, each control is painted in turn, parents first.
    static void Compose(Control c,Graphics g,Point origin){
        if(c.Width<1||c.Height<1)return;
        using(var b=new Bitmap(c.Width,c.Height)){using(var bg=Graphics.FromImage(b))bg.DrawImage(Canvas,new Rectangle(0,0,b.Width,b.Height),new Rectangle(origin,b.Size),GraphicsUnit.Pixel);c.DrawToBitmap(b,new Rectangle(0,0,b.Width,b.Height));g.DrawImageUnscaled(b,origin);}
        for(int i=c.Controls.Count-1;i>=0;i--){Control child=c.Controls[i];if(child.Visible)Compose(child,g,new Point(origin.X+child.Left,origin.Y+child.Top));}
    }
    static Bitmap Canvas;
    static Bitmap Composite(Form f){
        Canvas=new Bitmap(f.ClientSize.Width,f.ClientSize.Height);
        using(var g=Graphics.FromImage(Canvas)){Theme.PaintBackdrop(g,new Rectangle(Point.Empty,Canvas.Size));foreach(Control child in f.Controls)if(child.Visible)Compose(child,g,child.Location);}
        return Canvas;
    }
    [StructLayout(LayoutKind.Sequential)] struct IconInfo {[MarshalAs(UnmanagedType.Bool)]public bool Icon;public uint X,Y;public IntPtr Mask,Color;}
    [DllImport("user32.dll")] static extern bool GetIconInfo(IntPtr cursor,out IconInfo info);
    // Pictures of every page, for review without a Windows session (also works where native calls are missing).
    public static void Shots(string folder,bool dark){
        Directory.CreateDirectory(folder);var settings=new HaloSettings();bool composite=Type.GetType("Mono.Runtime")!=null;
        using(var studio=new StudioForm(settings,false)){
            if(dark){Theme.Apply(true);}
            studio.Show();Application.DoEvents();
            using(var frame=new Bitmap(192,108))using(var g=Graphics.FromImage(frame))using(var sky=new System.Drawing.Drawing2D.LinearGradientBrush(new Rectangle(0,0,192,108),Color.FromArgb(40,60,140),Color.FromArgb(240,140,70),70f)){g.FillRectangle(sky,0,0,192,108);studio.Preview.SetFrame(frame);}
            studio.Live(Color.FromArgb(120,110,240),false,null,true);
            for(int i=0;i<StudioForm.PageNames.Length;i++){studio.SelectPage(i);studio.Invalidate(true);Application.DoEvents();using(var shot=composite?Composite(studio):Snapshot(studio))shot.Save(Path.Combine(folder,(dark?"dark-":"page-")+i+".png"));}
            studio.Size=studio.MinimumSize;studio.SelectPage(0);Application.DoEvents();using(var shot=composite?Composite(studio):Snapshot(studio))shot.Save(Path.Combine(folder,(dark?"dark-":"page-")+"compact.png"));
            studio.AllowClose=true;studio.Close();
        }
    }
    public static void Run(string folder,List<string> log){
        var defaults=new HaloSettings();Check(defaults.VideoOnly&&!defaults.ScreenEnabled,"video halo is enabled by default without permanent screen glow",log);
        Check(AppUpdater.Compare("1.0.0-beta.2","1.0.0-beta.1")>0&&AppUpdater.Compare("1.0.0","1.0.0-beta.9")>0&&AppUpdater.Compare("0.9.9","1.0.0-beta.1")<0,"updater orders beta, stable and older versions correctly",log);
        string digest=new string('a',64);
        string safe="[{\"draft\":false,\"tag_name\":\"v1.0.0-beta.2\",\"assets\":[{\"name\":\"HaloStudio-Setup-1.0.0-beta.2.exe\",\"browser_download_url\":\""+BuildInfo.RepositoryUrl+"/releases/download/v1.0.0-beta.2/HaloStudio-Setup-1.0.0-beta.2.exe\",\"size\":1234,\"digest\":\"sha256:"+digest+"\"}]}]";
        Check(AppUpdater.Parse(safe).Version=="1.0.0-beta.2","updater accepts a published beta with a SHA-256 digest",log);
        Check(AppUpdater.Parse(safe.Replace(BuildInfo.RepositoryUrl,"https://example.com/unsafe"))==null,"updater rejects downloads outside the project release",log);
        Check(AppUpdater.Parse(safe.Replace("sha256:"+digest,""))==null,"updater rejects missing checksums",log);
        Check(AppUpdater.Parse(safe.Replace("\"draft\":false","\"draft\":true"))==null,"updater ignores draft releases",log);
        string integrity=Path.Combine(folder,"integrity-test.txt");File.WriteAllText(integrity,"valid");string expectedDigest=AppUpdater.Hash(integrity);Check(AppUpdater.Verified(integrity,expectedDigest),"updater accepts matching content",log);File.WriteAllText(integrity,"changed");Check(!AppUpdater.Verified(integrity,expectedDigest),"updater rejects tampered content",log);
        HashSet<long> shapes=new HashSet<long>();
        using(var sheet=new Bitmap(PointerArt.Names.Length*140,190))using(var g=Graphics.FromImage(sheet)){
            g.Clear(Color.FromArgb(245,247,253));
            for(int style=0;style<PointerArt.Names.Length;style++){
                using(var pointer=PointerArt.Render(style,Color.RoyalBlue,100,150)){
                    shapes.Add(Hash(pointer));g.DrawImageUnscaled(pointer,style*140+15,15);
                    Check(pointer.GetPixel(pointer.Width-1,pointer.Height-1).A==0,"pointer "+style+" has no detached circular glow",log);
                }
                using(var font=new Font("Segoe UI",10))g.DrawString(PointerArt.Names[style],font,Brushes.MidnightBlue,style*140+12,135);
                IntPtr h=PointerArt.CreateCursor(style,Color.Magenta,85,140);IconInfo info;
                try{Check(GetIconInfo(h,out info),"native pointer "+style+" is valid",log);Point expected=PointerArt.Hotspot(style,140);Check(info.X==expected.X&&info.Y==expected.Y,"pointer "+style+" keeps its exact click point",log);Native.DeleteObject(info.Mask);Native.DeleteObject(info.Color);}finally{Native.DestroyIcon(h);}
            }
            Check(shapes.Count==PointerArt.Names.Length,"all pointer styles are visually distinct",log);sheet.Save(Path.Combine(folder,"styles-pointeur.png"));
        }
        HashSet<long> roles=new HashSet<long>();
        using(var sheet=new Bitmap(PointerArt.Roles.Length*76,84))using(var g=Graphics.FromImage(sheet)){
            g.Clear(Color.FromArgb(245,247,253));
            for(int role=0;role<PointerArt.Roles.Length;role++){
                using(var image=PointerArt.Render(0,Color.DodgerBlue,90,100,role)){roles.Add(Hash(image));g.DrawImageUnscaled(image,role*76+4,8);}
                IntPtr h=PointerArt.CreateCursor(5,Color.DodgerBlue,90,100,role);IconInfo info;
                try{Check(GetIconInfo(h,out info),"cursor '"+PointerArt.Roles[role]+"' is a valid native pointer",log);Point expected=PointerArt.RoleHotspot(5,100,role);Check(info.X==expected.X&&info.Y==expected.Y&&expected.X<68&&expected.Y<68,"cursor '"+PointerArt.Roles[role]+"' keeps its click point",log);Native.DeleteObject(info.Mask);Native.DeleteObject(info.Color);}finally{Native.DestroyIcon(h);}
            }
            sheet.Save(Path.Combine(folder,"roles-pointeur.png"));
        }
        Check(roles.Count==PointerArt.Roles.Length&&PointerTheme.CursorIds.Length==PointerArt.Roles.Length,"every Windows cursor role has its own themed shape",log);
        using(var off=PointerArt.Render(0,Color.Red,0,100))using(var on=PointerArt.Render(0,Color.Red,100,100)){
            int extra=0;for(int y=0;y<off.Height;y++)for(int x=0;x<off.Width;x++)if(off.GetPixel(x,y).A==0&&on.GetPixel(x,y).A>15)extra++;
            Check(extra>100,"pointer intensity adds light along the silhouette",log);Check(Hash(off)!=Hash(on),"zero and full pointer intensity differ",log);
        }
        var settings=new HaloSettings{AutoColor=false,ColorArgb=Color.HotPink.ToArgb(),PointerStyle=3,PointerIntensity=31,ScreenIntensity=72,PointerSize=140,Width=130,ScreenEnabled=true};
        var serializer=new XmlSerializer(typeof(HaloSettings));using(var memory=new MemoryStream()){serializer.Serialize(memory,settings);memory.Position=0;var loaded=(HaloSettings)serializer.Deserialize(memory);Check(loaded.ColorArgb==settings.ColorArgb&&loaded.PointerStyle==3&&loaded.PointerIntensity==31&&loaded.ScreenIntensity==72&&loaded.Width==130,"all customization settings survive serialization",log);}
        using(var monitor=new MonitorGlow(Screen.PrimaryScreen.Bounds)){monitor.Update(new EngineConfig{Enabled=false,AutoColor=false,Fixed=Color.HotPink},33);Check(monitor.Global.ToArgb()==Color.HotPink.ToArgb(),"single-color mode keeps the exact chosen color",log);}
        using(var studio=new StudioForm(settings,false)){
            studio.Show();Application.DoEvents();int changes=0;studio.Changed+=delegate{changes++;};
            studio.ColorMode.SelectedIndex=0;studio.StyleBox.SelectedIndex=4;studio.ScreenSlider.Value=20;studio.PointerSlider.Value=90;Application.DoEvents();
            Check(settings.AutoColor&&settings.PointerStyle==4&&settings.ScreenIntensity==20&&settings.PointerIntensity==90&&changes>=4,"UI controls apply color, style and independent intensity",log);
            using(var low=Snapshot(studio.Preview)){studio.ScreenSlider.Value=100;Application.DoEvents();using(var high=Snapshot(studio.Preview))Check(Hash(low)!=Hash(high),"miniature preview reacts to screen halo intensity",log);}
            using(var low=Snapshot(studio.Preview)){studio.PointerSlider.Value=0;Application.DoEvents();using(var high=Snapshot(studio.Preview))Check(Hash(low)!=Hash(high),"miniature preview reacts to pointer intensity",log);}
            using(var red=new Bitmap(192,108))using(var blue=new Bitmap(192,108))using(var rg=Graphics.FromImage(red))using(var bg=Graphics.FromImage(blue)){
                rg.Clear(Color.Red);bg.Clear(Color.Blue);studio.Preview.SetFrame(red);
                using(var first=Snapshot(studio.Preview)){studio.Preview.SetFrame(blue);using(var second=Snapshot(studio.Preview))Check(Hash(first)!=Hash(second),"live preview changes with captured screen frames",log);}
                studio.Preview.SetFrame((Bitmap)null);
            }
            studio.BrightnessSlider.Value=80;Check(studio.Preview.Brightness==80,"miniature preview reacts to PC brightness",log);
            studio.PointerSlider.Value=85;studio.StyleBox.SelectedIndex=0;studio.ColorMode.SelectedIndex=1;
            studio.Live(Color.MediumPurple,false,null,true);Application.DoEvents();
            for(int i=0;i<StudioForm.PageNames.Length;i++){studio.SelectPage(i);Application.DoEvents();using(var shot=Snapshot(studio))shot.Save(Path.Combine(folder,"studio-page-"+i+".png"));}
            studio.Size=studio.MinimumSize;studio.SelectPage(3);Application.DoEvents();Check(studio.Preview.Parent.Right<=studio.ClientSize.Width&&studio.Preview.Parent.Bottom<studio.ClientSize.Height,"preview fits a compact laptop window",log);using(var shot=Snapshot(studio))shot.Save(Path.Combine(folder,"studio-laptop.png"));
            studio.AllowClose=true;studio.Close();
        }
        var brightness=new BrightnessControl();if(brightness.Available){int initial=brightness.Current;brightness.Set(initial);Check(brightness.Current==initial,"hardware brightness supports read/write without changing the current value",log);}else log.Add("INFO hardware brightness unavailable: "+brightness.Error);
    }
}

// Analysis and drawing checked on synthetic frames: nothing here needs a screen.
static class EngineTests {
    const int W=ScreenGrabber.W,H=ScreenGrabber.H;
    static void Check(bool ok,string message,List<string> log){if(!ok)throw new Exception(message);log.Add("PASS "+message);}
    static int Rgb(int r,int g,int b){return (r<<16)|(g<<8)|b;}
    static readonly int[] Ink={Rgb(250,250,250),Rgb(40,90,200),Rgb(250,250,250),Rgb(220,60,60),Rgb(235,235,235),Rgb(30,160,90),Rgb(250,250,250),Rgb(240,170,40)};
    // A page of colored blocks, moved up by `offset` rows: what scrolling a web page looks like.
    static int[] Page(int offset){
        int[] f=new int[W*H];
        for(int y=0;y<H;y++)for(int x=0;x<W;x++){int row=(y+offset)/5,column=x/11;f[y*W+x]=Ink[(row*7+column*3+row*column)%Ink.Length];}
        return f;
    }
    static void Paint(int[] f,Rectangle r,Random random){
        for(int y=r.Top;y<r.Bottom;y+=8)for(int x=r.Left;x<r.Right;x+=8){
            int c=Rgb(random.Next(256),random.Next(120),random.Next(256));
            for(int yy=y;yy<Math.Min(r.Bottom,y+8);yy++)for(int xx=x;xx<Math.Min(r.Right,x+8);xx++)f[yy*W+xx]=c;
        }
    }
    static void Fill(int[] f,Rectangle r,int color){for(int y=r.Top;y<r.Bottom;y++)for(int x=r.Left;x<r.Right;x++)f[y*W+x]=color;}
    public static void Run(List<string> log){
        Rectangle all=new Rectangle(0,0,W,H),video=new Rectangle(40,20,96,56);var random=new Random(7);
        var windowed=new VideoDetector();int found=-1;
        for(int i=0;i<45;i++){int[] f=Page(0);Paint(f,video,random);windowed.Update(f,all,33,false);if(found<0&&windowed.Detected)found=i;}
        Check(found>=0&&found*33<=500,"a video in a window is detected in under half a second",log);
        Check(windowed.Boost>.9,"the halo reaches full video strength",log);
        Check(Math.Abs(windowed.Region.Left-video.Left)<=4&&Math.Abs(windowed.Region.Top-video.Top)<=4&&Math.Abs(windowed.Region.Right-video.Right)<=4&&Math.Abs(windowed.Region.Bottom-video.Bottom)<=4,"the detected area matches the video rectangle",log);
        for(int i=0;i<420;i++)windowed.Update(Page(0),all,33,false);
        Check(!windowed.Detected&&windowed.Boost<.02,"the halo fades out after the video stops",log);
        var scroll=new VideoDetector();bool lit=false,noticed=false;
        for(int i=0;i<90;i++){scroll.Update(Page(i*3),all,33,false);lit|=scroll.Detected;noticed|=scroll.Scrolling;}
        Check(noticed&&!lit,"scrolling a colorful page is not mistaken for a video",log);
        var spinner=new VideoDetector();lit=false;
        for(int i=0;i<90;i++){int[] f=Page(0);Fill(f,new Rectangle(90,50,10,10),i%2==0?Rgb(240,40,40):Rgb(40,40,240));spinner.Update(f,all,33,false);lit|=spinner.Detected;}
        Check(!lit,"a small animation does not light the video halo",log);
        var full=new VideoDetector();found=-1;
        for(int i=0;i<12;i++){int[] f=new int[W*H];Paint(f,all,random);full.Update(f,all,33,true);if(found<0&&full.Detected)found=i;}
        Check(found>=0&&found*33<=250,"a full-screen video is detected in a quarter of a second",log);
        int[] film=new int[W*H];Paint(film,new Rectangle(0,16,W,76),random);
        Rectangle picture=VideoDetector.TrimBlack(film,all);
        Check(picture.Top==16&&picture.Bottom==92&&picture.Left==0&&picture.Right==W,"black bars are left out of the picture",log);
        int[] zone=new int[W*H];Fill(zone,all,Rgb(200,30,20));Color warm=Colors.Ambient(zone,W,new Rectangle(0,0,40,30));
        Check(warm.A>200&&warm.R>warm.G*3&&warm.R>warm.B*3,"a video zone keeps its own color",log);
        Check(Colors.Ambient(new int[W*H],W,new Rectangle(0,0,40,30)).A==0,"a black video zone gives no light",log);
        Fill(zone,all,Rgb(40,6,4));Check(Colors.Ambient(zone,W,new Rectangle(0,0,40,30)).A<warm.A,"a dark zone gives less light than a bright one",log);
        int[] edge=new int[240*64];var renderer=new EdgeRenderer();
        renderer.Render(edge,240,64,0,new[]{Color.FromArgb(255,200,40,40),Color.FromArgb(0,200,40,40)},1,0);
        Check((edge[4]>>24&255)>200&&(edge[236]>>24&255)<30,"each part of an edge has its own strength",log);
        Check((edge[20*240+120]>>24&255)<(edge[2*240+120]>>24&255)&&(edge[63*240+120]>>24&255)==0,"the halo fades away from the screen edge",log);
        int p=edge[2*240+20],alpha=p>>24&255;Check((p>>16&255)<=alpha&&(p>>8&255)<=alpha&&(p&255)<=alpha,"edge pixels are premultiplied for the layered window",log);
        var clock=System.Diagnostics.Stopwatch.StartNew();int[] big=new int[1920*118];Color[] colors=new Color[16];for(int i=0;i<16;i++)colors[i]=Color.FromArgb(255,i*15,120,255-i*15);
        for(int i=0;i<20;i++)renderer.Render(big,1920,118,0,colors,1,i/20.0);
        log.Add("INFO one full-HD edge drawn in "+(clock.Elapsed.TotalMilliseconds/20).ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+" ms");
        clock.Restart();var timing=new VideoDetector();int[] sample=Page(0);for(int i=0;i<50;i++){Paint(sample,video,random);timing.Update(sample,all,33,false);}
        log.Add("INFO video analysis: "+(clock.Elapsed.TotalMilliseconds/50).ToString("0.00",System.Globalization.CultureInfo.InvariantCulture)+" ms per frame");
    }
}
