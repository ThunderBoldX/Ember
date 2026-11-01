"""Exercise the production Android BLE queue against a deterministic GATT fixture."""
from pathlib import Path
import os,shutil,subprocess,uuid

root=Path(__file__).resolve().parents[1]
stage=root/'build'/('delivery-'+uuid.uuid4().hex)
stage.mkdir(parents=True)
sources={
'android/os/SystemClock.java':'''package android.os;public class SystemClock{public static long now=100000;public static long elapsedRealtime(){return now;}}''',
'android/os/Build.java':'''package android.os;public class Build{public static class VERSION{public static int SDK_INT=35;}}''',
'android/os/Handler.java':'''package android.os;import java.util.*;public class Handler{
 static class Event{long at,id;Runnable r;Event(long a,long i,Runnable v){at=a;id=i;r=v;}}
 static long seq;static PriorityQueue<Event> q=new PriorityQueue<>(Comparator.<Event>comparingLong(e->e.at).thenComparingLong(e->e.id));
 public void post(Runnable r){postDelayed(r,0);}public void postDelayed(Runnable r,long delay){q.add(new Event(SystemClock.now+delay,seq++,r));}
 public void removeCallbacks(Runnable r){q.removeIf(e->e.r==r);}public static void runFor(long ms){long end=SystemClock.now+ms;while(!q.isEmpty()&&q.peek().at<=end){Event e=q.remove();SystemClock.now=e.at;e.r.run();}SystemClock.now=end;}}
''',
'android/content/Context.java':'''package android.content;public class Context{public static final String BLUETOOTH_SERVICE="bluetooth";public Object getSystemService(String n){return new android.bluetooth.BluetoothManager();}}''',
'android/bluetooth/BluetoothManager.java':'''package android.bluetooth;public class BluetoothManager{public BluetoothAdapter getAdapter(){return new BluetoothAdapter();}}''',
'android/bluetooth/BluetoothAdapter.java':'''package android.bluetooth;public class BluetoothAdapter{public boolean isEnabled(){return true;}public BluetoothDevice getRemoteDevice(String a){return new BluetoothDevice();}}''',
'android/bluetooth/BluetoothDevice.java':'''package android.bluetooth;import android.os.*;public class BluetoothDevice{public static final int TRANSPORT_LE=2;public String getName(){return "ELK-BLEDDM";}public BluetoothGatt connectGatt(android.content.Context c,boolean a,BluetoothGattCallback cb,int t){BluetoothGatt g=new BluetoothGatt(this,cb);new Handler().postDelayed(()->cb.onConnectionStateChange(g,0,2),1);return g;}}''',
'android/bluetooth/BluetoothProfile.java':'''package android.bluetooth;public class BluetoothProfile{public static final int STATE_CONNECTED=2,STATE_DISCONNECTED=0;}''',
'android/bluetooth/BluetoothStatusCodes.java':'''package android.bluetooth;public class BluetoothStatusCodes{public static final int SUCCESS=0;}''',
'android/bluetooth/BluetoothGattCallback.java':'''package android.bluetooth;public class BluetoothGattCallback{public void onConnectionStateChange(BluetoothGatt g,int s,int state){}public void onServicesDiscovered(BluetoothGatt g,int s){}public void onCharacteristicWrite(BluetoothGatt g,BluetoothGattCharacteristic c,int s){}}''',
'android/bluetooth/BluetoothGattCharacteristic.java':'''package android.bluetooth;import java.util.*;public class BluetoothGattCharacteristic{public static final int PROPERTY_WRITE=8,PROPERTY_WRITE_NO_RESPONSE=4,WRITE_TYPE_NO_RESPONSE=1,WRITE_TYPE_DEFAULT=2;byte[] value;public int getProperties(){return 4;}public UUID getUuid(){return UUID.fromString("0000fff3-0000-1000-8000-00805f9b34fb");}public void setWriteType(int t){}public void setValue(byte[] b){value=b;}}''',
'android/bluetooth/BluetoothGattService.java':'''package android.bluetooth;import java.util.*;public class BluetoothGattService{BluetoothGattCharacteristic c=new BluetoothGattCharacteristic();public BluetoothGattCharacteristic getCharacteristic(UUID u){return c;}}''',
'android/bluetooth/BluetoothGatt.java':'''package android.bluetooth;import android.os.*;import java.util.*;public class BluetoothGatt{
 public static final int GATT_SUCCESS=0,CONNECTION_PRIORITY_HIGH=1;BluetoothDevice device;BluetoothGattCallback cb;BluetoothGattService service=new BluetoothGattService();
 public BluetoothGatt(BluetoothDevice d,BluetoothGattCallback c){device=d;cb=c;}public boolean requestConnectionPriority(int p){return true;}
 public boolean discoverServices(){new Handler().postDelayed(()->cb.onServicesDiscovered(this,0),1);return true;}
 public BluetoothGattService getService(UUID id){return id.toString().startsWith("0000fff0")?service:null;}public BluetoothDevice getDevice(){return device;}
 public int writeCharacteristic(BluetoothGattCharacteristic c,byte[] bytes,int t){Fixture.accept(bytes);new Handler().postDelayed(()->cb.onCharacteristicWrite(this,c,0),1);return 0;}
 public boolean writeCharacteristic(BluetoothGattCharacteristic c){writeCharacteristic(c,c.value,1);return true;}public void disconnect(){}public void close(){}
}''',
'android/bluetooth/Fixture.java':'''package android.bluetooth;import android.os.*;import java.util.*;public class Fixture{
 public static List<Long> times=new ArrayList<>();public static int dropped,mode=-1;static boolean previousRgb,mic=true;static long last=-10000;
 public static void accept(byte[] b){long now=SystemClock.now;boolean rgb=b[2]==5;boolean ok=now-last>=(rgb&&previousRgb?10:250);times.add(now);if(!ok)dropped++;else{if(b[2]==7)mic=b[3]!=0;if(b[2]==3&&(b[3]&255)>=135){if(!mic)mode=b[3]&255;else dropped++;}if(rgb&&!mic)mode=-1;}last=now;previousRgb=rgb;}}
''',
'org/json/JSONObject.java':'''package org.json;public class JSONObject{public String optString(String k){return "";}}''',
'org/json/JSONArray.java':'''package org.json;public class JSONArray{public int length(){return 0;}public JSONObject optJSONObject(int i){return null;}}''',
'ua/ember/SavedDevices.java':'''package ua.ember;public class SavedDevices{public static org.json.JSONArray load(android.content.Context c){return new org.json.JSONArray();}}''',
'DeliveryTest.java':'''import ua.ember.*;import android.bluetooth.*;import android.os.*;public class DeliveryTest{
 static int checks;static void check(boolean ok,String text){if(!ok)throw new AssertionError(text);checks++;System.out.println("PASS "+text);}
 public static void main(String[] args){
  BleController ble=new BleController(new android.content.Context(),new Handler(),new BleController.Listener(){public void changed(boolean b,String s){}public void written(long l,int n){}});
  ble.connect("AA:BB:CC:DD:EE:FF");Handler.runFor(5);check(ble.connected()&&ble.profile()==2,"Android fixture connects through real service discovery");
  ble.write(LedProtocol.mic(false));ble.write(LedProtocol.mode(135,2));ble.write(LedProtocol.brightness(70,2));ble.write(LedProtocol.speed(50,2));Handler.runFor(1200);
  check(Fixture.mode==135&&Fixture.dropped==0&&Fixture.times.size()==4,"single autonomous selection survives firmware that drops rapid controls");
  ble.writeLatest(LedProtocol.color(0x123456));Handler.runFor(350);int start=Fixture.times.size();long began=SystemClock.elapsedRealtime();
  for(int i=0;i<8;i++){ble.writeLatest(LedProtocol.color(0x223300+i));Handler.runFor(20);}
  check(Fixture.times.size()-start==8&&SystemClock.elapsedRealtime()-began<300,"Android music retains fast frame delivery");
  ble.writeLatest(LedProtocol.color(0x334455));ble.write(LedProtocol.mic(false));ble.write(LedProtocol.mode(136,2));ble.write(LedProtocol.speed(40,2));Handler.runFor(1200);
  check(Fixture.mode==136&&Fixture.dropped==0,"switching from music to autonomous mode does not lose controls");
  ble.write(LedProtocol.mode(137,2));ble.write(LedProtocol.mode(138,2));ble.close();int count=Fixture.times.size();Handler.runFor(5000);
  check(!ble.connected()&&Fixture.times.size()==count,"explicit close cancels pending writes");System.out.println("TOTAL "+checks+" DELIVERY PASS");
 }}'''
}
paths=[]
for name,text in sources.items():
 p=stage/name;p.parent.mkdir(parents=True,exist_ok=True);p.write_text(text,encoding='utf-8');paths.append(str(p))
paths += [str(root/'src/ua/ember'/name) for name in ['BleController.java','LedProtocol.java','ConnectionRetry.java','DeviceProfiles.java']]
java=Path(os.environ['JAVA_HOME'])/'bin' if os.environ.get('JAVA_HOME') else Path(shutil.which('javac')).parent
subprocess.run([str(java/'javac.exe'),'-encoding','UTF-8','--release','8','-d',str(stage/'classes')]+paths,check=True)
subprocess.run([str(java/'java.exe'),'-cp',str(stage/'classes'),'DeliveryTest'],check=True)
