using Sspcl.Core.Store.Forum;
using Sspcl.Core.Versioning;

namespace Sspcl.Core.Mods;
public sealed class ModSelectionDiff
{
    public List<string> Added { get; set; } = new();
    public List<string> Removed { get; set; } = new();
    public List<string> Automatic { get; set; } = new();
}
public static class ModManagement
{
    public static ModSelectionDiff Compare(IEnumerable<string> previous, IEnumerable<string> current, IEnumerable<string> automatic)
    {
        var before = new HashSet<string>(previous,StringComparer.Ordinal);var after=new HashSet<string>(current,StringComparer.Ordinal);
        return new() {Added=after.Except(before).OrderBy(id=>id,StringComparer.Ordinal).ToList(),Removed=before.Except(after).OrderBy(id=>id,StringComparer.Ordinal).ToList(),Automatic=automatic.Where(after.Contains).Distinct(StringComparer.Ordinal).OrderBy(id=>id,StringComparer.Ordinal).ToList()};
    }
    public static bool Matches(ModSpec? mod,string id,string search,bool enabled,string mode,string author,bool update)
    {
        if(search.Length>0 && ((mod?.Name ?? id)+" "+id+" "+mod?.Folder+" "+string.Join(" ",mod?.Authors??Array.Empty<string>())).IndexOf(search,StringComparison.OrdinalIgnoreCase)<0)return false;
        if(author.Length>0 && (mod==null || !mod.Authors.Contains(author,StringComparer.OrdinalIgnoreCase)))return false;
        return mode switch {"enabled"=>enabled,"disabled"=>!enabled,"missing"=>mod==null,"updates"=>update,_=>true};
    }
    public static Dictionary<string,string> FindUpdates(IReadOnlyList<ModSpec> installed,IEnumerable<ForumMod> forum)
    {
        var latest = new Dictionary<string,(ModVersion Version,string Raw)>(StringComparer.Ordinal);
        foreach(var mod in forum.Where(m=>m!=null && !string.IsNullOrWhiteSpace(m.Id)))
            foreach(var version in (mod.Releases?.Where(r=>r!=null).Select(r=>r.ModVersion)??Enumerable.Empty<string>()).Concat(new[]{mod.Version}).Where(v=>!string.IsNullOrWhiteSpace(v)))
            {
                ModVersion parsed;
                try { parsed=ModVersion.FromString(version); } catch(OverflowException) {continue;}
                if(parsed==ModVersion.Unspecified)continue;
                if(!latest.TryGetValue(mod.Id,out var old) || parsed>old.Version)latest[mod.Id]=(parsed,version);
            }
        var result=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var mod in installed)
            if(mod.Version!=ModVersion.Unspecified && latest.TryGetValue(mod.Id,out var value) && value.Version>mod.Version)result[mod.Id]=value.Raw;
        return result;
    }
}
