using System;using System.IO;using System.Linq;using System.Text;using System.Diagnostics;using System.Collections.Generic;using System.Security.Cryptography;using System.Runtime.InteropServices;using System.Threading;using System.Threading.Tasks;

sealed class NativeBridge : IDisposable {
 const string Hash="98702f88a547cde2eaf29a85f93b85f68ee4cf8148336a4f7afaeb75319dd518";
 const long ConsoleSlot=0x5c6a590,SelectedSlot=0x54dbc00,GameSlot=0x5c68c50,ManagerSlot=0x5c67568,ExecuteRva=0x3885990;
 const uint Message=0xbc31;IntPtr handle;long page,block,oldproc,console,cookie;IntPtr hwnd;bool installed,pending,disposed,remoteUncertain;
 readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);readonly Dictionary<string,ProcessModule> modules=new Dictionary<string,ProcessModule>(StringComparer.OrdinalIgnoreCase);
 public readonly int Pid;public readonly long Base;public readonly string GamePath,LogPath;public long GameState,PlayerPointer;public uint PlayerID;public bool Alive {get{try{return !Process.GetProcessById(Pid).HasExited;}catch{return false;}}}
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
 [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr h);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool ReadProcessMemory(IntPtr h,IntPtr p,byte[] b,UIntPtr n,out UIntPtr got);
 [DllImport("kernel32.dll",SetLastError=true)]static extern bool WriteProcessMemory(IntPtr h,IntPtr p,byte[] b,UIntPtr n,out UIntPtr got);
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr VirtualAllocEx(IntPtr h,IntPtr p,UIntPtr n,uint type,uint protect);
 [DllImport("kernel32.dll")]static extern bool VirtualFreeEx(IntPtr h,IntPtr p,UIntPtr n,uint type);
 [DllImport("kernel32.dll")]static extern bool FlushInstructionCache(IntPtr h,IntPtr p,UIntPtr n);
 [DllImport("kernel32.dll",SetLastError=true)]static extern IntPtr CreateRemoteThread(IntPtr h,IntPtr attrs,UIntPtr stack,IntPtr start,IntPtr arg,uint flags,IntPtr tid);
 [DllImport("kernel32.dll")]static extern uint WaitForSingleObject(IntPtr h,uint ms);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr GetModuleHandle(string name);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern IntPtr LoadLibrary(string name);
 [DllImport("kernel32.dll",CharSet=CharSet.Ansi)]static extern IntPtr GetProcAddress(IntPtr h,string name);
 delegate bool EnumProc(IntPtr w,IntPtr arg);
 [DllImport("user32.dll")]static extern bool EnumWindows(EnumProc fn,IntPtr arg);
 [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr w,out uint pid);
 [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr w);
 [DllImport("user32.dll")]static extern bool IsWindowUnicode(IntPtr w);
 [DllImport("user32.dll")]static extern IntPtr GetWindow(IntPtr w,uint command);
 [DllImport("user32.dll",SetLastError=true)]static extern IntPtr SendMessageTimeout(IntPtr w,uint msg,UIntPtr wp,IntPtr lp,uint flags,uint ms,out UIntPtr result);
 static Exception Error(string task){return new InvalidOperationException(task+" (Win32 "+Marshal.GetLastWin32Error()+")");}
 public NativeBridge(Process process,string logs){
  Pid=process.Id;foreach(ProcessModule m in process.Modules)modules[m.ModuleName]=m;
  ProcessModule exe=modules["ck3.exe"];Base=exe.BaseAddress.ToInt64();GamePath=Directory.GetParent(Path.GetDirectoryName(exe.FileName)).FullName;LogPath=logs;
  using(var h=SHA256.Create())using(var f=File.OpenRead(exe.FileName))if(BitConverter.ToString(h.ComputeHash(f)).Replace("-","").ToLowerInvariant()!=Hash)throw new InvalidOperationException(Lang.T("Поддерживается CK3 1.20.0.4 Crozier. У этой версии игры другая сигнатура."));
  handle=OpenProcess(0x043a,false,Pid);if(handle==IntPtr.Zero)throw Error(Lang.T("Нет доступа к процессу CK3"));
  try{
   if(!Read(Base+ExecuteRva,10).SequenceEqual(new byte[]{0x48,0x89,0x5c,0x24,0x08,0x57,0x48,0x83,0xec,0x60}))throw new InvalidOperationException(Lang.T("Сигнатура игровой функции не совпала."));
   console=Q(Base+ConsoleSlot);if(console==0)throw new InvalidOperationException(Lang.T("Движок ещё загружается."));
   EnumWindows(delegate(IntPtr w,IntPtr arg){uint pid;GetWindowThreadProcessId(w,out pid);if(pid==(uint)Pid && IsWindowVisible(w) && GetWindow(w,4)==IntPtr.Zero){hwnd=w;return false;}return true;},IntPtr.Zero);
   if(hwnd==IntPtr.Zero)throw new InvalidOperationException(Lang.T("Окно CK3 ещё не готово."));
   page=VirtualAllocEx(handle,IntPtr.Zero,(UIntPtr)65536,0x3000,0x40).ToInt64();if(page==0)throw Error(Lang.T("Не удалось создать мост"));block=page+0x2000;cookie=DateTime.UtcNow.Ticks&0x7fffffffffffL;
   var v=Values();Remote(MachineCode.Getter(v));oldproc=Q(block+0x30);if(oldproc==0)throw Error(Lang.T("Не найдена процедура окна"));
   v=Values();Write(page,MachineCode.Proc(v));FlushInstructionCache(handle,(IntPtr)page,(UIntPtr)4096);Remote(MachineCode.Setter(v));
   installed=true;long previous=Q(block+0x30);if(previous==0)throw Error(Lang.T("Не удалось установить канал окна"));if(previous!=oldproc){var restore=Values();restore["newproc"]=previous;Remote(MachineCode.Setter(restore));installed=false;UIntPtr barrier;SendMessageTimeout(hwnd,0,UIntPtr.Zero,IntPtr.Zero,3,3000,out barrier);throw new InvalidOperationException(Lang.T("Оконный канал занят другим инструментом. Подключение отменено."));}Refresh();
  }catch{Dispose();throw;}
 }
 Dictionary<string,long> Values(){return new Dictionary<string,long>{{"block",block},{"cookie",cookie},{"console",console},{"selected",Base+SelectedSlot},{"execute",Base+ExecuteRva},{"oldproc",oldproc},{"forwardproc",Address("user32.dll",IsWindowUnicode(hwnd)?"CallWindowProcW":"CallWindowProcA")},{"setproc",Address("user32.dll","SetWindowLongPtrW")},{"getproc",Address("user32.dll","GetWindowLongPtrW")},{"hwnd",hwnd.ToInt64()},{"page",page},{"newproc",page},{"result",block+0x30}};}
 long Address(string dll,string symbol){IntPtr local=GetModuleHandle(dll);if(local==IntPtr.Zero)local=LoadLibrary(dll);IntPtr p=GetProcAddress(local,symbol);if(p==IntPtr.Zero || !modules.ContainsKey(dll))throw Error(Lang.T("Не найдена системная функция ")+symbol);return modules[dll].BaseAddress.ToInt64()+p.ToInt64()-local.ToInt64();}
 byte[] Read(long address,int n){if(address<65536 || n<0 || n>5000000)throw new InvalidDataException(Lang.T("Неверный адрес чтения."));byte[] b=new byte[n];UIntPtr got;if(!ReadProcessMemory(handle,(IntPtr)address,b,(UIntPtr)n,out got) || got.ToUInt64()!=(ulong)n)throw Error(Lang.T("Игра изменила состояние"));return b;}
 void Write(long address,byte[] b){UIntPtr done;if(!WriteProcessMemory(handle,(IntPtr)address,b,(UIntPtr)b.Length,out done) || done.ToUInt64()!=(ulong)b.Length)throw Error(Lang.T("Мост потерял связь"));}
 long Q(long a){return BitConverter.ToInt64(Read(a,8),0);}uint D(long a){return BitConverter.ToUInt32(Read(a,4),0);}
 void Remote(byte[] code){Write(page+0x1000,code);FlushInstructionCache(handle,(IntPtr)(page+0x1000),(UIntPtr)code.Length);IntPtr t=CreateRemoteThread(handle,IntPtr.Zero,UIntPtr.Zero,(IntPtr)(page+0x1000),IntPtr.Zero,0,IntPtr.Zero);if(t==IntPtr.Zero)throw Error(Lang.T("Не удалось подключить оконный канал"));try{if(WaitForSingleObject(t,5000)!=0){remoteUncertain=true;throw new TimeoutException(Lang.T("Оконный канал не ответил."));}}finally{CloseHandle(t);}}
 public void Refresh(){
  if(!Alive)throw new InvalidOperationException(Lang.T("CK3 закрыта."));long gs=Q(Base+GameSlot);if(gs==0)throw new InvalidOperationException(Lang.T("Загрузи одиночную кампанию."));

  uint id=D(Base+SelectedSlot);
  if(id==0 || !LivingCharacter(id))throw new InvalidOperationException(Lang.T("Управляемый персонаж ещё не выбран (режим наблюдателя или окно выбора персонажа)."));
  long m=Q(Base+ManagerSlot),character=Q(Q(m+0x20)+(long)(id&0xffffff)*16+8);GameState=gs;PlayerID=id;PlayerPointer=character;
 }
 public bool ValidCharacter(uint id){try{long m=Q(Base+ManagerSlot);if(m==0)return false;uint index=id&0xffffff;if(index>=D(m+0x2c))return false;long arr=Q(m+0x20),c=Q(arr+(long)index*16+8);return c!=0 && D(c+0x18)==id && D(c+0x1c)==0x43686172;}catch{return false;}}
 public bool LivingCharacter(uint id){try{if(id==0 || !ValidCharacter(id))return false;long m=Q(Base+ManagerSlot),arr=Q(m+0x20),c=Q(arr+(long)(id&0xffffff)*16+8);return Q(c+0x1d0)==0;}catch{return false;}}
 internal byte[] ReadNative(long address,int length){return Read(address,length);}
 internal long RegimentAddress(uint id){long m=Q(Base+0x5d1eb68);if(m==0 || (id&0xffffff)>=D(m+0x2c))throw new InvalidOperationException(Lang.T("Полк исчез."));long p=Q(Q(m+0x20)+(long)(id&0xffffff)*16+8);if(p<65536 || D(p+0x10)!=id || D(p+0x14)!=0x52656769)throw new InvalidOperationException(Lang.T("Идентичность полка не совпала."));return p;}
 internal OccupationRecord Occupation(uint id){long p=ProvinceAddress(id);if(D(p+0x10)!=id || D(p+0x85c)!=0x50726f76)throw new InvalidOperationException(Lang.T("Идентичность провинции не совпала."));return new OccupationRecord{Province=id,Address=p,Occupant=D(p+0x73c),Title=D(p+0x738),Campaign=GameState};}
 internal async Task<int> ClearOccupation(OccupationRecord record,bool dryRun){
  await gate.WaitAsync().ConfigureAwait(false);try{
   Refresh();if(Occupation(record.Province).Fingerprint!=record.Fingerprint)throw new InvalidOperationException(Lang.T("Оккупация или владение изменились; обнови предпросмотр."));
   if(pending)throw new InvalidOperationException(Lang.T("Предыдущая команда ещё не завершена. Повтора нет."));
   var code=OccupationCode.Clear(Base+GameSlot,record.Campaign,record.Address,record.Province,record.Occupant,record.Title,block+0x300,Base+0x247d120,dryRun);
   Write(page+0x1800,code);FlushInstructionCache(handle,(IntPtr)(page+0x1800),(UIntPtr)code.Length);Write(block+0x300,new byte[8]);Write(block+0x80,BitConverter.GetBytes(page+0x1800));
   try{Dispatch(3);}catch{if(D(block)==2)remoteUncertain=true;throw;}
   int result=(int)Q(block+0x300);if(result==0)throw new InvalidOperationException(Lang.T("Условия снятия оккупации изменились. Повтора нет."));return result;
  }finally{gate.Release();}
 }
 internal StationRecord Station(uint id,uint destination){Refresh();long regiment=RegimentAddress(id),origin=Q(regiment+0x120);if(origin<65536)throw new InvalidOperationException(Lang.T("Неизвестное размещение полка."));uint current=D(origin+0x85c)==0x50726f76?D(origin+0x10):0;long target=destination==0?0:ProvinceAddress(destination);if(target!=0 && (D(target+0x85c)!=0x50726f76 || D(target+0x10)!=destination))throw new InvalidOperationException(Lang.T("Целевая провинция не совпала."));uint occupied=target==0?UInt32.MaxValue:D(target+0x820);if(occupied!=UInt32.MaxValue && occupied!=id)throw new InvalidOperationException(Lang.T("В этом поселении уже размещён другой полк: ")+occupied+Lang.T(". Сначала сними его размещение."));return new StationRecord{ID=id,Address=regiment,Origin=origin,Current=current,Destination=destination,Target=target,Campaign=GameState};}
 internal async Task<int> ChangeStation(StationRecord record,bool dryRun){
  await gate.WaitAsync().ConfigureAwait(false);
  try{
   Refresh();var current=Station(record.ID,record.Destination);if(current.Fingerprint!=record.Fingerprint)throw new InvalidOperationException(Lang.T("Полк или размещение изменились; обнови предпросмотр."));
   if(pending)throw new InvalidOperationException(Lang.T("Предыдущая команда ещё не завершена. Повтора нет."));
   byte[] code=NativeCommandCode.Station(Base+GameSlot,record.Campaign,Base+0x5d1eb68,record.Address,record.ID,record.Origin,record.Target,record.Destination,block+0x300,Base,dryRun);
   if(code.Length>0x800)throw new InvalidOperationException(Lang.T("Слишком большая нативная команда."));
   Write(page+0x1800,code);FlushInstructionCache(handle,(IntPtr)(page+0x1800),(UIntPtr)code.Length);Write(block+0x300,new byte[8]);Write(block+0x80,BitConverter.GetBytes(page+0x1800));
   try{Dispatch(3);}catch{if(D(block)==2)remoteUncertain=true;throw;}
   int result=(int)Q(block+0x300);if(result==0)throw new InvalidOperationException(Lang.T("CK3 отклонила размещение: проверь владение поселением и распущен ли полк. Повтора нет."));
   if(dryRun || result==2)return result;
   DateTime end=DateTime.UtcNow.AddSeconds(16);
   while(DateTime.UtcNow<end){Refresh();if(GameState!=record.Campaign)throw new InvalidOperationException(Lang.T("Кампания изменилась после отправки команды."));long r=RegimentAddress(record.ID),origin=Q(r+0x120);uint actual=D(origin+0x85c)==0x50726f76?D(origin+0x10):0;if(actual==record.Destination && (record.Destination==0 || D(record.Target+0x820)==record.ID))return result;await Task.Delay(100).ConfigureAwait(false);}
   throw new TimeoutException(Lang.T("Команда размещения передана в очередь CK3, результат ещё не подтверждён. Повтора нет; проверь полк в игре."));
  }finally{gate.Release();}
 }
 internal long ProvinceAddress(uint id){Refresh();long context=Q(GameState+0xa0);if(id==0 || id>=D(context+0x14c))throw new InvalidOperationException(Lang.T("Провинции нет в текущей кампании."));long p=Q(Q(context+0x140)+(long)id*8);if(p<65536)throw new InvalidOperationException(Lang.T("Провинция ещё не готова."));return p;}
 internal long WarAddress(uint id){long manager=Q(Base+0x5d1de58);if(manager==0 || (id&0xffffff)>=D(manager+0x2c))throw new InvalidOperationException(Lang.T("Война завершилась или исчезла."));long p=Q(Q(manager+0x20)+(long)(id&0xffffff)*16+8);if(p<65536 || D(p+8)!=id)throw new InvalidOperationException(Lang.T("Внутренний ID войны не совпал."));return p;}
 internal long SiegeAddress(uint id){long m=Q(Base+0x5d1ec88);if(m==0 || (id&0xffffff)>=D(m+0x2c))throw new InvalidOperationException(Lang.T("Осада завершилась или исчезла."));long p=Q(Q(m+0x20)+(long)(id&0xffffff)*16+8);if(p<65536 || D(p+8)!=id)throw new InvalidOperationException(Lang.T("Внутренний ID осады не совпал."));long province=Q(p+0x200);if(province<65536 || D(province+0x85c)!=0x50726f76 || D(province+0x788)!=id)throw new InvalidOperationException(Lang.T("Осада уже не связана с провинцией."));return p;}
 internal uint ProvinceSiege(uint province){uint id=D(ProvinceAddress(province)+0x788);SiegeAddress(id);return id;}
 internal long CharacterAddress(uint id){if(!ValidCharacter(id))throw new InvalidOperationException(Lang.T("Персонаж исчез."));long m=Q(Base+ManagerSlot);return Q(Q(m+0x20)+(long)(id&0xffffff)*16+8);}
 internal uint CharacterCulture(uint id){return D(CharacterAddress(id)+0xb0);}
 internal long CultureAddress(uint id){long m=Q(Base+0x5d1e2f0);if(m==0 || (id&0xffffff)>=D(m+0x2c))throw new InvalidOperationException(Lang.T("Культура исчезла."));long p=Q(Q(m+0x20)+(long)(id&0xffffff)*16+8);if(p<65536 || D(p+0x10)!=id || D(p+0x14)!=0x43756c74)throw new InvalidOperationException(Lang.T("Идентичность культуры не совпала."));return p;}
 internal bool TryCultureHead(uint id,out uint head){head=UInt32.MaxValue;long m=Q(Base+0x5d1e2f0);if(m==0 || (id&0xffffff)>=D(m+0x2c))return false;long p=Q(Q(m+0x20)+(long)(id&0xffffff)*16+8);if(p<65536 || D(p+0x10)!=id || D(p+0x14)!=0x43756c74)return false;head=D(p+0x6b8);return true;}
 internal uint CultureHead(uint id){return D(CultureAddress(id)+0x6b8);}
 internal async Task ChangeCultureHead(uint id,uint before,uint after,long campaign){await gate.WaitAsync().ConfigureAwait(false);try{Refresh();if(GameState!=campaign)throw new InvalidOperationException(Lang.T("Кампания изменилась."));if(after!=UInt32.MaxValue && !LivingCharacter(after))throw new InvalidOperationException(Lang.T("Главой культуры может стать живой персонаж."));long c=CultureAddress(id),word=Q(c+0x6b8);if((uint)word!=before)throw new InvalidOperationException(Lang.T("Глава культуры изменился; обнови предпросмотр."));long next=(word&unchecked((long)0xffffffff00000000UL))|after;Remote(CultureFieldCode.Change(Base+GameSlot,campaign,c,id,word,next,block+0x300));if(Q(block+0x300)!=1 || CultureHead(id)!=after)throw new InvalidOperationException(Lang.T("Условия нативной записи изменились. Повтора нет."));}finally{gate.Release();}}
 internal ConstructionRecord Construction(uint id){long p=ProvinceAddress(id);if(D(p+0x10)!=id)throw new InvalidOperationException(Lang.T("Идентичность провинции не совпала."));long building=Q(p+0x688);if(building==0)return null;long length=Q(building+0x28),capacity=Q(building+0x30);if(length<1 || length>128 || capacity<length || capacity>10000)throw new InvalidOperationException(Lang.T("Неизвестная структура очереди строительства."));long data=capacity<16?building+0x18:Q(building+0x18);string key=Encoding.UTF8.GetString(Read(data,(int)length));if(!System.Text.RegularExpressions.Regex.IsMatch(key,@"^[a-zA-Z0-9_]+$"))throw new InvalidOperationException(Lang.T("Неизвестный тип строящегося здания."));var r=new ConstructionRecord{Province=id,Key=key,Address=p,Building=building,Start=D(p+0x694),Work=Q(p+0x6a0)};if(Q(p+0x688)!=building || Q(p+0x6a0)!=r.Work)throw new InvalidOperationException(Lang.T("Очередь поменялась во время чтения."));return r;}
 void Dispatch(int op){Write(block+8,BitConverter.GetBytes(op));Write(block,BitConverter.GetBytes(1));UIntPtr result;IntPtr ok=SendMessageTimeout(hwnd,Message,(UIntPtr)(ulong)cookie,(IntPtr)block,3,3000,out result);uint state=D(block);if(ok==IntPtr.Zero || state!=3)throw new InvalidOperationException(state==4?Lang.T("Консоль движка занята. Команда не отправлена."):Lang.T("Игра не подтвердила доставку команды."));}
 public Task<EngineReply> Execute(string body,uint root,string marker){return ExecuteText(body,root,marker,false);}
 public Task<EngineReply> ExecuteConsole(string command,uint root){if(!System.Text.RegularExpressions.Regex.IsMatch(command,@"^pregnancy [0-9]+ [0-9]+ yes$"))throw new InvalidOperationException(Lang.T("Команда не разрешена."));return ExecuteText(command,root,Guid.NewGuid().ToString("N"),true);}
 public const string TargetMarker="ck3t_target_marker";public bool MarkerReady;

 async Task<EngineReply> ExecuteText(string body,uint root,string marker,bool native){
  await gate.WaitAsync().ConfigureAwait(false);long oldState=GameState,oldPointer=PlayerPointer;uint oldPlayer=PlayerID;
  try{
   Refresh();if(GameState!=oldState || PlayerID!=oldPlayer || PlayerPointer!=oldPointer)throw new InvalidOperationException(Lang.T("Правитель или кампания изменились. Команда отменена."));if(!ValidCharacter(root))throw new InvalidOperationException(Lang.T("Персонажа с этим внутренним ID нет в текущей кампании."));
   if(native || root==PlayerID)return await Core(body,marker,native).ConfigureAwait(false);
   if(!MarkerReady)throw new InvalidOperationException(Lang.T("Действия над другими персонажами требуют пакета 3. Установи его (install-pack.ps1), сохрани кампанию и вручную перезапусти CK3."));
   if(!LivingCharacter(root))throw new InvalidOperationException(Lang.T("Действие доступно только для живого персонажа."));
   var tag=await Core("add_trait "+TargetMarker+" "+root,Guid.NewGuid().ToString("N"),true).ConfigureAwait(false);
   if(tag.Error)throw new InvalidOperationException(Lang.T("Не удалось отметить цель. Команда не выполнена."));
   string scoped=body.Replace("SCOPE.GetRootScope.GetCharacter","GetGlobalVariable('ck3t_target_scope').GetCharacter");
   string wrapped="set_global_variable = { name = ck3t_marked value = 0 } every_living_character = { limit = { has_trait = "+TargetMarker+" } change_global_variable = { name = ck3t_marked add = 1 } } "+
    "if = { limit = { global_var:ck3t_marked = 1 } every_living_character = { limit = { has_trait = "+TargetMarker+" } remove_trait = "+TargetMarker+" set_global_variable = { name = ck3t_target_scope value = this } "+scoped+" remove_global_variable = ck3t_target_scope } } "+
    "else = { every_living_character = { limit = { has_trait = "+TargetMarker+" } remove_trait = "+TargetMarker+" } debug_log = \"CK3T_TARGET_FAIL\" } remove_global_variable = ck3t_marked";
   var reply=await Core(wrapped,marker,false).ConfigureAwait(false);
   if(reply.Rows("CK3T_TARGET_FAIL").Any())throw new InvalidOperationException(Lang.T("Цель не определилась однозначно. Команда не выполнена."));
   return reply;
  }finally{gate.Release();}
 }
 async Task<EngineReply> Core(string body,string marker,bool native){
  try{
   if(pending)throw new InvalidOperationException(Lang.T("Предыдущая команда ещё не завершена. Автоматического повтора нет."));
   string command=native?body:"effect "+body+" debug_log = \"CK3T_ACK:"+marker+"\"";byte[] text=Encoding.UTF8.GetBytes(command);if(text.Length>49000)throw new InvalidOperationException(Lang.T("Слишком большой пакет действий."));
   long logStart=File.Exists(LogPath)?new FileInfo(LogPath).Length:0;string errorLog=Path.Combine(Path.GetDirectoryName(LogPath),"error.log");long errorStart=File.Exists(errorLog)?new FileInfo(errorLog).Length:0;
   Write(block+0x400,text.Concat(new byte[]{0}).ToArray());byte[] str=new byte[32];Array.Copy(BitConverter.GetBytes(block+0x400),0,str,0,8);Array.Copy(BitConverter.GetBytes((long)text.Length),0,str,16,8);Array.Copy(BitConverter.GetBytes(Math.Max(16L,text.Length)),0,str,24,8);Write(block+0x100,str);Write(block+4,BitConverter.GetBytes(PlayerID));
   pending=true;try{Dispatch(0);}catch{if(D(block)==4)pending=false;throw;}DateTime end=DateTime.UtcNow.AddSeconds(16);
   while(DateTime.UtcNow<end){
    if(!Alive)throw new InvalidOperationException(Lang.T("CK3 закрыта до ответа."));
    string tail=ReadTail(logStart);
    int running=tail.LastIndexOf("Running console command: "+command,StringComparison.Ordinal);string own=running<0?"":tail.Substring(running);bool ack=native?(own.Contains("console_success:") || own.Contains("console_failure:")):tail.Split('\n').Any(line=>line.Contains("jomini_effect_impl.cpp") && line.Contains("CK3T_ACK:"+marker));
    if(ack){Dispatch(1);pending=false;string errors=ReadFileTail(errorLog,errorStart);bool failed=own.Contains("console_failure:") || EngineReply.CommandErrors(errors).Count>0;return new EngineReply(tail+"\n"+errors,failed);}
    await Task.Delay(100).ConfigureAwait(false);
   }
   throw new TimeoutException(Lang.T("Команда доставлена, но итог не подтверждён журналом движка. Повтора нет; проверь состояние игры."));
  }finally{if(pending && Alive){try{if(D(console+0x14)==0){Thread.Sleep(100);Dispatch(1);pending=false;}}catch{}}}
 }
 string ReadTail(long start){return ReadFileTail(LogPath,start);}
 static string ReadFileTail(string path,long start){try{using(var f=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){if(f.Length<start)start=0;f.Position=start;using(var r=new StreamReader(f,Encoding.UTF8))return r.ReadToEnd();}}catch{return "";}}
 public void Dispose(){
  if(disposed)return;disposed=true;
  try{
   if(handle!=IntPtr.Zero && page!=0 && Alive && !remoteUncertain){
    if(installed){if(pending){DateTime end=DateTime.UtcNow.AddSeconds(8);while(Alive && D(console+0x14)!=0 && DateTime.UtcNow<end)Thread.Sleep(100);Thread.Sleep(100);Dispatch(1);pending=false;}Dispatch(2);installed=false;UIntPtr result;if(SendMessageTimeout(hwnd,0,UIntPtr.Zero,IntPtr.Zero,3,3000,out result)!=IntPtr.Zero)VirtualFreeEx(handle,(IntPtr)page,UIntPtr.Zero,0x8000);}
    else VirtualFreeEx(handle,(IntPtr)page,UIntPtr.Zero,0x8000);
   }
  }catch{}finally{if(handle!=IntPtr.Zero)CloseHandle(handle);handle=IntPtr.Zero;}
 }
}
sealed class EngineReply {
 public readonly string Text;public readonly bool Error;
 public EngineReply(string text,bool error){Text=text;Error=error;}
 public static List<string> CommandErrors(string text){var result=new List<string>();string block="";foreach(string line in (text??"").Split('\n')){if(line.StartsWith("[")){if(block.Contains("effect console command") || block.Contains("Data error in loc string 'CK3T"))result.Add(block);block=line.Contains("[E]")?line:"";}else if(block!="")block+="\n"+line;}if(block.Contains("effect console command") || block.Contains("Data error in loc string 'CK3T"))result.Add(block);return result;}
 public IEnumerable<string> Rows(string key){return Text.Split('\n').Where(x=>x.Contains("jomini_effect_impl.cpp") && x.Contains(key)).Select(x=>x.Substring(x.IndexOf(key)+key.Length).Trim());}
}
sealed class ConstructionRecord {
 public uint Province,Start;public long Address,Building,Work;public string Key;
 public string Fingerprint {get{return Province+":"+Address+":"+Building+":"+Start+":"+Work+":"+Key;}}
}

sealed class StationRecord {
 public uint ID,Current,Destination;public long Address,Origin,Target,Campaign;
 public string Fingerprint {get{return ID+":"+Address+":"+Origin+":"+Current+":"+Destination+":"+Target+":"+Campaign;}}
}

sealed class OccupationRecord {
 public uint Province,Occupant,Title;public long Address,Campaign;
 public string Fingerprint {get{return Province+":"+Address+":"+Occupant+":"+Title+":"+Campaign;}}
}
