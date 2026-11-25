namespace Dedicado;
public static class DependencyAnalysis {
    public static string[] Inspect(string shard,IReadOnlyDictionary<string,bool> selected,Func<string,ModMetadata?> load,IReadOnlyDictionary<string,string[]>? workshop=null){
        var messages=new HashSet<string>(StringComparer.Ordinal);
        var explored=new HashSet<string>(StringComparer.Ordinal);
        var visiting=new HashSet<string>(StringComparer.Ordinal);
        void Add(string message)=>messages.Add(shard+": "+message);
        void Visit(string id,int depth){
            if(depth>64||explored.Count>2000){Add("limite de análise de dependências atingido; resultado incompleto.");return;}
            if(!visiting.Add(id)){Add("ciclo de dependências envolvendo "+id+"; revise as declarações.");return;}
            if(!explored.Add(id)){visiting.Remove(id);return;}
            var local=load(id);
            var published=workshop!=null&&workshop.TryGetValue(id,out var value)?value:null;
            if(local==null&&published==null)Add("mod "+id+": dependências desconhecidas (metadados indisponíveis ou dinâmicos).");
            if(local?.ClientOnly==true)Add("mod "+id+" declara uso exclusivo no cliente (modinfo.lua).");
            if(local?.DstCompatible==false)Add("mod "+id+" declara incompatibilidade com DST (modinfo.lua).");
            var links=(local?.Dependencies??[]).Concat(published??[]).Distinct(StringComparer.Ordinal);
            foreach(var dependency in links){
                var source=local?.Dependencies.Contains(dependency)==true&&published?.Contains(dependency)==true?"modinfo.lua e Workshop":local?.Dependencies.Contains(dependency)==true?"modinfo.lua":"Workshop, dependência publicada";
                if(!selected.TryGetValue(dependency,out var enabled)||!enabled)
                    Add("mod "+id+" declara dependência "+dependency+" "+(selected.ContainsKey(dependency)?"desativada":"ausente")+" ("+source+").");
                Visit(dependency,depth+1);
            }
            visiting.Remove(id);
        }
        foreach(var id in selected.Where(x=>x.Value).Select(x=>x.Key))Visit(id,0);
        if(selected.Any(x=>x.Value))Add("compatibilidade entre pares desconhecida sem evidência explícita; ausência de aviso não confirma compatibilidade.");
        return messages.ToArray();
    }
}