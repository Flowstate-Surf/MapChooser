using HudKit.Shared;
using SwiftlyS2.Shared.Players;
namespace MapChanger.Menu;

public sealed class BattleMapVoteTemplate(Action<string> click) : MapChooserHudTemplate(click)
{
    public override string LayoutName => "mapchooser/battle_mapvote";
    public override string RootId => "battle-vote-root";
    public override bool CapturesInput => true;
    public override string MapButton(string buttonId)
    {
        foreach (string prefix in new[]{"photo_","missing_","bar_"})
            if (buttonId.StartsWith(prefix) && int.TryParse(buttonId[prefix.Length..].Split('_')[0],out int row) && row>=0 && row<6) return $"card_{row}";
        return buttonId;
    }
}

public sealed partial class MapChooserHudMenuService
{
    public Func<bool>? BattleVoteEnabled { get; set; }
    private sealed class PictureState
    {
        public required ulong Steam;
        public required List<string> Maps;
        public required Action<IPlayer,string> Select;
        public required BattleMapVoteTemplate Template;
        public int Page;
        public int Seconds;
        public bool AllowClose;
        public string? Selected;
        public IReadOnlyDictionary<string,int> Votes = new Dictionary<string,int>();
        public readonly string[] Images = new string[6];
    }
    private readonly Dictionary<int,PictureState> _pictureMenus = new();
    public bool IsPictureVoteOpen(IPlayer p) => _pictureMenus.TryGetValue(p.Slot,out var s) && s.Steam == p.SteamID;
    public void ShowPictureVote(IPlayer p, List<string> maps, Action<IPlayer,string> select, bool allowClose, int seconds, IReadOnlyDictionary<string,int> votes, string? selected)
    {
        if (!p.IsValid) return;
        Close(p.Slot);
        PictureState? state = null;
        var template = new BattleMapVoteTemplate(button => _core.Scheduler.NextWorldUpdate(() => {
            if (state == null || !_pictureMenus.TryGetValue(p.Slot,out var current) || !ReferenceEquals(state,current)) return;
            var viewer = _core.PlayerManager.GetPlayer(p.Slot);
            if (viewer is not { IsValid: true } || viewer.SteamID != state.Steam) return;
            if (button == "vote-close") { if (state.AllowClose) Close(viewer); return; }
            if (button == "vote-prev") state.Page--;
            else if (button == "vote-next") state.Page++;
            else if (button.StartsWith("card_") && int.TryParse(button[5..],out int row) && row >= 0 && row < 6) {
                int index=state.Page*6+row;
                if (index < state.Maps.Count && state.Seconds > 0) state.Select(viewer,state.Maps[index]);
            }
            if (_pictureMenus.TryGetValue(viewer.Slot,out var live) && ReferenceEquals(live,state)) {
                RenderPicture(state); _handles.GetValueOrDefault(viewer.Slot)?.Update();
            }
        }));
        state = new() { Steam=p.SteamID, Maps=new(maps), Select=select, Template=template, AllowClose=allowClose, Seconds=seconds, Votes=votes, Selected=selected };
        _pictureMenus[p.Slot]=state;
        RenderPicture(state);
        _handles[p.Slot]=_hudKit.Show(p,template);
    }
    public void UpdatePictureVote(IPlayer player, int seconds, IReadOnlyDictionary<string,int> votes, string? selected)
    {
        if (!_pictureMenus.TryGetValue(player.Slot,out var s) || s.Steam!=player.SteamID) return;
        s.Seconds=seconds; s.Votes=votes; s.Selected=selected;
        RenderPicture(s); _handles.GetValueOrDefault(player.Slot)?.Update();
    }
    private static void RenderPicture(PictureState s)
    {
        var t=s.Template;
        void Set(string key,string value)=>t.Set(t.RootId,key,value);
        int pages=Math.Max(1,(s.Maps.Count+5)/6); s.Page=Math.Clamp(s.Page,0,pages-1);
        int total=s.Votes.Values.Sum();
        Set("clock",Math.Max(0,s.Seconds).ToString()); Set("voters",$"{total} PLAYERS VOTED");
        Set("page",$"{s.Page+1} / {pages}"); Set("status",s.Selected==null ? "Your vote is waiting" : "Vote counted — "+MapChooserMenuTemplate.ParseMapLabel(s.Selected).Name);
        t.Class("vote-close","hidden",!s.AllowClose); t.Class("vote-prev","disabled",s.Page==0); t.Class("vote-next","disabled",s.Page==pages-1);
        for(int i=0;i<6;i++) {
            int n=s.Page*6+i; string panel=$"card_{i}"; t.Class(panel,"hidden",n>=s.Maps.Count); if(n>=s.Maps.Count)continue;
            string map=s.Maps[n]; bool extend=map=="map_chooser.extend_option";
            var info=MapChooserMenuTemplate.ParseMapLabel(map);
            Set($"name_{i}",extend?"EXTEND CURRENT MAP":info.Name.ToUpperInvariant());
            Set($"meta_{i}",extend?"KEEP SURFING":string.Join("  •  ",new[]{info.Tier,info.Type switch { "S"=>"STAGED","L"=>"LINEAR","H"=>"HYBRID",_=>"" },info.Bonus.Length>0?info.Bonus[..^1]+" BONUSES":""}.Where(x=>x.Length>0)));
            int votes=s.Votes.GetValueOrDefault(map,0); int percent=total==0?0:(int)Math.Round(100.0*votes/total);
            Set($"votes_{i}",$"{votes} VOTES"); Set($"percent_{i}",$"{percent}%");
            t.Class(panel,"selected",map==s.Selected);
            string image=BattleMapPictureCatalog.Images.GetValueOrDefault(info.Name.ToLowerInvariant(),"");
            if(s.Images[i]!=image) { if(!string.IsNullOrEmpty(s.Images[i]))t.Class($"photo_{i}",s.Images[i],false); if(image.Length>0)t.Class($"photo_{i}",image,true); s.Images[i]=image; }
            t.Class($"missing_{i}","hidden",image.Length>0); Set($"missing_{i}",extend?"STAY ON THIS MAP":"PREVIEW UNAVAILABLE");
            for(int bar=0;bar<10;bar++)t.Class($"bar_{i}_{bar}","filled",bar<Math.Round(percent/10.0));
        }
    }
}
