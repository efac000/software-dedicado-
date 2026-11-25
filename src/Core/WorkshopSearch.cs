using System.Net;
using System.Text.Json;
namespace Dedicado;
public sealed record WorkshopHit(string Id,string Title,string[]? PublishedDependencies=null);
public sealed record WorkshopPage(WorkshopHit[] Items,string? NextCursor,int ReceivedCount=0,int? Total=null);
public static class WorkshopSearch {
    const string Endpoint="https://api.steampowered.com/IPublishedFileService/QueryFiles/v1/";
    public static async Task<WorkshopPage> Search(string key,string query,string? cursor=null,CancellationToken token=default){
        using var client=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false});
        return await SearchCore(client,key,query,cursor,token);
    }
    internal static async Task<WorkshopPage> SearchCore(HttpClient client,string key,string query,string? cursor,CancellationToken token=default){
        query=query.Trim();
        if(query.Length==0||query.Length>120)throw new ArgumentException("Digite de 1 a 120 caracteres.");
        if(key.Length!=32||!key.All(Uri.IsHexDigit))throw new ArgumentException("Informe uma chave de usuário Steam Web API válida.");
        cursor??="*";
        if(cursor.Length>2048||cursor.Any(char.IsControl))throw new ArgumentException("Cursor inválido.");
        var parameters=JsonSerializer.Serialize(new{query_type=12,cursor,numperpage=30,appid=322330,search_text=query,filetype=0,ids_only=false,return_details=true,return_children=true});
        using var request=new HttpRequestMessage(HttpMethod.Get,Endpoint+"?input_json="+Uri.EscapeDataString(parameters));
        request.Headers.Add("x-webapi-key",key);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try{
            using var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
            if(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)throw new IOException("Steam recusou a autenticação. Confira a chave de usuário.");
            if(response.StatusCode==HttpStatusCode.TooManyRequests)throw new IOException("Limite de consultas da Steam. Tente novamente mais tarde.");
            if(!response.IsSuccessStatusCode)throw new IOException("Workshop indisponível.");
            const int max=2*1024*1024;
            if(response.Content.Headers.ContentLength>max)throw new IOException("Resposta Workshop muito grande.");
            using var stream=await response.Content.ReadAsStreamAsync(timeout.Token);
            using var data=new MemoryStream();var buffer=new byte[8192];
            for(int n;(n=await stream.ReadAsync(buffer.AsMemory(),timeout.Token))>0;){if(data.Length+n>max)throw new IOException("Resposta Workshop muito grande.");data.Write(buffer,0,n);}
            data.Position=0;using var json=JsonDocument.Parse(data);
            return Parse(json.RootElement,cursor);
        }catch(HttpRequestException){throw new IOException("Não foi possível conectar à Steam.");}
        catch(JsonException){throw new IOException("Resposta da Workshop inválida.");}
        catch(OperationCanceledException) when(!token.IsCancellationRequested){throw new IOException("Tempo limite da consulta Workshop.");}
    }
    static string? Scalar(JsonElement item,string property){
        if(!item.TryGetProperty(property,out var v))return null;
        return v.ValueKind==JsonValueKind.String?v.GetString():v.ValueKind==JsonValueKind.Number?v.GetRawText():null;
    }
    internal static WorkshopPage Parse(JsonElement root,string cursor){
        if(root.ValueKind!=JsonValueKind.Object||!root.TryGetProperty("response",out var response)||response.ValueKind!=JsonValueKind.Object)throw new IOException("Resposta Workshop incompleta.");
        var items=new List<WorkshopHit>();var seen=new HashSet<string>(StringComparer.Ordinal);int received=0;
        if(response.TryGetProperty("publishedfiledetails",out var list)){
            if(list.ValueKind!=JsonValueKind.Array||list.GetArrayLength()>100)throw new IOException("Lista Workshop inválida.");
            received=list.GetArrayLength();foreach(var item in list.EnumerateArray()){
                if(item.ValueKind!=JsonValueKind.Object)continue;
                var id=Scalar(item,"publishedfileid");var title=Scalar(item,"title");var app=Scalar(item,"consumer_appid")??Scalar(item,"consumer_app_id");
                var result=Scalar(item,"result");
                if(app!="322330"||(result!=null&&result!="1")||id==null||!WorkshopId.TryParse(id,out var parsed)||parsed!=id||!seen.Add(id)||string.IsNullOrWhiteSpace(title)||title.Length>512)continue;
                string[]? dependencies=null;
                // Steam describes non-collection children as soft published dependencies.
                // Require an ordinary item and a complete explicit child count.
                if(Scalar(item,"file_type")=="0"&&int.TryParse(Scalar(item,"num_children"),out var count)&&count>=0&&count<=500){
                    if(count==0)dependencies=[];
                    else if(item.TryGetProperty("children",out var children)&&children.ValueKind==JsonValueKind.Array&&children.GetArrayLength()==count){
                        var valid=new List<string>();
                        foreach(var child in children.EnumerateArray()){
                            if(child.ValueKind!=JsonValueKind.Object)break;
                            var childId=Scalar(child,"publishedfileid")??Scalar(child,"child_publishedfileid");
                            if(childId==null||!WorkshopId.TryParse(childId,out var parsedChild)||parsedChild!=childId)break;
                            valid.Add(childId);
                        }
                        if(valid.Count==count)dependencies=valid.Distinct().ToArray();
                    }
                }
                items.Add(new(id,title,dependencies));
            }
        }else if(Scalar(response,"total")!="0")throw new IOException("Resultados Workshop ausentes.");
        var next=Scalar(response,"next_cursor");
        if(string.IsNullOrEmpty(next)||next==cursor||next.Length>2048||next.Any(char.IsControl))next=null;
        return new(items.ToArray(),next,received,int.TryParse(Scalar(response,"total"),out var total)&&total>=0?total:null);
    }
}