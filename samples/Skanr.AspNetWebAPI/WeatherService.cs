using System;
using Skanr.Attributes;

namespace Skanr.AspNetWebAPI
{
    [TransientService(RegistrationMode.AllInterfaces)]
    [SingletonService(RegistrationMode.Instance)]
    [TransientService(mode:RegistrationMode.AllInterfaces)]
    public class WeatherService : IFirstInterface, ISecondInterface
    {
    }

    public interface IFirstInterface
    {
    }

    public interface ISecondInterface
    {
    }

    [TransientService(PreprocessorLabel = "DEBUG")]
    public class DebugWeatherService
    {
    }
}