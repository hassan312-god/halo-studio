using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Text.RegularExpressions;
using System.Diagnostics;

sealed class ReleaseInfo {public string Version,Url,Digest;public long Size;}
sealed class AppUpdater : IDisposable {
    readonly HttpClient http=new HttpClient{Timeout=TimeSpan.FromSeconds(90)};public string PendingFile,PendingDigest;public bool Busy;public string Message="Version "+BuildInfo.Version;
    public AppUpdater(){ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;http.DefaultRequestHeaders.UserAgent.ParseAdd("HaloStudio/"+BuildInfo.Version);http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");}
    public static int Compare(string a,string b){
        var pattern=new Regex(@"^v?(\d+)\.(\d+)\.(\d+)(?:-beta\.(\d+))?$");var x=pattern.Match(a);var y=pattern.Match(b);if(!x.Success||!y.Success)throw new FormatException("Version non reconnue.");
        for(int i=1;i<=3;i++){int c=int.Parse(x.Groups[i].Value).CompareTo(int.Parse(y.Groups[i].Value));if(c!=0)return c;}
        if(!x.Groups[4].Success)return y.Groups[4].Success?1:0;if(!y.Groups[4].Success)return -1;return int.Parse(x.Groups[4].Value).CompareTo(int.Parse(y.Groups[4].Value));
    }
    public static ReleaseInfo Parse(string json){
        var releases=new JavaScriptSerializer{MaxJsonLength=2*1024*1024}.DeserializeObject(json) as object[];if(releases==null)throw new InvalidDataException("Réponse GitHub inattendue.");ReleaseInfo best=null;
        foreach(var raw in releases){var r=raw as Dictionary<string,object>;if(r==null||Convert.ToBoolean(r["draft"]))continue;string version=Convert.ToString(r["tag_name"]).TrimStart('v');try{Compare(version,BuildInfo.Version);}catch{continue;}
            foreach(var item in (object[])r["assets"]){var a=(Dictionary<string,object>)item;if(Convert.ToString(a["name"])!="HaloStudio-Setup-"+version+".exe")continue;
                string url=Convert.ToString(a["browser_download_url"]);string required=BuildInfo.RepositoryUrl+"/releases/download/v"+version+"/HaloStudio-Setup-"+version+".exe";
                string digest=a.ContainsKey("digest")?Convert.ToString(a["digest"]):"";long size=Convert.ToInt64(a["size"]);
                if(url!=required||!Regex.IsMatch(digest,@"^sha256:[a-fA-F0-9]{64}$")||size<1||size>64*1024*1024)continue;
                if(best==null||Compare(version,best.Version)>0)best=new ReleaseInfo{Version=version,Url=url,Digest=digest.Substring(7).ToLowerInvariant(),Size=size};
            }
        }return best;
    }
    public static string Hash(string file){using(var sha=SHA256.Create())using(var stream=File.OpenRead(file))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
    public static bool Verified(string file,string digest){return File.Exists(file)&&Hash(file).Equals(digest,StringComparison.OrdinalIgnoreCase);}
    public async Task<ReleaseInfo> Latest(){string json=await http.GetStringAsync("https://api.github.com/repos/"+BuildInfo.Repository+"/releases?per_page=30");return Parse(json);}
    public async Task Check(bool download){
        if(Busy)return;Busy=true;Message="Recherche de mises à jour…";
        try{var release=await Latest();if(release==null||Compare(release.Version,BuildInfo.Version)<=0){Message="À jour · "+BuildInfo.Version;return;}
            if(!download){Message="Version "+release.Version+" disponible";return;}
            Message="Téléchargement de "+release.Version+"…";string folder=Path.Combine(HaloSettings.DataDirectory,"updates");Directory.CreateDirectory(folder);string final=Path.Combine(folder,"HaloStudio-Setup-"+release.Version+".exe"),partial=final+".part";
            if(!Verified(final,release.Digest)){
                try{using(var response=await http.GetAsync(release.Url,HttpCompletionOption.ResponseHeadersRead)){response.EnsureSuccessStatusCode();using(var input=await response.Content.ReadAsStreamAsync())using(var output=File.Create(partial)){byte[] buffer=new byte[65536];long total=0;int n;while((n=await input.ReadAsync(buffer,0,buffer.Length))>0){total+=n;if(total>release.Size)throw new InvalidDataException("Taille du téléchargement incorrecte.");await output.WriteAsync(buffer,0,n);}if(total!=release.Size)throw new InvalidDataException("Téléchargement incomplet.");}}
                    if(!Verified(partial,release.Digest))throw new InvalidDataException("La vérification SHA-256 a échoué.");if(File.Exists(final))File.Delete(final);File.Move(partial,final);
                }finally{if(File.Exists(partial))File.Delete(partial);}
            }
            PendingFile=final;PendingDigest=release.Digest;Message="Bêta "+release.Version+" prête · installation à la fermeture";
        }catch(Exception ex){Message="Mise à jour indisponible : "+ex.Message;}finally{Busy=false;}
    }
    public bool LaunchPending(bool relaunch){
        if(PendingFile==null)return false;if(!Verified(PendingFile,PendingDigest)){Message="Le fichier de mise à jour a changé. Télécharge-le à nouveau.";PendingFile=null;return false;}
        Process.Start(new ProcessStartInfo(PendingFile,"--silent --parent "+Process.GetCurrentProcess().Id+(relaunch?" --launch":"")){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden});PendingFile=null;return true;
    }
    public void Dispose(){http.Dispose();}
}
