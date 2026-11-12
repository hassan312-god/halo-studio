using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

// Colors, fonts and metrics of the window, modelled on Windows 11 Settings. Every control reads them when it
// paints, so switching between the light and dark theme only needs a repaint.
static class Theme {
    public static bool Dark;public static float Scale=1;
    public static Color Text,Muted,Card,CardBorder,Accent,OnAccent,Hover,Pressed,Field,FieldBorder,Base,Divider,Track,Disabled,Solid;
    static readonly Dictionary<int,Font> fonts=new Dictionary<int,Font>();static string regular,semibold;static Bitmap backdrop;
    static Theme(){Apply(false);}
    public static void Apply(bool dark){
        Dark=dark;
        if(dark){
            Base=Color.FromArgb(32,32,32);Text=Color.FromArgb(255,255,255);Muted=Color.FromArgb(200,200,200);Card=Color.FromArgb(14,255,255,255);CardBorder=Color.FromArgb(48,0,0,0);
            Accent=Color.FromArgb(96,205,255);OnAccent=Color.FromArgb(0,0,0);Hover=Color.FromArgb(18,255,255,255);Pressed=Color.FromArgb(10,255,255,255);
            Field=Color.FromArgb(18,255,255,255);FieldBorder=Color.FromArgb(40,255,255,255);Divider=Color.FromArgb(22,255,255,255);Track=Color.FromArgb(158,158,158);Disabled=Color.FromArgb(120,120,120);Solid=Color.FromArgb(45,45,45);
        }else{
            Base=Color.FromArgb(243,243,243);Text=Color.FromArgb(27,27,27);Muted=Color.FromArgb(96,96,96);Card=Color.FromArgb(180,255,255,255);CardBorder=Color.FromArgb(20,0,0,0);
            Accent=Color.FromArgb(0,103,192);OnAccent=Color.FromArgb(255,255,255);Hover=Color.FromArgb(12,0,0,0);Pressed=Color.FromArgb(7,0,0,0);
            Field=Color.FromArgb(190,255,255,255);FieldBorder=Color.FromArgb(34,0,0,0);Divider=Color.FromArgb(16,0,0,0);Track=Color.FromArgb(138,138,138);Disabled=Color.FromArgb(160,160,160);Solid=Color.FromArgb(251,251,251);
        }
        if(backdrop!=null){backdrop.Dispose();backdrop=null;}
    }
    public static bool SystemDark(){
        try{object value=Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",1);return value is int&&(int)value==0;}catch{return false;}
    }
    public static int S(double value){return (int)Math.Round(value*Scale);}
    static string Installed(params string[] names){
        foreach(string name in names)try{using(var probe=new Font(name,10))if(string.Equals(probe.Name,name,StringComparison.OrdinalIgnoreCase))return name;}catch{}
        return null;
    }
    public static void ResetFonts(){foreach(var f in fonts.Values)f.Dispose();fonts.Clear();}
    // Sizes are in pixels at 100 %: the scale of the current monitor is applied here.
    public static Font Font(float size,bool bold=false){
        if(regular==null){regular=Installed("Segoe UI Variable Text","Segoe UI")??FontFamily.GenericSansSerif.Name;semibold=Installed("Segoe UI Variable Text Semibold","Segoe UI Semibold")??"";}
        int pixels=Math.Max(8,S(size)),key=pixels*2+(bold?1:0);Font font;
        if(!fonts.TryGetValue(key,out font)){
            font=bold&&semibold!=""?new Font(semibold,pixels,FontStyle.Regular,GraphicsUnit.Pixel):new Font(regular,pixels,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Pixel);
            fonts[key]=font;
        }
        return font;
    }
    // The soft peach and blue wash behind the window. It is drawn small once and stretched: gradients lose nothing.
    public static void PaintBackdrop(Graphics g,Rectangle area){
        if(backdrop==null){
            backdrop=new Bitmap(160,104,PixelFormat.Format32bppPArgb);
            using(var b=Graphics.FromImage(backdrop)){
                b.Clear(Base);
                Glow(b,115,22,120,Dark?Color.FromArgb(46,120,70,40):Color.FromArgb(215,255,226,208));
                Glow(b,4,-6,84,Dark?Color.FromArgb(54,40,70,130):Color.FromArgb(190,214,228,252));
                Glow(b,34,112,74,Dark?Color.FromArgb(34,110,50,90):Color.FromArgb(150,252,232,224));
            }
        }
        var state=g.Save();
        try{
            g.InterpolationMode=InterpolationMode.HighQualityBilinear;g.PixelOffsetMode=PixelOffsetMode.Half;
            using(var wrap=new ImageAttributes()){wrap.SetWrapMode(WrapMode.TileFlipXY);g.DrawImage(backdrop,area,0,0,backdrop.Width,backdrop.Height,GraphicsUnit.Pixel,wrap);}
        }finally{g.Restore(state);}
    }
    static void Glow(Graphics g,int x,int y,int radius,Color color){
        using(var path=new GraphicsPath()){
            path.AddEllipse(x-radius,y-radius,radius*2,radius*2);
            using(var brush=new PathGradientBrush(path)){brush.CenterColor=color;brush.SurroundColors=new[]{Color.FromArgb(0,color)};g.FillPath(brush,path);}
        }
    }
    public static Color BackdropTop(){return Dark?Color.FromArgb(34,34,38):Color.FromArgb(238,240,246);}
    public static GraphicsPath Round(RectangleF r,float radius){
        var p=new GraphicsPath();float d=Math.Max(1,Math.Min(radius*2,Math.Min(r.Width,r.Height)));
        p.AddArc(r.Left,r.Top,d,d,180,90);p.AddArc(r.Right-d,r.Top,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.Left,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
    }
    public static void Fill(Graphics g,RectangleF r,float radius,Color fill,Color border){
        g.SmoothingMode=SmoothingMode.AntiAlias;
        using(var path=Round(new RectangleF(r.X+.5f,r.Y+.5f,r.Width-1,r.Height-1),radius)){
            if(fill.A>0)using(var brush=new SolidBrush(fill))g.FillPath(brush,path);
            if(border.A>0)using(var pen=new Pen(border))g.DrawPath(pen,path);
        }
    }
    public static string Plain(string text){
        var result=new StringBuilder();
        foreach(char c in (text??"").Normalize(NormalizationForm.FormD))if(CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark)result.Append(char.ToLowerInvariant(c));
        return result.ToString();
    }
}

// Pictograms are drawn as vectors on a 20-unit grid, so they stay sharp at every scale and need no icon font.
static class Icons {
    public static Color Tint(string name){
        switch(name){
            case "home":return Color.FromArgb(232,128,36);case "colors":return Color.FromArgb(170,80,220);case "mouse":return Color.FromArgb(34,128,224);
            case "screen":return Color.FromArgb(0,150,170);case "video":return Color.FromArgb(222,62,82);case "update":return Color.FromArgb(0,120,212);
            case "cinema":return Color.FromArgb(222,62,82);case "sun":return Color.FromArgb(226,150,0);
        }
        return Theme.Text;
    }
    public static void Draw(Graphics g,string name,RectangleF box,Color color){
        var state=g.Save();
        try{
            g.SmoothingMode=SmoothingMode.AntiAlias;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.TranslateTransform(box.X,box.Y);g.ScaleTransform(box.Width/20f,box.Height/20f);
            using(var pen=new Pen(color,1.5f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round})using(var brush=new SolidBrush(color)){
                switch(name){
                    case "home":g.DrawLines(pen,new[]{new PointF(2.5f,9.5f),new PointF(10,3),new PointF(17.5f,9.5f)});g.DrawLines(pen,new[]{new PointF(4.5f,8.5f),new PointF(4.5f,17),new PointF(8,17),new PointF(8,12),new PointF(12,12),new PointF(12,17),new PointF(15.5f,17),new PointF(15.5f,8.5f)});break;
                    case "colors":g.DrawEllipse(pen,2.5f,2.5f,15,15);g.FillEllipse(brush,6,6,2.6f,2.6f);g.FillEllipse(brush,11.2f,5.4f,2.6f,2.6f);g.FillEllipse(brush,12.4f,10.6f,2.6f,2.6f);g.FillEllipse(brush,6.6f,11.4f,3.4f,3.4f);break;
                    case "mouse":g.DrawPolygon(pen,new[]{new PointF(5,2.5f),new PointF(5,16),new PointF(8.6f,12.6f),new PointF(11,17.6f),new PointF(13.2f,16.6f),new PointF(10.8f,11.8f),new PointF(15.5f,11.8f)});break;
                    case "screen":using(var p=Theme.Round(new RectangleF(2,3.5f,16,10.5f),1.6f))g.DrawPath(pen,p);g.DrawLine(pen,10,14,10,17);g.DrawLine(pen,6.5f,17,13.5f,17);break;
                    case "video":using(var p=Theme.Round(new RectangleF(2,4,16,12),2.2f))g.DrawPath(pen,p);g.FillPolygon(brush,new[]{new PointF(8.2f,7),new PointF(13.2f,10),new PointF(8.2f,13)});break;
                    case "cinema":using(var p=Theme.Round(new RectangleF(2.5f,7.5f,15,9.5f),1.5f))g.DrawPath(pen,p);g.DrawPolygon(pen,new[]{new PointF(2.5f,7.5f),new PointF(3.2f,4.4f),new PointF(16.6f,2.4f),new PointF(17.2f,5.2f)});g.DrawLine(pen,7,3.8f,8.4f,6.6f);g.DrawLine(pen,11.4f,3.2f,12.8f,5.8f);break;
                    case "update":g.DrawArc(pen,3.5f,3.5f,13,13,200,130);g.DrawArc(pen,3.5f,3.5f,13,13,20,130);g.FillPolygon(brush,new[]{new PointF(14.2f,2.2f),new PointF(17.6f,6.8f),new PointF(12.4f,7.2f)});g.FillPolygon(brush,new[]{new PointF(5.8f,17.8f),new PointF(2.4f,13.2f),new PointF(7.6f,12.8f)});break;
                    case "search":g.DrawEllipse(pen,3,3,9.5f,9.5f);g.DrawLine(pen,11.4f,11.4f,16.5f,16.5f);break;
                    case "pause":g.DrawLine(pen,7,4.5f,7,15.5f);g.DrawLine(pen,13,4.5f,13,15.5f);break;
                    case "play":g.DrawPolygon(pen,new[]{new PointF(6.5f,4),new PointF(15.5f,10),new PointF(6.5f,16)});break;
                    case "power":g.DrawArc(pen,3.5f,4,13,13,-55,290);g.DrawLine(pen,10,2.5f,10,9.5f);break;
                    case "chevron":g.DrawLines(pen,new[]{new PointF(7.5f,4.5f),new PointF(13,10),new PointF(7.5f,15.5f)});break;
                    case "down":g.DrawLines(pen,new[]{new PointF(5,7.5f),new PointF(10,12.5f),new PointF(15,7.5f)});break;
                    case "sun":g.DrawEllipse(pen,6.5f,6.5f,7,7);for(int i=0;i<8;i++){double a=i*Math.PI/4;g.DrawLine(pen,(float)(10+6.2*Math.Cos(a)),(float)(10+6.2*Math.Sin(a)),(float)(10+8*Math.Cos(a)),(float)(10+8*Math.Sin(a)));}break;
                    case "size":g.DrawLine(pen,4,16,16,4);g.DrawLines(pen,new[]{new PointF(10.5f,4),new PointF(16,4),new PointF(16,9.5f)});g.DrawLines(pen,new[]{new PointF(4,10.5f),new PointF(4,16),new PointF(9.5f,16)});break;
                    case "glow":g.DrawEllipse(pen,7,7,6,6);g.DrawArc(pen,3,3,14,14,-70,140);g.DrawArc(pen,3,3,14,14,110,140);break;
                    case "info":g.DrawEllipse(pen,2.5f,2.5f,15,15);g.DrawLine(pen,10,9.2f,10,14);g.FillEllipse(brush,9.1f,5.4f,1.8f,1.8f);break;
                    case "keyboard":using(var p=Theme.Round(new RectangleF(2,5,16,10.5f),1.8f))g.DrawPath(pen,p);g.DrawLine(pen,6.5f,12.5f,13.5f,12.5f);g.FillEllipse(brush,5,7.6f,1.6f,1.6f);g.FillEllipse(brush,9.2f,7.6f,1.6f,1.6f);g.FillEllipse(brush,13.4f,7.6f,1.6f,1.6f);break;
                }
            }
        }finally{g.Restore(state);}
    }
}

// Base of every hand-drawn control: transparent over the window backdrop, double buffered, with hover and press state.
class FluentControl : Control {
    protected bool Hot,Down;
    public FluentControl(){
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.SupportsTransparentBackColor,true);
        SetStyle(ControlStyles.Selectable,false);BackColor=Color.Transparent;TabStop=false;
    }
    protected void Interactive(){SetStyle(ControlStyles.Selectable,true);TabStop=true;}
    protected override void OnMouseEnter(EventArgs e){Hot=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){Hot=false;Down=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnMouseDown(MouseEventArgs e){if(e.Button==MouseButtons.Left){Down=true;if(GetStyle(ControlStyles.Selectable))Focus();Invalidate();}base.OnMouseDown(e);}
    protected override void OnMouseUp(MouseEventArgs e){Down=false;Invalidate();base.OnMouseUp(e);}
    protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
    protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
    protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
    protected override void OnTextChanged(EventArgs e){Invalidate();base.OnTextChanged(e);}
    protected void Ring(Graphics g,RectangleF r,float radius){
        if(!Focused||!ShowFocusCues)return;
        g.SmoothingMode=SmoothingMode.AntiAlias;
        using(var path=Theme.Round(new RectangleF(r.X+1,r.Y+1,r.Width-2,r.Height-2),radius))using(var pen=new Pen(Theme.Text,Math.Max(1.5f,Theme.S(2))))g.DrawPath(pen,path);
    }
    protected static void Write(Graphics g,string text,Font font,Rectangle r,Color color,TextFormatFlags flags){
        if(string.IsNullOrEmpty(text)||r.Width<4||r.Height<4)return;
        TextRenderer.DrawText(g,text,font,r,color,flags|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding);
    }
    protected bool Activates(KeyEventArgs e){return e.KeyCode==Keys.Space||e.KeyCode==Keys.Enter;}
}

sealed class TextBlock : FluentControl {
    public float FontSize=14;public bool Bold,Subtle,AutoHeight;public ContentAlignment Align=ContentAlignment.TopLeft;
    public TextBlock(string text,float size=14,bool bold=false,bool subtle=false){Text=text;FontSize=size;Bold=bold;Subtle=subtle;Height=Theme.S(size*1.5);}
    TextFormatFlags Flags {get{return TextFormatFlags.WordBreak|(Align==ContentAlignment.MiddleLeft?TextFormatFlags.VerticalCenter:TextFormatFlags.Top)|TextFormatFlags.EndEllipsis;}}
    public void Fit(){
        if(!AutoHeight||Width<8)return;
        int h=TextRenderer.MeasureText(Text??"",Theme.Font(FontSize,Bold),new Size(Width,10000),TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding).Height+Theme.S(2);
        if(h!=Height)Height=h;
    }
    protected override void OnResize(EventArgs e){base.OnResize(e);Fit();}
    protected override void OnTextChanged(EventArgs e){base.OnTextChanged(e);Fit();}
    protected override void OnPaint(PaintEventArgs e){Write(e.Graphics,Text,Theme.Font(FontSize,Bold),ClientRectangle,Subtle?Theme.Muted:Theme.Text,Flags);}
}

sealed class NavItem : FluentControl {
    public string Icon;public bool Selected,Tinted=true;
    public NavItem(string text,string icon){Text=text;Icon=icon;Height=Theme.S(38);Interactive();AccessibleRole=AccessibleRole.MenuItem;AccessibleName=text;}
    protected override void OnKeyDown(KeyEventArgs e){if(Activates(e)){OnClick(EventArgs.Empty);e.Handled=true;}base.OnKeyDown(e);}
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;Rectangle r=ClientRectangle;
        if(Selected||Hot)Theme.Fill(g,r,Theme.S(5),Down?Theme.Pressed:Selected&&!Hot?Theme.Hover:Color.FromArgb(Math.Min(255,Theme.Hover.A+(Selected?8:0)),Theme.Hover),Color.Empty);
        if(Selected){g.SmoothingMode=SmoothingMode.AntiAlias;using(var pill=Theme.Round(new RectangleF(0,(Height-Theme.S(16))/2f,Theme.S(3),Theme.S(16)),Theme.S(1.5)))using(var brush=new SolidBrush(Theme.Accent))g.FillPath(brush,pill);}
        int icon=Theme.S(18);Icons.Draw(g,Icon,new RectangleF(Theme.S(14),(Height-icon)/2f,icon,icon),Tinted?Icons.Tint(Icon):Theme.Text);
        Write(g,Text,Theme.Font(14),new Rectangle(Theme.S(44),0,Width-Theme.S(50),Height),Theme.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        Ring(g,r,Theme.S(5));
    }
}

sealed class ToggleSwitch : FluentControl {
    bool value;float position;Timer motion;
    public event EventHandler CheckedChanged;
    public ToggleSwitch(){Size=new Size(Theme.S(40),Theme.S(20));Interactive();AccessibleRole=AccessibleRole.CheckButton;}
    public bool Checked {
        get{return value;}
        set{
            if(this.value==value)return;this.value=value;
            // The knob glides when the switch is on screen; otherwise it is simply in place.
            if(IsHandleCreated&&Visible){if(motion==null){motion=new Timer{Interval=15};motion.Tick+=delegate{float target=this.value?1:0;position+=(target-position)*.4f;if(Math.Abs(target-position)<.03f){position=target;motion.Stop();}Invalidate();};}motion.Start();}
            else position=value?1:0;
            Invalidate();if(CheckedChanged!=null)CheckedChanged(this,EventArgs.Empty);
        }
    }
    protected override void OnClick(EventArgs e){Checked=!Checked;base.OnClick(e);}
    protected override void OnKeyDown(KeyEventArgs e){if(Activates(e)){Checked=!Checked;e.Handled=true;}base.OnKeyDown(e);}
    protected override void Dispose(bool disposing){if(disposing&&motion!=null)motion.Dispose();base.Dispose(disposing);}
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;RectangleF r=new RectangleF(.5f,.5f,Width-1,Height-1);
        Color on=Enabled?Theme.Accent:Theme.Disabled,off=Enabled?Theme.Track:Theme.Disabled;
        using(var path=Theme.Round(r,r.Height/2)){
            if(position>.5f)using(var brush=new SolidBrush(Hot&&Enabled?Color.FromArgb(230,on):on))g.FillPath(brush,path);
            else{if(Hot)using(var brush=new SolidBrush(Theme.Hover))g.FillPath(brush,path);using(var pen=new Pen(off))g.DrawPath(pen,path);}
        }
        float knob=Height*(Hot&&Enabled?.7f:.6f),x=Height/2f+(Width-Height)*position-knob/2;
        using(var brush=new SolidBrush(position>.5f?Theme.OnAccent:off))g.FillEllipse(brush,x,(Height-knob)/2f,knob,knob);
        Ring(g,new RectangleF(-1,-1,Width+2,Height+2),Height/2f);
    }
}

sealed class FluentSlider : FluentControl {
    int minimum,maximum=100,value;bool dragging;
    public event EventHandler ValueChanged;
    public FluentSlider(){Size=new Size(Theme.S(240),Theme.S(32));Interactive();AccessibleRole=AccessibleRole.Slider;}
    public int Minimum {get{return minimum;}set{minimum=value;Value=this.value;Invalidate();}}
    public int Maximum {get{return maximum;}set{maximum=value;Value=this.value;Invalidate();}}
    public int Value {
        get{return value;}
        set{int next=Math.Max(minimum,Math.Min(maximum,value));if(next==this.value)return;this.value=next;Invalidate();if(ValueChanged!=null)ValueChanged(this,EventArgs.Empty);}
    }
    int Thumb {get{return Theme.S(20);}}
    void Follow(int x){double t=(x-Thumb/2.0)/Math.Max(1,Width-Thumb);Value=minimum+(int)Math.Round(Math.Max(0,Math.Min(1,t))*(maximum-minimum));}
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left){dragging=true;Capture=true;Follow(e.X);}}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging)Follow(e.X);}
    protected override void OnMouseUp(MouseEventArgs e){dragging=false;Capture=false;base.OnMouseUp(e);}
    protected override bool IsInputKey(Keys key){return key==Keys.Left||key==Keys.Right||key==Keys.Up||key==Keys.Down||base.IsInputKey(key);}
    protected override void OnKeyDown(KeyEventArgs e){
        int step=Math.Max(1,(maximum-minimum)/10);
        if(e.KeyCode==Keys.Left||e.KeyCode==Keys.Down)Value--;else if(e.KeyCode==Keys.Right||e.KeyCode==Keys.Up)Value++;
        else if(e.KeyCode==Keys.PageDown)Value-=step;else if(e.KeyCode==Keys.PageUp)Value+=step;else if(e.KeyCode==Keys.Home)Value=minimum;else if(e.KeyCode==Keys.End)Value=maximum;
        base.OnKeyDown(e);
    }
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        float t=(value-minimum)/(float)Math.Max(1,maximum-minimum),track=Theme.S(4),cx=Thumb/2f+(Width-Thumb)*t,cy=Height/2f;
        Color accent=Enabled?Theme.Accent:Theme.Disabled;
        using(var path=Theme.Round(new RectangleF(Thumb/2f-track/2,cy-track/2,Width-Thumb+track,track),track/2))using(var brush=new SolidBrush(Enabled?Theme.Track:Theme.Disabled))g.FillPath(brush,path);
        using(var path=Theme.Round(new RectangleF(Thumb/2f-track/2,cy-track/2,cx-Thumb/2f+track,track),track/2))using(var brush=new SolidBrush(accent))g.FillPath(brush,path);
        float outer=Thumb-1,inner=Thumb*(dragging?.42f:Hot&&Enabled?.62f:.52f);
        using(var brush=new SolidBrush(Theme.Dark?Color.FromArgb(69,69,69):Color.White))g.FillEllipse(brush,cx-outer/2,cy-outer/2,outer,outer);
        using(var pen=new Pen(Theme.FieldBorder))g.DrawEllipse(pen,cx-outer/2,cy-outer/2,outer,outer);
        using(var brush=new SolidBrush(accent))g.FillEllipse(brush,cx-inner/2,cy-inner/2,inner,inner);
        Ring(g,new RectangleF(0,0,Width,Height),Theme.S(4));
    }
}

