public enum GamepadButtons : int {
  Invalid = -1,

  /// <summary>
  /// Bottom face button (e.g. Xbox A button)
  /// </summary>
  /// <unmanaged>SDL_GAMEPAD_BUTTON_SOUTH</unmanaged>
  South = 0,
  /// <summary>
  /// Right face button (e.g. Xbox B button)
  /// </summary>
  /// <unmanaged>SDL_GAMEPAD_BUTTON_EAST</unmanaged>
  East = 1,
  /// <summary>
  /// Left face button (e.g. Xbox X button)
  /// </summary>
  /// <unmanaged>SDL_GAMEPAD_BUTTON_WEST</unmanaged>
  West = 2,
  /// <summary>
  /// Top face button (e.g. Xbox Y button)
  /// </summary>
  /// <unmanaged>SDL_GAMEPAD_BUTTON_NORTH</unmanaged>
  North = 3,
  /// <unmanaged>SDL_GAMEPAD_BUTTON_BACK</unmanaged>
  Back = 4,
  /// <unmanaged>SDL_GAMEPAD_BUTTON_GUIDE</unmanaged>
  Guide = 5,
  /// <unmanaged>SDL_GAMEPAD_BUTTON_START</unmanaged>
  Start = 6,
  /// <unmanaged>SDL_GAMEPAD_BUTTON_LEFT_STICK</unmanaged>
  LeftStick = 7,
  /// <unmanaged>SDL_GAMEPAD_BUTTON_RIGHT_STICK</unmanaged>
  RightStick = 8,
  /// <unmanaged>SDL_GAMEPAD_BUTTON_LEFT_SHOULDER</unmanaged>
  LeftShoulder = 9,
  /// <unmanaged>SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER</unmanaged>
  RightShoulder = 10,
  /// <unmanaged>SDL_GAMEPAD_BUTTON_DPAD_UP</unmanaged>
  DpadUp = 11,
  /// <unmanaged>SDL_GAMEPAD_BUTTON_DPAD_DOWN</unmanaged>
  DpadDown = 12,
  /// <unmanaged>SDL_GAMEPAD_BUTTON_DPAD_LEFT</unmanaged>
  DpadLeft = 13,
  /// <unmanaged>SDL_GAMEPAD_BUTTON_DPAD_RIGHT</unmanaged>
  DpadRight = 14,
  /// <summary>
  /// Additional button (e.g. Xbox Series X share button, PS5 microphone button, Nintendo Switch Pro capture button, Amazon Luna microphone button, Google Stadia capture button)
  /// </summary>
  /// <unmanaged>SDL_GAMEPAD_BUTTON_MISC1</unmanaged>
  Misc1 = 15,
  /// <summary>
  /// Upper or primary paddle, under your right hand (e.g. Xbox Elite paddle P1)
  /// </summary>
  /// <unmanaged>SDL_GAMEPAD_BUTTON_RIGHT_PADDLE1</unmanaged>
  RightPaddle1 = 16,
  /// <summary>
  /// Upper or primary paddle, under your left hand (e.g. Xbox Elite paddle P3)
  /// </summary>
  /// <unmanaged>SDL_GAMEPAD_BUTTON_LEFT_PADDLE1</unmanaged>
  LeftPaddle1 = 17,
  /// <summary>
  /// Lower or secondary paddle, under your right hand (e.g. Xbox Elite paddle P2)
  /// </summary>
  /// <unmanaged>SDL_GAMEPAD_BUTTON_RIGHT_PADDLE2</unmanaged>
  RightPaddle2 = 18,
  /// <summary>
  /// Lower or secondary paddle, under your left hand (e.g. Xbox Elite paddle P4)
  /// </summary>
  /// <unmanaged>SDL_GAMEPAD_BUTTON_LEFT_PADDLE2</unmanaged>
  LeftPaddle2 = 19,
  /// <summary>
  /// PS4/PS5 touchpad button
  /// </summary>
  /// <unmanaged>SDL_GAMEPAD_BUTTON_TOUCHPAD</unmanaged>
  Touchpad = 20,
  /// <summary>
  /// Additional button
  /// </summary>
  /// <unmanaged>SDL_GAMEPAD_BUTTON_MISC2</unmanaged>
  Misc2 = 21,
  /// <summary>
  /// Additional button
  /// </summary>
  /// <unmanaged>SDL_GAMEPAD_BUTTON_MISC3</unmanaged>
  Misc3 = 22,
  /// <summary>
  /// Additional button
  /// </summary>
  /// <unmanaged>SDL_GAMEPAD_BUTTON_MISC4</unmanaged>
  Misc4 = 23,
  /// <summary>
  /// Additional button
  /// </summary>
  /// <unmanaged>SDL_GAMEPAD_BUTTON_MISC5</unmanaged>
  Misc5 = 24,
  /// <summary>
  /// Additional button
  /// </summary>
  /// <unmanaged>SDL_GAMEPAD_BUTTON_MISC6</unmanaged>
  Misc6 = 25,
  Count = 26,
}