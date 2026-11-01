package ua.ember;
public final class MicPresets {
 public static final int[] MODES={0,1,2,3,4,5,6,7,0,1,2,3,4,5,6,7};
 public static final int[] SENSITIVITY={35,30,45,50,40,35,45,50,65,60,75,80,70,65,75,80};
 public static void check(int id){if(id<0||id>=16)throw new IllegalArgumentException("Невідомий звуковий пресет.");}
}
