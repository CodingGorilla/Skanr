using System;
using System.Security.Principal;

namespace Skanr.AspNetWebAPI
{
    public partial class SkanrServiceRegistration
    {
        static partial void RegisterAdditionalServices(IServiceCollection services)
        {
            // Add custom registrations here
            services.AddHttpContextAccessor();
            services.AddScoped<IPrincipal>((sp) => sp.GetRequiredService<HttpContext>().User);
        }
    }
}