using System;
using Microsoft.Extensions.DependencyInjection;

namespace Skanr.Attributes
{
    public class TransientServiceAttribute : InjectableAttribute
    {
        public TransientServiceAttribute(RegistrationMode mode = RegistrationMode.Auto, params Type[] interfaces) : base(ServiceLifetime.Transient, mode, interfaces)
        {
        }
    }
}