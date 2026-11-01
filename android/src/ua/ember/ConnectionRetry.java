package ua.ember;

// Connection attempts stop after success, cancellation, permission errors, or 30 seconds.
public final class ConnectionRetry {
 private int attempts;private long deadline;private boolean active;
 public void start(long now){attempts=0;deadline=now+30000;active=true;}
 public boolean next(long now){if(!active||attempts>=3||deadline-now<1000)return false;attempts++;return true;}
 public boolean canRetry(int status,long now){return active&&attempts<3&&deadline-now>1600&&status!=5&&status!=15;}
 public long timeout(long now){return Math.max(1,Math.min(10000,deadline-now));}
 public int attempts(){return attempts;}
 public void finish(){active=false;}
}
