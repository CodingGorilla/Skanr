﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Skanr
{
    internal class CodeGenerator
    {
        private readonly string _nameSpace;
        private readonly List<PendingRegistration> _pendingRegistrations = new();

        public CodeGenerator(string nameSpace)
        {
            _nameSpace = nameSpace;
        }

        public void AddPendingRegistrations(params IEnumerable<PendingRegistration> prs)
            => _pendingRegistrations.AddRange(prs);

        public override string ToString()
        {
            // Start the generated code builder
            var code = new StringBuilder();
            code.AppendLine("// <auto-generated />");

            // Generate the required using directives
            var namespaces = _pendingRegistrations.Select(pr => pr.ServiceType.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                                                  .Concat(_pendingRegistrations.Select(pr => pr.ImplementationType.ContainingNamespace.ToDisplayString(
                                                                                           SymbolDisplayFormat.FullyQualifiedFormat)))
                                                  .Concat(["Microsoft.Extensions.DependencyInjection"])
                                                  .Distinct()
                                                  .OrderBy(ns => ns)
                                                  .ToArray();

            // Write the using directives for the namespaces
            foreach(var ns in namespaces)
            {
                code.AppendLine($"using {ns};");
            }

            // Generate the namespace and class
            code.AppendLine();
            code.AppendLine($"namespace {_nameSpace};");
            code.AppendLine();
            code.AppendLine("public static partial class SkanrServiceRegistration");
            code.AppendLine("{");
            code.AppendLine("    static partial void RegisterAdditionalServices(IServiceCollection services);");
            code.AppendLine();
            code.AppendLine("    public static void RegisterServices(this IServiceCollection services)");
            code.AppendLine("    {");


            IGrouping<string, PendingRegistration>[] groups = _pendingRegistrations.GroupBy(pr => pr.GroupName)
                                                                                   .ToArray();

            foreach(var group in groups)
            {
                var ppBlocks = group.GroupBy(y => y.PreprocessorLabel)
                                    .ToArray();

                // Write the #region for the group
                code.AppendLine($"    #region {group.Key}");

                foreach(var block in ppBlocks)
                {
                    var closeIf = false;

                    if(!string.IsNullOrEmpty(block.Key))
                    {
                        // Write the #if for the preprocessor block
                        code.AppendLine($"    #if {block.Key}");
                        closeIf = true;
                    }

                    foreach(var registration in block)
                    {
                        var interfaceName = registration.ServiceType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                        var implName = registration.ImplementationType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                        code.AppendLine($"        services.Add{registration.Lifetime}<{interfaceName}, {implName}>();");
                    }

                    if(!closeIf)
                        continue;

                    // Write the #endif for the preprocessor block
                    code.AppendLine("    #endif");
                    code.AppendLine();
                }

                // Write the #endregion for the group
                code.AppendLine("    #endregion");
                code.AppendLine();
            }

            // Write the call to the additional services method
            code.AppendLine();
            code.AppendLine("        RegisterAdditionalServices(services);");
            code.AppendLine("    }");
            code.AppendLine("}");

            return code.ToString();
        }
    }
}