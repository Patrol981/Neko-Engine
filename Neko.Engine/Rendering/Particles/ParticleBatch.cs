using System.Numerics;
using Neko.AbstractionLayer;

namespace Neko.Rendering.Particles;

public class ParticleBatch {
  private List<Particle> _particles = [];

  internal List<Particle> Particles => _particles;

  public ParticleBatch() {

  }

  protected ParticleBatch(List<Particle> particles) {
    _particles = particles;
  }

  public class Builder(Application app) {
    private readonly Application _app = app;
    private List<Particle> _particles = [];
    private ParticlePropagationConfig _particleConfig;
    private ITexture? _particleTexture;

    public struct ParticlePropagationConfig {
      public Vector3 VelocityMin;
      public Vector3 VelocityMax;
      public Vector3 PositionMin;
      public Vector3 PositionMax;
      public float GravityEffectMin;
      public float GravityEffectMax;
      public float LengthMin;
      public float LengthMax;
      public float ScaleMin;
      public float ScaleMax;
      public float RotationMin;
      public float RotationMax;
    }

    public Builder Configure(ParticlePropagationConfig propagationConfig) {
      _particleConfig = propagationConfig;

      return this;
    }

    public Builder AddParticle(
      Vector3 position,
      Vector3 velocity,
      float gravityEffect,
      float length,
      float rotation,
      float scale
    ) {
      _particles.Add(new Particle(
        _app,
        position,
        velocity,
        gravityEffect,
        length,
        rotation,
        scale
      ));

      return this;
    }

    public Builder PropagateParticles(int count) {
      var rnd = new Random();

      for (int i = 0; i < count; i++) {
        _particles.Add(new Particle(
          _app,
          NextVector3(_particleConfig.PositionMin, _particleConfig.PositionMax, rnd),
          NextVector3(_particleConfig.VelocityMin, _particleConfig.VelocityMax, rnd),
          NextFloat(_particleConfig.GravityEffectMin, _particleConfig.GravityEffectMax, rnd),
          NextFloat(_particleConfig.LengthMin, _particleConfig.LengthMax, rnd),
          NextFloat(_particleConfig.RotationMin, _particleConfig.RotationMax),
          NextFloat(_particleConfig.ScaleMin, _particleConfig.ScaleMax, rnd)
        ));
      }

      return this;
    }

    public Builder WithTexture(string texturePath) {
      if (!_app.TextureManager.TextureExistsLocal(texturePath)) {
        _app.TextureManager.AddTextureLocal(texturePath).Wait();
      }
      var textureId = _app.TextureManager.GetTextureIdLocal(texturePath);
      _particleTexture = _app.TextureManager.GetTextureLocal(textureId);
      return this;
    }

    public Builder WithTextures() {
      throw new NotImplementedException();
    }

    public ParticleBatch Build() {
      if (_particleTexture != null) {
        _particles.ForEach(x => x.SetTexture(_particleTexture));
      }
      return new ParticleBatch(_particles);
    }

    private static Vector3 NextVector3(Vector3 min, Vector3 max, Random? random = null) {
      random ??= new Random();

      return new Vector3(
        (float)random.NextDouble() * (max.X - min.X) + min.X,
        (float)random.NextDouble() * (max.Y - min.Y) + min.Y,
        (float)random.NextDouble() * (max.Z - min.Z) + min.Z
      );
    }

    private static float NextFloat(float min, float max, Random? random = null) {
      random ??= new Random();

      return (float)random.NextDouble() * (max - min) + min;
    }
  }
}