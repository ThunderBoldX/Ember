import ua.ember.ConnectionRetry;
public final class ConnectionRetryTest {
 private static int passed;
 private static void check(boolean ok,String text){if(!ok)throw new AssertionError(text);passed++;System.out.println("PASS "+text);}
 public static void main(String[] args){
  ConnectionRetry retry=new ConnectionRetry();retry.start(1000);
  check(retry.next(1000)&&retry.attempts()==1,"first connection starts immediately");
  check(retry.canRetry(133,11000)&&retry.next(11600),"transient Android GATT error can recover");
  check(retry.canRetry(8,21600)&&retry.next(22200),"timeout receives final attempt");
  check(retry.timeout(22200)==8800&&!retry.canRetry(133,23000)&&!retry.next(23000),"three attempts stay within total deadline");
  retry.start(0);retry.next(0);
  check(!retry.canRetry(5,500)&&!retry.canRetry(15,500),"authentication and encryption failures do not retry");
  retry.finish();check(!retry.canRetry(133,500)&&!retry.next(500),"success or explicit close prevents reconnect");
  retry.start(0);retry.next(0);check(!retry.canRetry(133,29000)&&!retry.next(30000),"expired connection has no additional attempts");
  retry.start(40000);check(retry.next(40000)&&retry.attempts()==1,"new user connection resets previous deadline");
  System.out.println("TOTAL "+passed+" CONNECTION PASS");
 }
}
