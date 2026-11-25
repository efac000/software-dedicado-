namespace Gerenciador;
static class PreviewImage {
    static readonly HttpClient Http=new(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(10)};
    public static bool Allowed(string url){
        if(!Uri.TryCreate(url,UriKind.Absolute,out var u)||u.Scheme!="https"||!u.IsDefaultPort||u.UserInfo.Length!=0)return false;
        return u.Host=="steamuserimages-a.akamaihd.net"||u.Host=="images.steamusercontent.com"||u.Host=="shared.fastly.steamstatic.com"||u.Host.EndsWith(".steamusercontent.com",StringComparison.OrdinalIgnoreCase)||u.Host.EndsWith(".steamstatic.com",StringComparison.OrdinalIgnoreCase);
    }
    public static async Task<Image?> Fetch(string url,CancellationToken cancellation){
        if(!Allowed(url))return null;
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var response=await Http.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
        if(!response.IsSuccessStatusCode||response.Content.Headers.ContentLength>4_000_000)return null;
        var type=response.Content.Headers.ContentType?.MediaType;
        if(type is not ("image/png" or "image/jpeg" or "image/gif" or "image/webp"))return null;
        using var stream=await response.Content.ReadAsStreamAsync(timeout.Token);
        using var data=new MemoryStream();var buf=new byte[8192];
        for(int n;(n=await stream.ReadAsync(buf.AsMemory(),timeout.Token))>0;){if(data.Length+n>4_000_000)return null;data.Write(buf,0,n);}
        data.Position=0;
        using var original=Image.FromStream(data,false,true);
        if(original.Width>4096||original.Height>4096)return null;
        return new Bitmap(original,new Size(Math.Max(1,(int)(original.Width*Math.Min(1.0,240.0/Math.Max(original.Width,original.Height)))),Math.Max(1,(int)(original.Height*Math.Min(1.0,240.0/Math.Max(original.Width,original.Height))))));
    }
}
