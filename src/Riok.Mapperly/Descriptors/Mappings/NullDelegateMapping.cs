using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Riok.Mapperly.Helpers;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Riok.Mapperly.Emit.Syntax.SyntaxFactoryHelper;

namespace Riok.Mapperly.Descriptors.Mappings;

/// <summary>
/// Null aware delegate mapping. Abstracts handling null values of the delegated mapping.
/// </summary>
public class NullDelegateMapping : NewInstanceMapping
{
    private const string NullableValueProperty = nameof(Nullable<int>.Value);

    private readonly INewInstanceMapping _delegateMapping;
    private readonly NullFallbackValue _nullFallbackValue;

    public NullDelegateMapping(
        ITypeSymbol nullableSourceType,
        ITypeSymbol nullableTargetType,
        INewInstanceMapping delegateMapping,
        NullFallbackValue nullFallbackValue
    )
        : base(nullableSourceType, nullableTargetType)
    {
        _delegateMapping = delegateMapping;
        _nullFallbackValue = nullFallbackValue;

        // the mapping is synthetic (produces no code)
        // if and only if the delegate mapping is synthetic (produces also no code)
        // and no null handling is required
        // (this is the case if the delegate mapping source type accepts nulls
        // or the source type is not nullable and the target type is not a nullable value type (otherwise a conversion is needed)).
        IsSynthetic =
            _delegateMapping.IsSynthetic
            && (_delegateMapping.SourceType.IsNullable() || !SourceType.IsNullable() && !TargetType.IsNullableValueType());
    }

    public override bool IsSynthetic { get; }

    public override ExpressionSyntax Build(TypeMappingBuildContext ctx)
    {
        if (_delegateMapping.SourceType.IsNullable())
            return _delegateMapping.Build(ctx);

        if (!SourceType.IsNullable())
        {
            // if the target type is a nullable value type, there needs to be an additional cast in some cases
            // (eg. in a linq expression, int => int?)
            return TargetType.IsNullableValueType()
                ? CastExpression(FullyQualifiedIdentifier(TargetType), _delegateMapping.Build(ctx))
                : _delegateMapping.Build(ctx);
        }

        // source is nullable and the mapping method cannot handle nulls,
        // call mapping only if source is not null.
        // source == null ? <null-substitute> : Map(source)
        // or for nullable value types:
        // source == null ? <null-substitute> : Map(source.Value)
        var sourceValue = SourceType.IsNullableValueType() ? MemberAccess(ctx.Source, NullableValueProperty) : ctx.Source;

        // disable nullable waring if accessing array
        if (sourceValue is ElementAccessExpressionSyntax)
        {
            sourceValue = PostfixUnaryExpression(SyntaxKind.SuppressNullableWarningExpression, sourceValue);
        }

        return Conditional(
            IsNull(ctx.Source),
            NullSubstitute(TargetType.NonNullable(), ctx.Source, _nullFallbackValue),
            _delegateMapping.Build(ctx.WithSource(sourceValue))
        );
    }
}
