using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Xml.Serialization;
using System.Management;
using System.Threading;
using System.Runtime.InteropServices;

public sealed class HaloSettings {
    public bool AutoColor=true,ScreenEnabled=false,PointerEnabled=true,VideoBoost=true,VideoOnly=true,VideoModeConfigured=false,AutoUpdates=true;
    public int ColorArgb=Color.FromArgb(75,95,245).ToArgb(),ScreenIntensity=100,PointerIntensity=85,PointerStyle=0,PointerSize=100,Width=100,SettingsVersion=0;
    public Color FixedColor {get{return Color.FromArgb(ColorArgb);}}
    public static string DataDirectory {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"HaloStudio");}}
    public static string FileName {get{return Path.Combine(DataDirectory,"reglages.xml");}}
    public void Normalize(){ScreenIntensity=Clamp(ScreenIntensity,0,100);PointerIntensity=Clamp(PointerIntensity,0,100);PointerStyle=Clamp(PointerStyle,0,7);PointerSize=Clamp(PointerSize,80,160);Width=Clamp(Width,50,180);ColorArgb=Color.FromArgb(255,FixedColor.R,FixedColor.G,FixedColor.B).ToArgb();}
    static int Clamp(int x,int min,int max){return Math.Max(min,Math.Min(max,x));}
    public static HaloSettings Load(){
        try{using(var stream=File.OpenRead(FileName)){var s=(HaloSettings)new XmlSerializer(typeof(HaloSettings)).Deserialize(stream);if(!s.VideoModeConfigured){s.ScreenEnabled=false;s.VideoOnly=true;s.VideoModeConfigured=true;}if(s.SettingsVersion<5){s.VideoBoost=true;s.SettingsVersion=5;}s.Normalize();return s;}}catch{}
        var fallback=new HaloSettings();
        try{var p=File.ReadAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preferences.txt"));fallback.ScreenEnabled=bool.Parse(p[0]);fallback.PointerEnabled=bool.Parse(p[1]);fallback.ScreenIntensity=(int)(double.Parse(p[2],System.Globalization.CultureInfo.InvariantCulture)*100);}catch{}
        fallback.ScreenEnabled=false;fallback.VideoOnly=true;fallback.VideoModeConfigured=true;fallback.SettingsVersion=5;return fallback;
    }
    public void Save(){Normalize();SettingsVersion=5;Directory.CreateDirectory(DataDirectory);string temp=FileName+".tmp";using(var stream=File.Create(temp))new XmlSerializer(typeof(HaloSettings)).Serialize(stream,this);if(File.Exists(FileName))File.Replace(temp,FileName,null);else File.Move(temp,FileName);}
}

