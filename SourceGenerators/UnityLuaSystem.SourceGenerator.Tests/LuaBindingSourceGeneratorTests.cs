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
    public interface ILuaGeneratedApiMetadata {
        string Definition { get; }
    }
    public sealed class LuaRuntime {
        public void ValidateGeneratedArgumentCount(IntPtr state, int count) {}
        public T ReadGeneratedValue<T>(IntPtr state, int index) => default;
        public void PushGeneratedValue<T>(IntPtr state, T value) {}
    }
}
namespace Sample {
    /// <summary>プレイヤー</summary>
    [UnityLuaSystem.LuaObject]
    public sealed class Player {
        /// <summary>体力を回復</summary>
        /// <param name=""amount"">回復量</param>
        /// <returns>回復後の体力</returns>
        [UnityLuaSystem.LuaFunction(""heal"")]
        public int Heal(int amount) => amount;
    }

    /// <summary>計算処理を提供</summary>
    [UnityLuaSystem.LuaModule(""calculator"")]
    public sealed class Calculator {
        /// <summary>2つの整数を加算</summary>
        /// <param name=""left"">左辺</param>
        /// <param name=""right"">右辺</param>
        /// <returns>加算結果</returns>
        [UnityLuaSystem.LuaFunction(""add"")]
        public int Add(int left, int right) => left + right;
        [UnityLuaSystem.LuaFunction(""sum"")]
        public int[] Sum(int[] values) => values;
        /// <summary>非同期にメッセージを取得</summary>
        /// <returns>取得したメッセージ</returns>
        [UnityLuaSystem.LuaFunction(""load_async"")]
        public System.Threading.Tasks.Task<string> LoadAsync() => null;
        [UnityLuaSystem.LuaFunction(""unsupported"")]
        public System.DateTime Unsupported(System.DateTime value) => value;
    }
}";

        /// <summary>
        /// Attribute付きメソッドを直接呼ぶバインディングが生成されることを確認
        /// </summary>
        [Test]
        public void Generate_CreatesDirectMethodInvocation() {
            var syntaxTree = CSharpSyntaxTree.ParseText(Source, new CSharpParseOptions(documentationMode: DocumentationMode.None, preprocessorSymbols: new[] { "UNITY_EDITOR" }));
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

            var generatedSource = driver.GetRunResult().Results.Single().GeneratedSources
                .Single(source => source.HintName.EndsWith(".Binding.g.cs", StringComparison.Ordinal))
                .SourceText.ToString();
            Assert.That(diagnostics, Is.Empty);
            Assert.That(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
            Assert.That(generatedSource, Does.Contain("_target.@Add(argument1, argument2)"));
            Assert.That(generatedSource, Does.Contain("runtime.PushGeneratedValue<int>"));
            Assert.That(generatedSource, Does.Contain("runtime.ReadGeneratedValue<int[]>"));
            Assert.That(generatedSource, Does.Contain("runtime.PushGeneratedValue<int[]>"));
        }

        /// <summary>
        /// XMLコメントを含むLua APIメタデータが生成されることを確認
        /// </summary>
        [Test]
        public void Generate_CreatesDocumentedApiMetadata() {
            var syntaxTree = CSharpSyntaxTree.ParseText(Source, new CSharpParseOptions(documentationMode: DocumentationMode.None, preprocessorSymbols: new[] { "UNITY_EDITOR" }));
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

            var generatedSource = driver.GetRunResult().Results.Single().GeneratedSources
                .Single(source => source.HintName == "UnityLuaSystem.ApiMetadata.g.cs")
                .SourceText.ToString();
            Assert.That(diagnostics, Is.Empty);
            Assert.That(outputCompilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), Is.Empty);
            Assert.That(generatedSource, Does.Contain("---計算処理を提供"));
            Assert.That(generatedSource, Does.Contain("---2つの整数を加算"));
            Assert.That(generatedSource, Does.Contain("---@param left integer 左辺"));
            Assert.That(generatedSource, Does.Contain("---@return integer 加算結果"));
            Assert.That(generatedSource, Does.Contain("---@class Sample_Player"));
            Assert.That(generatedSource, Does.Contain("---@param amount integer 回復量"));
            Assert.That(generatedSource, Does.Contain("function __Sample_Player:heal(amount) end"));
            Assert.That(generatedSource, Does.Contain("---@return string 取得したメッセージ"));
            Assert.That(generatedSource, Does.Contain("function calculator.load_async() end"));
            Assert.That(generatedSource, Does.Not.Contain("calculator.unsupported"));
            Assert.That(generatedSource, Does.Contain("ILuaGeneratedApiMetadata"));
        }
    }
}
