package ua.ember;
import android.content.*;
import org.json.*;
public final class SavedDevices {
 public static JSONArray load(Context c){try{return new JSONArray(c.getSharedPreferences("ember",0).getString("devices","[]"));}catch(Exception e){return new JSONArray();}}
 public static void remember(Context c,String address,String name,int profile){try{JSONArray old=load(c),list=new JSONArray();list.put(new JSONObject().put("address",address).put("name",name==null||name.isEmpty()?"RGB стрічка":name).put("profile",profile).put("saved",true).put("lastConnected",System.currentTimeMillis()));for(int i=0;i<old.length()&&list.length()<30;i++){JSONObject row=old.getJSONObject(i);if(!row.optString("address").equals(address))list.put(row);}c.getSharedPreferences("ember",0).edit().putString("devices",list.toString()).apply();}catch(JSONException ignored){}}
 public static void forget(Context c,String address){JSONArray old=load(c),list=new JSONArray();for(int i=0;i<old.length();i++){JSONObject row=old.optJSONObject(i);if(row!=null&&!row.optString("address").equals(address))list.put(row);}c.getSharedPreferences("ember",0).edit().putString("devices",list.toString()).apply();}
}
