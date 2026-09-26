using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NUnit.Framework;

namespace UnityLuaSystem.SourceGenerator.Tests {
    /// <summary>
    /// LuaBindingSourceGeneratorの生成結果を確認するテスト
    /// </summary>
    public sealed class LuaBindingSourceGeneratorTests {
        private const string Source = @"
using System;
namespace UnityLuaSystem {
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class LuaModuleAttribute : Attribute {
        public LuaModuleAttribute(string name) {}
    }
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class LuaFunctionAttribute : Attribute {
        public LuaFunctionAttribute(string name) {}
    }
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class LuaObjectAttribute : Attribute {}
    public delegate int LuaGeneratedFunctionCallback(LuaRuntime runtime, IntPtr state);
    public interface ILuaGeneratedModuleBinding {
        Type ModuleType { get; }
        bool TryGetFunction(string methodName, out string functionName, out LuaGeneratedFunctionCallback callback);
    }
    public sealed class LuaRuntime {
        public void ValidateGeneratedArgumentCount(IntPtr state, int count) {}
        public T ReadGeneratedValue<T>(IntPtr state, int index) => default;
        public void PushGeneratedValue<T>(IntPtr state, T value) {}
    }
}
namespace Sample {
    [UnityLuaSystem.LuaModule(""calculator"")]
    public sealed class Calculator {
        [UnityLuaSystem.LuaFunction(""add"")]
        public int Add(int left, int right) => left + right;
        [UnityLuaSystem.LuaFunction(""sum"")]
        public int[] Sum(int[] values) => values;
    }
}";

        /// <summary>
        /// Attribute付きメソッドを直接呼ぶバインディングが生成されることを確認
        /// </summary>
        [Test]
        public void Generate_CreatesDirectMethodInvocation() {
            var syntaxTree = CSharpSyntaxTree.ParseText(Source);
            var references = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                .Select(assembly => MetadataReference.CreateFromFile(assembly.Location));
            var compilation = CSharpCompilation.Create(
                "GeneratorTest",
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            GeneratorDriver driver = CSharpGeneratorDriver.Create(new LuaBindingSourceGenerator());

            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

            var generatedSource = driver.GetRunResult().Results.Single().GeneratedSources.Single().SourceText.ToString();
            Assert.That(diagnostics, Is.Empty);
            Assert.That(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
            Assert.That(generatedSource, Does.Contain("_target.@Add(argument1, argument2)"));
            Assert.That(generatedSource, Does.Contain("runtime.PushGeneratedValue<int>"));
            Assert.That(generatedSource, Does.Contain("runtime.ReadGeneratedValue<int[]>"));
            Assert.That(generatedSource, Does.Contain("runtime.PushGeneratedValue<int[]>"));
        }
    }
}
