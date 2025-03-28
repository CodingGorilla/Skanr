using System;
using Microsoft.Extensions.DependencyInjection;

namespace Skanr.Attributes
{
    public class SingletonServiceAttribute : InjectableAttribute
    {
        public SingletonServiceAttribute(RegistrationMode mode = RegistrationMode.Auto, params Type[] interfaces)
            : base(ServiceLifetime.Singleton, mode, interfaces)
        {
        }
    }
}