static class Branding {
    public static Icon AppIcon(){using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Halo.Icon")){try{if(stream!=null)return new Icon(stream);}catch{}return (Icon)SystemIcons.Application.Clone();}}
    public static Bitmap Logo(){using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Halo.Logo")){if(stream==null)return null;using(var image=Image.FromStream(stream))return new Bitmap(image);}}
}

static class PointerArt {
    public static readonly string[] Names={"Pointe douce","Flèche classique","Flèche fine","Diamant","Croix précise","Flèche contrastée","Chevron lumineux","Pointe futuriste"};
    public static Point Hotspot(int style,int size){double scale=size/100.0;return style==3||style==4?new Point((int)(32*scale),(int)(32*scale)):new Point((int)(17*scale),(int)(14*scale));}
    // One themed shape per Windows cursor, in the order of PointerTheme.CursorIds.
    public static readonly string[] Roles={"Flèche","Lien","Texte","Occupé","Travail en arrière-plan","Précision","Aide","Indisponible","Redimensionner haut-bas","Redimensionner gauche-droite","Redimensionner diagonale 1","Redimensionner diagonale 2","Déplacer","Autre sélection"};
    public static Point RoleHotspot(int style,int size,int role){
        double scale=size/100.0;
        if(role==0||role==4||role==6)return Hotspot(style,size);
        if(role==1)return new Point((int)(27*scale),(int)(15*scale));
        if(role==13)return new Point((int)(32*scale),(int)(15*scale));
        return new Point((int)(32*scale),(int)(31*scale));
    }
    static readonly PointF[] DoubleArrow={new PointF(15,1),new PointF(22,9),new PointF(17.5f,9),new PointF(17.5f,25),new PointF(22,25),new PointF(15,33),new PointF(8,25),new PointF(12.5f,25),new PointF(12.5f,9),new PointF(8,9)};
    static void Turned(GraphicsPath p,PointF[] points,float angle){p.AddPolygon(points);using(var m=new Matrix()){m.RotateAt(angle,new PointF(15,17));p.Transform(m);}}
    static GraphicsPath Shape(int style,int role=0){
        var p=new GraphicsPath();
        if(role==1){p.AddPolygon(new[]{new PointF(8,1),new PointF(13,1),new PointF(14,18),new PointF(18,14),new PointF(22,15),new PointF(25,18),new PointF(29,19),new PointF(31,23),new PointF(27,35),new PointF(11,35),new PointF(3,23),new PointF(5,19),new PointF(9,23)});}
        else if(role==2){p.AddPolygon(new[]{new PointF(3,1),new PointF(27,1),new PointF(27,5),new PointF(18,5),new PointF(18,30),new PointF(27,30),new PointF(27,34),new PointF(3,34),new PointF(3,30),new PointF(12,30),new PointF(12,5),new PointF(3,5)});}
        else if(role==3){p.AddEllipse(3,5,24,24);p.AddEllipse(9,11,12,12);p.AddEllipse(13,15,4,4);}
        else if(role==5){p.AddPolygon(new[]{new PointF(14,3),new PointF(16,3),new PointF(16,16),new PointF(29,16),new PointF(29,18),new PointF(16,18),new PointF(16,31),new PointF(14,31),new PointF(14,18),new PointF(1,18),new PointF(1,16),new PointF(14,16)});}
        else if(role==7){p.AddEllipse(3,5,24,24);p.AddEllipse(7.5f,9.5f,15,15);var bar=new GraphicsPath();Turned(bar,new[]{new PointF(13.2f,10.8f),new PointF(16.8f,10.8f),new PointF(16.8f,23.2f),new PointF(13.2f,23.2f)},-45);p.AddPath(bar,false);bar.Dispose();}
        else if(role==8)Turned(p,DoubleArrow,0);
        else if(role==9)Turned(p,DoubleArrow,90);
        else if(role==10)Turned(p,DoubleArrow,-45);
        else if(role==11)Turned(p,DoubleArrow,45);
        else if(role==12){p.AddPolygon(new[]{new PointF(15,1),new PointF(20,7),new PointF(17,7),new PointF(17,15),new PointF(25,15),new PointF(25,12),new PointF(31,17),new PointF(25,22),new PointF(25,19),new PointF(17,19),new PointF(17,27),new PointF(20,27),new PointF(15,33),new PointF(10,27),new PointF(13,27),new PointF(13,19),new PointF(5,19),new PointF(5,22),new PointF(-1,17),new PointF(5,12),new PointF(5,15),new PointF(13,15),new PointF(13,7),new PointF(10,7)});}
        else if(role==13){p.AddPolygon(new[]{new PointF(15,1),new PointF(24,12),new PointF(18,12),new PointF(18,33),new PointF(12,33),new PointF(12,12),new PointF(6,12)});}
        if(role!=0&&role!=4&&role!=6){using(var m=new Matrix()){m.Translate(17,14);p.Transform(m);}return p;}
        // Arrow-based cursors keep the chosen style and gain a badge: a ring while working, a question mark for help.
        if(role==4){p.AddEllipse(21,22,13,13);p.AddEllipse(25,26,5,5);}
        if(role==6)using(var format=new StringFormat())p.AddString("?",FontFamily.GenericSansSerif,(int)FontStyle.Bold,19,new PointF(18,17),format);
        if(style==0)p.AddPolygon(new[]{new PointF(0,0),new PointF(25,9),new PointF(15,16),new PointF(9,29)});
        if(style==1)p.AddPolygon(new[]{new PointF(0,0),new PointF(0,29),new PointF(8,23),new PointF(13,34),new PointF(19,31),new PointF(14,21),new PointF(27,21)});
        if(style==2)p.AddPolygon(new[]{new PointF(0,0),new PointF(9,29),new PointF(13,17),new PointF(28,11)});
        if(style==3)p.AddPolygon(new[]{new PointF(15,2),new PointF(26,18),new PointF(15,34),new PointF(4,18)});
        if(style==4)p.AddPolygon(new[]{new PointF(12,2),new PointF(18,2),new PointF(18,15),new PointF(30,15),new PointF(30,21),new PointF(18,21),new PointF(18,34),new PointF(12,34),new PointF(12,21),new PointF(0,21),new PointF(0,15),new PointF(12,15)});
        if(style==5)p.AddPolygon(new[]{new PointF(0,0),new PointF(2,32),new PointF(10,25),new PointF(15,36),new PointF(23,32),new PointF(17,21),new PointF(29,20)});
        if(style==6)p.AddPolygon(new[]{new PointF(0,0),new PointF(11,18),new PointF(0,35),new PointF(9,30),new PointF(18,18),new PointF(9,6)});
        if(style==7)p.AddPolygon(new[]{new PointF(0,0),new PointF(31,17),new PointF(5,33),new PointF(11,18)});
        using(var m=new Matrix()){m.Translate(17,14);p.Transform(m);}return p;
    }
    static readonly Dictionary<int,double[]> glows=new Dictionary<int,double[]>();
    // Glow is the blurred silhouette of the actual pointer, never a radial circle. It depends only on the shape and
    // size, so it is computed once and merely recolored when the screen color changes.
    static double[] Glow(int style,int size,int role,int n,GraphicsPath path){
        int key=style|(role<<4)|(size<<8);double[] glow;
        lock(glows)if(glows.TryGetValue(key,out glow))return glow;
        double scale=size/100.0;int[] mask;
        using(var bitmap=new Bitmap(n,n,PixelFormat.Format32bppArgb)){using(var g=Graphics.FromImage(bitmap)){g.SmoothingMode=SmoothingMode.AntiAlias;g.FillPath(Brushes.White,path);}mask=Colors.ToPixels(bitmap);}
        int radius=Math.Max(2,(int)(6*scale));double sigma=radius/2.0;double[] kernel=new double[2*radius+1];double sum=0;
        for(int i=-radius;i<=radius;i++){kernel[i+radius]=Math.Exp(-(i*i)/(2*sigma*sigma));sum+=kernel[i+radius];}for(int i=0;i<kernel.Length;i++)kernel[i]/=sum;
        double[] horizontal=new double[n*n];glow=new double[n*n];
        for(int y=0;y<n;y++)for(int x=0;x<n;x++)for(int k=-radius;k<=radius;k++)if(x+k>=0&&x+k<n)horizontal[y*n+x]+=((mask[y*n+x+k]>>24)&255)/255.0*kernel[k+radius];
        for(int y=0;y<n;y++)for(int x=0;x<n;x++)for(int k=-radius;k<=radius;k++)if(y+k>=0&&y+k<n)glow[y*n+x]+=horizontal[(y+k)*n+x]*kernel[k+radius];
        lock(glows)glows[key]=glow;return glow;
    }
    public static Bitmap Render(int style,Color color,int intensity,int size,int role=0){
        int n=(int)(68*size/100.0);double scale=size/100.0;
        using(var path=Shape(style,role)){
            using(var m=new Matrix()){m.Scale((float)scale,(float)scale);path.Transform(m);}
            double[] glow=Glow(style,size,role,n,path);
            Bitmap result=Colors.Pixels(n,n,delegate(int x,int y){return Color.FromArgb((int)Math.Min(250,glow[y*n+x]*620*intensity/100.0),color);});
            using(var g=Graphics.FromImage(result)){
                g.SmoothingMode=SmoothingMode.AntiAlias;
                if(intensity>0)using(var line=new Pen(Color.FromArgb((int)(240*intensity/100.0),color),(float)(5*scale))){line.LineJoin=LineJoin.Round;g.DrawPath(line,path);}
                using(var fill=new SolidBrush(style==5?Color.FromArgb(8,10,15):style>=6?color:Color.FromArgb(32,39,53)))g.FillPath(fill,path);
                using(var line=new Pen(Color.FromArgb(247,250,255),(float)((style==5?3.2:1.5)*scale))){line.LineJoin=LineJoin.Round;g.DrawPath(line,path);}
                if(role==0&&(style==3||style==4))using(var dot=new SolidBrush(color))g.FillEllipse(dot,(float)(30*scale),(float)(30*scale),(float)(4*scale),(float)(4*scale));
            }return result;
        }
    }
    [StructLayout(LayoutKind.Sequential)] struct ICONINFO { [MarshalAs(UnmanagedType.Bool)] public bool Icon;public uint X,Y;public IntPtr Mask,Color; }
    [DllImport("user32.dll",SetLastError=true)] static extern IntPtr CreateIconIndirect(ref ICONINFO info);
    [DllImport("gdi32.dll")] static extern IntPtr CreateBitmap(int width,int height,uint planes,uint bits,byte[] bytes);
    public static IntPtr CreateCursor(int style,Color color,int intensity,int size,int role=0){
        using(var b=Render(style,color,intensity,size,role)){
            IntPtr pixels=b.GetHbitmap(Color.FromArgb(0));IntPtr mask=CreateBitmap(b.Width,b.Height,1,1,new byte[((b.Width+15)/16)*2*b.Height]);
            try{Point hot=RoleHotspot(style,size,role);var info=new ICONINFO{Icon=false,X=(uint)hot.X,Y=(uint)hot.Y,Mask=mask,Color=pixels};IntPtr cursor=CreateIconIndirect(ref info);if(cursor==IntPtr.Zero)throw new System.ComponentModel.Win32Exception();return cursor;}
            finally{Native.DeleteObject(pixels);Native.DeleteObject(mask);}
        }
    }
}

sealed class BrightnessControl {
    public int Current;public string Instance;public bool Available;public string Error;
    public BrightnessControl(){Refresh();}
    public void Refresh(){
        try{using(var search=new ManagementObjectSearcher("root\\WMI","SELECT * FROM WmiMonitorBrightness WHERE Active=True"))using(var rows=search.Get())foreach(ManagementObject row in rows)using(row){Current=Convert.ToInt32(row["CurrentBrightness"]);Instance=Convert.ToString(row["InstanceName"]);Available=true;Error=null;return;}
            Available=false;Error="Cet écran ne permet pas le réglage de luminosité via Windows.";
        }catch{Available=false;Error="Le réglage de luminosité n’est pas disponible sur cet écran.";}
    }
    public void Set(int value){
        if(!Available)throw new InvalidOperationException(Error);bool found=false;
        using(var search=new ManagementObjectSearcher("root\\WMI","SELECT * FROM WmiMonitorBrightnessMethods WHERE Active=True"))using(var rows=search.Get())foreach(ManagementObject row in rows)using(row){
            if(Convert.ToString(row["InstanceName"])!=Instance)continue;
            using(var input=row.GetMethodParameters("WmiSetBrightness")){input["Timeout"]=(uint)0;input["Brightness"]=(byte)Math.Max(0,Math.Min(100,value));using(var output=row.InvokeMethod("WmiSetBrightness",input,null)){if(output!=null&&Convert.ToUInt32(output["ReturnValue"])!=0)throw new InvalidOperationException("Windows a refusé ce niveau de luminosité.");}}found=true;
        }
        if(!found)throw new InvalidOperationException("L’écran n’est plus connecté.");Refresh();
    }
}

// Miniature screen showing the live capture with the halo and pointer as they are configured.
sealed class PreviewPanel : FluentControl {
    public HaloSettings Settings;public Color LiveColor=Color.RoyalBlue;public int Brightness=100;public bool Paused,VideoDetected;Bitmap frame;
    public PreviewPanel(){Size=new Size(Theme.S(176),Theme.S(110));AccessibleName="Aperçu en temps réel";}
    public void SetFrame(Bitmap image){if(frame!=null)frame.Dispose();frame=image==null?null:(Bitmap)image.Clone();Invalidate();}
    public void SetFrame(int[] pixels){if(frame!=null)frame.Dispose();frame=pixels==null?null:Colors.FromPixels(pixels,ScreenGrabber.W,ScreenGrabber.H,PixelFormat.Format32bppRgb);Invalidate();}
    protected override void Dispose(bool disposing){if(disposing&&frame!=null)frame.Dispose();base.Dispose(disposing);}
    public void RefreshState(Color color,bool paused){if(color.ToArgb()!=LiveColor.ToArgb()||Paused!=paused){LiveColor=color;Paused=paused;Invalidate();}}
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;var s=Settings??new HaloSettings();Color color=s.AutoColor?LiveColor:s.FixedColor;
        int bezel=Math.Max(3,Theme.S(4)),w=Width-2*bezel,h=Height-2*bezel;if(w<20||h<20)return;
        using(var scene=new Bitmap(w,h,PixelFormat.Format32bppPArgb))using(var sg=Graphics.FromImage(scene)){
            sg.InterpolationMode=InterpolationMode.HighQualityBilinear;
            if(frame!=null)sg.DrawImage(frame,new Rectangle(0,0,w,h));else sg.Clear(Theme.Dark?Color.FromArgb(52,56,66):Color.FromArgb(226,232,242));
            if(!Paused&&(s.ScreenEnabled||(s.VideoOnly&&VideoDetected))){
                int d=Math.Max(6,Math.Min(h/3,(int)(h*(s.VideoBoost&&VideoDetected?.2:.14)*s.Width/100.0)));Color[] palette={color,color};
                for(int side=0;side<4;side++)using(var b=Colors.Edge(side<2?w:d,side<2?d:h-2*d,side,palette,s.ScreenIntensity/100.0*(VideoDetected?1.3:1)))sg.DrawImageUnscaled(b,side==3?w-d:0,side==1?h-d:side>=2?d:0);
            }
            if(s.PointerEnabled&&!Paused)using(var pointer=PointerArt.Render(s.PointerStyle,color,s.PointerIntensity,s.PointerSize)){
                int size=pointer.Width*h/190;sg.DrawImage(pointer,new Rectangle(w/2-size/3,h/2-size/3,size,size));
            }
            // The hardware brightness can only be suggested here: a veil stands in for a dimmer panel.
            if(Brightness<100)using(var veil=new SolidBrush(Color.FromArgb((100-Brightness)*140/100,0,0,0)))sg.FillRectangle(veil,0,0,w,h);
            g.SmoothingMode=SmoothingMode.AntiAlias;
            using(var path=Theme.Round(new RectangleF(0,0,Width-1,Height-1),Theme.S(7)))using(var brush=new SolidBrush(Color.FromArgb(16,17,20)))g.FillPath(brush,path);
            using(var clip=Theme.Round(new RectangleF(bezel,bezel,w,h),Theme.S(3))){var state=g.Save();g.SetClip(clip);g.DrawImageUnscaled(scene,bezel,bezel);g.Restore(state);}
        }
    }
}

// The settings window, laid out like Windows 11 Settings: navigation on the left, a header with the live
// preview, then cards of setting rows. All positions are computed from the window size and monitor scale.
sealed class StudioForm : Form {
    public readonly HaloSettings Settings;public PreviewPanel Preview;public BrightnessControl Brightness;
    public bool AllowClose;public FluentSlider ScreenSlider,PointerSlider,BrightnessSlider;public FluentCombo StyleBox,ColorMode;
    public FluentButton CheckUpdateButton,InstallUpdateButton;public TextBlock UpdateStatus;
    public event Action Changed,PauseRequested,QuitRequested,CheckUpdatesRequested,InstallUpdateRequested;
    public static readonly string[] PageNames={"Accueil","Couleurs","Souris","Écran","Vidéo","Mises à jour"};
    static readonly string[] PageIcons={"home","colors","mouse","screen","video","update"};
    static readonly string[] AmbianceNames={"Automatique","Indigo","Améthyste","Néon rose","Lagon","Coucher de soleil"};
    static readonly Color[] Swatches={Color.FromArgb(75,95,245),Color.FromArgb(160,65,225),Color.FromArgb(230,55,140),Color.FromArgb(0,165,190),Color.FromArgb(20,170,100),Color.FromArgb(245,125,35),
        Color.FromArgb(235,50,60),Color.FromArgb(250,190,30),Color.FromArgb(130,205,40),Color.FromArgb(40,140,250),Color.FromArgb(255,120,160),Color.FromArgb(240,240,245)};
    static readonly string[] SwatchNames={"Indigo","Améthyste","Néon rose","Lagon","Forêt","Coucher de soleil","Rubis","Or","Citron vert","Azur","Corail","Blanc doux"};
    static readonly Color[] Ambiances={Color.Empty,Swatches[0],Swatches[1],Swatches[2],Swatches[3],Swatches[5]};
    readonly List<PagePanel> pages=new List<PagePanel>();readonly List<NavItem> navigation=new List<NavItem>();readonly List<SettingRow> rows=new List<SettingRow>();
    readonly List<Action> binders=new List<Action>();readonly List<Bitmap> art=new List<Bitmap>();
    readonly bool persist;bool loading=true,paused,videoNow;int current;string status,message="Prêt à vérifier les mises à jour.";Color liveColor=Color.RoyalBlue;
    FluentControl side,hero;SearchBox search;TextBlock title,headline,detail,searchHint;FluentButton pauseLink;NavItem pauseItem,quitItem;InfoTile colorTile,updateTile;
    SettingRow brightnessRow,detectionRow;Bitmap logo;System.Windows.Forms.Timer saveTimer,brightnessTimer;
    public StudioForm(HaloSettings settings,bool probeBrightness){
        persist=probeBrightness;Settings=settings;Text="Halo Studio";StartPosition=FormStartPosition.CenterScreen;AutoScaleMode=AutoScaleMode.None;Icon=Branding.AppIcon();
        SetStyle(ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.ResizeRedraw,true);
        Theme.Apply(Theme.SystemDark());logo=Branding.Logo();
        using(var g=CreateGraphics())Theme.Scale=Math.Max(1,g.DpiX/96f);Theme.ResetFonts();
        ClientSize=new Size(Theme.S(1120),Theme.S(740));MinimumSize=new Size(Theme.S(900),Theme.S(620));
        saveTimer=new System.Windows.Forms.Timer{Interval=350};saveTimer.Tick+=delegate{saveTimer.Stop();try{Settings.Save();}catch{SetStatus("Impossible d’enregistrer les réglages.");}};
        brightnessTimer=new System.Windows.Forms.Timer{Interval=350};brightnessTimer.Tick+=delegate{brightnessTimer.Stop();if(Brightness==null||!Brightness.Available)return;try{Brightness.Set(BrightnessSlider.Value);brightnessRow.Description="Luminosité matérielle · Windows";}catch(Exception ex){brightnessRow.Description=ex.Message;}brightnessRow.Invalidate();};
        Build();
        if(probeBrightness){
            // The hardware query can take a second: it must not delay the window.
            System.Threading.Tasks.Task.Factory.StartNew(delegate{return new BrightnessControl();}).ContinueWith(delegate(System.Threading.Tasks.Task<BrightnessControl> done){
                try{if(!IsDisposed&&IsHandleCreated)BeginInvoke((Action)delegate{Brightness=done.Result;ShowBrightness();});}catch{}
            });
            try{Microsoft.Win32.SystemEvents.UserPreferenceChanged+=ThemeChanged;}catch{}
        }
        FormClosing+=delegate(object sender,FormClosingEventArgs e){if(!AllowClose){e.Cancel=true;Hide();}};
    }
    void ThemeChanged(object sender,Microsoft.Win32.UserPreferenceChangedEventArgs e){
        if(e.Category!=Microsoft.Win32.UserPreferenceCategory.General||IsDisposed||!IsHandleCreated)return;
        BeginInvoke((Action)delegate{bool dark=Theme.SystemDark();if(dark==Theme.Dark)return;Theme.Apply(dark);search.Restyle();Chrome();Invalidate(true);});
    }
    // Title bar in the color of the backdrop and dark caption buttons when needed (Windows 11; ignored elsewhere).
    void Chrome(){
        try{
            int dark=Theme.Dark?1:0;Native.DwmSetWindowAttribute(Handle,20,ref dark,4);
            Color top=Theme.BackdropTop();int caption=top.R|(top.G<<8)|(top.B<<16);Native.DwmSetWindowAttribute(Handle,35,ref caption,4);
        }catch{}
    }
    protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);Chrome();}
    protected override void OnPaintBackground(PaintEventArgs e){Theme.PaintBackdrop(e.Graphics,ClientRectangle);}
    protected override void OnResize(EventArgs e){base.OnResize(e);Arrange();}
    protected override void WndProc(ref Message m){
        // Moving to a monitor with another scale: take the size Windows suggests and rebuild at the new scale.
        if(m.Msg==0x02E0&&side!=null){
            float scale=Math.Max(1,(m.WParam.ToInt32()>>16&0xffff)/96f);
            if(Math.Abs(scale-Theme.Scale)>.01f){
                var r=(Native.RECT)System.Runtime.InteropServices.Marshal.PtrToStructure(m.LParam,typeof(Native.RECT));Theme.Scale=scale;Theme.ResetFonts();
                MinimumSize=Size.Empty;Bounds=Rectangle.FromLTRB(r.Left,r.Top,r.Right,r.Bottom);MinimumSize=new Size(Theme.S(900),Theme.S(620));Build();
            }
        }
        base.WndProc(ref m);
    }

    SettingRow Row(Card card,int page,string icon,string name,string description,Control action=null,string keywords=null){
        var row=card.Add(new SettingRow(icon,name,description,action){Page=page,Keywords=keywords});rows.Add(row);return row;
    }
    ToggleSwitch Toggle(Card card,int page,string icon,string name,string description,Func<bool> read,Action<bool> write,string keywords=null){
        var toggle=new ToggleSwitch{Checked=read()};var row=Row(card,page,icon,name,description,toggle,keywords);
        Action show=delegate{toggle.Checked=read();string label=toggle.Checked?"Activé":"Désactivé";if(row.Value!=label){row.Value=label;row.Invalidate();}};
        toggle.CheckedChanged+=delegate{if(loading)return;write(toggle.Checked);Apply();};binders.Add(show);show();return toggle;
    }
    FluentSlider Slider(Card card,int page,string icon,string name,string description,int min,int max,Func<int> read,Action<int> write,string keywords=null){
        var slider=new FluentSlider{Minimum=min,Maximum=max,Value=read()};var row=Row(card,page,icon,name,description,slider,keywords);
        Action show=delegate{string label=slider.Value+" %";if(row.Value!=label){row.Value=label;row.Invalidate();}};
        slider.ValueChanged+=delegate{show();if(loading)return;write(slider.Value);Apply();};binders.Add(delegate{slider.Value=read();show();});show();return slider;
    }
    PagePanel Page(){var p=new PagePanel{Visible=false};pages.Add(p);Controls.Add(p);return p;}
    int AmbianceIndex(){if(Settings.AutoColor)return 0;for(int i=1;i<Ambiances.Length;i++)if(Ambiances[i].ToArgb()==Settings.FixedColor.ToArgb())return i;return -1;}
    int SwatchIndex(){if(Settings.AutoColor)return -1;for(int i=0;i<Swatches.Length;i++)if(Swatches[i].ToArgb()==Settings.FixedColor.ToArgb())return i;return -1;}

    void Build(){
        loading=true;SuspendLayout();
        foreach(Control c in new List<Control>(System.Linq.Enumerable.Cast<Control>(Controls)))c.Dispose();
        Controls.Clear();pages.Clear();navigation.Clear();rows.Clear();binders.Clear();foreach(var b in art)b.Dispose();art.Clear();
        var s=Settings;

        side=new FluentControl();Controls.Add(side);
        side.Paint+=delegate(object sender,PaintEventArgs e){
            int mark=Theme.S(46);
            if(logo!=null){e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;e.Graphics.DrawImage(logo,new Rectangle(Theme.S(18),Theme.S(16),mark,mark));}
            TextRenderer.DrawText(e.Graphics,"Halo Studio",Theme.Font(14,true),new Rectangle(Theme.S(76),Theme.S(18),side.Width-Theme.S(80),Theme.S(22)),Theme.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding|TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(e.Graphics,"Version "+BuildInfo.Version.Replace("-beta."," · bêta "),Theme.Font(12),new Rectangle(Theme.S(76),Theme.S(40),side.Width-Theme.S(80),Theme.S(20)),Theme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding|TextFormatFlags.EndEllipsis);
        };
        search=new SearchBox("Rechercher un réglage");side.Controls.Add(search);search.Input.TextChanged+=delegate{Search(search.Input.Text);};
        searchHint=new TextBlock("",12,false,true){Visible=false};side.Controls.Add(searchHint);
        for(int i=0;i<PageNames.Length;i++){int index=i;var item=new NavItem(PageNames[i],PageIcons[i]);item.Click+=delegate{SelectPage(index);};navigation.Add(item);side.Controls.Add(item);}
        pauseItem=new NavItem("Mettre en pause","pause"){Tinted=false};pauseItem.Click+=delegate{if(PauseRequested!=null)PauseRequested();};side.Controls.Add(pauseItem);
        quitItem=new NavItem("Quitter Halo","power"){Tinted=false};quitItem.Click+=delegate{if(QuitRequested!=null)QuitRequested();};side.Controls.Add(quitItem);

        title=new TextBlock(PageNames[0],28,true){Height=Theme.S(40)};Controls.Add(title);
        hero=new FluentControl();Controls.Add(hero);
        Preview=new PreviewPanel{Settings=s,LiveColor=liveColor,Paused=paused,VideoDetected=videoNow};hero.Controls.Add(Preview);
        headline=new TextBlock("Halo actif",20,true){Height=Theme.S(28)};hero.Controls.Add(headline);
        detail=new TextBlock("",14,false,true){Height=Theme.S(22)};hero.Controls.Add(detail);
        pauseLink=new FluentButton("Mettre en pause"){Link=true};pauseLink.Click+=delegate{if(PauseRequested!=null)PauseRequested();};hero.Controls.Add(pauseLink);
        colorTile=new InfoTile("colors","Couleur");colorTile.Click+=delegate{SelectPage(1);};hero.Controls.Add(colorTile);
        updateTile=new InfoTile("update","Mises à jour");updateTile.Click+=delegate{SelectPage(5);};hero.Controls.Add(updateTile);

        // Accueil
        var home=Page();
        var recommended=home.Add(new Card("Réglages recommandés","Les réglages les plus utilisés"),0);
        Toggle(recommended,0,"video","Halo vidéo","S’allume tout seul quand une vidéo est à l’écran",delegate{return s.VideoOnly;},delegate(bool v){s.VideoOnly=v;});
        Toggle(recommended,0,"mouse","Pointeur lumineux","Un contour de lumière autour du pointeur",delegate{return s.PointerEnabled;},delegate(bool v){s.PointerEnabled=v;});
        var modeLink=Row(recommended,0,"colors","Mode de couleur",null);modeLink.AsLink();modeLink.Click+=delegate{SelectPage(1);};
        binders.Add(delegate{string label=s.AutoColor?"Automatique":"Couleur unique";if(modeLink.Value!=label){modeLink.Value=label;modeLink.Invalidate();}});
        var shortcuts=home.Add(new Card(),0);
        Row(shortcuts,0,"keyboard","Pause ou reprise","Depuis n’importe quelle application",null,"raccourci clavier").Value="Ctrl + Alt + Maj + H";
        Row(shortcuts,0,"power","Quitter Halo","Arrête les effets et restaure le pointeur",null,"raccourci clavier fermer").Value="Ctrl + Alt + Maj + Q";
        var cinema=home.Add(new Card("Mode cinéma"),1);
        cinema.Add(new TextBlock("Pendant une vidéo, le halo s’élargit, gagne en intensité et suit les couleurs de l’image au plus près, barres noires exclues. Le reste du temps, il reste discret.",14,false,true){AutoHeight=true});
        var cinemaButton=cinema.Add(new FluentButton("Activer le mode cinéma",true));
        cinemaButton.Click+=delegate{s.VideoBoost=!s.VideoBoost;if(s.VideoBoost)s.VideoOnly=true;Apply();};
        binders.Add(delegate{string label=s.VideoBoost?"Désactiver le mode cinéma":"Activer le mode cinéma";if(cinemaButton.Text!=label){cinemaButton.Primary=!s.VideoBoost;cinemaButton.Text=label;cinemaButton.AccessibleName=label;cinemaButton.AutoWidth();}});
        var personal=home.Add(new Card("Personnalisez votre halo"),1);
        var ambiance=personal.Add(new TileStrip{Count=Ambiances.Length,Columns=3,Names=AmbianceNames,AccessibleName="Ambiances",Painter=PaintAmbiance});
        ambiance.Picked+=delegate(int i){if(i==0)s.AutoColor=true;else{s.AutoColor=false;s.ColorArgb=Ambiances[i].ToArgb();}Apply();};
        binders.Add(delegate{ambiance.SelectedIndex=AmbianceIndex();});

        // Couleurs
        var colors=Page();var modeCard=colors.Add(new Card());
        ColorMode=new FluentCombo();ColorMode.Items.Add("Automatique · applications et vidéos");ColorMode.Items.Add("Couleur unique");ColorMode.SelectedIndex=s.AutoColor?0:1;
        Row(modeCard,1,"colors","Mode de couleur","La même ambiance pour l’écran et le pointeur",ColorMode,"automatique teinte synchronisation");
        ColorMode.SelectedIndexChanged+=delegate{if(loading)return;s.AutoColor=ColorMode.SelectedIndex==0;Apply();};binders.Add(delegate{ColorMode.SelectedIndex=s.AutoColor?0:1;});
        var palette=colors.Add(new Card("Ta couleur","Choisir une teinte passe en couleur unique"));
        var swatches=palette.Add(new TileStrip{Count=Swatches.Length,TileWidth=Theme.S(54),TileHeight=Theme.S(42),Names=SwatchNames,AccessibleName="Couleurs proposées",Painter=delegate(Graphics g,Rectangle r,int i){Theme.Fill(g,Rectangle.Inflate(r,-Theme.S(5),-Theme.S(5)),Theme.S(4),Swatches[i],Color.Empty);}});
        swatches.Picked+=delegate(int i){s.AutoColor=false;s.ColorArgb=Swatches[i].ToArgb();Apply();};binders.Add(delegate{swatches.SelectedIndex=SwatchIndex();});
        var choose=palette.Add(new FluentButton("Autre couleur…"));
        choose.Click+=delegate{using(var dialog=new ColorDialog{Color=s.FixedColor,FullOpen=true})if(dialog.ShowDialog(this)==DialogResult.OK){s.ColorArgb=dialog.Color.ToArgb();s.AutoColor=false;Apply();}};

        // Souris
        var mouse=Page();var pointerCard=mouse.Add(new Card());
        Toggle(pointerCard,2,"mouse","Pointeur lumineux","Flèche, main et curseur de texte prennent le thème choisi",delegate{return s.PointerEnabled;},delegate(bool v){s.PointerEnabled=v;},"curseur souris");
        StyleBox=new FluentCombo();StyleBox.Items.AddRange(PointerArt.Names);StyleBox.SelectedIndex=s.PointerStyle;
        Row(pointerCard,2,"glow","Style du pointeur",null,StyleBox,"forme flèche diamant croix chevron");
        StyleBox.SelectedIndexChanged+=delegate{if(loading)return;s.PointerStyle=StyleBox.SelectedIndex;Apply();};binders.Add(delegate{StyleBox.SelectedIndex=s.PointerStyle;});
        for(int i=0;i<PointerArt.Names.Length;i++)art.Add(PointerArt.Render(i,Swatches[0],90,70));
        var styles=pointerCard.Add(new TileStrip{Count=PointerArt.Names.Length,Names=PointerArt.Names,AccessibleName="Styles de pointeur",Painter=delegate(Graphics g,Rectangle r,int i){Bitmap b=art[i];g.DrawImageUnscaled(b,r.X+(r.Width-b.Width)/2-Theme.S(3),r.Y+(r.Height-b.Height)/2-Theme.S(2));}});
        styles.Picked+=delegate(int i){StyleBox.SelectedIndex=i;};binders.Add(delegate{styles.SelectedIndex=s.PointerStyle;});
        var pointerSliders=mouse.Add(new Card());
        var family=mouse.Add(new Card("Tous les pointeurs","Chaque pointeur de Windows reçoit le thème : lien, texte, attente, redimensionnement, déplacement…"));
        var roleStrip=family.Add(new TileStrip{Count=PointerArt.Roles.Length,Names=PointerArt.Roles,AccessibleName="Pointeurs thémés",TabStop=false,Painter=delegate(Graphics g,Rectangle r,int i){
            using(var b=PointerArt.Render(s.PointerStyle,s.AutoColor?liveColor:s.FixedColor,s.PointerIntensity,80,i)){Point hot=PointerArt.RoleHotspot(s.PointerStyle,80,i);bool arrow=i==0||i==4||i==6;g.DrawImageUnscaled(b,r.X+r.Width/2-(arrow?hot.X+Theme.S(9):b.Width*47/100),r.Y+r.Height/2-(arrow?hot.Y+Theme.S(11):b.Height*46/100));}}});
        binders.Add(delegate{roleStrip.Invalidate();});
        PointerSlider=Slider(pointerSliders,2,"sun","Intensité du contour lumineux",null,0,100,delegate{return s.PointerIntensity;},delegate(int v){s.PointerIntensity=v;},"lueur souris");
        Slider(pointerSliders,2,"size","Taille du pointeur",null,80,160,delegate{return s.PointerSize;},delegate(int v){s.PointerSize=v;},"grand petit souris");

        // Écran
        var display=Page();var glow=display.Add(new Card());
        Toggle(glow,3,"screen","Halo permanent","Visible même sans vidéo, aux couleurs de l’application active",delegate{return s.ScreenEnabled;},delegate(bool v){s.ScreenEnabled=v;},"toujours bords lumière");
        ScreenSlider=Slider(glow,3,"sun","Intensité du halo",null,0,100,delegate{return s.ScreenIntensity;},delegate(int v){s.ScreenIntensity=v;},"force lumière");
        Slider(glow,3,"size","Étendue du halo","Largeur de la lumière sur les bords",50,180,delegate{return s.Width;},delegate(int v){s.Width=v;},"largeur taille");
        var panel=display.Add(new Card());
        BrightnessSlider=new FluentSlider{Minimum=0,Maximum=100,Value=100,Enabled=false};
        brightnessRow=Row(panel,3,"sun","Luminosité du PC","Recherche de l’écran…",BrightnessSlider,"écran matériel portable");brightnessRow.Value="";
        BrightnessSlider.ValueChanged+=delegate{Preview.Brightness=BrightnessSlider.Value;Preview.Invalidate();brightnessRow.Value=BrightnessSlider.Value+" %";brightnessRow.Invalidate();if(!loading&&Brightness!=null&&Brightness.Available){brightnessTimer.Stop();brightnessTimer.Start();}};
        ShowBrightness();

        // Vidéo
        var video=Page();var videoCard=video.Add(new Card());
        Toggle(videoCard,4,"video","Halo vidéo","S’allume quand une vidéo est détectée, s’éteint ensuite",delegate{return s.VideoOnly;},delegate(bool v){s.VideoOnly=v;},"film youtube automatique");
        Toggle(videoCard,4,"cinema","Mode cinéma","Halo plus large et plus intense pendant les vidéos",delegate{return s.VideoBoost;},delegate(bool v){s.VideoBoost=v;},"film amplification plein écran");
        detectionRow=Row(videoCard,4,"info","Détection","Aucune vidéo à l’écran pour l’instant",null,"état");
        var about=video.Add(new Card());
        about.Add(new TextBlock("Halo repère la zone de l’écran qui bouge comme une vidéo et lit ses couleurs bord par bord, sans les barres noires. En plein écran, il réagit immédiatement. Faire défiler une page ne l’allume pas. Les vidéos protégées et certains jeux en plein écran exclusif ne peuvent pas être lus.",14,false,true){AutoHeight=true});

        // Mises à jour
        var updates=Page();var version=updates.Add(new Card());
        Row(version,5,"info","Halo Studio · canal bêta","Les nouveautés arrivent d’abord en version bêta",null,"version").Value=BuildInfo.Version;
        Toggle(version,5,"update","Mises à jour automatiques","Téléchargées, puis installées à la fermeture",delegate{return s.AutoUpdates;},delegate(bool v){s.AutoUpdates=v;});
        var actions=updates.Add(new Card());
        UpdateStatus=actions.Add(new TextBlock(message,14,false,true){AutoHeight=true});
        CheckUpdateButton=actions.Add(new FluentButton("Vérifier et télécharger",true));CheckUpdateButton.Click+=delegate{if(CheckUpdatesRequested!=null)CheckUpdatesRequested();};
        InstallUpdateButton=actions.Add(new FluentButton("Installer et relancer"){Enabled=false});InstallUpdateButton.Click+=delegate{if(InstallUpdateRequested!=null)InstallUpdateRequested();};

        ResumeLayout(false);foreach(var bind in binders)bind();loading=false;
        Arrange();SelectPage(Math.Min(current,pages.Count-1));ShowState();
    }
    void PaintAmbiance(Graphics g,Rectangle r,int i){
        Rectangle inner=Rectangle.Inflate(r,-Theme.S(5),-Theme.S(5));if(inner.Width<8||inner.Height<8)return;
        g.SmoothingMode=SmoothingMode.AntiAlias;
        using(var path=Theme.Round(inner,Theme.S(4))){
            if(i==0)using(var brush=new LinearGradientBrush(inner,Color.Black,Color.Black,20f)){brush.InterpolationColors=new ColorBlend{Colors=new[]{Swatches[3],Swatches[0],Swatches[2],Swatches[5]},Positions=new[]{0f,.36f,.7f,1f}};g.FillPath(brush,path);}
            else using(var brush=new LinearGradientBrush(inner,Colors.Mix(Ambiances[i],Color.White,.25),Colors.Mix(Ambiances[i],Color.Black,.3),60f))g.FillPath(brush,path);
        }
        // A small dark screen in the middle: the tile reads as "this light around your display".
        Rectangle screen=new Rectangle(inner.X+inner.Width*9/100,inner.Y+inner.Height*26/100,inner.Width*82/100,inner.Height*48/100);
        Theme.Fill(g,screen,Theme.S(3),Color.FromArgb(232,20,22,28),Color.Empty);
        TextRenderer.DrawText(g,AmbianceNames[i],Theme.Font(11),screen,Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
    }
    void ShowBrightness(){
        if(BrightnessSlider==null)return;bool available=Brightness!=null&&Brightness.Available,was=loading;loading=true;
        BrightnessSlider.Enabled=available;if(available)BrightnessSlider.Value=Brightness.Current;
        brightnessRow.Description=available?"Luminosité matérielle · Windows":Brightness==null?(persist?"Recherche de l’écran…":"Non mesurée"):"Écran non compatible : utiliser ses boutons physiques";
        brightnessRow.Value=available?BrightnessSlider.Value+" %":"";brightnessRow.Invalidate();loading=was;
    }

    void Arrange(){
        if(side==null||ClientSize.Width<50)return;
        int w=ClientSize.Width,h=ClientSize.Height,rail=Theme.S(w>=Theme.S(1040)?280:236),pad=Theme.S(12);
        side.SetBounds(0,0,rail,h);search.SetBounds(pad+Theme.S(4),Theme.S(78),rail-2*pad-Theme.S(8),search.Height);
        searchHint.SetBounds(pad+Theme.S(8),search.Bottom+Theme.S(4),rail-2*pad-Theme.S(16),Theme.S(18));
        int y=search.Bottom+Theme.S(26);foreach(var item in navigation){item.SetBounds(pad,y,rail-2*pad,item.Height);y+=item.Height+Theme.S(4);}
        quitItem.SetBounds(pad,h-pad-quitItem.Height,rail-2*pad,quitItem.Height);pauseItem.SetBounds(pad,quitItem.Top-Theme.S(4)-pauseItem.Height,rail-2*pad,pauseItem.Height);
        int x=rail+Theme.S(28),width=Math.Min(Theme.S(1040),w-x-Theme.S(24));
        title.SetBounds(x,Theme.S(18),width,Theme.S(40));
        int heroTop=Theme.S(72),heroHeight=Theme.S(112);hero.SetBounds(x,heroTop,width,heroHeight);
        Preview.SetBounds(0,Theme.S(1),Theme.S(176),Theme.S(110));
        // Status tiles on the right only when the header is wide enough to keep the text readable.
        int tiles=width>=Theme.S(840)?2:width>=Theme.S(610)?1:0,tileWidth=colorTile.Width,textLeft=Preview.Right+Theme.S(20),textWidth=Math.Max(Theme.S(120),width-textLeft-tiles*(tileWidth+Theme.S(8)));
        headline.SetBounds(textLeft,Theme.S(14),textWidth,Theme.S(28));detail.SetBounds(textLeft,Theme.S(44),textWidth,Theme.S(22));pauseLink.Location=new Point(textLeft,Theme.S(68));
        updateTile.Visible=tiles>=1;colorTile.Visible=tiles>=2;
        updateTile.Location=new Point(width-tileWidth,Theme.S(22));colorTile.Location=new Point(width-2*tileWidth-Theme.S(8),Theme.S(22));
        int top=hero.Bottom+Theme.S(16);foreach(var page in pages)page.SetBounds(x,top,w-x-Theme.S(8),Math.Max(Theme.S(80),h-top-Theme.S(8)));
    }
    public void SelectPage(int index){
        current=index;for(int i=0;i<pages.Count;i++){pages[i].Visible=i==index;if(navigation[i].Selected!=(i==index)){navigation[i].Selected=i==index;navigation[i].Invalidate();}}
        title.Text=PageNames[index];
    }
    void Search(string text){
        string query=Theme.Plain(text).Trim();
        if(query.Length<2){searchHint.Visible=false;return;}
        var found=rows.FindAll(delegate(SettingRow r){return r.Matches(query);});
        searchHint.Text=found.Count==0?"Aucun réglage trouvé":found.Count==1?"1 réglage · "+PageNames[found[0].Page]:found.Count+" réglages · "+PageNames[found[0].Page];searchHint.Visible=true;
        if(found.Count==0)return;
        SelectPage(found[0].Page);pages[found[0].Page].ScrollControlIntoView(found[0]);
        foreach(var row in found)if(row.Page==found[0].Page)row.Flash();
    }
    void Apply(){
        if(loading)return;Settings.Normalize();loading=true;try{foreach(var bind in binders)bind();}finally{loading=false;}
        Preview.Invalidate();ShowState();if(persist){saveTimer.Stop();saveTimer.Start();}if(Changed!=null)Changed();
    }
    void ShowState(){
        if(headline==null)return;
        string head=paused?"Halo en pause":videoNow?"Vidéo détectée":"Halo actif";
        string line=status??(paused?"Pointeur d’origine restauré":videoNow?"Le halo suit les couleurs de l’image":Settings.AutoColor?"Couleurs synchronisées avec l’écran":"Couleur unique");
        if(headline.Text!=head)headline.Text=head;if(detail.Text!=line)detail.Text=line;
        string action=paused?"Reprendre":"Mettre en pause";
        if(pauseLink.Text!=action){pauseLink.Text=action;pauseLink.AccessibleName=action;pauseLink.AutoWidth();pauseItem.Text=action;pauseItem.Icon=paused?"play":"pause";pauseItem.AccessibleName=action;}
        Color shown=Settings.AutoColor?liveColor:Settings.FixedColor;
        colorTile.Set((Settings.AutoColor?"Automatique · ":"Unique · ")+ColorTranslator.ToHtml(Color.FromArgb(shown.R,shown.G,shown.B)),shown);updateTile.Set(message);
        string seen=videoNow?"Vidéo à l’écran : le halo la suit":"Aucune vidéo à l’écran pour l’instant";
        if(detectionRow.Description!=seen){detectionRow.Description=seen;detectionRow.Value=videoNow?"Active":"";detectionRow.Invalidate();}
    }
    public bool PreviewVisible {get{return Visible&&WindowState!=FormWindowState.Minimized;}}
    public void SetStatus(string text){status=text;ShowState();}
    public void SetUpdate(string text,bool busy,bool pending){
        if(text!=message){message=text;UpdateStatus.Text=text;ShowState();}
        if(CheckUpdateButton.Enabled==busy)CheckUpdateButton.Enabled=!busy;if(InstallUpdateButton.Enabled!=pending){InstallUpdateButton.Enabled=pending;InstallUpdateButton.Primary=pending;InstallUpdateButton.Invalidate();}
    }
    public void Live(Color color,bool nowPaused,int[] frame=null,bool videoDetected=false){
        bool changed=color.ToArgb()!=liveColor.ToArgb()||paused!=nowPaused||videoNow!=videoDetected;
        liveColor=color;paused=nowPaused;videoNow=videoDetected;
        if(Preview.VideoDetected!=videoDetected){Preview.VideoDetected=videoDetected;Preview.Invalidate();}
        Preview.RefreshState(color,nowPaused);if(frame!=null&&PreviewVisible)Preview.SetFrame(frame);
        if(changed)ShowState();
    }
    protected override void Dispose(bool disposing){
        if(disposing){
            try{Microsoft.Win32.SystemEvents.UserPreferenceChanged-=ThemeChanged;}catch{}
            if(saveTimer!=null)saveTimer.Dispose();if(brightnessTimer!=null)brightnessTimer.Dispose();foreach(var b in art)b.Dispose();art.Clear();if(logo!=null)logo.Dispose();
        }
        base.Dispose(disposing);
    }
}

sealed class HaloApp : ApplicationContext {
    readonly HaloSettings settings;readonly PointerTheme pointer;readonly NotifyIcon tray;readonly Hotkeys keys;readonly StudioForm studio;readonly EventWaitHandle stop;readonly EventWaitHandle show=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\HaloStudioShow");
    readonly HaloEngine engine=new HaloEngine();readonly System.Windows.Forms.Timer timer;readonly System.Windows.Forms.Timer updateTimer;readonly AppUpdater updater=new AppUpdater();bool paused,closing,suppressUpdate;int[] shown;
    public HaloApp(EventWaitHandle exitEvent){
        // The halo starts first: it is already following the screen while the window is being built.
        stop=exitEvent;settings=HaloSettings.Load();Configure();engine.Start();
        pointer=new PointerTheme();pointer.Configure(settings.PointerStyle,settings.FixedColor,settings.PointerIntensity,settings.PointerSize,true);pointer.SetEnabled(settings.PointerEnabled);
        studio=new StudioForm(settings,true);studio.Changed+=Apply;studio.PauseRequested+=Toggle;studio.QuitRequested+=ExitThread;
        studio.CheckUpdatesRequested+=async delegate{await updater.Check(true);RefreshUpdates();};
        studio.InstallUpdateRequested+=delegate{try{if(updater.LaunchPending(true)){suppressUpdate=true;ExitThread();}}catch(Exception ex){studio.SetStatus(ex.Message);}};
        var menu=new ContextMenuStrip();menu.Items.Add("Ouvrir Halo Studio",null,delegate{Open();});menu.Items.Add("Pause / Reprise",null,delegate{Toggle();});menu.Items.Add("Quitter et restaurer le pointeur",null,delegate{ExitThread();});
        tray=new NotifyIcon{Visible=true,Icon=Branding.AppIcon(),Text="Halo Studio · couleurs et pointeur",ContextMenuStrip=menu};tray.DoubleClick+=delegate{Open();};
        keys=new Hotkeys(delegate(int id){if(id==1)Toggle();else ExitThread();});
        timer=new System.Windows.Forms.Timer{Interval=66};timer.Tick+=Tick;timer.Start();
        updateTimer=new System.Windows.Forms.Timer{Interval=15000};updateTimer.Tick+=async delegate{updateTimer.Interval=6*60*60*1000;if(settings.AutoUpdates){await updater.Check(true);RefreshUpdates();}};updateTimer.Start();
        Native.SetWindowDisplayAffinity(studio.Handle,0x11);Open();
    }
    void Open(){studio.Show();Native.ShowWindow(studio.Handle,5);studio.Activate();}
    void Configure(){
        engine.Configure(new EngineConfig{Enabled=settings.ScreenEnabled||settings.VideoOnly,VideoOnly=settings.VideoOnly&&!settings.ScreenEnabled,AutoColor=settings.AutoColor,Cinema=settings.VideoBoost,Paused=paused,
            Strength=settings.ScreenIntensity/100.0,Width=settings.Width/100.0,Fixed=settings.FixedColor});
    }
    void Apply(){Configure();pointer.Configure(settings.PointerStyle,settings.AutoColor?engine.State.Color:settings.FixedColor,settings.PointerIntensity,settings.PointerSize,true);pointer.SetEnabled(settings.PointerEnabled&&!paused);}
    void RefreshUpdates(){if(!closing)studio.SetUpdate(updater.Message,updater.Busy,updater.PendingFile!=null);}
    void Toggle(){paused=!paused;Configure();pointer.SetEnabled(settings.PointerEnabled&&!paused);studio.Live(engine.State.Color,paused);}
    // The engine works on its own thread; the window only picks up its latest result here.
    void Tick(object sender,EventArgs e){
        if(stop.WaitOne(0)){suppressUpdate=true;ExitThread();return;}if(show.WaitOne(0))Open();RefreshUpdates();
        engine.PreviewWanted=studio.PreviewVisible&&!paused;if(paused)return;
        EngineState state=engine.State;Color color=settings.AutoColor?state.Color:settings.FixedColor;
        try{if(settings.PointerEnabled)pointer.Configure(settings.PointerStyle,color,settings.PointerIntensity,settings.PointerSize,false);}catch(Exception ex){studio.SetStatus("Halo : "+ex.Message);}
        studio.SetStatus(state.Error==null?null:"Halo : "+state.Error);
        int[] frame=state.Preview==shown?null:state.Preview;shown=state.Preview;studio.Live(color,paused,frame,state.Video);
    }
    protected override void ExitThreadCore(){if(closing)return;closing=true;timer.Stop();timer.Dispose();updateTimer.Stop();updateTimer.Dispose();engine.Dispose();pointer.Dispose();keys.Dispose();tray.Visible=false;tray.Dispose();show.Dispose();try{settings.Save();if(!suppressUpdate&&settings.AutoUpdates)updater.LaunchPending(false);}catch{}updater.Dispose();studio.AllowClose=true;studio.Dispose();base.ExitThreadCore();}
}
