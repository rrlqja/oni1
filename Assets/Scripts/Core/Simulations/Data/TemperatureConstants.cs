namespace Core.Simulation.Data
{
    public static class TemperatureConstants
    {
        public const float ABSOLUTE_ZERO = 0f;
        public const float CELSIUS_OFFSET = 273.15f;
        public const float DEFAULT_TEMPERATURE = 293.15f;
        public const float TRANSITION_OVERSHOOT = 3f;
        public const float TRANSITION_REBOUND = 1.5f;
        public const float MIN_HEAT_EXCHANGE = 0.001f;

        public static float CelsiusToKelvin(float celsius) => celsius + CELSIUS_OFFSET;
        public static float KelvinToCelsius(float kelvin) => kelvin - CELSIUS_OFFSET;
    }
}