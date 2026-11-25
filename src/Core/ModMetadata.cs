using System.Text.RegularExpressions;

namespace Dedicado;

public sealed record ModMetadata(bool ClientOnly,bool? DstCompatible,string[] Dependencies);

public static partial class Lua {
    public static ModMetadata? GetModMetadata(string text){
        Dictionary<string,object?> entries;
        try{entries=new Parser(Tokenize(text.TrimStart('\uFEFF'),out _)).Assignments();}
        catch(FormatException){return null;}
        catch(RegexMatchTimeoutException){return null;}

        var dependencies=new List<string>();
        if(entries.GetValueOrDefault("mod_dependencies") is Dictionary<string,object?> declarations){
            foreach(var value in declarations.Values){
                if(value is not Dictionary<string,object?> declaration
                    ||declaration.GetValueOrDefault("workshop") is not string literal
                    ||!SimpleString.IsMatch(literal))continue;
                var name=literal[1..^1];
                if(name.StartsWith("workshop-",StringComparison.Ordinal)&&WorkshopId.TryParse(name[9..],out var id))
                    dependencies.Add(id);
            }
        }
        return new ModMetadata(entries.GetValueOrDefault("client_only_mod") is true,
            entries.GetValueOrDefault("dst_compatible") as bool?,dependencies.Distinct().ToArray());
    }
}
