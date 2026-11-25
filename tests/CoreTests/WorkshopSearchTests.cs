using System.Net;
using System.Text.Json;
using Dedicado;
static class WorkshopSearchTests{
    sealed class Handler:HttpMessageHandler{
        public string? Cursor;public string? Search;public bool Header;public bool KeyInUrl;
        public bool Fail;public bool Empty;public bool Error;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){
            Header=request.Headers.Contains("x-webapi-key");KeyInUrl=request.RequestUri!.Query.Contains(new string('A',32),StringComparison.Ordinal);
            using var json=JsonDocument.Parse(Uri.UnescapeDataString(request.RequestUri.Query["?input_json=".Length..]));
            Cursor=json.RootElement.GetProperty("cursor").GetString();Search=json.RootElement.GetProperty("search_text").GetString();
            if(Error)throw new HttpRequestException("simulated network");
            if(Fail)return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(Empty?"""{"response":{"total":0}}""":"""{"response":{"total":2,"next_cursor":"next","publishedfiledetails":[{"publishedfileid":"42","title":"Example","consumer_appid":322330,"result":1},{"publishedfileid":"99","title":"Other app","consumer_appid":1,"result":1}]}}""")});
        }
    }
    public static void Run(){
        var handler=new Handler();using var client=new HttpClient(handler);var key=new string('A',32);
        WorkshopPage Call(string q,string? cursor=null)=>WorkshopSearch.SearchCore(client,key,q,cursor).GetAwaiter().GetResult();
        void Check(bool valid,string message){if(!valid)throw new Exception(message);}
        var page=Call("A & B");
        Check(page.Items.Length==1&&page.Items[0].Id=="42"&&page.NextCursor=="next"&&handler.Header&&!handler.KeyInUrl&&handler.Cursor=="*"&&handler.Search=="A & B","Consulta não filtrada");
        Check(Call("A & B","next").NextCursor==null&&handler.Cursor=="next","Cursor incorreto");
        using var published=JsonDocument.Parse("""{"response":{"total":2,"publishedfiledetails":[{"publishedfileid":"42","title":"Mod","consumer_appid":322330,"file_type":0,"num_children":1,"children":[{"publishedfileid":"43"}]},{"publishedfileid":"44","title":"Colecao","consumer_appid":322330,"file_type":2,"num_children":1,"children":[{"publishedfileid":"43"}]}]}}""");
        var parsed=WorkshopSearch.Parse(published.RootElement,"*");
        Check(parsed.Items[0].PublishedDependencies!.SequenceEqual(new[]{"43"})&&parsed.Items[1].PublishedDependencies==null,"Dependência publicada sem evidência");
        handler.Empty=true;Check(Call("empty").Items.Length==0,"Pesquisa vazia");
        handler.Empty=false;handler.Fail=true;
        try{Call("query");throw new Exception("Autenticação inválida aceita");}catch(IOException){}
        handler.Fail=false;handler.Error=true;
        try{Call("query");throw new Exception("Erro de rede ignorado");}catch(IOException){}
        try{Call("");throw new Exception("Busca vazia aceita");}catch(ArgumentException){}
    }
}