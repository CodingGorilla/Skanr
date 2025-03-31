using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Skanr.Attributes;
using Xunit.Abstractions;

namespace Skanr.UnitTests
{
    public class CodeGenerationTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public CodeGenerationTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void Test_Execute_GeneratesExpectedCode()
        {
            // Arrange
            var source = @"
                using Skanr.Attributes;
                using Microsoft.Extensions.DependencyInjection;

                namespace TestNamespace
                {
                    [Injectable(ServiceLifetime.Transient)]
                    public class TestService : ITestService
                    {
                    }

                    public interface ITestService
                    {
                    }
                }
            ";

            var syntaxTree = CSharpSyntaxTree.ParseText(source);

            var metadataReferences = GetMetadataReferences();

            // Print the location of each metadata reference
            foreach(var reference in metadataReferences)
            {
                _testOutputHelper.WriteLine(reference.Display);
            }

            var compilation = CSharpCompilation.Create("TestAssembly",
                [syntaxTree],
                metadataReferences,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var initialDiagnostics = compilation.GetDiagnostics();
            foreach(var diag in initialDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
            {
                _testOutputHelper.WriteLine($"Pre-generator error: {diag}");
            }

            initialDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ShouldBeEmpty("Compilation failed"); // Fail fast if compilation is broken

            var generator = new ServiceRegistrationGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);

            // Act
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

            // Print diagnostics
            foreach(var diagnostic in diagnostics)
            {
                _testOutputHelper.WriteLine(diagnostic.ToString());
            }

            // Print generated source code
            foreach(var tree in outputCompilation.SyntaxTrees)
            {
                _testOutputHelper.WriteLine(tree.ToString());
            }

            // Assert
            var generatedTrees = outputCompilation.SyntaxTrees.ToList();
            generatedTrees.Count.ShouldBe(2);
            var generatedCode = generatedTrees[1].ToString();
            generatedCode.ShouldContain("public static partial class SkanrServiceRegistration");
            generatedCode.ShouldContain("services.AddTransient<ITestService, TestService>();");
        }

