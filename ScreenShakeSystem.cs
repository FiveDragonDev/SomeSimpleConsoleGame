using SomeSimpleConsoleGame.Core;

namespace SomeSimpleConsoleGame
{
    public sealed class ScreenShakeSystem : IUpdateSystem
    {
        private const float _sincosDependence = 0.72375872384f;

        private readonly float _amplitudeSensetivity;
        private readonly float _amplitudeDrag;
        private readonly float _amplitudeSpeed;

        private float _offset;
        private float _amplitude;

        private float _time;

        public ScreenShakeSystem(float amplitudeSensetivity, float amplitudeDrag, float amplitudeSpeed)
        {
            _amplitudeSensetivity = amplitudeSensetivity;
            _amplitudeDrag = amplitudeDrag;
            _amplitudeSpeed = amplitudeSpeed;

            ConsoleUtils.CenterConsoleWindow();
        }

        public void AddAmplitude(float amplitude)
        {
            _amplitude += amplitude;
            _offset += MathUtils.Sqrt2;
        }

        public void Update(float deltaTime)
        {
            var (centerLeft, centerTop) = ConsoleUtils.GetCenteredConsolePosition();
            var (left, top) = ConsoleUtils.GetConsolePosition();

            float windowVelocityX = (centerLeft - left) * deltaTime * 5f;
            float windowVelocityY = (centerTop - top) * deltaTime * 5f;

            if (_amplitude > 0.1f)
            {
                windowVelocityX += _amplitudeSensetivity * _amplitude * MathUtils.QSin((_time * _amplitudeSpeed * _sincosDependence) + _offset);
                windowVelocityY += _amplitudeSensetivity * _amplitude * MathUtils.QSin((_time * _amplitudeSpeed) + _offset);
                _amplitude *= 1 - (_amplitudeDrag * deltaTime);
            }

            ConsoleUtils.SetConsolePosition((int)(left + windowVelocityX), (int)(top + windowVelocityY));
            _time += deltaTime;
        }
    }
}
