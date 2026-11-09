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
    public bool AutoColor=true,ScreenEnabled=true,PointerEnabled=true,VideoBoost=false,VideoOnly=false,AutoUpdates=true;
    public int ColorArgb=Color.FromArgb(75,95,245).ToArgb(),ScreenIntensity=100,PointerIntensity=85,PointerStyle=0,PointerSize=100,Width=100;
    public Color FixedColor {get{return Color.FromArgb(ColorArgb);}}
    public static string DataDirectory {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"HaloStudio");}}
    public static string FileName {get{return Path.Combine(DataDirectory,"reglages.xml");}}
    public void Normalize(){ScreenIntensity=Clamp(ScreenIntensity,0,100);PointerIntensity=Clamp(PointerIntensity,0,100);PointerStyle=Clamp(PointerStyle,0,7);PointerSize=Clamp(PointerSize,80,160);Width=Clamp(Width,50,180);ColorArgb=Color.FromArgb(255,FixedColor.R,FixedColor.G,FixedColor.B).ToArgb();}
    static int Clamp(int x,int min,int max){return Math.Max(min,Math.Min(max,x));}
    public static HaloSettings Load(){
        try{using(var stream=File.OpenRead(FileName)){var s=(HaloSettings)new XmlSerializer(typeof(HaloSettings)).Deserialize(stream);s.Normalize();return s;}}catch{}
        var fallback=new HaloSettings();
        try{var p=File.ReadAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preferences.txt"));fallback.ScreenEnabled=bool.Parse(p[0]);fallback.PointerEnabled=bool.Parse(p[1]);fallback.ScreenIntensity=(int)(double.Parse(p[2],System.Globalization.CultureInfo.InvariantCulture)*100);fallback.VideoBoost=p.Length>4&&bool.Parse(p[4]);}catch{}
        return fallback;
    }
    public void Save(){Normalize();Directory.CreateDirectory(DataDirectory);string temp=FileName+".tmp";using(var stream=File.Create(temp))new XmlSerializer(typeof(HaloSettings)).Serialize(stream,this);if(File.Exists(FileName))File.Replace(temp,FileName,null);else File.Move(temp,FileName);}
}

