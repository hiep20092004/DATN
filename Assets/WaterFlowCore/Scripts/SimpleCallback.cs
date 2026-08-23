using UnityEngine;

// Global callback delegates used across WaterFlowCore modules (Tween, UI, Save, Haptic).
// Kept in the global namespace so every Assembly-CSharp module sees them without a using.
public delegate void SimpleCallback();
public delegate void SimpleFloatCallback(float value);
public delegate void SimpleIntCallback(int value);
public delegate void SimpleBoolCallback(bool value);
public delegate void SimpleStringCallback(string value);
public delegate void SimpleVector2Callback(Vector2 value);
