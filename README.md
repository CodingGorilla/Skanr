# Skanr

A C# source generator that automates Dependency Injection (DI) registration by scanning your code for injectable classes. Supports flexible registration modes (Auto, Manual, etc.) and lifetime attributes (Singleton, Scoped, Transient) to streamline DI setup in .NET applications.

## Table of Contents
- [Description](#description)
- [Installation](#installation)
- [Usage](#usage)
- [Attributes](#attributes)
- [License](#license)

## Description

Skanr is a C# source generator that simplifies Dependency Injection (DI) setup in .NET applications by automatically registering services based on custom attributes. It supports various registration modes and service lifetimes, making it easier to manage DI configurations.

## Installation

To install Skanr, add the following package reference to your .NET project:

```xml
<PackageReference Include="Skanr" Version="1.0.0" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
<PackageReference Include="Skanr.Attributes" Version="1.0.0" />
```

## Usage

To use Skanr, apply the provided attributes to your classes and configure the DI container in the `Program.cs` file.

### Example

```csharp name=samples/Skanr.AspNetWebAPI/Program.cs
namespace Skanr.AspNetWebAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddAuthorization();

            // Register Skanr services
            builder.Services.RegisterServices();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            app.UseHttpsRedirection();
            app.UseAuthorization();

            var summaries = new[]
            {
                "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
            };

            app.MapGet("/weatherforecast", (HttpContext httpContext) =>
            {
                var forecast = Enumerable.Range(1, 5).Select(index =>
                    new WeatherForecast
                    {
                        Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                        TemperatureC = Random.Shared.Next(-20, 55),
                        Summary = summaries[Random.Shared.Next(summaries.Length)]
                    })
                    .ToArray();
                return forecast;
            });

            app.Run();
        }
    }
}
```

### Applying Attributes

Use the provided attributes to mark your classes for DI registration:

```csharp name=src/Skanr.Attributes/SingletonServiceAttribute.cs
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
```

## Attributes

Skanr provides several attributes to control the DI registration of your classes:

- `InjectableAttribute`: Base attribute for DI registration.
- `SingletonServiceAttribute`: Registers the class as a singleton service.
- `ScopedServiceAttribute`: Registers the class with a scoped lifetime.
- `TransientServiceAttribute`: Registers the class with a transient lifetime.

### Example

```csharp name=samples/Skanr.AspNetWebAPI/WeatherService.cs
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
```

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for more details.
