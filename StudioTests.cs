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
    [StructLayout(LayoutKind.Sequential)] struct IconInfo {[MarshalAs(UnmanagedType.Bool)]public bool Icon;public uint X,Y;public IntPtr Mask,Color;}
    [DllImport("user32.dll")] static extern bool GetIconInfo(IntPtr cursor,out IconInfo info);
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
        for(int role=1;role<=2;role++){IntPtr h=PointerArt.CreateCursor(5,Color.DodgerBlue,90,100,role);IconInfo info;try{Check(GetIconInfo(h,out info),"hand or text cursor is a valid native pointer",log);Check(info.X==32&&info.Y==31,"hand and text cursor retain their click point",log);Native.DeleteObject(info.Mask);Native.DeleteObject(info.Color);}finally{Native.DestroyIcon(h);}}
        using(var off=PointerArt.Render(0,Color.Red,0,100))using(var on=PointerArt.Render(0,Color.Red,100,100)){
            int extra=0;for(int y=0;y<off.Height;y++)for(int x=0;x<off.Width;x++)if(off.GetPixel(x,y).A==0&&on.GetPixel(x,y).A>15)extra++;
            Check(extra>100,"pointer intensity adds light along the silhouette",log);Check(Hash(off)!=Hash(on),"zero and full pointer intensity differ",log);
        }
        var settings=new HaloSettings{AutoColor=false,ColorArgb=Color.HotPink.ToArgb(),PointerStyle=3,PointerIntensity=31,ScreenIntensity=72,PointerSize=140,Width=130,ScreenEnabled=true};
        var serializer=new XmlSerializer(typeof(HaloSettings));using(var memory=new MemoryStream()){serializer.Serialize(memory,settings);memory.Position=0;var loaded=(HaloSettings)serializer.Deserialize(memory);Check(loaded.ColorArgb==settings.ColorArgb&&loaded.PointerStyle==3&&loaded.PointerIntensity==31&&loaded.ScreenIntensity==72&&loaded.Width==130,"all customization settings survive serialization",log);}
        using(var monitor=new MonitorGlow(Screen.PrimaryScreen.Bounds)){monitor.Update(false,1,false,false,Color.HotPink,1);Check(monitor.Global.ToArgb()==Color.HotPink.ToArgb(),"single-color mode keeps the exact chosen color",log);}
        using(var studio=new StudioForm(settings,false)){
            studio.Show();Application.DoEvents();int changes=0;studio.Changed+=delegate{changes++;};
            studio.ColorMode.SelectedIndex=0;studio.StyleBox.SelectedIndex=4;studio.ScreenSlider.Value=20;studio.PointerSlider.Value=90;Application.DoEvents();
            Check(settings.AutoColor&&settings.PointerStyle==4&&settings.ScreenIntensity==20&&settings.PointerIntensity==90&&changes>=4,"UI controls apply color, style and independent intensity",log);
            using(var low=Snapshot(studio.Preview)){studio.ScreenSlider.Value=100;Application.DoEvents();using(var high=Snapshot(studio.Preview))Check(Hash(low)!=Hash(high),"miniature preview reacts to screen halo intensity",log);}
            using(var low=Snapshot(studio.Preview)){studio.PointerSlider.Value=0;Application.DoEvents();using(var high=Snapshot(studio.Preview))Check(Hash(low)!=Hash(high),"miniature preview reacts to pointer intensity",log);}
            using(var red=new Bitmap(192,108))using(var blue=new Bitmap(192,108))using(var rg=Graphics.FromImage(red))using(var bg=Graphics.FromImage(blue)){
                rg.Clear(Color.Red);bg.Clear(Color.Blue);studio.Preview.SetFrame(red);
                using(var first=Snapshot(studio.Preview)){studio.Preview.SetFrame(blue);using(var second=Snapshot(studio.Preview))Check(Hash(first)!=Hash(second),"live preview changes with captured screen frames",log);}
                studio.Preview.SetFrame(null);
            }
            studio.BrightnessSlider.Value=80;Check(studio.Preview.Brightness==80,"miniature preview reacts to PC brightness",log);
            studio.PointerSlider.Value=85;studio.StyleBox.SelectedIndex=0;studio.ColorMode.SelectedIndex=1;
            for(int i=0;i<4;i++){studio.SelectPage(i);Application.DoEvents();using(var shot=Snapshot(studio))shot.Save(Path.Combine(folder,"studio-tab-"+i+".png"));}
            studio.ClientSize=new Size(900,620);studio.SelectPage(2);Application.DoEvents();Check(studio.Preview.Right<=studio.ClientSize.Width&&studio.Preview.Bottom<studio.ClientSize.Height-55,"preview fits a compact laptop window without covering the footer",log);using(var shot=Snapshot(studio))shot.Save(Path.Combine(folder,"studio-laptop.png"));
            studio.AllowClose=true;studio.Close();
        }
        var brightness=new BrightnessControl();if(brightness.Available){int initial=brightness.Current;brightness.Set(initial);Check(brightness.Current==initial,"hardware brightness supports read/write without changing the current value",log);}else log.Add("INFO hardware brightness unavailable: "+brightness.Error);
    }
}
