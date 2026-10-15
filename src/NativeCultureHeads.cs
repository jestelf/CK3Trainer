using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Threading.Tasks;using System.Web.Script.Serialization;using System.Diagnostics;
sealed class CultureHeadEntry {public uint Culture,Previous,Target;}
sealed class CultureHeadSave {public int Pid;public long Start,Campaign;public List<CultureHeadEntry> Entries=new List<CultureHeadEntry>();}
sealed class NativeCultureHeads {
 readonly NativeBridge bridge;readonly string file;CultureHeadSave state;public int Count {get{return state.Entries.Count;}}
 public NativeCultureHeads(NativeBridge b){bridge=b;file=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CK3PersonalTrainer","culture-heads.json");state=NewState();}
 CultureHeadSave NewState(){return new CultureHeadSave{Pid=bridge.Pid,Start=Process.GetProcessById(bridge.Pid).StartTime.ToUniversalTime().Ticks,Campaign=bridge.GameState};}
 void Save(){AtomicStorage.Write(file,new JavaScriptSerializer().Serialize(state));}
 public async Task Recover(){if(!File.Exists(file))return;var prior=new JavaScriptSerializer().Deserialize<CultureHeadSave>(File.ReadAllText(file));if(prior!=null && prior.Pid==state.Pid && prior.Start==state.Start && prior.Campaign==bridge.GameState){state=prior;await Clear();}}
 public async Task Set(uint culture,uint target,uint expected){
  bridge.Refresh();if(bridge.GameState!=state.Campaign)throw new InvalidOperationException(Lang.T("Нативный редактор культуры относится к другой кампании."));
  if(!bridge.LivingCharacter(target) || bridge.CharacterCulture(target)!=culture)throw new InvalidOperationException(Lang.T("Выбранный живой глава должен принадлежать этой культуре."));
  if(bridge.CultureHead(culture)!=expected)throw new InvalidOperationException(Lang.T("Глава культуры изменился; обнови предпросмотр."));
  var existing=state.Entries.FirstOrDefault(e=>e.Culture==culture);var entry=new CultureHeadEntry{Culture=culture,Target=target,Previous=existing==null?expected:existing.Previous};
  state.Entries.RemoveAll(e=>e.Culture==culture);state.Entries.Add(entry);
  try{Save();}catch{state.Entries.Remove(entry);if(existing!=null)state.Entries.Add(existing);throw;}
  try{await bridge.ChangeCultureHead(culture,expected,target,state.Campaign);}catch{

   uint current;if(bridge.TryCultureHead(culture,out current) && current!=target){state.Entries.Remove(entry);if(existing!=null)state.Entries.Add(existing);Save();}throw;
  }
 }
 public async Task Remove(uint culture){var e=state.Entries.FirstOrDefault(x=>x.Culture==culture);if(e==null)return;bridge.Refresh();if(bridge.GameState==state.Campaign){uint current;if(bridge.TryCultureHead(culture,out current) && current==e.Target)await bridge.ChangeCultureHead(culture,current,bridge.LivingCharacter(e.Previous)?e.Previous:UInt32.MaxValue,state.Campaign);}state.Entries.Remove(e);Save();}
 public async Task Tick(){bridge.Refresh();if(bridge.GameState!=state.Campaign){state=NewState();Save();return;}foreach(var e in state.Entries.ToList()){if(!bridge.LivingCharacter(e.Target) || bridge.CharacterCulture(e.Target)!=e.Culture){await Remove(e.Culture);continue;}uint current=bridge.CultureHead(e.Culture);if(current!=e.Target)await bridge.ChangeCultureHead(e.Culture,current,e.Target,state.Campaign);}}
 public async Task Clear(){bridge.Refresh();if(bridge.GameState!=state.Campaign){state=NewState();Save();return;}var errors=new List<Exception>();foreach(var e in state.Entries.ToList())try{await Remove(e.Culture);}catch(Exception x){errors.Add(x);}if(errors.Count>0)throw new AggregateException(Lang.T("Не все назначения глав культур сняты; записи восстановления сохранены."),errors);}
}
