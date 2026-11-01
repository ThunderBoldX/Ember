using Ember;
static class GateTests
{
 public static void Run(Action<bool,string> assert)
 {
  AudioFrame loud=new(.2,.5,.15,.1,.01,120,true,1,new double[32],DateTime.UtcNow),quiet=new(.001,.002,.001,.001,.001,0,false,0,new double[32],DateTime.UtcNow);
  CustomEffect Make(StepTrigger gate)=>new(){Name="Умова",Repeat=false,Steps={new(){Color="#FF0000",DurationMs=500,Trigger=gate},new(){Color="#0000FF",DurationMs=500}}};var player=new EffectPlayer();
  var beat=Make(StepTrigger.MusicBeat);beat.Steps[0].BeatCount=3;player.Start(beat,0);player.Sample(.2,true);player.Sample(.6,true);player.Sample(.8,true);assert(player.Index==0,"beats before minimum duration are not accumulated");player.Sample(1,true);assert(player.Index==1,"configured beat count completes gate");
  foreach(var gate in new[]{StepTrigger.MusicBass,StepTrigger.MusicPeak,StepTrigger.MusicLevel}){player.Start(Make(gate),0);player.Sample(.7,false,quiet);assert(player.Index==0,"quiet audio cannot satisfy "+gate);player.Sample(.9,false,loud);assert(player.Index==1,"real threshold crossing satisfies "+gate);}
  var silence=Make(StepTrigger.MusicSilence);silence.Steps[0].ThresholdDb=-40;silence.Steps[0].HoldMs=500;player.Start(silence,0);player.Sample(.6);player.Sample(2);assert(player.Index==0,"absence of audio frames is not silence");player.Sample(2.1,false,quiet);player.Sample(2.3,false,quiet);player.Sample(2.4,false,loud);player.Sample(2.5,false,quiet);player.Sample(2.7,false,quiet);assert(player.Index==0,"interrupted condition resets sustained hold");player.Sample(2.9,false,quiet);player.Sample(3.1,false,quiet);assert(player.Index==1,"continuous silence completes hold");
  player.Start(silence,0);player.Sample(.6,false,quiet);player.Sample(1.2,false,quiet);assert(player.Index==0,"stale frame gaps do not count toward hold");player.Sample(1.4,false,quiet);player.Sample(1.6,false,quiet);player.Sample(1.8,false,quiet);assert(player.Index==1,"hold restarts after fresh frames return");
  var manual=Make(StepTrigger.Manual);player.Start(manual,0);player.RequestNext();player.Sample(.3);assert(player.Index==0,"manual request honors minimum step duration");player.Sample(.6);assert(player.Index==1,"early manual request remains pending until eligible");
  var deadline=Make(StepTrigger.MusicBeatOrTimer);deadline.Steps[0].TimeoutMs=1000;deadline.Steps[0].BeatCount=2;player.Start(deadline,0);player.Sample(1.3);assert(player.Index==0,"fallback timeout starts after minimum duration");player.Sample(1.6);assert(player.Index==1,"fallback timeout prevents indefinite waiting");
  deadline.Steps[0].Trigger=StepTrigger.MusicPeak;player.Start(deadline,0);player.Sample(.7,false,quiet);player.Pause(.8);player.Sample(10);player.Resume(10);player.Sample(10.5);assert(player.Index==0,"pause preserves timeout remaining duration");player.Sample(10.8);assert(player.Index==1,"resumed timeout completes at adjusted deadline");
  var parsed=EffectPack.Parse(EffectPack.Serialize(new[]{silence,manual,deadline}));assert(parsed.Count==3&&parsed[0].Steps[0].HoldMs==500&&parsed[0].Steps[0].ThresholdDb==-40&&parsed[2].Steps[0].TimeoutMs==1000,"pack v2 retains all new condition parameters");
  string v1="""{"format":"ember-effects","version":1,"effects":[{"name":"v1","repeat":true,"steps":[{"color":"#FF5A1F","durationMs":1000,"transition":"cut","trigger":"musicBeat"}]}]}""";var old=EffectPack.Parse(v1);assert(old[0].Steps[0].BeatCount==1&&old[0].Steps[0].TimeoutMs==0,"v1 retains original one-beat behavior without implicit timeout");
 }
}
