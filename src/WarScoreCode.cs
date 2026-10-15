using System;using System.Collections.Generic;
static class WarScoreCode {
static byte[] Hex(string s){var b=new byte[s.Length/2];for(int i=0;i<b.Length;i++)b[i]=Convert.ToByte(s.Substring(i*2,2),16);return b;}
public static byte[] Guard(long data,long slot,long original){var b=Hex("5341544883ec28448b610848b82222223000503412ffd09c49ba11111130005034124d8b1249bb00000030005034124d3b530875214d8b13418b1a4983c20885db7413453b227506418b4204eb084983c208ffcbebe99d4883c428415c5bc3");foreach(var p in new Dictionary<long,long>{{1311761353207185408L,data},{1311761353208303889L,slot},{1311761353209422370L,original}}){var old=BitConverter.GetBytes(p.Key);var next=BitConverter.GetBytes(p.Value);for(int i=0;i<=b.Length-8;i++){bool match=true;for(int j=0;j<8;j++)if(b[i+j]!=old[j]){match=false;break;}if(match){Array.Copy(next,0,b,i,8);i+=7;}}}return b;}
}
