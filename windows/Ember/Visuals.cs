using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
namespace Ember;
public sealed class Atmosphere : FrameworkElement
{
 protected override void OnRender(DrawingContext d)
 {
  var w=ActualWidth;var h=ActualHeight;
  var glow=new RadialGradientBrush(Color.FromArgb(65,194,65,12),Color.FromArgb(0,194,65,12)){Center=new(.57,.22),GradientOrigin=new(.57,.22),RadiusX=.43,RadiusY=.37};d.DrawRectangle(glow,null,new Rect(0,0,w,h));
  var pen=new Pen(new SolidColorBrush(Color.FromArgb(5,255,255,255)),1);for(double x=12;x<w;x+=28)for(double y=12;y<h;y+=28)d.DrawEllipse(pen.Brush,null,new Point(x,y),.7,.7);
  var random=new Random(31);for(int i=0;i<44;i++)d.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)random.Next(15,60),255,176,126)),null,new Point(random.NextDouble()*w,60+random.NextDouble()*230),.6,.6);
  var geometry=new StreamGeometry();using(var c=geometry.Open()){c.BeginFigure(new Point(-w*.2,193),false,false);c.BezierTo(new Point(w*.18,13),new Point(w*.82,13),new Point(w*1.2,193),true,false);}
  for(int i=10;i>0;i--)d.DrawGeometry(null,new Pen(new SolidColorBrush(Color.FromArgb((byte)(2+i),255,90,31)),i*2),geometry);d.DrawGeometry(null,new Pen(new SolidColorBrush(Color.FromArgb(150,255,90,31)),1),geometry);
  for(int i=0;i<3;i++){var ray=new StreamGeometry();using(var c=ray.Open()){c.BeginFigure(new Point(w*(.36+i*.12),0),true,true);c.LineTo(new Point(w*(.2+i*.2),300),true,false);c.LineTo(new Point(w*(.3+i*.2),300),true,false);}d.DrawGeometry(new LinearGradientBrush(Color.FromArgb(7,255,170,110),Color.FromArgb(0,255,170,110),90),null,ray);}
  d.DrawRectangle(new RadialGradientBrush(Color.FromArgb(0,0,0,0),Color.FromArgb(110,0,0,0)){RadiusX=.7,RadiusY=.7},null,new Rect(0,0,w,h));
 }
}
public sealed class SpectrumView : FrameworkElement
{
 private double[] values=new double[32]; public void Update(double[] data){for(int i=0;i<32;i++)values[i]=Math.Max(data[i],values[i]*.82);InvalidateVisual();}
 protected override void OnRender(DrawingContext d){double w=ActualWidth,h=ActualHeight,step=w/32;var grid=new Pen(new SolidColorBrush(Color.FromArgb(10,255,255,255)),1);for(int j=1;j<4;j++)d.DrawLine(grid,new Point(0,h*j/4),new Point(w,h*j/4));for(int i=0;i<32;i++){var amount=values[i];double height=Math.Max(3,amount*h);var brush=amount>.02?new LinearGradientBrush(Color.FromRgb(255,112,55),Color.FromRgb(194,65,12),90):new LinearGradientBrush(Color.FromRgb(40,40,41),Color.FromRgb(31,31,32),90);d.DrawRoundedRectangle(brush,null,new Rect(i*step,h-height,Math.Max(2,step-4),height),3,3);}}
}
public sealed class HistoryView : FrameworkElement
{
 private readonly Queue<double> values=new();public void Push(double value){values.Enqueue(Math.Clamp(value,0,1));while(values.Count>96)values.Dequeue();InvalidateVisual();}
 protected override void OnRender(DrawingContext d)
 {
  double w=ActualWidth,h=ActualHeight;var grid=new Pen(new SolidColorBrush(Color.FromArgb(10,255,255,255)),1);for(int i=1;i<4;i++)d.DrawLine(grid,new Point(0,h*i/4),new Point(w,h*i/4));var arr=values.ToArray();if(arr.Length<2){d.DrawLine(new Pen(new SolidColorBrush(Color.FromRgb(48,35,29)),1),new Point(0,h-3),new Point(w,h-3));return;}
  var line=new StreamGeometry();using(var c=line.Open()){c.BeginFigure(new Point(0,h-arr[0]*(h-5)-3),false,false);for(int i=1;i<arr.Length;i++)c.LineTo(new Point(i*w/(arr.Length-1),h-arr[i]*(h-5)-3),true,false);}
  var fill=new StreamGeometry();using(var c=fill.Open()){c.BeginFigure(new Point(0,h),true,true);for(int i=0;i<arr.Length;i++)c.LineTo(new Point(i*w/(arr.Length-1),h-arr[i]*(h-5)-3),true,false);c.LineTo(new Point(w,h),true,false);}
  d.DrawGeometry(new LinearGradientBrush(Color.FromArgb(65,255,90,31),Colors.Transparent,90),null,fill);d.DrawGeometry(null,new Pen(new SolidColorBrush(Color.FromArgb(18,255,90,31)),6),line);d.DrawGeometry(null,new Pen(new SolidColorBrush(Color.FromRgb(255,90,31)),1.7),line);d.DrawEllipse(new SolidColorBrush(Color.FromRgb(255,90,31)),null,new Point(w,h-arr[^1]*(h-5)-3),2.5,2.5);
 }
}
public sealed class RingMeter : FrameworkElement
{
 public static readonly DependencyProperty ValueProperty=DependencyProperty.Register(nameof(Value),typeof(double),typeof(RingMeter),new FrameworkPropertyMetadata(0.0,FrameworkPropertyMetadataOptions.AffectsRender));public double Value{get=>(double)GetValue(ValueProperty);set=>SetValue(ValueProperty,value);}
 protected override void OnRender(DrawingContext d){var center=new Point(ActualWidth/2,ActualHeight/2);double r=Math.Min(ActualWidth,ActualHeight)/2-7;d.DrawEllipse(null,new Pen(new SolidColorBrush(Color.FromRgb(40,40,41)),6),center,r,r);double value=Math.Clamp(Value,.0001,.9999);var end=new Point(center.X+r*Math.Sin(value*Math.PI*2),center.Y-r*Math.Cos(value*Math.PI*2));var g=new StreamGeometry();using(var c=g.Open()){c.BeginFigure(new Point(center.X,center.Y-r),false,false);c.ArcTo(end,new Size(r,r),0,value>.5,SweepDirection.Clockwise,true,false);}d.DrawGeometry(null,new Pen(new SolidColorBrush(Color.FromArgb(25,255,90,31)),12),g);d.DrawGeometry(null,new Pen(new SolidColorBrush(Color.FromRgb(255,90,31)),6){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},g);}
}
