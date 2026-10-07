using System.Reflection;
using HudKit.Shared;
using MapChanger.Menu;
using SwiftlyS2.Shared.Players;

HudTemplate? shown = null;
var handle = Fake<IHudHandle>((m,a) => null);
var hud = Fake<IHudKit>((m,a) => { if (m.Name == "Show") { shown = (HudTemplate)a[1]!; return handle; } return null; });
var player = Fake<IPlayer>((m,a) => m.Name switch { "get_IsValid" => true, "get_Slot" or "get_PlayerID" => 3, "get_SteamID" => 123UL, _ => null });
var coreType=typeof(SwiftlyS2.Shared.ISwiftlyCore);
var manager=DispatchProxy.Create(coreType.GetProperty("PlayerManager")!.PropertyType,typeof(Proxy));
((Proxy)manager).Call=(m,a)=>m.Name=="GetPlayer" ? player : null;
var scheduler=DispatchProxy.Create(coreType.GetProperty("Scheduler")!.PropertyType,typeof(Proxy));
((Proxy)scheduler).Call=(m,a)=> { if(m.Name=="NextWorldUpdate") foreach(var callback in a.OfType<Delegate>()) callback.DynamicInvoke(); return null; };
var core=Fake<SwiftlyS2.Shared.ISwiftlyCore>((m,a)=>m.Name switch {"get_PlayerManager"=>manager,"get_Scheduler"=>scheduler,_=>null});
var service = new MapChooserHudMenuService(core, hud);
var values = new Dictionary<string,string>();
var classes = new Dictionary<string,bool>();
var surface = Fake<IHudSurface>((m,a) => {
    if(m.Name=="Set")values[(string)a[1]!] = (string)a[2]!;
    if(m.Name=="Class")classes[$"{a[0]}:{a[1]}"] = (bool)a[2]!;
    return null;
});
void Render(int count, bool back=false) {
    values.Clear(); classes.Clear();
    var options = Enumerable.Range(0,count).Select(i=>new MapChooserHudOption($"Map {i}", true, _=>{})).ToList();
    service.Show(player,"Maps", options, back ? _=>{} : null);
    shown!.Apply(surface);
}
Render(8);
Check(values["option_7"]=="Map 7", "eight map choices fit on one page");
Check(classes["mapchooser-root:show"], "every menu asserts viewer visibility");
Render(3,true);
Check(values["option_3"]=="◄ Back" && classes["option_4:hidden"], "Back occupies a real visible row after short submenu");
Render(9);
Check(values["option_6"]=="◄ Prev" && values["option_7"]=="Next ►", "long menus reserve navigation rows");
Check(classes["option_6:disabled"] && !classes["option_7:disabled"], "first page disables only previous navigation");
Render(8,true);
var menus=(System.Collections.IDictionary)typeof(MapChooserHudMenuService).GetField("_menus",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(service)!;
var state=menus[3]!;
state.GetType().GetField("Page")!.SetValue(state,1);
values.Clear();classes.Clear();
typeof(MapChooserHudMenuService).GetMethod("Render",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(service,[player,3,state]);
shown!.Apply(surface);
Check(values["option_2"]=="◄ Back" && classes["option_7:disabled"], "Back remains reachable on final paginated submenu page");
Check(shown.MapButton("option_2_label")=="option_2", "child-label clicks map to their option row");
service.Show(player,"Vote",[],allowClose:false); shown!.Apply(surface);
Check(classes["menu_close:hidden"], "mandatory vote honours disabled exit");
Check(shown!.CapturesInput, "mouse menus retain click input");
int selected=-1;
var votes=new List<MapChooserHudOption> {
    new("surf_aura T1 | 8B | S",true,_=>selected=0),
    new("surf_boreas T1 | L",false,_=>selected=1)
};
service.Show(player,"Vote",votes,capturesMouse:false);
shown!.Apply(surface);
Check(!shown.CapturesInput && classes["mapchooser-root:passive-vote"], "RTV and end votes never request mouse capture");
Check(values["number_0"]=="!1" && values["hint"].Contains("!1"), "passive votes display chat command instructions");
Check(values["option_0"]=="surf_aura" && values["tier_0"]=="T1" && values["bonus_0"]=="8B" && values["type_0"]=="S", "configured map metadata becomes separate badges");
service.SelectNumber(player,2); Check(selected==-1,"disabled vote rows reject numbered commands");
service.SelectNumber(player,1); Check(selected==0,"!1 selects the first enabled vote");
Check(!service.SelectNumber(player,9),"out-of-range commands cannot select a vote");
service.SelectNumber(player,0);Check(!service.IsOpen(player),"!0 closes an optional vote");
service.Show(player,"Vote",votes,allowClose:false,capturesMouse:false);
service.SelectNumber(player,0);Check(service.IsOpen(player),"!0 cannot close a mandatory vote");
service.Show(player,"Nominate",votes);
selected=-1; Check(!service.SelectNumber(player,1) && selected==-1,"numeric votes do not activate nomination choices");
var labels=MapChooserMenuTemplate.ParseMapLabel("All Maps");
Check(labels.Name=="All Maps" && labels.Tier=="", "non-map options do not acquire invented badges");
service.ForgetAll();
Check(!service.IsOpen(player) && !service.SelectNumber(player,1), "map teardown removes menu and numeric vote state");
var commands = new List<string>();
var delayed = new List<(Action Callback, CancellationTokenSource Token)>();
object ProxyService(string property, Func<MethodInfo,object?[],object?> call) {
    var p=DispatchProxy.Create(coreType.GetProperty(property)!.PropertyType,typeof(Proxy));((Proxy)p).Call=call;return p;
}
var engine=ProxyService("Engine",(m,a)=>{if(m.Name=="ExecuteCommand")commands.Add((string)a[0]!);return null;});
var mapScheduler=ProxyService("Scheduler",(m,a)=>{if(m.Name=="DelayBySeconds"){var token=new CancellationTokenSource();delayed.Add(((Action)a[1]!,token));return token;}return null;});
var localizer=ProxyService("Localizer",(m,a)=>"message");
var changingCore=Fake<SwiftlyS2.Shared.ISwiftlyCore>((m,a)=>m.Name switch {
    "get_Engine"=>engine,"get_Scheduler"=>mapScheduler,"get_PlayerManager"=>manager,"get_Localizer"=>localizer,
    "get_Logger"=>Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,_=>null });
var mapState=new MapChanger.Dependencies.PluginState();
var lister=new MapChanger.Helpers.MapLister();
lister.AddMap(new MapChanger.Models.Map("surf_test", "ws:123456"));
lister.AddMap(new MapChanger.Models.Map("de_dust2"));
var change=(MapChanger.Helpers.ChangeMapManager)Activator.CreateInstance(typeof(MapChanger.Helpers.ChangeMapManager),BindingFlags.Instance|BindingFlags.NonPublic,null,[changingCore,mapState,lister,new MapChanger.Models.MapChangerConfig(),null],null)!;
change.ScheduleMapChange("surf_test",true);change.ChangeMap();
Check(delayed.Count==1,"duplicate map triggers schedule one command and no delayed fallback");
delayed[0].Callback();
Check(commands.Count(x=>x.StartsWith("host_workshop_map"))==1 && commands.Last()=="host_workshop_map 123456","Workshop ID uses the expected map command exactly once");
mapState.MapSwitchInFlight=false;
change.ScheduleMapChange("de_dust2",true);delayed[1].Callback();
Check(commands.Last()=="changelevel de_dust2","local maps without an explicit ID use their map name");
mapState.MapSwitchInFlight=false;
change.ScheduleMapChange("de_dust2",true);change.CancelPending();
Check(delayed[2].Token.IsCancellationRequested,"map teardown cancels a pending map command");
mapState.MapSwitchInFlight=false; mapState.MatchEnded=false;
bool battleRunning=true; int cancels=0;
change.RtvBattleDecision=()=>battleRunning;
change.CancelBattleWait=()=>cancels++;
int before=commands.Count;
change.ScheduleMapChange("surf_test",false,true);
var wait=delayed.Last();
Check(!mapState.MapSwitchInFlight && mapState.NextMap=="surf_test" && commands.Count==before,"RTV preserves selected map while battle runs");
int count=delayed.Count; change.ChangeMap();
Check(delayed.Count==count,"concurrent triggers do not stack battle waits");
wait.Callback();
Check(!mapState.MapSwitchInFlight && commands.Count==before,"running battle remains deferred on retry");
battleRunning=false; delayed.Last().Callback();
Check(mapState.MapSwitchInFlight,"settled battle starts map-change countdown");
delayed.Last().Callback();
Check(commands.Count(x=>x=="host_workshop_map 123456")==2,"settled RTV issues exactly one additional map command");
mapState.MapSwitchInFlight=false; battleRunning=true;
change.ScheduleMapChange("surf_test",true,true); var stale=delayed.Last();
change.CancelPending(); count=delayed.Count; stale.Callback();
Check(stale.Token.IsCancellationRequested && delayed.Count==count && cancels>0,"teardown cancels gate and rejects stale retry");
change.ScheduleMapChange("surf_test",true,true); stale=delayed.Last();
change.ScheduleMapChange("de_dust2",true,false); count=delayed.Count; stale.Callback();
Check(mapState.MapSwitchInFlight && delayed.Count==count && stale.Token.IsCancellationRequested,"explicit admin change supersedes a waiting RTV");
service.ForgetAll();
string? picked=null;
var imageMaps=new List<string>{"surf_aura T1 | 8B | S","surf_missing T2 | L","surf_beginner","surf_boreas","surf_tropic","surf_test","surf_last"};
service.ShowPictureVote(player,imageMaps,(p,map)=>picked=map,true,30,new Dictionary<string,int>{{imageMaps[0],2}},null);
Check(shown!.LayoutName=="mapchooser/battle_mapvote" && shown.CapturesInput,"battle vote uses separate full-screen mouse layout");
values.Clear();classes.Clear();shown.Apply(surface);
Check(values["clock"]=="30" && values["votes_0"]=="2 VOTES" && values["percent_0"]=="100%","picture vote renders real countdown and tally");
Check(!classes["missing_1:hidden"] && classes["missing_0:hidden"],"unavailable image gets fallback; known map gets image");
void ClickPicture(string id)=>shown!.OnClickHandler!(Fake<IHudClickContext>((m,a)=>m.Name=="get_ButtonId"?id:null));
ClickPicture("card_0");Check(picked==imageMaps[0],"image card submits actual configured map name");
service.UpdatePictureVote(player,19,new Dictionary<string,int>{{imageMaps[0],3}},imageMaps[0]);
values.Clear();classes.Clear();shown.Apply(surface);Check(values["clock"]=="19" && classes["card_0:selected"],"vote updates selection and countdown in place");
ClickPicture("vote-next");values.Clear();classes.Clear();shown.Apply(surface);Check(values["name_0"]=="SURF_LAST" && classes["card_1:hidden"],"picture vote pagination keeps every configured choice accessible");
ClickPicture("card_0");Check(picked=="surf_last","paged card votes for its correct map");
Check(shown.MapButton("photo_3")=="card_3" && shown.MapButton("bar_2_5")=="card_2","image and bar clicks resolve to their card");
var oldPicture=shown;service.Close(player);picked=null;oldPicture.OnClickHandler!(Fake<IHudClickContext>((m,a)=>m.Name=="get_ButtonId"?"card_0":null));Check(picked==null,"closed picture vote rejects stale clicks");
Check(!typeof(MapChanger.MapChanger).Assembly.GetReferencedAssemblies().Any(a=>a.Name=="FlowtimerS2.Contract"), "MapChanger loads without mandatory Flowtimer contract assembly reference");
Console.WriteLine("All map menu regression checks passed.");
static T Fake<T>(Func<MethodInfo,object?[],object?> call) where T:class {var proxy=DispatchProxy.Create<T,Proxy>(); ((Proxy)(object)proxy).Call=call;return proxy;}
static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS: "+message);}
public class Proxy:DispatchProxy { public Func<MethodInfo,object?[],object?> Call=null!; protected override object? Invoke(MethodInfo? m,object?[]? a)=>Call(m!,a??[]) ?? (m!.ReturnType.IsValueType && m.ReturnType!=typeof(void)?Activator.CreateInstance(m.ReturnType):null); }
