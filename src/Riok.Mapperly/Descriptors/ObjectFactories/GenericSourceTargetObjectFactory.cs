using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Riok.Mapperly.Emit.Syntax.SyntaxFactoryHelper;

namespace Riok.Mapperly.Descriptors.ObjectFactories;

public class GenericSourceTargetObjectFactory : ObjectFactory
{
    private readonly int _sourceTypeParameterIndex;
    private readonly int _targetTypeParameterIndex;

    public GenericSourceTargetObjectFactory(SymbolAccessor symbolAccessor, IMethodSymbol method, int sourceTypeParameterIndex)
        : base(symbolAccessor, method)
    {
        _sourceTypeParameterIndex = sourceTypeParameterIndex;
        _targetTypeParameterIndex = (sourceTypeParameterIndex + 1) % 2;
    }

    public override bool CanCreateType(ITypeSymbol sourceType, ITypeSymbol targetTypeToCreate) =>
        SymbolAccessor.DoesTypeSatisfyTypeParameterConstraints(
            Method.TypeParameters[_sourceTypeParameterIndex],
            sourceType,
            Method.Parameters[0].Type.NullableAnnotation
        )
        && SymbolAccessor.DoesTypeSatisfyTypeParameterConstraints(
            Method.TypeParameters[_targetTypeParameterIndex],
            targetTypeToCreate,
            Method.ReturnType.NullableAnnotation
        );

    protected override ExpressionSyntax BuildCreateType(ITypeSymbol sourceType, ITypeSymbol targetTypeToCreate, ExpressionSyntax source)
    {
        var typeParams = new TypeSyntax[2];
        typeParams[_sourceTypeParameterIndex] = NonNullableIdentifier(sourceType);
        typeParams[_targetTypeParameterIndex] = NonNullableIdentifier(targetTypeToCreate);
        return GenericInvocation(Method.Name, typeParams, source);
    }
}
