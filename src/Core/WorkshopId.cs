using System.Text.RegularExpressions;
namespace Dedicado;
public static class WorkshopId {
    public static bool TryParse(string input,out string id){
        id="";
        var value=input.Trim();
        if(value.Length>2048)return false;
        if(Regex.IsMatch(value,@"\A[1-9][0-9]{0,19}\z")&&ulong.TryParse(value,out _)){id=value;return true;}
        if(!Uri.TryCreate(value,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.Host!="steamcommunity.com"||!uri.IsDefaultPort||uri.UserInfo.Length!=0||uri.AbsolutePath.TrimEnd('/')!="/sharedfiles/filedetails")return false;
        var keys=uri.Query.TrimStart('?').Split('&').Where(s=>s.StartsWith("id=",StringComparison.Ordinal)).ToArray();
        if(keys.Length!=1)return false;
        value=keys[0][3..];
        if(!Regex.IsMatch(value,@"\A[1-9][0-9]{0,19}\z")||!ulong.TryParse(value,out _))return false;
        id=value;return true;
    }
}
