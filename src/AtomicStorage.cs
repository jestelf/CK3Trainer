using System;
using System.IO;
using System.Text;

static class AtomicStorage {
 public static void Write(string path,string text){
  Directory.CreateDirectory(Path.GetDirectoryName(path));
  string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
  try {
   using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
    byte[] bytes=new UTF8Encoding(false).GetBytes(text);stream.Write(bytes,0,bytes.Length);stream.Flush(true);
   }
   if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);
  }finally{if(File.Exists(temporary))File.Delete(temporary);}
 }
}
