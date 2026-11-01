using System;
namespace Ember;
public static class WindowGeometry
{
 public static (double Width,double Height) Fit(double physicalWidth,double physicalHeight,double scaleX,double scaleY,double preferredWidth,double preferredHeight,bool fill){if(physicalWidth<=0||physicalHeight<=0||scaleX<=0||scaleY<=0)throw new ArgumentOutOfRangeException(nameof(physicalWidth));double w=Math.Max(1,physicalWidth/scaleX-24),h=Math.Max(1,physicalHeight/scaleY-24);return(fill?w:Math.Min(preferredWidth,w),fill?h:Math.Min(preferredHeight,h));}
}
