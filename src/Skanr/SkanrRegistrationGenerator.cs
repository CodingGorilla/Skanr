using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Immutable;
using Skanr.Attributes;

namespace Skanr
{
    [Generator]
    public class ServiceRegistrationGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var classDeclarations = context.SyntaxProvider
                                           .CreateSyntaxProvider(
                                               predicate:static (s, _) => s is ClassDeclarationSyntax,
                                               transform:static (ctx, _) => (ClassDeclarationSyntax)ctx.Node)
                                           .Where(static m => m != null);

            var compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());

            context.RegisterSourceOutput(compilationAndClasses, (spc, source) => Execute(source.Left, source.Right, spc));
        }

        private static void Execute(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classDeclarations, SourceProductionContext context)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "SG001", "Generator Started", "Starting ServiceRegistrationGenerator",
                    "Debug", DiagnosticSeverity.Info, true),
                Location.None));

            var registrations = new List<PendingRegistration>();

            // Get the InjectableAttribute symbol for inheritance checking
            var injectableAttributeSymbol = compilation.GetTypeByMetadataName("Skanr.Attributes.InjectableAttribute");
            if(injectableAttributeSymbol == null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "SG005", "InjectableAttribute not found", "InjectableAttribute not found",
                        "Debug", DiagnosticSeverity.Error, true),
                    Location.None));
                return; // Exit if base attribute not found
            }

            foreach(var classDecl in classDeclarations)
            {
                var semanticModel = compilation.GetSemanticModel(classDecl.SyntaxTree);
                if(semanticModel.GetDeclaredSymbol(classDecl) is not INamedTypeSymbol symbol)
                    continue;

                // Find any attribute that inherits from InjectableAttribute
                var attributes = symbol.GetAttributes();

                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "SG100", "Found symbol", $"Found: {string.Join(",", attributes)} on {symbol.Name}",
                        "Debug", DiagnosticSeverity.Info, true),
                    Location.None));
                var injectableAttrs = attributes.Where(a => IsSameOrDerivedFrom(a.AttributeClass, injectableAttributeSymbol));

                foreach(var injectableAttr in injectableAttrs)
                {
                    if(injectableAttr?.AttributeClass != null)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            new DiagnosticDescriptor(
                                "SG002", "Found injectable service", $"Found {injectableAttr.AttributeClass.Name} on {symbol.Name}",
                                "Debug", DiagnosticSeverity.Info, true),
                            Location.None));

                        var (lifetime, mode, specifiedInterfaces, ppLabel) = GetMetadataFromAttribute(injectableAttr);

                        var interfaces = symbol.Interfaces;

                        var groupName = symbol.Name;

                        // Handle registration based on mode
                        switch(mode)
                        {
                            case "Instance":
                                registrations.Add(new(groupName, ppLabel, symbol, symbol, lifetime));
                                break;

                            case "AllInterfaces" when interfaces.Length > 0:
                                foreach(var iface in interfaces)
                                {
                                    registrations.Add(new(groupName, ppLabel, iface, symbol, lifetime));
                                }

                                break;

                            case "FirstInterface" when interfaces.Length > 0:
                                registrations.Add(new(groupName, ppLabel, interfaces[0], symbol, lifetime));
                                break;

                            case "Auto" when interfaces.Length > 0:
                                registrations.Add(new(groupName, ppLabel, interfaces[0], symbol, lifetime));
                                break;

                            case "Manual" when specifiedInterfaces.Length > 0:
                                foreach(var iface in specifiedInterfaces)
                                {
                                    registrations.Add(new(groupName, ppLabel, iface, symbol, lifetime));
                                }

                                break;

                            case "Auto": // If no interfaces are available, fall back to instance
                            case "Manual": // If no interfaces specified, fall back to instance
                            default:
                                registrations.Add(new(groupName, ppLabel, symbol, symbol, lifetime));
                                break;
                        }
                    }
                }
            }

            if(!registrations.Any())
            {
                // Create a diagnostic message if no registrations were found and stop
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "SG300", "No registrations found", "No registrations found",
                        "Debug", DiagnosticSeverity.Warning, true),
                    Location.None));
                return;
            }

            // Generate the source code
            var cg = new CodeGenerator(compilation.AssemblyName ?? "Skanr.Registrations");
            cg.AddPendingRegistrations(registrations);
            var code = cg.ToString();

            context.AddSource("SkanrRegistrations.g.cs", SourceText.From(code, Encoding.UTF8));
        }

        private static (string lifetime, string mode, INamedTypeSymbol[] interfaces, string? preprocessorLabel) GetMetadataFromAttribute(
            AttributeData attributeData)
        {
            string lifetime;
            var mode = "Auto";
            INamedTypeSymbol[] interfaces = [];
            string preprocessorLabel = null;

            var constructorArgs = attributeData.ConstructorArguments;

            // Parse meta data based on attribute name
            if(attributeData.AttributeClass?.Name == nameof(InjectableAttribute))
                parseInjectableAttribute();
            else if(attributeData.AttributeClass?.Name == nameof(TransientServiceAttribute))
                parseTransientAttribute();
            else if(attributeData.AttributeClass?.Name == nameof(ScopedServiceAttribute))
                parseScopedAttribute();
            else if(attributeData.AttributeClass?.Name == nameof(SingletonServiceAttribute))
                parseSingletonAttribute();
            else
                throw new Exception($"Invalid attribute: ${attributeData.AttributeClass?.Name ?? "Unknown"}");

            // Check for preprocessor label
            var ppLabelArg = attributeData.NamedArguments.FirstOrDefault(na => na.Key.Equals(nameof(InjectableAttribute.PreprocessorLabel)));
            {
                if(ppLabelArg.Value.Value is string label && !string.IsNullOrWhiteSpace(label))
                    preprocessorLabel = ppLabelArg.Value.Value.ToString();
            }

            return (lifetime, mode, interfaces, preprocessorLabel);

            // Helper methods
            void parseInjectableAttribute()
            {
                // Get positional arguments
                lifetime = constructorArgs[0].Value?.ToString() switch
                {
                    "0" => "Singleton",
                    "1" => "Scoped",
                    "2" => "Transient",
                    _ => "Transient"
                };

                if(constructorArgs.Length > 1)
                {
                    mode = constructorArgs[1].Value?.ToString() switch
                    {
                        "0" => "Auto",
                        "1" => "FirstInterface",
                        "2" => "AllInterfaces",
                        "3" => "Instance",
                        _ => "Auto"
                    };
                }

                if(constructorArgs.Length > 2)
                {
                    interfaces = constructorArgs[2].Values
                                                   .Select(v => v.Value as INamedTypeSymbol)
                                                   .Where(i => i != null)
                                                   .ToArray()!;
                }

                checkNamedArgs();
            }

            void parseTransientAttribute()
            {
                lifetime = "Transient";

                // Check positional arguments
                if(constructorArgs.Length > 0)
                {
                    mode = constructorArgs[0].Value?.ToString() switch
                    {
                        "0" => "Auto",
                        "1" => "FirstInterface",
                        "2" => "AllInterfaces",
                        "3" => "Instance",
                        "4" => "Manual",
                        _ => "Auto"
                    };
                }

                if(constructorArgs.Length > 1)
                {
                    interfaces = constructorArgs[1].Values
                                                   .Select(v => v.Value as INamedTypeSymbol)
                                                   .Where(i => i != null)
                                                   .ToArray()!;
                }

                checkNamedArgs();
            }

            void parseScopedAttribute()
            {
                lifetime = "Scoped";

                // Check positional arguments
                if(constructorArgs.Length > 0)
                {
                    mode = constructorArgs[0].Value?.ToString() switch
                    {
                        "0" => "Auto",
                        "1" => "FirstInterface",
                        "2" => "AllInterfaces",
                        "3" => "Instance",
                        _ => "Auto"
                    };
                }

                if(constructorArgs.Length > 1)
                {
                    interfaces = constructorArgs[1].Values
                                                   .Select(v => v.Value as INamedTypeSymbol)
                                                   .Where(i => i != null)
                                                   .ToArray()!;
                }

                checkNamedArgs();
            }

            void parseSingletonAttribute()
            {
                lifetime = "Singleton";

                // Check positional arguments
                if(constructorArgs.Length > 0)
                {
                    mode = constructorArgs[0].Value?.ToString() switch
                    {
                        "0" => "Auto",
                        "1" => "FirstInterface",
                        "2" => "AllInterfaces",
                        "3" => "Instance",
                        _ => "Auto"
                    };
                }

                if(constructorArgs.Length > 1)
                {
                    interfaces = constructorArgs[1].Values
                                                   .Select(v => v.Value as INamedTypeSymbol)
                                                   .Where(i => i != null)
                                                   .ToArray()!;
                }

                checkNamedArgs();
            }

            void checkNamedArgs()
            {
                // Look for named arguments
                foreach(var namedArg in attributeData.NamedArguments)
                {
                    switch(namedArg.Key)
                    {
                        case "lifetime":
                            lifetime = namedArg.Value.Value?.ToString() switch
                            {
                                "0" => "Singleton",
                                "1" => "Scoped",
                                "2" => "Transient",
                                _ => "Transient"
                            };
                            break;
                        case "mode":
                            mode = namedArg.Value.Value?.ToString() switch
                            {
                                "0" => "Auto",
                                "1" => "FirstInterface",
                                "2" => "AllInterfaces",
                                "3" => "Instance",
                                "4" => "Manual",
                                _ => "Auto"
                            };
                            break;
                        case "interfaces":
                            interfaces = namedArg.Value.Values
                                                 .Select(v => v.Value as INamedTypeSymbol)
                                                 .Where(i => i != null)
                                                 .ToArray()!;
                            break;
                    }
                }
            }
        }

        private static bool IsSameOrDerivedFrom(INamedTypeSymbol? symbol, INamedTypeSymbol potentialBase)
        {
            if(symbol == null)
                return false;

            if(SymbolEqualityComparer.Default.Equals(symbol, potentialBase))
                return true;

            var current = symbol.BaseType;
            while(current != null)
            {
                if(SymbolEqualityComparer.Default.Equals(current, potentialBase))
                    return true;
                current = current.BaseType;
            }

            return false;
        }
    }

    internal record struct PendingRegistration(
        string GroupName,
        string? PreprocessorLabel,
        INamedTypeSymbol ServiceType,
        INamedTypeSymbol ImplementationType,
        string Lifetime);
}