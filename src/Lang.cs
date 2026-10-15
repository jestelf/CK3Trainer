using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

static partial class Lang {
 static readonly string Settings=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CK3PersonalTrainer","language.txt");
 public static bool En=Load();
 static Dictionary<string,string> names,reverse;
 static bool Load(){
  try{if(File.Exists(Settings)){string v=File.ReadAllText(Settings).Trim();if(v=="en")return true;if(v=="ru")return false;}}catch{}
  string c=System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;return !(c=="ru"||c=="uk"||c=="be"||c=="kk");
 }
 public static void Set(bool en){En=en;try{Directory.CreateDirectory(Path.GetDirectoryName(Settings));File.WriteAllText(Settings,en?"en":"ru");}catch{}}
 public static string T(string s){string e;return En && s!=null && Ui.TryGetValue(s,out e)?e:s;}
 public static string Name(string s){
  if(!En || String.IsNullOrEmpty(s))return s;
  if(names==null){names=new Dictionary<string,string>();try{string f=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"names.en.json");
   if(File.Exists(f))names=new System.Web.Script.Serialization.JavaScriptSerializer{MaxJsonLength=50000000}.Deserialize<Dictionary<string,string>>(File.ReadAllText(f));}catch{}}
  string e;return names.TryGetValue(s,out e)?e:T(s);
 }
 static string Other(string s){
  if(reverse==null){reverse=new Dictionary<string,string>();foreach(var kv in Ui)if(!reverse.ContainsKey(kv.Value))reverse[kv.Value]=kv.Key;}
  if(En)return T(s);string r;return reverse.TryGetValue(s,out r)?r:s;
 }
 public static void Retranslate(Control root){
  foreach(Control c in root.Controls){
   if(!(c is TextBox) && !(c is NumericUpDown) && !(c is ListControl) && !String.IsNullOrEmpty(c.Text))c.Text=Other(c.Text);
   Retranslate(c);
  }
 }
 static readonly Dictionary<object,string[]> originals=new Dictionary<object,string[]>();
 public static void Localize(IEnumerable<Feature> features){foreach(var f in features){string[] o;if(!originals.TryGetValue(f,out o))originals[f]=o=new[]{f.title,f.description};f.title=T(o[0]);f.description=T(o[1]);}}
 public static void Localize(IEnumerable<CampaignAction> actions){foreach(var a in actions){string[] o;if(!originals.TryGetValue(a,out o))originals[a]=o=new[]{a.title,a.description,a.text,a.text2};a.title=T(o[0]);a.description=T(o[1]);a.text=T(o[2]);a.text2=T(o[3]);}}
 public static void Localize(Dictionary<string,List<CatalogItem>> catalogs){foreach(var list in catalogs.Values)foreach(var x in list){string[] o;if(!originals.TryGetValue(x,out o))originals[x]=o=new[]{x.name,x.group};x.name=Name(o[0]);x.group=T(o[1]);}}
 public static string Role(string role){
  if(role==null)return "";
  if(role.StartsWith("Поиск: "))return T("Поиск: ")+role.Substring(7);
  return T(role);
 }
}