sealed class FluentButton : FluentControl {
    public bool Primary,Link;public string Icon;
    public FluentButton(string text,bool primary=false){Text=text;Primary=primary;Height=Theme.S(32);Interactive();AccessibleRole=AccessibleRole.PushButton;AccessibleName=text;AutoWidth();}
    public void AutoWidth(){Width=TextRenderer.MeasureText(Text??"",Theme.Font(14,Primary),new Size(2000,100),TextFormatFlags.NoPrefix).Width+Theme.S(Link?6:26)+(Icon!=null?Theme.S(22):0);}
    protected override void OnKeyDown(KeyEventArgs e){if(Activates(e)){OnClick(EventArgs.Empty);e.Handled=true;}base.OnKeyDown(e);}
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;Rectangle r=ClientRectangle;Color text;
        if(Link)text=!Enabled?Theme.Disabled:Hot?Theme.Text:Theme.Accent;
        else if(Primary){
            Color fill=!Enabled?Theme.Disabled:Down?Color.FromArgb(205,Theme.Accent):Hot?Color.FromArgb(232,Theme.Accent):Theme.Accent;
            Theme.Fill(g,r,Theme.S(4),fill,Color.Empty);text=Theme.OnAccent;
        }else{
            Theme.Fill(g,r,Theme.S(4),Down?Theme.Pressed:Hot&&Enabled?Color.FromArgb(Math.Max(0,Theme.Field.A-60),Theme.Field):Theme.Field,Theme.FieldBorder);text=Enabled?Theme.Text:Theme.Disabled;
        }
        Rectangle label=r;
        if(Icon!=null){int size=Theme.S(16);Icons.Draw(g,Icon,new RectangleF(Theme.S(10),(Height-size)/2f,size,size),text);label=new Rectangle(Theme.S(30),0,Width-Theme.S(34),Height);}
        Write(g,Text,Theme.Font(14,Primary),label,text,(Icon!=null||Link?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter)|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        Ring(g,r,Theme.S(4));
    }
}

