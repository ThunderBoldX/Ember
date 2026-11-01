using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace Ember;

public sealed class ColorWheel : FrameworkElement
{
 private BitmapSource? disk;private double hue,saturation=1,value=1;
 public event Action<Rgb>? Changed,Committed;
 public Rgb Color=>Rgb.Hsv(hue,saturation,value);
 public ColorWheel(){Width=190;Height=190;Focusable=true;Cursor=Cursors.Cross;ToolTip="Колір: стрілки ← →. Насиченість: ↑ ↓.";}
 public void SetColor(Rgb color){var hsv=color.ToHsv();if(hsv.Saturation>0)hue=hsv.Hue;saturation=hsv.Saturation;value=hsv.Value;InvalidateVisual();}
 public void SetValue(double v){value=Math.Clamp(v,0,1);Changed?.Invoke(Color);InvalidateVisual();}
 public static (double Hue,double Saturation) Polar(double x,double y,double radius)=>((Math.Atan2(y,x)/(Math.PI*2)+1)%1,Math.Clamp(Math.Sqrt(x*x+y*y)/radius,0,1));
 protected override void OnRender(DrawingContext context)
 {
  if(disk==null){const int n=240;var pixels=new byte[n*n*4];for(int y=0;y<n;y++)for(int x=0;x<n;x++){double dx=x+.5-n/2.0,dy=y+.5-n/2.0;var (h,s)=Polar(dx,dy,n/2.0-2);if(dx*dx+dy*dy>Math.Pow(n/2.0-2,2))continue;var color=Rgb.Hsv(h,s);int p=(y*n+x)*4;pixels[p]=color.B;pixels[p+1]=color.G;pixels[p+2]=color.R;pixels[p+3]=255;}disk=BitmapSource.Create(n,n,96,96,PixelFormats.Bgra32,null,pixels,n*4);disk.Freeze();}
  double size=Math.Min(ActualWidth,ActualHeight),r=size/2-2;context.DrawImage(disk,new Rect((ActualWidth-size)/2,(ActualHeight-size)/2,size,size));context.DrawEllipse(null,new Pen(new SolidColorBrush(System.Windows.Media.Color.FromArgb(60,255,255,255)),1),new(ActualWidth/2,ActualHeight/2),r,r);
  var point=new Point(ActualWidth/2+Math.Cos(hue*Math.PI*2)*saturation*r,ActualHeight/2+Math.Sin(hue*Math.PI*2)*saturation*r);context.DrawEllipse(null,new Pen(Brushes.Black,4),point,6,6);context.DrawEllipse(null,new Pen(Brushes.White,2),point,6,6);
  if(IsKeyboardFocused)context.DrawEllipse(null,new Pen(new SolidColorBrush(System.Windows.Media.Color.FromRgb(255,90,31)),1),new(ActualWidth/2,ActualHeight/2),size/2,size/2);
 }
 private void Pick(Point point){(hue,saturation)=Polar(point.X-ActualWidth/2,point.Y-ActualHeight/2,Math.Min(ActualWidth,ActualHeight)/2-2);Changed?.Invoke(Color);InvalidateVisual();}
 protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e){base.OnMouseLeftButtonDown(e);Focus();CaptureMouse();Pick(e.GetPosition(this));e.Handled=true;}
 protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(IsMouseCaptured&&e.LeftButton==MouseButtonState.Pressed)Pick(e.GetPosition(this));}
 protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e){base.OnMouseLeftButtonUp(e);if(IsMouseCaptured){Pick(e.GetPosition(this));ReleaseMouseCapture();Committed?.Invoke(Color);e.Handled=true;}}
 protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);switch(e.Key){case Key.Left:hue-=.01;break;case Key.Right:hue+=.01;break;case Key.Up:saturation=Math.Min(1,saturation+.03);break;case Key.Down:saturation=Math.Max(0,saturation-.03);break;default:return;}e.Handled=true;Changed?.Invoke(Color);Committed?.Invoke(Color);InvalidateVisual();}
}
public sealed class ColorPicker : StackPanel
{
 private readonly ColorWheel wheel=new();private readonly Slider brightness=new(){Minimum=0,Maximum=100,Value=100};private readonly TextBox hex=new(){Text="#FF5A1F",Height=38};private readonly Border swatch=new(){Width=28,Height=28,CornerRadius=new(8),Margin=new(0,0,9,0)};private bool updating;
 public event Action<Rgb>? ColorChanged,ColorCommitted;
 public double WheelSize{get=>wheel.Width;set{wheel.Width=wheel.Height=value;}}
 public string Hex{get=>hex.Text.Trim().ToUpperInvariant();set{var color=Rgb.Parse(value);updating=true;wheel.SetColor(color);brightness.Value=color.ToHsv().Value*100;hex.Text=color.Hex;swatch.Background=new SolidColorBrush(System.Windows.Media.Color.FromRgb(color.R,color.G,color.B));updating=false;}}
 public ColorPicker()
 {
  wheel.HorizontalAlignment=HorizontalAlignment.Center;Children.Add(wheel);Children.Add(new TextBlock{Text="СВІТЛОТА КОЛЬОРУ",FontSize=9,Foreground=Brushes.Gray,Margin=new(0,12,0,0)});Children.Add(brightness);
  var row=new Grid{Margin=new(0,5,0,0)};row.ColumnDefinitions.Add(new(){Width=GridLength.Auto});row.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});row.Children.Add(swatch);Grid.SetColumn(hex,1);row.Children.Add(hex);Children.Add(row);
  wheel.Changed+=color=>{updating=true;hex.Text=color.Hex;swatch.Background=new SolidColorBrush(System.Windows.Media.Color.FromRgb(color.R,color.G,color.B));updating=false;ColorChanged?.Invoke(color);};wheel.Committed+=color=>ColorCommitted?.Invoke(color);
  brightness.ValueChanged+=(_,e)=>{if(!updating)wheel.SetValue(e.NewValue/100);};brightness.PreviewMouseLeftButtonUp+=(_,_)=>ColorCommitted?.Invoke(wheel.Color);brightness.KeyUp+=(_,e)=>{if(e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown)ColorCommitted?.Invoke(wheel.Color);};
  hex.TextChanged+=(_,_)=>{if(updating)return;try{var color=Rgb.Parse(Hex);updating=true;wheel.SetColor(color);brightness.Value=color.ToHsv().Value*100;swatch.Background=new SolidColorBrush(System.Windows.Media.Color.FromRgb(color.R,color.G,color.B));updating=false;hex.BorderBrush=(Brush)FindResource("Line");ColorChanged?.Invoke(color);}catch{updating=false;hex.BorderBrush=(Brush)FindResource("Ember");}};
  hex.LostKeyboardFocus+=(_,_)=>CommitHex();hex.KeyDown+=(_,e)=>{if(e.Key==Key.Enter){CommitHex();e.Handled=true;}};Hex="#FF5A1F";
 }
 private void CommitHex(){try{ColorCommitted?.Invoke(Rgb.Parse(Hex));}catch(ArgumentException){}}
}
