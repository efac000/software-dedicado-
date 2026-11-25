using System.Text.RegularExpressions;
namespace Dedicado;
public sealed record ModChoice(string Label,LuaOptionKind Kind,string Value);
public sealed record DeclaredModOption(string Name,string Label,ModChoice Default,ModChoice[] Choices);
public static partial class Lua {
    static string? LiteralText(object? value) {
        if(value is not string token||!SimpleString.IsMatch(token))return null;
        return Regex.Replace(token[1..^1],"""\\(["'\\])""","$1");
    }
    static ModChoice? Choice(object? data,string? label=null){
        if(data is bool flag)return new(label??(flag?"true":"false"),LuaOptionKind.Boolean,flag?"true":"false");
        if(data is string number&&number.Length<=128&&OptionNumber.IsMatch(number))return new(label??number,LuaOptionKind.Number,number);
        if(LiteralText(data) is string text&&text.Length<=4096&&!text.Any(char.IsControl))return new(label??text,LuaOptionKind.Text,text);
        return null;
    }
    public static DeclaredModOption[] DeclaredOptions(string metadata){
        try{
            var entries=new Parser(Tokenize(metadata.TrimStart('\uFEFF'),out _)).Assignments();
            if(entries.GetValueOrDefault("configuration_options") is not Dictionary<string,object?> table)return [];
            var result=new List<DeclaredModOption>();
            var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var item in table.Values){
                if(item is not Dictionary<string,object?> opt)continue;
                var name=LiteralText(opt.GetValueOrDefault("name"));
                if(string.IsNullOrEmpty(name)||name.Length>128||name.Any(char.IsControl)||name.Contains('\\')||name.Contains('"')||!seen.Add(name))continue;
                var def=Choice(opt.GetValueOrDefault("default"));
                if(def==null||opt.GetValueOrDefault("options") is not Dictionary<string,object?> variants)continue;
                var choices=variants.Values.OfType<Dictionary<string,object?>>().Select(v=>Choice(v.GetValueOrDefault("data"),LiteralText(v.GetValueOrDefault("description")))).OfType<ModChoice>().ToArray();
                if(choices.Length==0||choices.Length>256||choices.Length!=variants.Count||choices.Any(c=>c.Kind!=def.Kind)||!choices.Any(c=>c.Kind==def.Kind&&c.Value==def.Value))continue;
                result.Add(new(name,LiteralText(opt.GetValueOrDefault("label"))??name,def,choices));
            }
            return result.ToArray();
        }catch(FormatException){return [];}catch(RegexMatchTimeoutException){return [];}
    }
    public static string ImportDeclaredOptions(string text,string id,IEnumerable<DeclaredModOption> declared){
        if(!WorkshopId.TryParse(id,out var parsed)||parsed!=id)throw new ArgumentException("ID inválido.");
        var parser=Parse(text,out var root,out var positions);
        if(!root.TryGetValue("workshop-"+id,out var entry)||entry is not Dictionary<string,object?> mod)throw new FormatException("Mod ausente.");
        Dictionary<string,object?>? current=null;
        if(mod.TryGetValue("configuration_options",out var raw)){
            current=raw as Dictionary<string,object?>;
            if(current==null)throw new FormatException("Opções existentes não são uma tabela.");
        }
        string Encode(ModChoice c)=>c.Kind switch{
            LuaOptionKind.Boolean when c.Value is "true" or "false"=>c.Value,
            LuaOptionKind.Number when OptionNumber.IsMatch(c.Value)=>c.Value,
            LuaOptionKind.Text when !c.Value.Any(char.IsControl)=>"\""+c.Value.Replace("\\","\\\\").Replace("\"","\\\"")+"\"",
            _=>throw new FormatException("Valor de opção inválido.")
        };
        var added=new List<string>();var seen=new HashSet<string>(StringComparer.Ordinal);
        foreach(var option in declared){
            if(current?.ContainsKey(option.Name)==true||!seen.Add(option.Name))continue;
            if(option.Name.Length==0||option.Name.Length>128||option.Name.Any(char.IsControl)||option.Name.Contains('\\')||option.Name.Contains('"'))throw new FormatException("Nome de opção não suportado.");
            added.Add("[\""+option.Name+"\"]="+Encode(option.Default));
        }
        if(added.Count==0)return text;
        var target=current??mod;
        var prefix=target.Count>0&&!parser.TableSeparators.GetValueOrDefault(target)?",":"";
        var content=string.Join(",",added);
        if(current==null)content="configuration_options={"+content+"}";
        var result=text.Insert(positions[parser.TableCloses[target]],prefix+"\n"+content+"\n");
        Entries(result);return result;
    }
}