// A drop-down list drawn like the Windows 11 one; the choices open in a menu.
sealed class FluentCombo : FluentControl {
    public readonly List<string> Items=new List<string>();int selected=-1;
    public event EventHandler SelectedIndexChanged;
    public FluentCombo(){Size=new Size(Theme.S(250),Theme.S(32));Interactive();AccessibleRole=AccessibleRole.ComboBox;}
    public int SelectedIndex {
        get{return selected;}
        set{if(value==selected||value<-1||value>=Items.Count)return;selected=value;Invalidate();if(SelectedIndexChanged!=null)SelectedIndexChanged(this,EventArgs.Empty);}
    }
    void Open(){
        var menu=new ContextMenuStrip{Font=Theme.Font(14),ShowImageMargin=false,ShowCheckMargin=true,BackColor=Theme.Solid,ForeColor=Theme.Text};
        for(int i=0;i<Items.Count;i++){int index=i;var item=new ToolStripMenuItem(Items[i]){Checked=i==selected,ForeColor=Theme.Text,AutoSize=false,Size=new Size(Math.Max(Width,Theme.S(180)),Theme.S(34))};item.Click+=delegate{SelectedIndex=index;};menu.Items.Add(item);}
        menu.Closed+=delegate{BeginInvoke((Action)menu.Dispose);};menu.Show(this,new Point(0,Height+Theme.S(2)));
    }
    protected override void OnClick(EventArgs e){base.OnClick(e);Open();}
    protected override bool IsInputKey(Keys key){return key==Keys.Up||key==Keys.Down||base.IsInputKey(key);}
    protected override void OnKeyDown(KeyEventArgs e){
        if(Activates(e)){Open();e.Handled=true;}else if(e.KeyCode==Keys.Up)SelectedIndex=Math.Max(0,selected-1);else if(e.KeyCode==Keys.Down)SelectedIndex=Math.Min(Items.Count-1,selected+1);
        base.OnKeyDown(e);
    }
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;Rectangle r=ClientRectangle;
        Theme.Fill(g,r,Theme.S(4),Down?Theme.Pressed:Hot?Color.FromArgb(Math.Max(0,Theme.Field.A-60),Theme.Field):Theme.Field,Theme.FieldBorder);
        Write(g,selected<0?"":Items[selected],Theme.Font(14),new Rectangle(Theme.S(11),0,Width-Theme.S(40),Height),Theme.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        int size=Theme.S(12);Icons.Draw(g,"down",new RectangleF(Width-Theme.S(24),(Height-size)/2f,size,size),Theme.Muted);
        Ring(g,r,Theme.S(4));
    }
}

