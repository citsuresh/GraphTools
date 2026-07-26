using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GraphTools.Core;

public static class GraphExtractor
{
    public static async Task<(List<GraphNode> Nodes, List<GraphEdge> Edges)> ExtractProjectAsync(
        Project project, Action<string>? progress = null)
    {
        var nodes = new List<GraphNode>();
        var edges = new List<GraphEdge>();

        var compilation = await project.GetCompilationAsync();
        if (compilation == null)
        {
            progress?.Invoke($"Warning: could not get compilation for project {project.Name}");
            return (nodes, edges);
        }

        var seenTypeIds = new HashSet<string>();
        VisitNamespace(compilation.Assembly.GlobalNamespace, compilation, project.Name, nodes, edges, seenTypeIds);
        return (nodes, edges);
    }

    private static void VisitNamespace(
        INamespaceSymbol ns, Compilation compilation, string projectName,
        List<GraphNode> nodes, List<GraphEdge> edges, HashSet<string> seenTypeIds)
    {
        foreach (var member in ns.GetMembers())
        {
            if (member is INamespaceSymbol childNs)
            {
                VisitNamespace(childNs, compilation, projectName, nodes, edges, seenTypeIds);
            }
            else if (member is INamedTypeSymbol type && SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly))
            {
                VisitType(type, compilation, projectName, nodes, edges, seenTypeIds);
            }
        }
    }

    private static void VisitType(
        INamedTypeSymbol type, Compilation compilation, string projectName,
        List<GraphNode> nodes, List<GraphEdge> edges, HashSet<string> seenTypeIds)
    {
        var typeId = GetTypeId(type);
        if (!seenTypeIds.Add(typeId))
        {
            return;
        }

        var location = GetFirstDeclarationLocation(type);
        if (location == null)
        {
            // Type is only declared in generated build-output files (e.g. XAML .g.cs); skip it entirely.
            return;
        }

        var (filePath, startLine, endLine) = location.Value;

        nodes.Add(new GraphNode
        {
            Id = typeId,
            Kind = GetKindForType(type),
            Name = type.Name,
            ContainingType = type.ContainingType != null ? GetTypeId(type.ContainingType) : null,
            Project = projectName,
            FilePath = filePath,
            StartLine = startLine,
            EndLine = endLine,
            Accessibility = GetAccessibility(type.DeclaredAccessibility),
            IsStatic = type.IsStatic,
            IsAbstract = type.IsAbstract,
        });

        // extends
        if (type.TypeKind == TypeKind.Class && type.BaseType != null &&
            type.BaseType.SpecialType != SpecialType.System_Object)
        {
            edges.Add(new GraphEdge
            {
                Type = "extends",
                SourceId = typeId,
                TargetId = GetTypeId(type.BaseType),
                FilePath = filePath,
                Line = startLine,
            });
        }

        // implements
        foreach (var iface in type.Interfaces)
        {
            edges.Add(new GraphEdge
            {
                Type = "implements",
                SourceId = typeId,
                TargetId = GetTypeId(iface),
                FilePath = filePath,
                Line = startLine,
            });
        }

        foreach (var member in type.GetMembers())
        {
            VisitMember(member, typeId, compilation, projectName, nodes, edges);
        }

        // nested types
        foreach (var nested in type.GetTypeMembers())
        {
            VisitType(nested, compilation, projectName, nodes, edges, seenTypeIds);
        }
    }

    private static void VisitMember(
        ISymbol member, string typeId, Compilation compilation, string projectName,
        List<GraphNode> nodes, List<GraphEdge> edges)
    {
        if (member.IsImplicitlyDeclared)
        {
            return;
        }

        switch (member)
        {
            case IMethodSymbol method:
                if (method.MethodKind is MethodKind.PropertyGet or MethodKind.PropertySet
                    or MethodKind.EventAdd or MethodKind.EventRemove or MethodKind.EventRaise)
                {
                    return; // covered by property/event node
                }

                var methodId = GetMethodId(typeId, method);
                var methodLocation = GetFirstDeclarationLocation(method);
                if (methodLocation == null)
                {
                    return; // only declared in generated build-output files
                }

                var (mFile, mStart, mEnd) = methodLocation.Value;
                nodes.Add(new GraphNode
                {
                    Id = methodId,
                    Kind = method.MethodKind == MethodKind.Constructor ? "constructor" : "method",
                    Name = method.Name,
                    ContainingType = typeId,
                    Project = projectName,
                    FilePath = mFile,
                    StartLine = mStart,
                    EndLine = mEnd,
                    Accessibility = GetAccessibility(method.DeclaredAccessibility),
                    IsStatic = method.IsStatic,
                    IsAbstract = method.IsAbstract,
                });

                if (method.IsOverride && method.OverriddenMethod != null)
                {
                    var overriddenTypeId = GetTypeId(method.OverriddenMethod.ContainingType);
                    edges.Add(new GraphEdge
                    {
                        Type = "overrides",
                        SourceId = methodId,
                        TargetId = GetMethodId(overriddenTypeId, method.OverriddenMethod),
                        FilePath = mFile,
                        Line = mStart,
                    });
                }

                ExtractBodyEdges(method, methodId, compilation, edges);
                break;

            case IPropertySymbol property:
                var propId = GetPropertyId(typeId, property);
                var propLocation = GetFirstDeclarationLocation(property);
                if (propLocation == null)
                {
                    return; // only declared in generated build-output files
                }

                var (pFile, pStart, pEnd) = propLocation.Value;
                nodes.Add(new GraphNode
                {
                    Id = propId,
                    Kind = "property",
                    Name = property.Name,
                    ContainingType = typeId,
                    Project = projectName,
                    FilePath = pFile,
                    StartLine = pStart,
                    EndLine = pEnd,
                    Accessibility = GetAccessibility(property.DeclaredAccessibility),
                    IsStatic = property.IsStatic,
                    IsAbstract = property.IsAbstract,
                });

                if (property.Type is INamedTypeSymbol propType && propType.Locations.Any(l => l.IsInSource))
                {
                    edges.Add(new GraphEdge
                    {
                        Type = "uses",
                        SourceId = propId,
                        TargetId = GetTypeId(propType),
                        FilePath = pFile,
                        Line = pStart,
                    });
                }

                ExtractBodyEdges(property, propId, compilation, edges);
                break;

            case IFieldSymbol field:
                var fieldId = GetPropertyId(typeId, field.Name);
                var fieldLocation = GetFirstDeclarationLocation(field);
                if (fieldLocation == null)
                {
                    return; // only declared in generated build-output files
                }

                var (fFile, fStart, fEnd) = fieldLocation.Value;
                nodes.Add(new GraphNode
                {
                    Id = fieldId,
                    Kind = "field",
                    Name = field.Name,
                    ContainingType = typeId,
                    Project = projectName,
                    FilePath = fFile,
                    StartLine = fStart,
                    EndLine = fEnd,
                    Accessibility = GetAccessibility(field.DeclaredAccessibility),
                    IsStatic = field.IsStatic,
                    IsAbstract = false,
                });

                if (field.Type is INamedTypeSymbol fieldType && fieldType.Locations.Any(l => l.IsInSource))
                {
                    edges.Add(new GraphEdge
                    {
                        Type = "uses",
                        SourceId = fieldId,
                        TargetId = GetTypeId(fieldType),
                        FilePath = fFile,
                        Line = fStart,
                    });
                }
                break;

            case IEventSymbol ev:
                var evId = GetPropertyId(typeId, ev.Name);
                var evLocation = GetFirstDeclarationLocation(ev);
                if (evLocation == null)
                {
                    return; // only declared in generated build-output files
                }

                var (eFile, eStart, eEnd) = evLocation.Value;
                nodes.Add(new GraphNode
                {
                    Id = evId,
                    Kind = "event",
                    Name = ev.Name,
                    ContainingType = typeId,
                    Project = projectName,
                    FilePath = eFile,
                    StartLine = eStart,
                    EndLine = eEnd,
                    Accessibility = GetAccessibility(ev.DeclaredAccessibility),
                    IsStatic = ev.IsStatic,
                    IsAbstract = ev.IsAbstract,
                });
                break;
        }
    }

    private static void ExtractBodyEdges(ISymbol member, string memberId, Compilation compilation, List<GraphEdge> edges)
    {
        foreach (var syntaxRef in member.DeclaringSyntaxReferences)
        {
            if (PathUtils.IsInBuildOutputFolder(syntaxRef.SyntaxTree.FilePath))
            {
                continue; // skip generated build-output declarations (e.g. XAML .g.cs)
            }

            var node = syntaxRef.GetSyntax();
            var tree = syntaxRef.SyntaxTree;
            var semanticModel = compilation.GetSemanticModel(tree);
            var filePath = tree.FilePath;

            foreach (var invocation in node.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var symbolInfo = semanticModel.GetSymbolInfo(invocation);
                if (symbolInfo.Symbol is IMethodSymbol calledMethod && calledMethod.ContainingType != null)
                {
                    var targetTypeId = GetTypeId(calledMethod.ContainingType);
                    var lineSpan = invocation.GetLocation().GetLineSpan();
                    edges.Add(new GraphEdge
                    {
                        Type = "calls",
                        SourceId = memberId,
                        TargetId = GetMethodId(targetTypeId, calledMethod),
                        FilePath = filePath,
                        Line = lineSpan.StartLinePosition.Line + 1,
                    });
                }
            }

            foreach (var creation in node.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                var symbolInfo = semanticModel.GetSymbolInfo(creation);
                if (symbolInfo.Symbol is IMethodSymbol ctor && ctor.ContainingType != null)
                {
                    var targetTypeId = GetTypeId(ctor.ContainingType);
                    var lineSpan = creation.GetLocation().GetLineSpan();
                    edges.Add(new GraphEdge
                    {
                        Type = "uses",
                        SourceId = memberId,
                        TargetId = targetTypeId,
                        FilePath = filePath,
                        Line = lineSpan.StartLinePosition.Line + 1,
                    });
                }
            }
        }
    }

    public static string GetTypeId(INamedTypeSymbol type)
    {
        var display = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted));
        return display;
    }

    public static string GetMethodId(string typeId, IMethodSymbol method)
    {
        var name = method.MethodKind == MethodKind.Constructor ? ".ctor" : method.Name;
        var paramTypes = string.Join(",", method.Parameters.Select(FormatParameter));

        // Generic methods can be overloaded purely by type-parameter arity (e.g. Foo() vs
        // Foo<T>() vs Foo<T,U>()), so the arity must be encoded using the standard .NET
        // metadata `N convention, since parameter lists alone cannot disambiguate them.
        var arity = method.TypeParameters.Length > 0 ? $"`{method.TypeParameters.Length}" : string.Empty;

        // op_Implicit/op_Explicit can be overloaded by return type alone (C# allows this only
        // for conversion operators), so parameter types alone are not enough to disambiguate them.
        if (method.MethodKind == MethodKind.Conversion)
        {
            var returnType = method.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            return $"{typeId}.{name}{arity}({paramTypes}):{returnType}";
        }

        return $"{typeId}.{name}{arity}({paramTypes})";
    }

    // ref/out/in parameter modifiers make an otherwise-identical parameter type into a distinct
    // overload in C# (e.g. Foo(int) vs Foo(ref int) vs Foo(out int) vs Foo(in int)), so the
    // RefKind must be included alongside the type to avoid collisions.
    private static string FormatParameter(IParameterSymbol parameter)
    {
        var typeName = parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        return parameter.RefKind switch
        {
            RefKind.Ref => $"ref {typeName}",
            RefKind.Out => $"out {typeName}",
            RefKind.In => $"in {typeName}",
            _ => typeName,
        };
    }

    public static string GetPropertyId(string typeId, string memberName) => $"{typeId}.{memberName}";

    public static string GetPropertyId(string typeId, IPropertySymbol property)
    {
        if (!property.IsIndexer)
        {
            return GetPropertyId(typeId, property.Name);
        }

        var paramTypes = string.Join(",", property.Parameters.Select(p => p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        return $"{typeId}.this[{paramTypes}]";
    }

    private static string GetKindForType(INamedTypeSymbol type) => type.TypeKind switch
    {
        TypeKind.Class => "class",
        TypeKind.Interface => "interface",
        TypeKind.Struct => "struct",
        TypeKind.Enum => "enum",
        _ => "class",
    };

    private static string GetAccessibility(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Public => "public",
        Accessibility.Private => "private",
        Accessibility.Protected => "protected",
        Accessibility.Internal => "internal",
        Accessibility.ProtectedOrInternal => "protected internal",
        Accessibility.ProtectedAndInternal => "private protected",
        _ => "private",
    };

    /// <summary>
    /// Returns the file/line span of the symbol's first declaration, preferring a declaration
    /// outside build-output folders (obj/bin) when the symbol has multiple partial declarations
    /// (e.g. a WPF partial class split between a hand-written .xaml.cs and generated .g.cs).
    /// Returns null if every declaration is in a build-output folder.
    /// </summary>
    private static (string FilePath, int StartLine, int EndLine)? GetFirstDeclarationLocation(ISymbol symbol)
    {
        var syntaxRef = symbol.DeclaringSyntaxReferences.FirstOrDefault(r => !PathUtils.IsInBuildOutputFolder(r.SyntaxTree.FilePath))
            ?? symbol.DeclaringSyntaxReferences.FirstOrDefault();

        if (syntaxRef == null || PathUtils.IsInBuildOutputFolder(syntaxRef.SyntaxTree.FilePath))
        {
            return null;
        }

        var node = syntaxRef.GetSyntax();
        var lineSpan = node.GetLocation().GetLineSpan();
        return (syntaxRef.SyntaxTree.FilePath, lineSpan.StartLinePosition.Line + 1, lineSpan.EndLinePosition.Line + 1);
    }
}
