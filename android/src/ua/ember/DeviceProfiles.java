package ua.ember;
import java.util.Locale;
public final class DeviceProfiles {
 public static int detect(String name,String uuid){name=name==null?"":name.toUpperCase(Locale.ROOT);uuid=uuid.toLowerCase(Locale.ROOT);if(uuid.startsWith("0000ffd9"))return 3;if(!uuid.startsWith("0000fff3")&&!uuid.startsWith("0000ffe9")&&!uuid.startsWith("0000ffe1"))throw new IllegalArgumentException("Ця стрічка поки не підтримується.");if(name.contains("BLEDOB"))return 0;if(name.contains("MELK"))return 1;if(name.contains("BLEDOM")||name.contains("BLEDDM"))return 2;throw new IllegalArgumentException("Не вдалося визначити протокол. Потрібна Bluetooth-назва або модель стрічки.");}
 public static String name(int p){return new String[]{"ELK-BLEDOB","MELK","ELK-BLEDOM / BLEDDM","Triones / HappyLighting"}[p];}
 public static boolean hasMicrophone(int p){return p>=0&&p<=2;}
 public static int mapMode(int id){int[] modes={0x31,0x38,0x34,0x25,0x26,0x27,0x28,0x29,0x2a,0x2b,0x2c,0x2d,0x2e,0x2f,0x30};if(id<135||id>149)throw new IllegalArgumentException("Непідтримуваний режим Triones.");return modes[id-135];}
 public static final class Adapter {
  public int profile=2;private int brightness=100,speed=50,mode=-1,r=255,g=90,b=31;
  public byte[] convert(byte[] p){if(profile!=3)return p;if(p.length!=9||(p[0]&255)!=126||(p[8]&255)!=239)throw new IllegalArgumentException("Невідома команда.");switch(p[2]){case 5:r=p[4]&255;g=p[5]&255;b=p[6]&255;mode=-1;return color();case 1:brightness=p[3]&255;return mode<0?color():mode();case 2:speed=p[3]&255;return mode<0?color():mode();case 3:mode=mapMode(p[3]&255);return mode();case 4:return new byte[]{(byte)0xcc,(byte)((p[3]&255)>0?0x23:0x24),0x33};case 7:if(p[3]==0)return new byte[0];}throw new IllegalArgumentException("Звуковий сенсор цієї стрічки не підтримується.");}
  private byte[] color(){return new byte[]{0x56,(byte)(r*brightness/100),(byte)(g*brightness/100),(byte)(b*brightness/100),0,(byte)0xf0,(byte)0xaa};}
  private byte[] mode(){return new byte[]{(byte)0xbb,(byte)mode,(byte)(255-speed*254/100),0x44};}
 }
}