sealed class SearchBox : FluentControl {
    public readonly TextBox Input=new TextBox{BorderStyle=BorderStyle.None};
    public SearchBox(string hint){
        Height=Theme.S(34);Input.Font=Theme.Font(14);Input.AccessibleName=hint;Controls.Add(Input);Restyle();
        Input.GotFocus+=delegate{Invalidate();};Input.LostFocus+=delegate{Invalidate();};
        Input.HandleCreated+=delegate{try{Native.SendMessage(Input.Handle,0x1501,new IntPtr(1),hint);}catch{}};
    }
    public void Restyle(){Input.BackColor=Theme.Solid;Input.ForeColor=Theme.Text;Invalidate();}
    protected override void OnClick(EventArgs e){Input.Focus();base.OnClick(e);}
    protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);Input.SetBounds(Theme.S(12),(Height-Input.PreferredHeight)/2,Math.Max(10,Width-Theme.S(46)),Input.PreferredHeight);}
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;Rectangle r=ClientRectangle;Theme.Fill(g,r,Theme.S(4),Theme.Solid,Theme.FieldBorder);
        // The accent underline of a focused Windows 11 text field.
        using(var pen=new Pen(Input.Focused?Theme.Accent:Theme.Track,Input.Focused?Theme.S(2):1))g.DrawLine(pen,Theme.S(4),Height-(Input.Focused?Theme.S(1):1),Width-Theme.S(4),Height-(Input.Focused?Theme.S(1):1));
        int size=Theme.S(15);Icons.Draw(g,"search",new RectangleF(Width-Theme.S(27),(Height-size)/2f,size,size),Theme.Muted);
    }
}

