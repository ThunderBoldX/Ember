using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
namespace Ember;
public static class WindowFit
{
 public static void Attach(Window window,bool fill=false){WindowChrome.SetWindowChrome(window,new WindowChrome{CaptionHeight=0,ResizeBorderThickness=new Thickness(6),GlassFrameThickness=new Thickness(0),CornerRadius=new CornerRadius(0),UseAeroCaptionButtons=false,NonClientFrameEdges=NonClientFrameEdges.None});window.SourceInitialized+=(_,_)=>{
  var handle=new WindowInteropHelper(window).Handle;try{int dark=1;DwmSetWindowAttribute(handle,20,ref dark,4);int noBorder=unchecked((int)0xfffffffe);DwmSetWindowAttribute(handle,34,ref noBorder,4);}catch{}var screen=System.Windows.Forms.Screen.FromHandle(handle);var area=screen.WorkingArea;var matrix=PresentationSource.FromVisual(window)?.CompositionTarget?.TransformFromDevice??Matrix.Identity;
  var origin=matrix.Transform(new Point(area.X,area.Y));var size=matrix.Transform(new Vector(area.Width,area.Height));var initial=WindowGeometry.Fit(area.Width,area.Height,1/matrix.M11,1/matrix.M22,window.Width,window.Height,fill);double width=Math.Max(1,size.X-24),height=Math.Max(1,size.Y-24);
  window.MinWidth=Math.Min(window.MinWidth,width);window.MinHeight=Math.Min(window.MinHeight,height);window.Width=initial.Width;window.Height=initial.Height;window.WindowStartupLocation=WindowStartupLocation.Manual;window.Left=origin.X+(size.X-window.Width)/2;window.Top=origin.Y+(size.Y-window.Height)/2;
  HwndSource.FromHwnd(handle)?.AddHook(MaximizeHook);
 };}
 private static IntPtr MaximizeHook(IntPtr hwnd,int msg,IntPtr wparam,IntPtr lparam,ref bool handled){if(msg==0x24){var monitor=MonitorFromWindow(hwnd,2);var info=new MONITORINFO{Size=Marshal.SizeOf<MONITORINFO>()};if(GetMonitorInfo(monitor,ref info)){var values=Marshal.PtrToStructure<MINMAXINFO>(lparam);values.MaxPosition=new(){X=info.Work.Left-info.Bounds.Left,Y=info.Work.Top-info.Bounds.Top};values.MaxSize=new(){X=info.Work.Right-info.Work.Left,Y=info.Work.Bottom-info.Work.Top};Marshal.StructureToPtr(values,lparam,false);handled=true;}}return IntPtr.Zero;}
 [StructLayout(LayoutKind.Sequential)] private struct POINT{public int X,Y;}
 [StructLayout(LayoutKind.Sequential)] private struct RECT{public int Left,Top,Right,Bottom;}
 [StructLayout(LayoutKind.Sequential)] private struct MINMAXINFO{public POINT Reserved,MaxSize,MaxPosition,MinTrackSize,MaxTrackSize;}
 [StructLayout(LayoutKind.Sequential)] private struct MONITORINFO{public int Size;public RECT Bounds,Work;public uint Flags;}
 [DllImport("dwmapi.dll")]private static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
 [DllImport("user32.dll")]private static extern IntPtr MonitorFromWindow(IntPtr hwnd,uint flags);
 [DllImport("user32.dll",CharSet=CharSet.Auto)]private static extern bool GetMonitorInfo(IntPtr monitor,ref MONITORINFO info);
}