static class Branding {
    public static Icon AppIcon(){using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Halo.Icon")){return stream==null?(Icon)SystemIcons.Application.Clone():new Icon(stream);}}
    public static Bitmap Logo(){using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Halo.Logo")){if(stream==null)return null;using(var image=Image.FromStream(stream))return new Bitmap(image);}}
}

static class PointerArt {
    public static readonly string[] Names={"Pointe douce","Flèche classique","Flèche fine","Diamant","Croix précise","Flèche contrastée","Chevron lumineux","Pointe futuriste"};
    public static Point Hotspot(int style,int size){double scale=size/100.0;return style==3||style==4?new Point((int)(32*scale),(int)(32*scale)):new Point((int)(17*scale),(int)(14*scale));}
    static GraphicsPath Shape(int style,int role=0){
        var p=new GraphicsPath();
        if(role==1){p.AddPolygon(new[]{new PointF(8,1),new PointF(13,1),new PointF(14,18),new PointF(18,14),new PointF(22,15),new PointF(25,18),new PointF(29,19),new PointF(31,23),new PointF(27,35),new PointF(11,35),new PointF(3,23),new PointF(5,19),new PointF(9,23)});using(var hand=new Matrix()){hand.Translate(17,14);p.Transform(hand);}return p;}
        if(role==2){p.AddPolygon(new[]{new PointF(3,1),new PointF(27,1),new PointF(27,5),new PointF(18,5),new PointF(18,30),new PointF(27,30),new PointF(27,34),new PointF(3,34),new PointF(3,30),new PointF(12,30),new PointF(12,5),new PointF(3,5)});using(var beam=new Matrix()){beam.Translate(17,14);p.Transform(beam);}return p;}
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
    public static Bitmap Render(int style,Color color,int intensity,int size,int role=0){
        int n=(int)(68*size/100.0);double scale=size/100.0;
        using(var mask=new Bitmap(n,n,PixelFormat.Format32bppArgb))using(var path=Shape(style,role)){
            using(var m=new Matrix()){m.Scale((float)scale,(float)scale);path.Transform(m);}
            using(var g=Graphics.FromImage(mask)){g.SmoothingMode=SmoothingMode.AntiAlias;g.FillPath(Brushes.White,path);}
            // Glow is the blurred silhouette of the actual pointer, never a radial circle.
            double[,] alpha=new double[n,n];for(int y=0;y<n;y++)for(int x=0;x<n;x++)alpha[x,y]=mask.GetPixel(x,y).A/255.0;
            int radius=Math.Max(2,(int)(6*scale));double sigma=radius/2.0;double[] kernel=new double[2*radius+1];double sum=0;
            for(int i=-radius;i<=radius;i++){kernel[i+radius]=Math.Exp(-(i*i)/(2*sigma*sigma));sum+=kernel[i+radius];}for(int i=0;i<kernel.Length;i++)kernel[i]/=sum;
            double[,] horizontal=new double[n,n];for(int y=0;y<n;y++)for(int x=0;x<n;x++)for(int k=-radius;k<=radius;k++)if(x+k>=0&&x+k<n)horizontal[x,y]+=alpha[x+k,y]*kernel[k+radius];
            Bitmap result=Colors.Pixels(n,n,delegate(int x,int y){double a=0;for(int k=-radius;k<=radius;k++)if(y+k>=0&&y+k<n)a+=horizontal[x,y+k]*kernel[k+radius];return Color.FromArgb((int)Math.Min(250,a*620*intensity/100.0),color);});
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
            try{Point hot=role==0?Hotspot(style,size):new Point((int)(32*size/100.0),(int)(31*size/100.0));var info=new ICONINFO{Icon=false,X=(uint)hot.X,Y=(uint)hot.Y,Mask=mask,Color=pixels};IntPtr cursor=CreateIconIndirect(ref info);if(cursor==IntPtr.Zero)throw new System.ComponentModel.Win32Exception();return cursor;}
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

sealed class PreviewPanel : Control {
    public HaloSettings Settings;public Color LiveColor=Color.RoyalBlue;public int Brightness=50;public bool Paused,Light=true,VideoDetected;Bitmap frame;
    public PreviewPanel(){DoubleBuffered=true;BackColor=Color.FromArgb(250,250,252);Size=new Size(315,480);}
    public void SetFrame(Bitmap image){if(frame!=null)frame.Dispose();frame=image==null?null:(Bitmap)image.Clone();Invalidate();}
    protected override void Dispose(bool disposing){if(disposing&&frame!=null)frame.Dispose();base.Dispose(disposing);}
    public void RefreshState(Color color,bool paused){if(color.ToArgb()!=LiveColor.ToArgb()||Paused!=paused){LiveColor=color;Paused=paused;Invalidate();}}
    protected override void OnPaint(PaintEventArgs e){
        base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float previewScale=Math.Min(Width/310f,Height/480f);g.ScaleTransform(previewScale,previewScale);var s=Settings??new HaloSettings();Color color=s.AutoColor?LiveColor:s.FixedColor;
        using(var title=new Font("Segoe UI",12,FontStyle.Bold))g.DrawString("APERÇU EN TEMPS RÉEL",title,Brushes.MidnightBlue,20,24);
        using(var text=new Font("Segoe UI",9))g.DrawString(frame!=null?"Écran actuel · synchronisé":"En attente de l’écran…",text,Brushes.SlateGray,20,57);
        Rectangle display=new Rectangle(20,89,270,176);int w=display.Width,h=display.Height;
        using(var scene=new Bitmap(w,h))using(var sg=Graphics.FromImage(scene)){
            if(frame!=null)sg.DrawImage(frame,new Rectangle(0,0,w,h));else sg.Clear(Color.FromArgb(238,242,248));
            if(s.ScreenEnabled&&!Paused&&(!s.VideoOnly||VideoDetected)){int d=Math.Max(14,(int)((s.VideoBoost?32:22)*s.Width/100.0));d=Math.Min(60,d);Color[] palette={color,color};
                for(int side=0;side<4;side++)using(var b=Colors.Edge(side<2?w:d,side<2?d:h-2*d,side,palette,s.ScreenIntensity/100.0*(s.VideoBoost?1.4:1)))sg.DrawImageUnscaled(b,side==3?w-d:0,side==1?h-d:side>=2?d:0);
            }
            if(s.PointerEnabled&&!Paused)using(var pointer=PointerArt.Render(s.PointerStyle,color,s.PointerIntensity,s.PointerSize))sg.DrawImageUnscaled(pointer,w/2-34,h/2-34);
            g.DrawImageUnscaled(scene,display.Location);
        }
        using(var frame=new Pen(Color.FromArgb(45,55,76),5))g.DrawRectangle(frame,display);
        using(var stand=new Pen(Color.FromArgb(75,86,109),7)){g.DrawLine(stand,Width/2,270,Width/2,291);g.DrawLine(stand,Width/2-37,294,Width/2+37,294);}
        using(var text=new Font("Segoe UI",9)){
            g.DrawString("Halo écran    "+s.ScreenIntensity+" %",text,Brushes.MidnightBlue,20,320);
            g.DrawString("Halo souris    "+s.PointerIntensity+" %",text,Brushes.MidnightBlue,20,345);
            g.DrawString("Écran du PC  "+Brightness+" %",text,Brushes.MidnightBlue,20,370);
            g.DrawString(Paused?"Effets en pause":"Les réglages s’appliquent en direct.",text,Brushes.SlateGray,20,411);
            g.DrawString("Capture locale, jamais enregistrée.",text,Brushes.SlateGray,20,436);
        }
    }
}

sealed class SettingsCard : Panel {
    public SettingsCard(){DoubleBuffered=true;BackColor=Color.White;Padding=new Padding(15);}
    static GraphicsPath Rounded(Rectangle r,int radius){var p=new GraphicsPath();int d=radius*2;p.AddArc(r.Left,r.Top,d,d,180,90);p.AddArc(r.Right-d,r.Top,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.Left,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
    protected override void OnResize(EventArgs e){base.OnResize(e);if(Width<24||Height<24)return;using(var path=Rounded(new Rectangle(0,0,Width,Height),12)){var old=Region;Region=new Region(path);if(old!=null)old.Dispose();}}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var path=Rounded(new Rectangle(0,0,Width-1,Height-1),12))using(var pen=new Pen(Color.FromArgb(225,228,236)))e.Graphics.DrawPath(pen,path);}
}

sealed class StudioForm : Form {
    public readonly HaloSettings Settings;public readonly PreviewPanel Preview;public readonly BrightnessControl Brightness;
    public bool AllowClose;public Label StateLabel,UpdateStatus;public Button CheckUpdateButton,InstallUpdateButton;public TrackBar ScreenSlider,PointerSlider,BrightnessSlider;public ComboBox StyleBox,ColorMode;
    public event Action Changed,PauseRequested,QuitRequested,CheckUpdatesRequested,InstallUpdateRequested;
    readonly List<Panel> pages=new List<Panel>();readonly List<Button> navigation=new List<Button>();Panel body,rail;Label pageTitle,pageSubtitle;Button choose;bool loading=true,persist;System.Windows.Forms.Timer saveTimer,brightnessTimer;Label brightnessInfo;
    static Color Ink=Color.FromArgb(27,31,41),Muted=Color.FromArgb(105,110,124),Background=Color.FromArgb(248,249,252),Blue=Color.FromArgb(38,105,214);
    public StudioForm(HaloSettings settings,bool probeBrightness){
        persist=probeBrightness;Settings=settings;Text="Halo Studio · "+BuildInfo.Version;ClientSize=new Size(1060,700);MinimumSize=new Size(900,620);StartPosition=FormStartPosition.CenterScreen;BackColor=Background;ForeColor=Ink;Font=new Font("Segoe UI",10);AutoScaleMode=AutoScaleMode.Dpi;Icon=Branding.AppIcon();
        rail=new Panel{Location=new Point(0,0),Size=new Size(190,700),Anchor=AnchorStyles.Left|AnchorStyles.Top|AnchorStyles.Bottom,BackColor=Color.FromArgb(239,240,246)};Controls.Add(rail);
        var logo=Branding.Logo();if(logo!=null)rail.Controls.Add(new PictureBox{Image=logo,SizeMode=PictureBoxSizeMode.Zoom,Location=new Point(10,14),Size=new Size(46,46)});
        var brandLabel=LabelAt(rail,"Halo Studio",64,15,true,113,30);brandLabel.Font=new Font("Segoe UI",10,FontStyle.Bold);LabelAt(rail,"Version bêta 3",64,44,false,120,25);
        string[] names={"Couleurs","Souris","Écran","Mises à jour"};
        for(int i=0;i<names.Length;i++){int selected=i;var button=ButtonAt(rail,names[i],12,132+i*52,166);button.TextAlign=ContentAlignment.MiddleLeft;button.FlatAppearance.BorderSize=0;using(var pill=new GraphicsPath()){pill.AddArc(0,0,24,24,180,90);pill.AddArc(button.Width-25,0,24,24,270,90);pill.AddArc(button.Width-25,button.Height-25,24,24,0,90);pill.AddArc(0,button.Height-25,24,24,90,90);pill.CloseFigure();button.Region=new Region(pill);}button.Click+=delegate{SelectPage(selected);};navigation.Add(button);}
        LabelAt(rail,"Local sur ton PC\nWindows 10 / 11",14,480,false,155,50);
        pageTitle=LabelAt(this,"Couleurs",211,30,true,620,46);pageTitle.Font=new Font("Segoe UI",25,FontStyle.Bold);
        pageSubtitle=LabelAt(this,"Une ambiance qui suit ton écran.",215,82,false,680,28);
        body=new Panel{Location=new Point(210,125),Size=new Size(500,500),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Bottom};Controls.Add(body);
        Preview=new PreviewPanel{Settings=settings,Location=new Point(730,125),Size=new Size(310,480),Anchor=AnchorStyles.Top|AnchorStyles.Right};Controls.Add(Preview);
        var colors=Page();var mouse=Page();var display=Page();var updates=Page();
        var mode=Card(colors,0,142);LabelAt(mode,"Mode de couleur",18,15,true);LabelAt(mode,"Même ambiance pour l’écran et le pointeur.",18,46,false);
        ColorMode=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Location=new Point(20,84),Size=new Size(445,30),Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top};ColorMode.Items.AddRange(new object[]{"Automatique · applications et vidéos","Couleur unique"});ColorMode.SelectedIndex=settings.AutoColor?0:1;ReadableCombo(ColorMode);mode.Controls.Add(ColorMode);
        var palette=Card(colors,153,227);LabelAt(palette,"Ta couleur",18,15,true);choose=ButtonAt(palette,"Choisir une couleur…",20,53,265);choose.BackColor=settings.FixedColor;choose.ForeColor=Color.White;
        choose.Click+=delegate{using(var dialog=new ColorDialog{Color=settings.FixedColor,FullOpen=true})if(dialog.ShowDialog(this)==DialogResult.OK){settings.ColorArgb=dialog.Color.ToArgb();ColorMode.SelectedIndex=1;Apply();}};
        Color[] swatches={Color.FromArgb(75,95,245),Color.FromArgb(160,65,225),Color.FromArgb(230,55,140),Color.FromArgb(0,165,190),Color.FromArgb(20,170,100),Color.FromArgb(245,125,35)};
        for(int i=0;i<swatches.Length;i++){Color c=swatches[i];var b=new Button{BackColor=c,Location=new Point(20+i*55,111),Size=new Size(43,36),FlatStyle=FlatStyle.Flat,AccessibleName="Couleur "+ColorTranslator.ToHtml(c)};b.FlatAppearance.BorderSize=0;b.Click+=delegate{settings.ColorArgb=c.ToArgb();ColorMode.SelectedIndex=1;Apply();};palette.Controls.Add(b);}
        LabelAt(palette,"Automatique : les couleurs suivent l’image.\nCouleur unique : la teinte choisie reste constante.",18,167,false,430,52);ColorMode.SelectedIndexChanged+=delegate{settings.AutoColor=ColorMode.SelectedIndex==0;Apply();};
        var pointerOnCard=Card(mouse,0,55);var pointerOn=CheckAt(pointerOnCard,"Activer le pointeur lumineux",18,12,settings.PointerEnabled);pointerOn.CheckedChanged+=delegate{settings.PointerEnabled=pointerOn.Checked;Apply();};
        var styles=Card(mouse,65,192);LabelAt(styles,"Style du pointeur",18,14,true);StyleBox=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Location=new Point(20,51),Size=new Size(445,30),Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top};StyleBox.Items.AddRange(PointerArt.Names);StyleBox.SelectedIndex=settings.PointerStyle;ReadableCombo(StyleBox);styles.Controls.Add(StyleBox);StyleBox.SelectedIndexChanged+=delegate{settings.PointerStyle=StyleBox.SelectedIndex;Apply();};
        for(int i=0;i<PointerArt.Names.Length;i++){int style=i;var tile=new Button{Location=new Point(20+i*53,101),Size=new Size(48,74),FlatStyle=FlatStyle.Flat,BackColor=Color.White,AccessibleName=PointerArt.Names[i],Image=PointerArt.Render(i,settings.FixedColor,90,70)};tile.FlatAppearance.BorderColor=Color.FromArgb(225,228,234);tile.Click+=delegate{StyleBox.SelectedIndex=style;};styles.Controls.Add(tile);}
        PointerSlider=SliderAt(Card(mouse,267,95),"Intensité du contour lumineux",0,100,settings.PointerIntensity,delegate(int v){settings.PointerIntensity=v;Apply();});
        SliderAt(Card(mouse,372,95),"Taille du pointeur",80,160,settings.PointerSize,delegate(int v){settings.PointerSize=v;Apply();});
        var enabled=Card(display,0,55);var screenOn=CheckAt(enabled,"Activer le halo de l’écran",18,12,settings.ScreenEnabled);screenOn.CheckedChanged+=delegate{settings.ScreenEnabled=screenOn.Checked;Apply();};
        ScreenSlider=SliderAt(Card(display,65,95),"Intensité du halo",0,100,settings.ScreenIntensity,delegate(int v){settings.ScreenIntensity=v;Apply();});
        SliderAt(Card(display,170,95),"Étendue du halo",50,180,settings.Width,delegate(int v){settings.Width=v;Apply();});
        var vid=Card(display,275,55);var video=CheckAt(vid,"Vidéo : amplification maximale",18,12,settings.VideoBoost);video.CheckedChanged+=delegate{settings.VideoBoost=video.Checked;Apply();};
        Brightness=probeBrightness?new BrightnessControl():null;int current=Brightness!=null&&Brightness.Available?Brightness.Current:50;Preview.Brightness=current;
        var videoOnlyCard=Card(display,340,55);var videoOnly=CheckAt(videoOnlyCard,"Activer le halo seulement pendant une vidéo",18,12,settings.VideoOnly);videoOnly.CheckedChanged+=delegate{settings.VideoOnly=videoOnly.Checked;Apply();};
        var pc=Card(display,405,135);BrightnessSlider=SliderAt(pc,"Luminosité du PC",0,100,current,delegate(int v){Preview.Brightness=v;Preview.Invalidate();if(!loading&&Brightness!=null&&Brightness.Available){brightnessTimer.Stop();brightnessTimer.Start();}});BrightnessSlider.Enabled=Brightness!=null&&Brightness.Available;brightnessInfo=LabelAt(pc,BrightnessSlider.Enabled?"Luminosité matérielle · Windows":"Écran non compatible : utiliser ses boutons physiques.",18,94,false,445,37);
        var version=Card(updates,0,135);LabelAt(version,"Halo Studio · canal bêta",18,17,true);LabelAt(version,"Version installée : "+BuildInfo.Version,18,55,false);LabelAt(version,"Les nouveautés arrivent d’abord en version bêta.",18,89,false);
        var autoCard=Card(updates,145,93);var automatic=CheckAt(autoCard,"Mises à jour automatiques",18,12,settings.AutoUpdates);automatic.CheckedChanged+=delegate{settings.AutoUpdates=automatic.Checked;Apply();};LabelAt(autoCard,"Téléchargées puis installées à la fermeture.",18,51,false);
        var actions=Card(updates,248,205);UpdateStatus=LabelAt(actions,"Prêt à vérifier les mises à jour.",18,18,false,445,56);CheckUpdateButton=ButtonAt(actions,"Vérifier et télécharger",20,85,280);CheckUpdateButton.Click+=delegate{if(CheckUpdatesRequested!=null)CheckUpdatesRequested();};InstallUpdateButton=ButtonAt(actions,"Installer et relancer",20,139,280);InstallUpdateButton.Enabled=false;InstallUpdateButton.Click+=delegate{if(InstallUpdateRequested!=null)InstallUpdateRequested();};
        var pause=ButtonAt(this,"Pause / Reprise",210,647,180);pause.Anchor=AnchorStyles.Left|AnchorStyles.Bottom;pause.Click+=delegate{if(PauseRequested!=null)PauseRequested();};var quit=ButtonAt(this,"Quitter Halo",895,647,145);quit.Anchor=AnchorStyles.Right|AnchorStyles.Bottom;quit.Click+=delegate{if(QuitRequested!=null)QuitRequested();};StateLabel=LabelAt(this,"Actif · réglages enregistrés",410,654,false,470,30);StateLabel.Anchor=AnchorStyles.Bottom|AnchorStyles.Left;
        saveTimer=new System.Windows.Forms.Timer{Interval=350};saveTimer.Tick+=delegate{saveTimer.Stop();try{settings.Save();}catch{StateLabel.Text="Impossible d’enregistrer les réglages.";}};
        brightnessTimer=new System.Windows.Forms.Timer{Interval=350};brightnessTimer.Tick+=delegate{brightnessTimer.Stop();try{Brightness.Set(BrightnessSlider.Value);brightnessInfo.Text="Luminosité Windows : "+Brightness.Current+" %";}catch(Exception ex){brightnessInfo.Text=ex.Message;}};
        FormClosing+=delegate(object sender,FormClosingEventArgs e){if(!AllowClose){e.Cancel=true;Hide();}};Resize+=delegate{Responsive();};loading=false;SelectPage(0);Responsive();
    }
    void Responsive(){if(body==null||Preview==null)return;int previewWidth=ClientSize.Width<980?260:310;Preview.Width=previewWidth;Preview.Height=Math.Min(480,ClientSize.Height-210);Preview.Left=ClientSize.Width-previewWidth-20;body.Width=Math.Max(345,Preview.Left-body.Left-20);body.Height=Math.Max(360,ClientSize.Height-194);foreach(var page in pages)foreach(Control c in page.Controls)if(c is SettingsCard)c.Width=body.Width-10;}
    public void SelectPage(int index){for(int i=0;i<pages.Count;i++){pages[i].Visible=i==index;navigation[i].BackColor=i==index?Color.White:rail.BackColor;navigation[i].ForeColor=i==index?Blue:Ink;}pageTitle.Text=navigation[index].Text;string[] descriptions={"Une ambiance qui suit ton écran.","Un contour lumineux attaché à ton pointeur.","Ajuste la lumière, sur portable comme sur PC.","Les nouveautés de Halo Studio, simplement."};pageSubtitle.Text=descriptions[index];}
    Panel Page(){var p=new Panel{Dock=DockStyle.Fill,AutoScroll=true,BackColor=Background};pages.Add(p);body.Controls.Add(p);return p;}
    SettingsCard Card(Control page,int y,int height){var c=new SettingsCard{Location=new Point(0,y),Size=new Size(body.Width-10,height),Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top};page.Controls.Add(c);return c;}
    Label LabelAt(Control host,string text,int x,int y,bool bold,int width=445,int height=30){var l=new Label{Text=text,Location=new Point(x,y),Size=new Size(width,height),ForeColor=bold?Ink:Muted,BackColor=Color.Transparent,Font=new Font("Segoe UI",bold?11:9.5f,bold?FontStyle.Bold:FontStyle.Regular)};host.Controls.Add(l);return l;}
    Button ButtonAt(Control host,string text,int x,int y,int width){var b=new Button{Text=text,Location=new Point(x,y),Size=new Size(width,38),FlatStyle=FlatStyle.Flat,BackColor=Color.White,ForeColor=Ink};b.FlatAppearance.BorderColor=Color.FromArgb(214,218,223);host.Controls.Add(b);return b;}
    CheckBox CheckAt(Control host,string text,int x,int y,bool value){var c=new CheckBox{Text=text,Checked=value,Location=new Point(x,y),Size=new Size(445,30),Anchor=AnchorStyles.Left|AnchorStyles.Right|AnchorStyles.Top};host.Controls.Add(c);return c;}
    TrackBar SliderAt(Control host,string text,int min,int max,int value,Action<int> change){LabelAt(host,text,18,12,true,335);var number=LabelAt(host,value+" %",host.Width-75,12,false,65);number.Anchor=AnchorStyles.Top|AnchorStyles.Right;var t=new TrackBar{Minimum=min,Maximum=max,Value=value,TickStyle=TickStyle.None,Location=new Point(15,47),Size=new Size(host.Width-35,40),Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,AccessibleName=text};t.ValueChanged+=delegate{number.Text=t.Value+" %";change(t.Value);};host.Controls.Add(t);return t;}
    void ReadableCombo(ComboBox combo){combo.DrawMode=DrawMode.OwnerDrawFixed;combo.ItemHeight=25;combo.DrawItem+=delegate(object sender,DrawItemEventArgs e){e.DrawBackground();int index=e.Index<0?combo.SelectedIndex:e.Index;if(index>=0)TextRenderer.DrawText(e.Graphics,combo.Items[index].ToString(),combo.Font,e.Bounds,Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.Left);e.DrawFocusRectangle();};
        Label selected=null;Action refresh=delegate{if(selected!=null){selected.Text=combo.SelectedItem==null?"":combo.SelectedItem.ToString();selected.Bounds=new Rectangle(combo.Left+6,combo.Top+3,Math.Max(5,combo.Width-32),combo.Height-6);selected.BringToFront();}};
        combo.ParentChanged+=delegate{if(combo.Parent!=null&&selected==null){selected=new Label{AutoEllipsis=true,BackColor=Color.White,ForeColor=Ink,TextAlign=ContentAlignment.MiddleLeft,Font=combo.Font};selected.Click+=delegate{combo.Focus();combo.DroppedDown=true;};combo.Parent.Controls.Add(selected);refresh();}};combo.SelectedIndexChanged+=delegate{refresh();};combo.SizeChanged+=delegate{refresh();};
    }
    void Apply(){if(loading)return;Settings.Normalize();choose.BackColor=Settings.FixedColor;Preview.Invalidate();if(persist){saveTimer.Stop();saveTimer.Start();}if(Changed!=null)Changed();}
    public void Live(Color color,bool paused,Bitmap frame=null,bool videoDetected=false){Preview.VideoDetected=videoDetected;Preview.RefreshState(color,paused);if(frame!=null&&Preview.Visible)Preview.SetFrame(frame);string text=paused?"En pause · pointeur restauré":Settings.AutoColor?"Actif · couleurs synchronisées":"Actif · couleur unique";if(StateLabel.Text!=text)StateLabel.Text=text;}
    protected override void Dispose(bool disposing){if(disposing){if(saveTimer!=null)saveTimer.Dispose();if(brightnessTimer!=null)brightnessTimer.Dispose();foreach(var page in pages)foreach(Control card in page.Controls)foreach(Control child in card.Controls)if(child is Button&&((Button)child).Image!=null)((Button)child).Image.Dispose();}base.Dispose(disposing);}
}

sealed class HaloApp : ApplicationContext {
    readonly HaloSettings settings;readonly PointerTheme pointer;readonly NotifyIcon tray;readonly Hotkeys keys;readonly StudioForm studio;readonly EventWaitHandle stop;readonly EventWaitHandle show=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\HaloStudioShow");
    readonly List<MonitorGlow> monitors=new List<MonitorGlow>();readonly System.Windows.Forms.Timer timer;readonly System.Windows.Forms.Timer updateTimer;readonly AppUpdater updater=new AppUpdater();bool paused,closing,suppressUpdate;string topology="";DateTime lastError=DateTime.MinValue;
    public HaloApp(EventWaitHandle exitEvent){
        stop=exitEvent;settings=HaloSettings.Load();pointer=new PointerTheme();pointer.Configure(settings.PointerStyle,settings.FixedColor,settings.PointerIntensity,settings.PointerSize,true);pointer.SetEnabled(settings.PointerEnabled);
        studio=new StudioForm(settings,true);studio.Changed+=Apply;studio.PauseRequested+=Toggle;studio.QuitRequested+=ExitThread;
        studio.CheckUpdatesRequested+=async delegate{await updater.Check(true);RefreshUpdates();};
        studio.InstallUpdateRequested+=delegate{try{if(updater.LaunchPending(true)){suppressUpdate=true;ExitThread();}}catch(Exception ex){studio.UpdateStatus.Text=ex.Message;}};
        var menu=new ContextMenuStrip();menu.Items.Add("Ouvrir Halo Studio",null,delegate{studio.Show();Native.ShowWindow(studio.Handle,5);studio.Activate();});menu.Items.Add("Pause / Reprise",null,delegate{Toggle();});menu.Items.Add("Quitter et restaurer le pointeur",null,delegate{ExitThread();});
        tray=new NotifyIcon{Visible=true,Icon=Branding.AppIcon(),Text="Halo Studio · couleurs et pointeur",ContextMenuStrip=menu};tray.DoubleClick+=delegate{studio.Show();Native.ShowWindow(studio.Handle,5);studio.Activate();};
        keys=new Hotkeys(delegate(int id){if(id==1)Toggle();else ExitThread();});
        timer=new System.Windows.Forms.Timer{Interval=90};timer.Tick+=Tick;timer.Start();
        updateTimer=new System.Windows.Forms.Timer{Interval=15000};updateTimer.Tick+=async delegate{updateTimer.Interval=6*60*60*1000;if(settings.AutoUpdates){await updater.Check(true);RefreshUpdates();}};updateTimer.Start();
        Native.SetWindowDisplayAffinity(studio.Handle,0x11);studio.Show();Native.ShowWindow(studio.Handle,5);
    }
    void Apply(){pointer.Configure(settings.PointerStyle,settings.AutoColor?studio.Preview.LiveColor:settings.FixedColor,settings.PointerIntensity,settings.PointerSize,true);pointer.SetEnabled(settings.PointerEnabled&&!paused);}
    void RefreshUpdates(){if(closing)return;studio.UpdateStatus.Text=updater.Message;studio.CheckUpdateButton.Enabled=!updater.Busy;studio.InstallUpdateButton.Enabled=updater.PendingFile!=null;}
    void Toggle(){paused=!paused;pointer.SetEnabled(settings.PointerEnabled&&!paused);if(paused)foreach(var m in monitors)m.Hide();studio.Live(studio.Preview.LiveColor,paused);}
    void Tick(object sender,EventArgs e){
        if(stop.WaitOne(0)){suppressUpdate=true;ExitThread();return;}if(show.WaitOne(0)){studio.Show();Native.ShowWindow(studio.Handle,5);studio.Activate();}RefreshUpdates();if(paused)return;
        try{
            string now="";foreach(var s in Screen.AllScreens)now+=s.Bounds.ToString();if(now!=topology){foreach(var m in monitors)m.Dispose();monitors.Clear();foreach(var s in Screen.AllScreens)monitors.Add(new MonitorGlow(s.Bounds));topology=now;}
            foreach(var m in monitors)m.Update(settings.ScreenEnabled,settings.ScreenIntensity/100.0,settings.VideoBoost,settings.AutoColor,settings.FixedColor,settings.Width/100.0,settings.VideoOnly);
            var active=monitors.Find(m=>m.Bounds.Contains(Cursor.Position));Color color=settings.AutoColor?(active==null?settings.FixedColor:active.Global):settings.FixedColor;
            if(settings.PointerEnabled)pointer.Configure(settings.PointerStyle,color,settings.PointerIntensity,settings.PointerSize,false);
            studio.Live(color,paused,active==null?null:active.PreviewFrame,active!=null&&active.VideoBoost>=.18);
        }catch(Exception ex){if((DateTime.UtcNow-lastError).TotalSeconds>30){lastError=DateTime.UtcNow;studio.StateLabel.Text="Halo : "+ex.Message;try{File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"erreurs.log"),DateTime.UtcNow.ToString("s")+" "+ex+Environment.NewLine);}catch{}}}
    }
    protected override void ExitThreadCore(){if(closing)return;closing=true;timer.Stop();timer.Dispose();updateTimer.Stop();updateTimer.Dispose();pointer.Dispose();keys.Dispose();tray.Visible=false;tray.Dispose();show.Dispose();foreach(var m in monitors)m.Dispose();try{settings.Save();if(!suppressUpdate&&settings.AutoUpdates)updater.LaunchPending(false);}catch{}updater.Dispose();studio.AllowClose=true;studio.Dispose();base.ExitThreadCore();}
}
