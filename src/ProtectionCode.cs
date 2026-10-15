using System;using System.Collections.Generic;
static class ProtectionCode {
static byte[] Hex(string s){var b=new byte[s.Length/2];for(int i=0;i<b.Length;i++)b[i]=Convert.ToByte(s.Substring(i*2,2),16);return b;}
static byte[] Patch(byte[] code,Dictionary<long,long> values){foreach(var pair in values){var a=BitConverter.GetBytes(pair.Key);var b=BitConverter.GetBytes(pair.Value);for(int i=0;i<=code.Length-8;i++){bool ok=true;for(int j=0;j<8;j++)if(code[i+j]!=a[j]){ok=false;break;}if(ok){Array.Copy(b,0,code,i,8);i+=7;}}}return code;}
public static byte[] Guard(long data,long slot,long back){return Patch(Hex("9c504152415349bb00000020005034124d8b530848b811111120005034124c3910753f817a1c7261684375368b42184d8b13458b1a4983c2084585db7424413b0274094983c20441ffcbebed49bb0000002000503412f041ff4310415b415a589dc3415b415a589d48895c24084c894c2420ff25000000002222222000503412"),new Dictionary<long,long>{{1311761352938749952L,data},{1311761352939868433L,slot},{1311761352940986914L,back}});}
public static byte[] CompareExchange(long target,long old,long next,long result){return Patch(Hex("48b9333333200050341248b8444444200050341248ba5555552000503412f0480fb11148b96666662000503412488901c3"),new Dictionary<long,long>{{1311761352942105395L,target},{1311761352943223876L,old},{1311761352944342357L,next},{1311761352945460838L,result}});}
}
