using System;
using Skanr.Attributes;

namespace Skanr.AspNetWebAPI
{
    [TransientService(RegistrationMode.AllInterfaces)]
    [SingletonService(RegistrationMode.Instance)]
    public class WeatherService : IFirstInterface, ISecondInterface
    {
    }

    public interface IFirstInterface
    {
    }

    public interface ISecondInterface
    {
    }
}