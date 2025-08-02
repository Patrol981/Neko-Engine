using SDL3;
using static SDL3.SDL3;

namespace Dwarf.Audio;

public class AudioSystem {
  private SDL_AudioDeviceID _audioDevice;

  public static AudioSystem? Instance { get; private set; }

  public AudioSystem() {
    Instance ??= this;
    Init();
  }

  public unsafe void Init() {
    var spec = new SDL_AudioSpec {
      freq = 44100,
      format = SDL_AudioFormat.S32,
      channels = 2,
    };

    _audioDevice = SDL_OpenAudioDevice(_audioDevice, &spec);
  }

  public static void Play() {

  }

  public static void Stop() {

  }
}