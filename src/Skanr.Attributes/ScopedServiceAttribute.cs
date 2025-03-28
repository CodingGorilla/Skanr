using System;
using Microsoft.Extensions.DependencyInjection;

namespace Skanr.Attributes
{
    public class ScopedServiceAttribute : InjectableAttribute
    {
        public ScopedServiceAttribute(RegistrationMode mode = RegistrationMode.Auto, params Type[] interfaces)
            : base(ServiceLifetime.Scoped, mode, interfaces)
        {
        }
    }
}