// One line of a settings card: pictogram, title, explanation, then a value and a control (or a chevron) on the right.
sealed class SettingRow : FluentControl {
    public string Icon,Title,Description,Value,Keywords;public Control Action;public bool Chevron;public int Page;
    Timer flash;bool lit;
    public SettingRow(string icon,string title,string description,Control action=null){
        Icon=icon;Title=title;Description=description;Action=action;Height=Theme.S(string.IsNullOrEmpty(description)?54:66);AccessibleName=title;
        if(action!=null){action.AccessibleName=title;Controls.Add(action);}
    }
    public bool Matches(string query){return Theme.Plain(Title+" "+Description+" "+Keywords).Contains(query);}
    public void Flash(){
        lit=true;Invalidate();
        if(flash==null){flash=new Timer{Interval=1400};flash.Tick+=delegate{flash.Stop();lit=false;Invalidate();};}
        flash.Stop();flash.Start();
    }
    public void AsLink(){Chevron=true;Interactive();AccessibleRole=AccessibleRole.Link;Cursor=Cursors.Hand;}
    protected override void OnKeyDown(KeyEventArgs e){if(Chevron&&Activates(e)){OnClick(EventArgs.Empty);e.Handled=true;}base.OnKeyDown(e);}
    protected override void Dispose(bool disposing){if(disposing&&flash!=null)flash.Dispose();base.Dispose(disposing);}
    int ActionWidth {get{return Action is FluentSlider?Math.Max(Theme.S(110),Math.Min(Theme.S(260),Width*42/100)):Action is FluentCombo?Math.Max(Theme.S(150),Math.Min(Theme.S(270),Width*46/100)):Action.Width;}}
    protected override void OnLayout(LayoutEventArgs e){
        base.OnLayout(e);if(Action==null)return;
        int w=ActionWidth;Action.SetBounds(Width-Theme.S(18)-w,(Height-Action.Height)/2,w,Action.Height);
    }
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;
        if(Chevron&&(Hot||Down))Theme.Fill(g,new Rectangle(Theme.S(2),Theme.S(2),Width-Theme.S(4),Height-Theme.S(4)),Theme.S(5),Down?Theme.Pressed:Theme.Hover,Color.Empty);
        if(lit)Theme.Fill(g,new Rectangle(Theme.S(3),Theme.S(3),Width-Theme.S(6),Height-Theme.S(6)),Theme.S(5),Color.FromArgb(26,Theme.Accent),Theme.Accent);
        int left=Theme.S(18),right=Width-Theme.S(18);
        if(Icon!=null){int size=Theme.S(20);Icons.Draw(g,Icon,new RectangleF(left,(Height-size)/2f,size,size),Theme.Text);left+=size+Theme.S(16);}
        if(Chevron){int size=Theme.S(12);Icons.Draw(g,"chevron",new RectangleF(right-size,(Height-size)/2f,size,size),Theme.Muted);right-=size+Theme.S(12);}
        if(Action!=null)right=Action.Left-Theme.S(12);
        if(!string.IsNullOrEmpty(Value)){
            int w=Math.Min(right-left-Theme.S(60),TextRenderer.MeasureText(Value,Theme.Font(14),new Size(2000,100),TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding).Width+Theme.S(4));
            if(w>8){Write(g,Value,Theme.Font(14),new Rectangle(right-w,0,w,Height),Action==null||Action.Enabled?Theme.Text:Theme.Disabled,TextFormatFlags.VerticalCenter|TextFormatFlags.Right|TextFormatFlags.EndEllipsis);right-=w+Theme.S(12);}
        }
        Rectangle text=new Rectangle(left,0,Math.Max(0,right-left),Height);
        if(string.IsNullOrEmpty(Description))Write(g,Title,Theme.Font(14),text,Theme.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        else{
            int line=Theme.S(20),top=(Height-line*2)/2;
            Write(g,Title,Theme.Font(14),new Rectangle(left,top,text.Width,line),Theme.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            Write(g,Description,Theme.Font(12),new Rectangle(left,top+line,text.Width,line),Theme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        }
        if(Chevron)Ring(g,ClientRectangle,Theme.S(5));
    }
}

// Rounded translucent surface that stacks its children and sizes itself to them.
sealed class Card : FluentControl {
    public string Title,Subtitle;public int Inset;bool arranging;
    public Card(string title=null,string subtitle=null){Title=title;Subtitle=subtitle;}
    int Header {get{return Title==null?0:Theme.S(Subtitle==null?58:86);}}
    public T Add<T>(T child) where T:Control {Controls.Add(child);return child;}
    protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);Arrange();}
    protected override void OnResize(EventArgs e){base.OnResize(e);Arrange();}
    // Called directly as well: a hidden page receives no layout events, yet must be ready when it is shown.
    public void Arrange(){
        if(arranging||Width<20)return;arranging=true;
        try{
            int y=Header,inset=Theme.S(Inset);
            for(int i=0;i<Controls.Count;i++){
                Control c=Controls[i];
                bool row=c is SettingRow;int x=row?0:Math.Max(inset,Theme.S(20)),w=Width-2*x;
                if(!row&&y==0)y=Theme.S(18);
                if(c is FluentButton)c.Location=new Point(x,y);else c.SetBounds(x,y,Math.Max(10,w),c.Height);
                if(c is TextBlock)((TextBlock)c).Fit();if(c is TileStrip)((TileStrip)c).Fit();if(row)c.PerformLayout();
                y+=c.Height+(row?0:Theme.S(12));
            }
            if(Controls.Count>0&&!(Controls[Controls.Count-1] is SettingRow))y+=Theme.S(8);
            if(Height!=Math.Max(y,Theme.S(20)))Height=Math.Max(y,Theme.S(20));
        }finally{arranging=false;}
    }
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;Theme.Fill(g,ClientRectangle,Theme.S(8),Theme.Card,Theme.CardBorder);
        if(Title!=null){
            Write(g,Title,Theme.Font(20,true),new Rectangle(Theme.S(22),Theme.S(18),Width-Theme.S(44),Theme.S(30)),Theme.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
            if(Subtitle!=null)Write(g,Subtitle,Theme.Font(14),new Rectangle(Theme.S(22),Theme.S(50),Width-Theme.S(44),Theme.S(22)),Theme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        }
        // Hairlines between rows, as in the reference cards.
        using(var pen=new Pen(Theme.Divider))for(int i=0;i<Controls.Count;i++){Control c=Controls[i];if(c.Visible&&c is SettingRow&&c.Top>Theme.S(4))g.DrawLine(pen,1,c.Top,Width-2,c.Top);}
    }
}

// A grid of pictures to choose from: pointer styles, colors, ambiances.
sealed class TileStrip : FluentControl {
    public int Count,Columns,TileWidth=Theme.S(56),TileHeight=Theme.S(64),Gap=Theme.S(8);public string[] Names;
    public Action<Graphics,Rectangle,int> Painter;public event Action<int> Picked;
    int selected=-1,hover=-1,cursor;bool sizing;
    public TileStrip(){Interactive();AccessibleRole=AccessibleRole.List;}
    public int SelectedIndex {get{return selected;}set{if(selected==value)return;selected=value;Invalidate();}}
    int PerRow {get{return Columns>0?Columns:Math.Max(1,(Width+Gap)/(TileWidth+Gap));}}
    Rectangle Tile(int i){
        int per=PerRow,w=Columns>0?(Width-Gap*(per-1))/per:TileWidth,h=Columns>0?Math.Max(Theme.S(44),w*58/100):TileHeight;
        return new Rectangle(i%per*(w+Gap),i/per*(h+Gap),w,h);
    }
    public void Fit(){
        if(sizing||Count==0||Width<10)return;sizing=true;
        try{int h=Tile(Count-1).Bottom+1;if(h!=Height)Height=h;}finally{sizing=false;}
    }
    protected override void OnResize(EventArgs e){base.OnResize(e);Fit();}
    protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);Fit();}
    int At(Point p){for(int i=0;i<Count;i++)if(Tile(i).Contains(p))return i;return -1;}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);int i=At(e.Location);if(i!=hover){hover=i;Invalidate();}}
    protected override void OnMouseLeave(EventArgs e){hover=-1;base.OnMouseLeave(e);}
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);int i=At(e.Location);if(i>=0&&e.Button==MouseButtons.Left){cursor=i;if(Picked!=null)Picked(i);}}
    protected override bool IsInputKey(Keys key){return key==Keys.Left||key==Keys.Right||key==Keys.Up||key==Keys.Down||base.IsInputKey(key);}
    protected override void OnKeyDown(KeyEventArgs e){
        if(e.KeyCode==Keys.Left)cursor=Math.Max(0,cursor-1);else if(e.KeyCode==Keys.Right)cursor=Math.Min(Count-1,cursor+1);
        else if(e.KeyCode==Keys.Up)cursor=Math.Max(0,cursor-PerRow);else if(e.KeyCode==Keys.Down)cursor=Math.Min(Count-1,cursor+PerRow);
        else if(Activates(e)&&Picked!=null)Picked(cursor);
        Invalidate();base.OnKeyDown(e);
    }
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;
        for(int i=0;i<Count;i++){
            Rectangle r=Tile(i);Theme.Fill(g,r,Theme.S(6),Theme.Field,i==hover?Theme.Track:Theme.FieldBorder);
            if(Painter!=null){var state=g.Save();try{Painter(g,r,i);}finally{g.Restore(state);}}
            g.SmoothingMode=SmoothingMode.AntiAlias;
            if(i==selected)using(var path=Theme.Round(new RectangleF(r.X+1,r.Y+1,r.Width-2,r.Height-2),Theme.S(6)))using(var pen=new Pen(Theme.Accent,Math.Max(2,Theme.S(2))))g.DrawPath(pen,path);
            if(Focused&&ShowFocusCues&&i==cursor)using(var path=Theme.Round(new RectangleF(r.X+3,r.Y+3,r.Width-6,r.Height-6),Theme.S(4)))using(var pen=new Pen(Theme.Text))g.DrawPath(pen,path);
        }
    }
}

