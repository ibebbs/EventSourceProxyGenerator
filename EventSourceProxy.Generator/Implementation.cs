// References: https://github.com/codecentric/net_automatic_interface/blob/master/AutomaticInterface/AutomaticInterface/AutomaticInterface.cs

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace EventSourceProxy.Generator
{
    [Generator]
    public class Implementation : ISourceGenerator
    {
        //private static readonly DiagnosticDescriptor ProxyableClass = new DiagnosticDescriptor(id: "BLAH123", title: "Proxyable class", messageFormat: "Proxyable class '{0}' using attribute '{1}'", category: "EventSourceProxy.Generator", DiagnosticSeverity.Info, isEnabledByDefault: true);

        public void Initialize(GeneratorInitializationContext context)
        {
//#if DEBUG
//            if (!System.Diagnostics.Debugger.IsAttached)
//            {
//                System.Diagnostics.Debugger.Launch();
//            }
//#endif 
            // Register a syntax receiver that will be created for each generation pass
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            // add the attribute text
            context.AddSource("ProxyAttribute", SourceText.From(Resources.ProxyAttribute, Encoding.UTF8));

            // retreive the populated receiver 
            if (!(context.SyntaxReceiver is SyntaxReceiver receiver))
                return;

            // we're going to create a new compilation that contains the attribute.
            // TODO: we should allow source generators to provide source during initialize, so that this step isn't required.
            CSharpParseOptions options = (context.Compilation as CSharpCompilation).SyntaxTrees[0].Options as CSharpParseOptions;
            Compilation compilation = context.Compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(SourceText.From(Resources.ProxyAttribute, Encoding.UTF8), options));

            // get the newly bound attribute, and INotifyPropertyChanged
            INamedTypeSymbol attributeSymbol = compilation.GetTypeByMetadataName("EventSourceProxy.Generator.ProxyAttribute");

            List<INamedTypeSymbol> classSymbols = new List<INamedTypeSymbol>();
            foreach (ClassDeclarationSyntax cls in receiver.CandidateClasses)
            {
                var model = compilation.GetSemanticModel(cls.SyntaxTree);

                var classSymbol = model.GetDeclaredSymbol(cls);

                if (classSymbol.GetAttributes().Any(ad => ad.AttributeClass.Name == attributeSymbol.Name)) // todo, weird that  ad.AttributeClass.Equals(attributeSymbol, SymbolEqualityComparer.Default) always returns null - see https://github.com/dotnet/roslyn/issues/30248 maybe?
                {
                    classSymbols.Add(classSymbol);
                }
            }

            foreach (var classSymbol in classSymbols)
            {
                var sourceBuilder = new StringBuilder();
                var namespaceName = classSymbol.ContainingNamespace.ToDisplayString();
                var proxyClassName = $"{classSymbol.Name}Proxy";
                sourceBuilder.Append($@"
using System;
namespace {namespaceName}
{{
    public class {proxyClassName} : {classSymbol.Name}
    {{
        private readonly {classSymbol.Name} _inner;

        public {proxyClassName}({classSymbol.Name} inner)
        {{
            _inner = inner;
        }}
         
");
                addMembersToInterface(classSymbol, sourceBuilder);

                sourceBuilder.Append(@"
    }      
}");

                var fileHint = $"{proxyClassName}.cs";
                var source = sourceBuilder.ToString();
                // inject the created source into the users compilation
                context.AddSource(fileHint, SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
            }
        }

        private void addMembersToInterface(INamedTypeSymbol classSymbol, StringBuilder sourceBuilder)
        {
            foreach (var member in classSymbol.GetMembers())
            {
                if (member is IMethodSymbol method 
                 && member.DeclaredAccessibility == Accessibility.Public 
                 && method.Parameters.IsEmpty 
                 && !method.DeclaringSyntaxReferences.IsEmpty
                 && method.IsVirtual)
                {
                    var name = method.Name;
                    sourceBuilder.Append($@"
        public override void {name} ()
         {{
                Console.WriteLine($""Calling method '{name}' on inner class"");

                base.{name}();

                Console.WriteLine($""Calling of method '{name}' complete"");
         }}");
                }
            }
        }

        class SyntaxReceiver : ISyntaxReceiver
        {
            public List<ClassDeclarationSyntax> CandidateClasses { get; } = new List<ClassDeclarationSyntax>();

            /// <summary>
            /// Called for every syntax node in the compilation, we can inspect the nodes and save any information useful for generation
            /// </summary>
            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                // any field with at least one attribute is a candidate for property generation
                if (syntaxNode is ClassDeclarationSyntax classDeclarationSyntax
                    && classDeclarationSyntax.AttributeLists.Count > 0)
                {
                    CandidateClasses.Add(classDeclarationSyntax);
                }
            }
        }
    }
}
