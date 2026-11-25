param([Parameter(Mandatory=$true)][string]$Exe,[Parameter(Mandatory=$true)][string]$SourceIcon,[Parameter(Mandatory=$true)][ValidateSet("x64","x86","arm64")][string]$Arch)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$exePath=(Resolve-Path -LiteralPath $Exe).Path
$iconPath=(Resolve-Path -LiteralPath $SourceIcon).Path
$stream=[IO.File]::OpenRead($exePath)
$reader=[IO.BinaryReader]::new($stream)
try{
    if($reader.ReadUInt16() -ne 0x5a4d){throw 'Cabecalho MZ invalido'}
    $stream.Position=0x3c;$offset=$reader.ReadInt32();$stream.Position=$offset
    if($reader.ReadUInt32() -ne 0x4550){throw 'Cabecalho PE invalido'}
    $machine=$reader.ReadUInt16()
    $expectedMachine=@{x64=0x8664;x86=0x14c;arm64=0xaa64}[$Arch]
    if($machine -ne $expectedMachine){throw "Arquitetura PE divergente: $machine"}
}finally{$reader.Dispose();$stream.Dispose()}
Add-Type -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
public static class GddIconResources {
    [DllImport("kernel32",CharSet=CharSet.Unicode,SetLastError=true)]
    static extern IntPtr LoadLibraryEx(string file,IntPtr reserved,uint flags);
    [DllImport("kernel32")] static extern bool FreeLibrary(IntPtr module);
    delegate bool EnumProc(IntPtr module,IntPtr type,IntPtr name,IntPtr data);
    [DllImport("kernel32",EntryPoint="EnumResourceNamesW",SetLastError=true)]
    static extern bool EnumResourceNames(IntPtr module,IntPtr type,EnumProc callback,IntPtr data);
    [DllImport("kernel32",EntryPoint="FindResourceW")] static extern IntPtr FindResource(IntPtr module,IntPtr name,IntPtr type);
    [DllImport("kernel32")] static extern uint SizeofResource(IntPtr module,IntPtr resource);
    [DllImport("kernel32")] static extern IntPtr LoadResource(IntPtr module,IntPtr resource);
    [DllImport("kernel32")] static extern IntPtr LockResource(IntPtr resource);
    static byte[] Read(IntPtr module,IntPtr type,IntPtr name){
        var r=FindResource(module,name,type);
        if(r==IntPtr.Zero)return null;
        var size=SizeofResource(module,r);
        if(size==0||size>16777216)return null;
        var loaded=LoadResource(module,r);
        if(loaded==IntPtr.Zero)return null;
        var address=LockResource(loaded);
        if(address==IntPtr.Zero)return null;
        var bytes=new byte[(int)size];Marshal.Copy(address,bytes,0,bytes.Length);return bytes;
    }
    static string Hash(byte[] data){
        using(var sha=System.Security.Cryptography.SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(data));
    }
    public static bool Verify(string exe,byte[] ico){
        if(ico.Length<6||BitConverter.ToUInt16(ico,0)!=0||BitConverter.ToUInt16(ico,2)!=1)return false;
        int count=BitConverter.ToUInt16(ico,4);
        if(count<1||count>256||ico.Length<6+count*16)return false;
        var expected=new List<string>();
        for(int i=0;i<count;i++){
            int e=6+i*16,size=BitConverter.ToInt32(ico,e+8),offset=BitConverter.ToInt32(ico,e+12);
            if(size<=0||offset<0||offset>ico.Length-size)return false;
            var bytes=new byte[size];Array.Copy(ico,offset,bytes,0,size);expected.Add(Hash(bytes));
        }
        expected.Sort(StringComparer.Ordinal);
        // LOAD_LIBRARY_AS_DATAFILE: read resources only, do not execute the binary.
        var module=LoadLibraryEx(exe,IntPtr.Zero,2);
        if(module==IntPtr.Zero)throw new InvalidOperationException("Cannot open executable resource table");
        try{
            bool match=false;
            EnumProc callback=(m,t,n,d)=>{
                var group=Read(m,new IntPtr(14),n);
                if(group==null||group.Length<6||BitConverter.ToUInt16(group,4)!=count||group.Length<6+count*14)return true;
                var hashes=new List<string>();
                for(int i=0;i<count;i++){
                    int e=6+i*14;
                    var bytes=Read(m,new IntPtr(3),new IntPtr(BitConverter.ToUInt16(group,e+12)));
                    if(bytes==null||bytes.Length!=BitConverter.ToInt32(group,e+8))return true;
                    hashes.Add(Hash(bytes));
                }
                hashes.Sort(StringComparer.Ordinal);
                if(hashes.SequenceEqual(expected)){match=true;return false;}
                return true;
            };
            EnumResourceNames(module,new IntPtr(14),callback,IntPtr.Zero);
            GC.KeepAlive(callback);
            return match;
        }finally{FreeLibrary(module);}
    }
}
"@
if(![GddIconResources]::Verify($exePath,[IO.File]::ReadAllBytes($iconPath))){throw 'Recursos ICO incorporados diferem do parafuso original'}
Write-Output "Icone parafuso (recursos ICO exatos) e arquitetura $Arch confirmados"