// Small status tile of the header: a pictogram or a color dot, a title and one line of detail.
sealed class InfoTile : FluentControl {
    public string Icon,Title,Detail="";public Color? Dot;
    public InfoTile(string icon,string title){Icon=icon;Title=title;Size=new Size(Theme.S(206),Theme.S(60));Interactive();Cursor=Cursors.Hand;AccessibleRole=AccessibleRole.Link;AccessibleName=title;}
    public void Set(string detail,Color? dot=null){if(detail==Detail&&Nullable.Equals(dot,Dot))return;Detail=detail;Dot=dot;Invalidate();}
    protected override void OnKeyDown(KeyEventArgs e){if(Activates(e)){OnClick(EventArgs.Empty);e.Handled=true;}base.OnKeyDown(e);}
    protected override void OnPaint(PaintEventArgs e){
        var g=e.Graphics;if(Hot)Theme.Fill(g,ClientRectangle,Theme.S(6),Down?Theme.Pressed:Theme.Hover,Color.Empty);
        int size=Theme.S(22),x=Theme.S(10);RectangleF mark=new RectangleF(x,Theme.S(10),size,size);
        if(Dot.HasValue){g.SmoothingMode=SmoothingMode.AntiAlias;using(var brush=new SolidBrush(Dot.Value))g.FillEllipse(brush,mark);using(var pen=new Pen(Theme.FieldBorder))g.DrawEllipse(pen,mark);}
        else Icons.Draw(g,Icon,mark,Icons.Tint(Icon));
        int left=x+size+Theme.S(12);
        Write(g,Title,Theme.Font(14,true),new Rectangle(left,Theme.S(6),Width-left-Theme.S(4),Theme.S(22)),Theme.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        Write(g,Detail,Theme.Font(12),new Rectangle(left,Theme.S(28),Width-left-Theme.S(4),Theme.S(32)),Theme.Muted,TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis);
        Ring(g,ClientRectangle,Theme.S(6));
    }
}

// Scrolling page that lays its cards out in one or two columns depending on the room available.
sealed class PagePanel : Panel {
    readonly List<Control> items=new List<Control>();readonly List<int> sides=new List<int>();bool arranging;
    public PagePanel(){DoubleBuffered=true;AutoScroll=true;BackColor=Color.Transparent;SetStyle(ControlStyles.ResizeRedraw,true);AutoScrollMargin=new Size(0,Theme.S(14));}
    public T Add<T>(T item,int side=0) where T:Control {items.Add(item);sides.Add(side);Controls.Add(item);return item;}
    public void Arrange(){
        if(arranging||ClientSize.Width<40)return;arranging=true;
        try{
            int gap=Theme.S(12),width=ClientSize.Width-Theme.S(6),scroll=AutoScrollPosition.Y;bool two=sides.Contains(1)&&width>=Theme.S(700);
            int column=two?(width-gap)/2:width;int[] y=new int[2];
            for(int i=0;i<items.Count;i++){
                Control c=items[i];int side=two?sides[i]:0;
                c.SetBounds(side*(column+gap),y[side]+scroll,column,c.Height);if(c is Card)((Card)c).Arrange();y[side]+=c.Height+gap;
            }
        }finally{arranging=false;}
    }
    protected override void OnLayout(LayoutEventArgs e){Arrange();base.OnLayout(e);}
    protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);if(Visible){Arrange();PerformLayout();}}
    protected override void OnScroll(ScrollEventArgs e){base.OnScroll(e);Invalidate(true);}
    protected override void OnMouseWheel(MouseEventArgs e){base.OnMouseWheel(e);Invalidate(true);}
}
