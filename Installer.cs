using System;
using System.IO;
using System.IO.Compression;
using System.Drawing;
using System.Reflection;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;
using Microsoft.Win32;

static class Setup {
    public static readonly string Folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","HaloStudio");
    const string RegistryPath="Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\HaloStudio";
    static readonly string[] Files={"Halo.exe","Halo.ico","logo.png","LIRE-MOI.txt"};
    [STAThread] static void Main(string[] args){
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        try{
            if(args.Length>0&&args[0]=="--extract-test"){Extract(args[1]);return;}
            if(Array.IndexOf(args,"--uninstall")>=0){
                if(MessageBox.Show("Désinstaller Halo Studio ?\nTon pointeur habituel sera restauré. Tes préférences seront conservées.","Halo Studio",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
                string copy=Path.Combine(Path.GetTempPath(),"HaloStudio-Uninstall-"+Guid.NewGuid().ToString("N")+".exe");File.Copy(Application.ExecutablePath,copy);Process.Start(new ProcessStartInfo(copy,"--uninstall-worker"){UseShellExecute=false});return;
            }
            if(Array.IndexOf(args,"--uninstall-worker")>=0){Uninstall();MessageBox.Show("Halo Studio a été désinstallé.","Halo Studio");return;}
            int parentAt=Array.IndexOf(args,"--parent");if(parentAt>=0){int parent=int.Parse(args[parentAt+1]);try{using(var p=Process.GetProcessById(parent)){if(!p.WaitForExit(30000))throw new IOException("Halo est encore ouvert. Réessaie après sa fermeture.");}}catch(ArgumentException){}}
            if(Array.IndexOf(args,"--silent")>=0){Install(Array.IndexOf(args,"--desktop")>=0);if(Array.IndexOf(args,"--launch")>=0)Launch();return;}
            Application.Run(new SetupForm());
        }catch(Exception ex){Environment.ExitCode=1;string log=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"HaloStudio","installation.log");try{Directory.CreateDirectory(Path.GetDirectoryName(log));File.AppendAllText(log,DateTime.UtcNow.ToString("s")+" "+ex+Environment.NewLine);}catch{}if(Array.IndexOf(args,"--silent")<0)MessageBox.Show("Installation interrompue : "+ex.Message,"Halo Studio",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    }
    static void StopApp(){
        var running=new List<Process>();foreach(var p in Process.GetProcessesByName("Halo")){try{if(p.MainModule.FileName.Equals(Path.Combine(Folder,"Halo.exe"),StringComparison.OrdinalIgnoreCase))running.Add(p);else p.Dispose();}catch{p.Dispose();}}
        try{using(var signal=EventWaitHandle.OpenExisting("Local\\HaloAmbientStop_Je_20261108"))signal.Set();}catch(WaitHandleCannotBeOpenedException){}
        foreach(var p in running)using(p)if(!p.WaitForExit(12000))throw new IOException("Ferme Halo Studio avant de continuer.");
        // The restoration guard exits after its parent and must release Halo.exe too.
        for(int i=0;i<40;i++){try{string exe=Path.Combine(Folder,"Halo.exe");if(!File.Exists(exe))return;using(var stream=new FileStream(exe,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){}return;}catch(IOException){Thread.Sleep(100);}}
        throw new IOException("Halo termine la restauration du pointeur. Réessaie dans un instant.");
    }
    public static void Extract(string destination){
        string root=Path.GetFullPath(destination);Directory.CreateDirectory(root);
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Halo.Payload"))using(var archive=new ZipArchive(stream,ZipArchiveMode.Read)){
            foreach(string name in Files){var entry=archive.GetEntry(name);if(entry==null||entry.Length>64*1024*1024)throw new InvalidDataException("Archive d’installation incomplète.");string file=Path.Combine(root,name);using(var source=entry.Open())using(var output=File.Create(file))source.CopyTo(output);}
            if(archive.Entries.Count!=Files.Length)throw new InvalidDataException("Contenu inattendu dans l’archive.");
        }
    }
    public static void Install(bool desktop){
        StopApp();Directory.CreateDirectory(Folder);
        if((File.GetAttributes(Folder)&FileAttributes.ReparsePoint)!=0)throw new IOException("Le dossier d’installation ne doit pas être un lien.");
        string stage=Path.Combine(Path.GetTempPath(),"HaloStudio-Stage-"+Guid.NewGuid().ToString("N"));Extract(stage);
        var backups=new Dictionary<string,string>();var installed=new List<string>();
        try{
            var targets=new List<string>(Files);targets.Add("Uninstall.exe");
            foreach(string name in targets){string target=Path.Combine(Folder,name);if(File.Exists(target)){string backup=Path.Combine(stage,name+".previous");File.Copy(target,backup);backups[target]=backup;}
                File.Copy(name=="Uninstall.exe"?Application.ExecutablePath:Path.Combine(stage,name),target,true);installed.Add(target);
            }
            string exe=Path.Combine(Folder,"Halo.exe");string icon=Path.Combine(Folder,"Halo.ico");
            Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"Halo Studio.lnk"),exe,icon);
            if(desktop)Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"Halo Studio.lnk"),exe,icon);
            using(var key=Registry.CurrentUser.CreateSubKey(RegistryPath)){
                key.SetValue("DisplayName","Halo Studio (bêta)");key.SetValue("DisplayVersion",BuildInfo.Version);key.SetValue("Publisher","Halo Studio");key.SetValue("DisplayIcon",icon);key.SetValue("InstallLocation",Folder);
                key.SetValue("UninstallString","\""+Path.Combine(Folder,"Uninstall.exe")+"\" --uninstall");key.SetValue("URLInfoAbout",BuildInfo.RepositoryUrl);key.SetValue("NoModify",1,RegistryValueKind.DWord);key.SetValue("NoRepair",1,RegistryValueKind.DWord);key.SetValue("EstimatedSize",4096,RegistryValueKind.DWord);
            }
        }catch{foreach(string target in installed){if(backups.ContainsKey(target))File.Copy(backups[target],target,true);else File.Delete(target);}throw;}
        finally{foreach(string file in Directory.GetFiles(stage))File.Delete(file);Directory.Delete(stage,false);}
    }
    static void Shortcut(string path,string target,string icon){
        dynamic shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));try{dynamic link=shell.CreateShortcut(path);try{link.TargetPath=target;link.WorkingDirectory=Folder;link.Description="Halo Studio · couleurs et pointeur lumineux";link.IconLocation=icon;link.Save();}finally{System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);}}finally{System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);}
    }
    static void Uninstall(){
        StopApp();string expected=Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","HaloStudio"));
        if(!Path.GetFullPath(Folder).Equals(expected,StringComparison.OrdinalIgnoreCase))throw new IOException("Dossier d’installation inattendu.");
        foreach(string name in Files){string target=Path.Combine(Folder,name);if(File.Exists(target))File.Delete(target);}
        foreach(string name in new[]{"Uninstall.exe","Pointeur-Halo.cur"}){string target=Path.Combine(Folder,name);if(File.Exists(target))File.Delete(target);}
        foreach(string parent in new[]{Environment.GetFolderPath(Environment.SpecialFolder.Programs),Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)}){string link=Path.Combine(parent,"Halo Studio.lnk");if(File.Exists(link))File.Delete(link);}
        Registry.CurrentUser.DeleteSubKeyTree(RegistryPath,false);if(Directory.Exists(Folder)&&Directory.GetFileSystemEntries(Folder).Length==0)Directory.Delete(Folder,false);
    }
    public static void Launch(){Process.Start(new ProcessStartInfo(Path.Combine(Folder,"Halo.exe")){UseShellExecute=true,WorkingDirectory=Folder});}
}

