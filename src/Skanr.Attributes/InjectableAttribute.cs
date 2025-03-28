using System;
using Microsoft.Extensions.DependencyInjection;

namespace Skanr.Attributes
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public class InjectableAttribute : Attribute
    {
        public InjectableAttribute(ServiceLifetime lifetime, RegistrationMode mode = RegistrationMode.Auto, params Type[] interfaces)
        {
            Lifetime = lifetime;
            Mode = mode;
            Interfaces = interfaces ?? [];
        }

        public ServiceLifetime Lifetime { get; }

        public RegistrationMode Mode { get; }

        public Type[] Interfaces { get; set; }
    }
}