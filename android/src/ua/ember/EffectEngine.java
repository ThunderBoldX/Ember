package ua.ember;

public final class EffectEngine {
 public static final class Step {
  public int color,durationMs=1000,beatCount=1,holdMs=0,timeoutMs=0;
  public double thresholdDb=-24;
  public String trigger="timer",transition="cut";
  public Step copy(){Step s=new Step();s.color=color;s.durationMs=durationMs;s.beatCount=beatCount;s.holdMs=holdMs;s.timeoutMs=timeoutMs;s.thresholdDb=thresholdDb;s.trigger=trigger;s.transition=transition;return s;}
  public void validate(){if(durationMs<125||durationMs>60000||beatCount<1||beatCount>16||holdMs<0||holdMs>30000||timeoutMs<0||timeoutMs>120000||(timeoutMs>0&&timeoutMs<125)||Double.isNaN(thresholdDb)||Double.isInfinite(thresholdDb)||thresholdDb < -60||thresholdDb>0)throw new IllegalArgumentException("Некоректні параметри кроку.");if(!("cut".equals(transition)||"fade".equals(transition)))throw new IllegalArgumentException("Некоректний перехід.");if(!("timer".equals(trigger)||"musicBeat".equals(trigger)||"musicBass".equals(trigger)||"musicPeak".equals(trigger)||"musicSilence".equals(trigger)||"musicLevel".equals(trigger)||"manual".equals(trigger)||"musicBeatOrTimer".equals(trigger)))throw new IllegalArgumentException("Невідома умова.");if("musicBeatOrTimer".equals(trigger)&&timeoutMs==0)throw new IllegalArgumentException("Задай тайм-аут.");}
 }
 public static final class Signal {
  public double rms,peak,bass,bpm;public int beats;public boolean beat;public long timeMs;public double[] spectrum=new double[32];
 }
 private Step[] steps;private boolean repeat;public boolean running,paused;public int index;
 private double started,pausedAt,heldSince=Double.NaN,lastSample=Double.NaN;private int beats,from;private boolean manual;
 public void start(Step[] source,boolean loop,double now){if(source.length<1||source.length>128)throw new IllegalArgumentException("Потрібно 1–128 кроків.");steps=new Step[source.length];for(int i=0;i<source.length;i++){source[i].validate();steps[i]=source[i].copy();}repeat=loop;index=0;started=now;from=steps[0].color;running=true;paused=false;resetGate();}
 private void resetGate(){heldSince=lastSample=Double.NaN;beats=0;manual=false;}
 public void stop(){running=false;paused=false;resetGate();}
 public void pause(double now){if(running&&!paused){paused=true;pausedAt=now;}}
 public void resume(double now){if(paused){double delta=now-pausedAt;started+=delta;if(!Double.isNaN(heldSince))heldSince+=delta;lastSample=now;paused=false;}}
 public void next(){if(running&&!paused&&"manual".equals(steps[index].trigger))manual=true;}
 public boolean needsAudio(){if(steps==null)return false;for(Step s:steps)if(s.trigger.startsWith("music"))return true;return false;}
 public String currentTrigger(){return steps==null?"":steps[index].trigger;}
 public int size(){return steps==null?0:steps.length;}
 public int sample(double now,boolean beat,Signal audio){if(steps==null)throw new IllegalStateException("Ефект не запущено.");if(paused)now=pausedAt;int limit=512;
  while(running&&!paused&&--limit>0){Step s=steps[index];double duration=s.durationMs/1000.0;if(now-started<duration)break;boolean passed="timer".equals(s.trigger);
   if("musicBeat".equals(s.trigger)||"musicBeatOrTimer".equals(s.trigger)){if(beat)beats++;passed=beats>=s.beatCount;}
   else if("manual".equals(s.trigger))passed=manual;
   else if(s.trigger.startsWith("music")){boolean signal=false;if(audio!=null){double value="musicBass".equals(s.trigger)?audio.bass:"musicPeak".equals(s.trigger)?audio.peak:audio.rms;signal="musicSilence".equals(s.trigger)?db(value)<=s.thresholdDb:db(value)>=s.thresholdDb;}if(!signal)heldSince=Double.NaN;else{if(Double.isNaN(heldSince)||(!Double.isNaN(lastSample)&&now-lastSample>.4))heldSince=now;passed=(now-heldSince)*1000+1e-6>=s.holdMs;}lastSample=now;}
   if(!"timer".equals(s.trigger)&&s.timeoutMs>0&&now-started>=duration+s.timeoutMs/1000.0)passed=true;if(!passed)break;from=s.color;if(index==steps.length-1&&!repeat){running=false;break;}index=(index+1)%steps.length;started="timer".equals(s.trigger)?started+duration:now;resetGate();if(!"timer".equals(s.trigger))break;
  }Step current=steps[index];return "fade".equals(current.transition)?lerp(from,current.color,(now-started)/(current.durationMs/1000.0)):current.color;
 }
 public static double db(double n){return n<=0?-120:20*Math.log10(n);}
 public static int lerp(int a,int b,double t){t=Math.max(0,Math.min(1,t));int r=(int)Math.round(((a>>16)&255)+(((b>>16)&255)-((a>>16)&255))*t),g=(int)Math.round(((a>>8)&255)+(((b>>8)&255)-((a>>8)&255))*t),v=(int)Math.round((a&255)+((b&255)-(a&255))*t);return r<<16|g<<8|v;}
}