sealed class SetupForm : Form {
    public SetupForm(){
        Text="Installer Halo Studio · bêta";ClientSize=new Size(570,390);StartPosition=FormStartPosition.CenterScreen;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;BackColor=Color.FromArgb(248,250,254);Font=new Font("Segoe UI",10);
        using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("Halo.Icon"))Icon=new Icon(s);
        using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("Halo.Logo"))using(var img=Image.FromStream(s))Controls.Add(new PictureBox{Image=new Bitmap(img),Location=new Point(28,28),Size=new Size(72,72),SizeMode=PictureBoxSizeMode.Zoom});
        Controls.Add(new Label{Text="Halo Studio",Location=new Point(120,30),Size=new Size(400,48),Font=new Font("Segoe UI",24,FontStyle.Bold)});
        Controls.Add(new Label{Text=BuildInfo.Version+" · Windows 10 / 11",Location=new Point(123,82),AutoSize=true});
        Controls.Add(new Label{Text="Halo adaptatif, pointeurs lumineux et aperçu en direct.\nInstallation pour ton compte, sans droits administrateur.\nDésinstallation depuis les applications Windows.",Location=new Point(30,131),Size=new Size(510,80)});
        var desktop=new CheckBox{Text="Créer un raccourci sur le bureau",Checked=true,Location=new Point(30,225),AutoSize=true};Controls.Add(desktop);
        var launch=new CheckBox{Text="Ouvrir Halo Studio après l’installation",Checked=true,Location=new Point(30,262),AutoSize=true};Controls.Add(launch);
        var status=new Label{Text="",Location=new Point(30,316),Size=new Size(320,55)};Controls.Add(status);
        var install=new Button{Text="Installer",Location=new Point(392,317),Size=new Size(145,40)};Controls.Add(install);
        install.Click+=delegate{install.Enabled=false;status.Text="Installation en cours…";status.Refresh();try{Setup.Install(desktop.Checked);status.Text="Installation terminée.";if(launch.Checked)Setup.Launch();Close();}catch(Exception ex){status.Text=ex.Message;install.Enabled=true;}};
    }
}
