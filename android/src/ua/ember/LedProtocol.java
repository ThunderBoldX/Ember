package ua.ember;
public final class LedProtocol {
 private static byte[] packet(int...data){byte[] b=new byte[data.length];for(int i=0;i<data.length;i++)b[i]=(byte)data[i];return b;}
 private static int check(int n,int max){if(n<0||n>max)throw new IllegalArgumentException("Значення поза діапазоном.");return n;}
 public static byte[] color(int rgb){return packet(126,7,5,3,(rgb>>16)&255,(rgb>>8)&255,rgb&255,16,239);}
 public static byte[] brightness(int n,int p){check(n,100);return p==0?packet(126,4,1,n,1,255,2,1,239):packet(126,p==2?4:7,1,n,255,255,255,0,239);}
 public static byte[] speed(int n,int p){return packet(126,p==0?7:4,2,check(n,100),255,255,255,0,239);}
 public static byte[] mode(int n,int p){check(n,p==1?212:156);return p==1?packet(126,7,3,n,255,255,255,0,239):packet(126,p==0?7:5,3,n,3,255,255,0,239);}
 public static byte[] power(boolean on,int p){return p==2?packet(126,4,4,on?240:0,0,on?1:0,255,0,239):packet(126,7,4,on?255:0,0,on?1:0,2,1,239);}
 public static byte[] mic(boolean on){return packet(126,4,7,on?1:0,255,255,255,0,239);}
 public static byte[] micMode(int n){return packet(126,5,3,128+check(n,7),4,255,255,0,239);}
 public static byte[] sensitivity(int n){return packet(126,4,6,check(n,100),255,255,255,0,239);}
}