        [Fact]
        public void Test_Execute_ReportsDiagnosticWhenInjectableAttributeNotFound()
        {
            // Arrange
            var source = @"
                namespace TestNamespace
                {
                    public class TestService
                    {
                    }
                }
            ";

            var syntaxTree = CSharpSyntaxTree.ParseText(source);
            var compilation = CSharpCompilation.Create("TestAssembly",
                [syntaxTree],
                GetMetadataReferences(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new ServiceRegistrationGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);

            // Act
            driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

            // Assert
            diagnostics.ShouldContain(d => d.Id == "SG300");
        }

        [Fact]
        public void Test_Execute_GeneratesExpectedCode_ForTransientServiceAttribute()
        {
            // Arrange
            var source = @"
        using Skanr.Attributes;
        using Microsoft.Extensions.DependencyInjection;

        namespace TestNamespace
        {
            [TransientService]
            public class TestService : ITestService
            {
            }

            public interface ITestService
            {
            }
        }
    ";

            var syntaxTree = CSharpSyntaxTree.ParseText(source);

            var metadataReferences = GetMetadataReferences();

            var compilation = CSharpCompilation.Create("TestAssembly",
                [syntaxTree],
                metadataReferences,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new ServiceRegistrationGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);

            // Act
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

            // Print generated source code
            foreach(var tree in outputCompilation.SyntaxTrees)
            {
                _testOutputHelper.WriteLine(tree.ToString());
            }

            // Assert
            var generatedTrees = outputCompilation.SyntaxTrees.ToList();
            generatedTrees.Count.ShouldBe(2);
            var generatedCode = generatedTrees[1].ToString();
            generatedCode.ShouldContain("public static partial class SkanrServiceRegistration");
            generatedCode.ShouldContain("services.AddTransient<ITestService, TestService>();");
        }

        [Fact]
        public void Test_Execute_GeneratesExpectedCode_ForScopedServiceAttribute()
        {
            // Arrange
            var source = @"
        using Skanr.Attributes;
        using Microsoft.Extensions.DependencyInjection;

        namespace TestNamespace
        {
            [ScopedService]
            public class TestService : ITestService
            {
            }

            public interface ITestService
            {
            }
        }
    ";

            var syntaxTree = CSharpSyntaxTree.ParseText(source);

            var metadataReferences = GetMetadataReferences();

            var compilation = CSharpCompilation.Create("TestAssembly",
                new[] { syntaxTree },
                metadataReferences,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new ServiceRegistrationGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);

            // Act
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

            // Print generated source code
            foreach (var tree in outputCompilation.SyntaxTrees)
            {
                _testOutputHelper.WriteLine(tree.ToString());
            }

            // Assert
            var generatedTrees = outputCompilation.SyntaxTrees.ToList();
            generatedTrees.Count.ShouldBe(2);
            var generatedCode = generatedTrees[1].ToString();
            generatedCode.ShouldContain("public static partial class SkanrServiceRegistration");
            generatedCode.ShouldContain("services.AddScoped<ITestService, TestService>();");
        }

        [Fact]
        public void Test_Execute_GeneratesExpectedCode_ForSingletonServiceAttribute()
        {
            // Arrange
            var source = @"
        using Skanr.Attributes;
        using Microsoft.Extensions.DependencyInjection;

        namespace TestNamespace
        {
            [SingletonService]
            public class TestService : ITestService
            {
            }

            public interface ITestService
            {
            }
        }
    ";

            var syntaxTree = CSharpSyntaxTree.ParseText(source);

            var metadataReferences = GetMetadataReferences();

            var compilation = CSharpCompilation.Create("TestAssembly",
                new[] { syntaxTree },
                metadataReferences,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new ServiceRegistrationGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);

            // Act
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

            // Print generated source code
            foreach (var tree in outputCompilation.SyntaxTrees)
            {
                _testOutputHelper.WriteLine(tree.ToString());
            }

            // Assert
            var generatedTrees = outputCompilation.SyntaxTrees.ToList();
            generatedTrees.Count.ShouldBe(2);
            var generatedCode = generatedTrees[1].ToString();
            generatedCode.ShouldContain("public static partial class SkanrServiceRegistration");
            generatedCode.ShouldContain("services.AddSingleton<ITestService, TestService>();");
        }

        [Fact]
        public void Test_Execute_GeneratesExpectedCode_ForFirstInterfaceMode_WithNamedArg()
        {
            // Arrange
            var source = @"
        using Skanr.Attributes;
        using Microsoft.Extensions.DependencyInjection;

        namespace TestNamespace
        {
            [TransientService(mode:RegistrationMode.FirstInterface)]
            public class TestService : ITestService2, ITestService
            {
            }

            public interface ITestService
            {
            }
            public interface ITestService2
            {
            }
        }
    ";

            var syntaxTree = CSharpSyntaxTree.ParseText(source);

            var metadataReferences = GetMetadataReferences();

            var compilation = CSharpCompilation.Create("TestAssembly",
                [syntaxTree],
                metadataReferences,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new ServiceRegistrationGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);

            // Act
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

            // Print generated source code
            foreach(var tree in outputCompilation.SyntaxTrees)
            {
                _testOutputHelper.WriteLine(tree.ToString());
            }

            // Assert
            var generatedTrees = outputCompilation.SyntaxTrees.ToList();
            generatedTrees.Count.ShouldBe(2);
            var generatedCode = generatedTrees[1].ToString();
            generatedCode.ShouldContain("public static partial class SkanrServiceRegistration");
            generatedCode.ShouldContain("services.AddTransient<ITestService2, TestService>();");
            generatedCode.ShouldNotContain("services.AddTransient<ITestService, TestService>();");

        }

        [Fact]
        public void Test_Execute_GeneratesExpectedCode_ForFirstInterfaceMode_WithPositionalArg()
        {
            // Arrange
            var source = @"
        using Skanr.Attributes;
        using Microsoft.Extensions.DependencyInjection;

        namespace TestNamespace
        {
            [TransientService(RegistrationMode.FirstInterface)]
            public class TestService : ITestService2, ITestService
            {
            }

            public interface ITestService
            {
            }
            public interface ITestService2
            {
            }
        }
    ";

            var syntaxTree = CSharpSyntaxTree.ParseText(source);

            var metadataReferences = GetMetadataReferences();

            var compilation = CSharpCompilation.Create("TestAssembly",
                [syntaxTree],
                metadataReferences,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new ServiceRegistrationGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);

            // Act
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

            // Print generated source code
            foreach(var tree in outputCompilation.SyntaxTrees)
            {
                _testOutputHelper.WriteLine(tree.ToString());
            }

            // Assert
            var generatedTrees = outputCompilation.SyntaxTrees.ToList();
            generatedTrees.Count.ShouldBe(2);
            var generatedCode = generatedTrees[1].ToString();
            generatedCode.ShouldContain("public static partial class SkanrServiceRegistration");
            generatedCode.ShouldContain("services.AddTransient<ITestService2, TestService>();");
            generatedCode.ShouldNotContain("services.AddTransient<ITestService, TestService>();");

        }

        [Fact]
        public void Test_Execute_GeneratesExpectedCode_ForAllInterfacesMode_WithNamedArg()
        {
            // Arrange
            var source = @"
        using Skanr.Attributes;
        using Microsoft.Extensions.DependencyInjection;

        namespace TestNamespace
        {
            [TransientService(mode:RegistrationMode.AllInterfaces)]
            public class TestService : ITestService2, ITestService
            {
            }

            public interface ITestService
            {
            }
            public interface ITestService2
            {
            }
        }
    ";

            var syntaxTree = CSharpSyntaxTree.ParseText(source);

            var metadataReferences = GetMetadataReferences();

            var compilation = CSharpCompilation.Create("TestAssembly",
                [syntaxTree],
                metadataReferences,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new ServiceRegistrationGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);

            // Act
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

            // Print generated source code
            foreach(var tree in outputCompilation.SyntaxTrees)
            {
                _testOutputHelper.WriteLine(tree.ToString());
            }

            // Assert
            var generatedTrees = outputCompilation.SyntaxTrees.ToList();
            generatedTrees.Count.ShouldBe(2);
            var generatedCode = generatedTrees[1].ToString();
            generatedCode.ShouldContain("public static partial class SkanrServiceRegistration");
            generatedCode.ShouldContain("services.AddTransient<ITestService2, TestService>();");
            generatedCode.ShouldContain("services.AddTransient<ITestService, TestService>();");

        }

        [Fact]
        public void Test_Execute_GeneratesExpectedCode_ForAllInterfacesMode_WithPositionalArg()
        {
            // Arrange
            var source = @"
        using Skanr.Attributes;
        using Microsoft.Extensions.DependencyInjection;

        namespace TestNamespace
        {
            [TransientService(RegistrationMode.AllInterfaces)]
            public class TestService : ITestService2, ITestService
            {
            }

            public interface ITestService
            {
            }
            public interface ITestService2
            {
            }
        }
    ";

            var syntaxTree = CSharpSyntaxTree.ParseText(source);

            var metadataReferences = GetMetadataReferences();

            var compilation = CSharpCompilation.Create("TestAssembly",
                [syntaxTree],
                metadataReferences,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new ServiceRegistrationGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);

            // Act
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

            // Print generated source code
            foreach(var tree in outputCompilation.SyntaxTrees)
            {
                _testOutputHelper.WriteLine(tree.ToString());
            }

            // Assert
            var generatedTrees = outputCompilation.SyntaxTrees.ToList();
            generatedTrees.Count.ShouldBe(2);
            var generatedCode = generatedTrees[1].ToString();
            generatedCode.ShouldContain("public static partial class SkanrServiceRegistration");
            generatedCode.ShouldContain("services.AddTransient<ITestService2, TestService>();");
            generatedCode.ShouldContain("services.AddTransient<ITestService, TestService>();");

        }

        [Fact]
        public void Test_Execute_GeneratesExpectedCode_ForInstanceMode_WithNamedArg()
        {
            // Arrange
            var source = @"
        using Skanr.Attributes;
        using Microsoft.Extensions.DependencyInjection;

        namespace TestNamespace
        {
            [TransientService(mode:RegistrationMode.Instance)]
            public class TestService : ITestService2, ITestService
            {
            }

            public interface ITestService
            {
            }
            public interface ITestService2
            {
            }
        }
    ";

            var syntaxTree = CSharpSyntaxTree.ParseText(source);

            var metadataReferences = GetMetadataReferences();

            var compilation = CSharpCompilation.Create("TestAssembly",
                [syntaxTree],
                metadataReferences,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new ServiceRegistrationGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);

            // Act
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

            // Print generated source code
            foreach(var tree in outputCompilation.SyntaxTrees)
            {
                _testOutputHelper.WriteLine(tree.ToString());
            }

            // Assert
            var generatedTrees = outputCompilation.SyntaxTrees.ToList();
            generatedTrees.Count.ShouldBe(2);
            var generatedCode = generatedTrees[1].ToString();
            generatedCode.ShouldContain("public static partial class SkanrServiceRegistration");
            generatedCode.ShouldContain("services.AddTransient<TestService, TestService>();");
            generatedCode.ShouldNotContain("services.AddTransient<ITestService2, TestService>();");
            generatedCode.ShouldNotContain("services.AddTransient<ITestService, TestService>();");
        }

        [Fact]
        public void Test_Execute_GeneratesExpectedCode_ForInstanceMode_WithPositionalArg()
        {
            // Arrange
            var source = @"
        using Skanr.Attributes;
        using Microsoft.Extensions.DependencyInjection;

        namespace TestNamespace
        {
            [TransientService(RegistrationMode.Instance)]
            public class TestService : ITestService2, ITestService
            {
            }

            public interface ITestService
            {
            }
            public interface ITestService2
            {
            }
        }
    ";

            var syntaxTree = CSharpSyntaxTree.ParseText(source);

            var metadataReferences = GetMetadataReferences();

            var compilation = CSharpCompilation.Create("TestAssembly",
                [syntaxTree],
                metadataReferences,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new ServiceRegistrationGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);

            // Act
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

            // Print generated source code
            foreach(var tree in outputCompilation.SyntaxTrees)
            {
                _testOutputHelper.WriteLine(tree.ToString());
            }

            // Assert
            var generatedTrees = outputCompilation.SyntaxTrees.ToList();
            generatedTrees.Count.ShouldBe(2);
            var generatedCode = generatedTrees[1].ToString();
            generatedCode.ShouldContain("public static partial class SkanrServiceRegistration");
            generatedCode.ShouldContain("services.AddTransient<TestService, TestService>();");
            generatedCode.ShouldNotContain("services.AddTransient<ITestService2, TestService>();");
            generatedCode.ShouldNotContain("services.AddTransient<ITestService, TestService>();");

        }


        [Fact]
        public void Test_Execute_GeneratesExpectedCode_ForManualMode_WithNamedArg()
        {
            // Arrange
            var source = @"
        using Skanr.Attributes;
        using Microsoft.Extensions.DependencyInjection;

        namespace TestNamespace
        {
            [TransientService(mode:RegistrationMode.Manual, interfaces:typeof(IBaseService))]
            public class TestService : ITestService2, ITestService
            {
            }

            public interface ITestService
            {
            }
            public interface ITestService2 : IBaseService
            {
            }

            public interface IBaseService
            {
            }
        }
    ";

            var syntaxTree = CSharpSyntaxTree.ParseText(source);

            var metadataReferences = GetMetadataReferences();

            var compilation = CSharpCompilation.Create("TestAssembly",
                [syntaxTree],
                metadataReferences,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new ServiceRegistrationGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);

            // Act
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

            // Print generated source code
            foreach(var tree in outputCompilation.SyntaxTrees)
            {
                _testOutputHelper.WriteLine(tree.ToString());
            }

            // Assert
            var generatedTrees = outputCompilation.SyntaxTrees.ToList();
            generatedTrees.Count.ShouldBe(2);
            var generatedCode = generatedTrees[1].ToString();
            generatedCode.ShouldContain("public static partial class SkanrServiceRegistration");
            generatedCode.ShouldContain("services.AddTransient<IBaseService, TestService>();");
            generatedCode.ShouldNotContain("services.AddTransient<TestService, TestService>();");
            generatedCode.ShouldNotContain("services.AddTransient<ITestService2, TestService>();");
            generatedCode.ShouldNotContain("services.AddTransient<ITestService, TestService>();");
        }

        [Fact]
        public void Test_Execute_GeneratesExpectedCode_ForManualMode_WithPositionalArg()
        {
            // Arrange
            var source = @"
        using Skanr.Attributes;
        using Microsoft.Extensions.DependencyInjection;

        namespace TestNamespace
        {
            [TransientService(RegistrationMode.Manual, typeof(IBaseService))]
            public class TestService : ITestService2, ITestService
            {
            }

            public interface ITestService
            {
            }
            public interface ITestService2 : IBaseService
            {
            }

            public interface IBaseService
            {
            }
        }
    ";

            var syntaxTree = CSharpSyntaxTree.ParseText(source);

            var metadataReferences = GetMetadataReferences();

            var compilation = CSharpCompilation.Create("TestAssembly",
                [syntaxTree],
                metadataReferences,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var generator = new ServiceRegistrationGenerator();
            var driver = CSharpGeneratorDriver.Create(generator);

            // Act
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

            // Print generated source code
            foreach(var tree in outputCompilation.SyntaxTrees)
            {
                _testOutputHelper.WriteLine(tree.ToString());
            }

            // Assert
            var generatedTrees = outputCompilation.SyntaxTrees.ToList();
            generatedTrees.Count.ShouldBe(2);
            var generatedCode = generatedTrees[1].ToString();
            generatedCode.ShouldContain("public static partial class SkanrServiceRegistration");
            generatedCode.ShouldContain("services.AddTransient<IBaseService, TestService>();");
            generatedCode.ShouldNotContain("services.AddTransient<TestService, TestService>();");
            generatedCode.ShouldNotContain("services.AddTransient<ITestService2, TestService>();");
            generatedCode.ShouldNotContain("services.AddTransient<ITestService, TestService>();");
        }

        private static IEnumerable<MetadataReference> GetMetadataReferences()
        {
            var dotnetPath = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
            IEnumerable<MetadataReference>? metadataReferences =
            [
                MetadataReference.CreateFromFile(Path.Combine(dotnetPath, "System.Private.CoreLib.dll")),
                MetadataReference.CreateFromFile(Path.Combine(dotnetPath, "System.Runtime.dll")), // Explicitly add System.Runtime
                MetadataReference.CreateFromFile(typeof(InjectableAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(ServiceLifetime).Assembly.Location), // Ensure all necessary references are included
                // Explicitly add netstandard.dll
                MetadataReference.CreateFromFile(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "netstandard.dll"))
            ];
            return metadataReferences;
        }
    }
}