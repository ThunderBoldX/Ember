using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
namespace Ember;
public record AudioFrame(double Rms,double Peak,double Bass,double Mid,double Treble,double Bpm,bool Beat,int Beats,double[] Spectrum,DateTime Time);
public sealed class AudioAnalyzer
{
 private readonly float[] ring=new float[1024]; private int index,filled,since; private double average=.001,previousBass; private DateTime lastBeat=DateTime.MinValue; private readonly Queue<double> intervals=new(); private int beats;
 public double Sensitivity {get;set;}=1.55;
 public AudioFrame? Push(float[] samples,int sampleRate)
 {
  foreach(var value in samples) {ring[index]=value; index=(index+1)%ring.Length; filled=Math.Min(ring.Length,filled+1); since++;}
  if(filled<ring.Length||since<sampleRate/100) return null; since=0;
  var fft=new Complex[ring.Length]; double sum=0,peak=0;
  for(var i=0;i<ring.Length;i++) {var x=ring[(index+i)%ring.Length];sum+=x*x;peak=Math.Max(peak,Math.Abs(x)); fft[i]=x*(.5-.5*Math.Cos(2*Math.PI*i/(ring.Length-1)));}
  Transform(fft); var spectrum=new double[32]; double bass=0,mid=0,treble=0;
  for(var k=1;k<fft.Length/2;k++) {var freq=k*(double)sampleRate/fft.Length;var energy=fft[k].Magnitude/fft.Length*4; if(freq<250) bass+=energy*energy;else if(freq<4000)mid+=energy*energy;else treble+=energy*energy;}
  bass=Math.Sqrt(bass); mid=Math.Sqrt(mid);treble=Math.Sqrt(treble);
  for(var b=0;b<32;b++) {double low=30*Math.Pow(600,b/32.0),high=30*Math.Pow(600,(b+1)/32.0); int lo=Math.Max(1,(int)(low*fft.Length/sampleRate)),hi=Math.Min(fft.Length/2-1,Math.Max(lo,(int)(high*fft.Length/sampleRate))); double mag=0;for(var k=lo;k<=hi;k++)mag=Math.Max(mag,fft[k].Magnitude/fft.Length*4);spectrum[b]=Math.Clamp((20*Math.Log10(Math.Max(1e-7,mag))+75)/75,0,1);}
  var now=DateTime.UtcNow; bool beat=bass>Math.Max(.004,average*Sensitivity)&&bass>previousBass*1.03&&(now-lastBeat).TotalSeconds>.23;
  average=average*.965+bass*.035; previousBass=bass;
  if(beat) {var gap=(now-lastBeat).TotalSeconds;if(gap>=.25&&gap<=1.4) {intervals.Enqueue(gap);while(intervals.Count>10)intervals.Dequeue();}else intervals.Clear();lastBeat=now;beats++;}
  double bpm=intervals.Count>=4&&(now-lastBeat).TotalSeconds<3 ? 60/intervals.Order().ElementAt(intervals.Count/2):0;
  return new(Math.Sqrt(sum/ring.Length),peak,bass,mid,treble,bpm,beat,beats,spectrum,now);
 }
 public static void Transform(Complex[] data)
 {
  int n=data.Length;if(n<2||(n&(n-1))!=0)throw new ArgumentException("FFT length must be a power of two.");
  for(int i=1,j=0;i<n;i++){int bit=n>>1;for(; (j&bit)!=0;bit>>=1)j^=bit;j^=bit;if(i<j)(data[i],data[j])=(data[j],data[i]);}
  for(var length=2;length<=n;length*=2){var root=Complex.FromPolarCoordinates(1,-2*Math.PI/length);for(var i=0;i<n;i+=length){var w=Complex.One;for(var j=0;j<length/2;j++){var u=data[i+j];var v=data[i+j+length/2]*w;data[i+j]=u+v;data[i+j+length/2]=u-v;w*=root;}}}
 }
}
