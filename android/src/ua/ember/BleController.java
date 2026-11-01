package ua.ember;
import android.bluetooth.*;
import android.content.Context;
import android.os.*;
import org.json.*;
import java.util.*;

public final class BleController extends BluetoothGattCallback {
 public interface Listener {void changed(boolean connected,String message);void written(long latency,int sent);}
 private final Context context;private final Handler handler;private final Listener listener;
 private BluetoothGatt gatt;private BluetoothGattCharacteristic characteristic;private boolean ready,busy,lastWasFrame;private int sent;private long began;
 private String name="",address="";private DeviceProfiles.Adapter adapter=new DeviceProfiles.Adapter();
 private final ConnectionRetry recovery=new ConnectionRetry();private BluetoothAdapter radio;
 private final ArrayDeque<byte[]> queue=new ArrayDeque<>();private byte[] latest,lastFrame;
 public BleController(Context context,Handler handler,Listener listener){this.context=context;this.handler=handler;this.listener=listener;}
 public boolean connected(){return ready;}public int profile(){return adapter.profile;}public String name(){return name;}public String address(){return address;}
 public void connect(String target){connect(target,"");}
 public void connect(String target,String hint){if(!target.matches("(?i)[0-9a-f]{2}(:[0-9a-f]{2}){5}"))throw new IllegalArgumentException("Некоректний MAC.");close();address=target;name=hint==null?"":hint;adapter=new DeviceProfiles.Adapter();BluetoothManager manager=(BluetoothManager)context.getSystemService(Context.BLUETOOTH_SERVICE);BluetoothAdapter bluetooth=manager.getAdapter();if(bluetooth==null||!bluetooth.isEnabled())throw new IllegalStateException("Увімкни Bluetooth.");radio=bluetooth;recovery.start(SystemClock.elapsedRealtime());startAttempt();}
 private final Runnable retryConnect=this::startAttempt;
 private final Runnable connectTimeout=this::connectTimedOut;
 private void startAttempt(){
  releaseGatt();if(!recovery.next(SystemClock.elapsedRealtime())){failed("Не вдалося підключитися.");return;}
  try{if(radio==null||!radio.isEnabled())throw new IllegalStateException("Увімкни Bluetooth.");
   gatt=radio.getRemoteDevice(address).connectGatt(context,false,this,BluetoothDevice.TRANSPORT_LE);
   listener.changed(false,"Підключення до стрічки · спроба "+recovery.attempts()+"/3…");
   if(gatt==null){retry(133,"Android не відкрив пристрій.");return;}
   handler.postDelayed(connectTimeout,recovery.timeout(SystemClock.elapsedRealtime()));
  }catch(Exception e){failed(e.getMessage());}
 }
 private void failed(String message){close();listener.changed(false,message+" Перевір живлення стрічки. Якщо вона увімкнена, звільни її від іншої програми та повтори.");}
 private void retry(int status,String message){
  if(recovery.canRetry(status,SystemClock.elapsedRealtime())){releaseGatt();listener.changed(false,message+" Повторюємо підключення…");handler.postDelayed(retryConnect,600);}
  else failed(message);
 }
 private void connectTimedOut(){if(!ready)retry(8,"Тайм-аут підключення.");}
 @Override public void onConnectionStateChange(BluetoothGatt source,int status,int state){handler.post(()->{if(source!=gatt)return;if(status==BluetoothGatt.GATT_SUCCESS&&state==BluetoothProfile.STATE_CONNECTED){source.requestConnectionPriority(BluetoothGatt.CONNECTION_PRIORITY_HIGH);if(!source.discoverServices())retry(133,"Не вдалося отримати служби стрічки.");}else if(state==BluetoothProfile.STATE_DISCONNECTED||status!=BluetoothGatt.GATT_SUCCESS){if(!ready)retry(status,"Bluetooth недоступний · код "+status);else{close();listener.changed(false,"Bluetooth від’єднано · код "+status);}}});}
 @Override public void onServicesDiscovered(BluetoothGatt source,int status){handler.post(()->{if(source!=gatt)return;try{
  if(status!=BluetoothGatt.GATT_SUCCESS){retry(status,"Не вдалося прочитати служби стрічки · код "+status);return;}
  String[][] pairs={{"fff0","fff3"},{"ffe5","ffe9"},{"ffe0","ffe1"},{"ffd5","ffd9"}};
  for(String[] pair:pairs){BluetoothGattService service=source.getService(uuid(pair[0]));if(service==null)continue;BluetoothGattCharacteristic c=service.getCharacteristic(uuid(pair[1]));if(c!=null&&(c.getProperties()&(BluetoothGattCharacteristic.PROPERTY_WRITE|BluetoothGattCharacteristic.PROPERTY_WRITE_NO_RESPONSE))!=0){characteristic=c;break;}}
  if(characteristic==null)throw new IllegalStateException("Ця Bluetooth-стрічка поки не підтримується.");
  String liveName=source.getDevice().getName();if(liveName!=null&&!liveName.isEmpty())name=liveName;if(name==null||name.isEmpty()){JSONArray saved=SavedDevices.load(context);for(int i=0;i<saved.length();i++){JSONObject row=saved.optJSONObject(i);if(row!=null&&address.equals(row.optString("address"))){name=row.optString("name");break;}}}
  adapter.profile=DeviceProfiles.detect(name,characteristic.getUuid().toString());handler.removeCallbacks(connectTimeout);recovery.finish();ready=true;sent=0;listener.changed(true,"Підключено · "+DeviceProfiles.name(adapter.profile));
 }catch(Exception e){close();listener.changed(false,e.getMessage());}});}
 private static UUID uuid(String n){return UUID.fromString("0000"+n+"-0000-1000-8000-00805f9b34fb");}
 public void write(byte[] packet){if(!ready)throw new IllegalStateException("Підключи стрічку.");byte[] copy=adapter.convert(packet).clone();if(copy.length==0)return;if(queue.size()>=32)throw new IllegalStateException("Черга команд переповнена.");latest=lastFrame=null;queue.add(copy);drain();}
 public void writeLatest(byte[] packet){if(!ready)return;byte[] copy=adapter.convert(packet).clone();if(Arrays.equals(copy,lastFrame)){latest=null;return;}latest=copy;drain();}
 private final Runnable nextWrite=this::drain;
 private final Runnable writeTimeout=this::writeTimedOut;private void writeTimedOut(){if(busy){close();listener.changed(false,"Тайм-аут Bluetooth-запису. Перепідключи стрічку.");}}
 @SuppressWarnings("deprecation") private void drain(){if(!ready||busy||(queue.isEmpty()&&latest==null))return;boolean frame=queue.isEmpty();int gap=frame&&lastWasFrame?(adapter.profile==0?30:15):(adapter.profile==2?300:adapter.profile==0?100:15);long wait=gap-(SystemClock.elapsedRealtime()-began);if(wait>0){handler.removeCallbacks(nextWrite);handler.postDelayed(nextWrite,wait);return;}
  byte[] data=frame?latest:queue.removeFirst();if(frame){latest=null;lastFrame=data;}
  int type=(characteristic.getProperties()&BluetoothGattCharacteristic.PROPERTY_WRITE_NO_RESPONSE)!=0?BluetoothGattCharacteristic.WRITE_TYPE_NO_RESPONSE:BluetoothGattCharacteristic.WRITE_TYPE_DEFAULT;boolean accepted;busy=true;lastWasFrame=frame;began=SystemClock.elapsedRealtime();
  if(Build.VERSION.SDK_INT>=33)accepted=gatt.writeCharacteristic(characteristic,data,type)==BluetoothStatusCodes.SUCCESS;else{characteristic.setWriteType(type);characteristic.setValue(data);accepted=gatt.writeCharacteristic(characteristic);}
  if(!accepted){busy=false;lastFrame=null;queue.clear();latest=null;listener.changed(true,"Android не прийняв команду. Повтори дію.");return;}handler.postDelayed(writeTimeout,3000);
 }
 @Override public void onCharacteristicWrite(BluetoothGatt source,BluetoothGattCharacteristic c,int status){handler.post(()->{if(source!=gatt||c!=characteristic||!busy)return;handler.removeCallbacks(writeTimeout);busy=false;if(status==BluetoothGatt.GATT_SUCCESS){sent++;listener.written(SystemClock.elapsedRealtime()-began,sent);drain();}else{queue.clear();latest=lastFrame=null;listener.changed(ready,"Помилка Bluetooth-запису · код "+status);}});}
 public void whenDrained(Runnable done){awaitDrain(done,SystemClock.elapsedRealtime()+4000);}
 private void awaitDrain(Runnable done,long deadline){if(!busy&&queue.isEmpty()&&latest==null){done.run();return;}if(SystemClock.elapsedRealtime()>=deadline){close();listener.changed(false,"Запис резервного ефекту не підтверджено.");done.run();return;}handler.postDelayed(()->awaitDrain(done,deadline),15);}
 public void close(){recovery.finish();handler.removeCallbacks(retryConnect);releaseGatt();}
 private void releaseGatt(){handler.removeCallbacks(connectTimeout);handler.removeCallbacks(writeTimeout);handler.removeCallbacks(nextWrite);ready=busy=lastWasFrame=false;began=0;characteristic=null;latest=lastFrame=null;queue.clear();BluetoothGatt old=gatt;gatt=null;if(old!=null){try{old.disconnect();}catch(Exception ignored){}old.close();}}
}
