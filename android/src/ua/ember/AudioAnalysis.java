package ua.ember;
public final class AudioAnalysis {
 private final short[] ring=new short[1024];private int index,filled;
 private double average=.002,lastBeat=-100;private int beats;private final double[] intervals=new double[8];private int used;
 public EffectEngine.Signal analyze(short[] pcm,int count,int rate,double now,long time){for(int i=0;i<count;i++){ring[index]=pcm[i];index=(index+1)%ring.length;filled=Math.min(filled+1,ring.length);}short[] window=new short[filled];for(int i=0;i<filled;i++)window[i]=ring[(index-filled+i+ring.length)%ring.length];pcm=window;count=filled;EffectEngine.Signal s=new EffectEngine.Signal();s.timeMs=time;if(count<2)return s;double energy=0;for(int i=0;i<count;i++){double value=pcm[i]/32768.0;energy+=value*value;s.peak=Math.max(s.peak,Math.abs(value));}s.rms=Math.sqrt(energy/count);
  for(int band=0;band<32;band++){double hz=30*Math.pow(600,band/31.0);hz=Math.min(hz,rate*.48);double coeff=2*Math.cos(2*Math.PI*hz/rate),a=0,b=0;for(int i=0;i<count;i++){double value=pcm[i]/32768.0*(.5-.5*Math.cos(2*Math.PI*i/(count-1)));double next=value+coeff*a-b;b=a;a=next;}double magnitude=2*Math.sqrt(Math.max(0,a*a+b*b-coeff*a*b))/count;s.spectrum[band]=Math.min(1,magnitude*6);if(hz<=220)s.bass=Math.max(s.bass,magnitude);}
  s.beat=s.rms>.008&&s.bass>Math.max(.005,average*1.7)&&now-lastBeat>.28;if(s.beat){if(lastBeat>0){intervals[used%8]=now-lastBeat;used++;}lastBeat=now;beats++;}average=average*.94+s.bass*.06;s.beats=beats;if(used>=2){double total=0;int n=Math.min(used,8);for(int i=0;i<n;i++)total+=intervals[i];s.bpm=60/(total/n);}return s;
 }
}
