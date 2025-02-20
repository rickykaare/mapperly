using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Riok.Mapperly.Helpers;
using Riok.Mapperly.Symbols;
using Riok.Mapperly.Templates;

namespace Riok.Mapperly.Descriptors.Mappings.UserMappings;

/// <summary>
/// Represents a mapping method declared but not implemented by the user which results in a new target object instance.
/// </summary>
public class UserDefinedNewInstanceMethodMapping : MethodMapping, IDelegateUserMapping
{
    private readonly bool _enableReferenceHandling;

    public UserDefinedNewInstanceMethodMapping(
        IMethodSymbol method,
        MethodParameter sourceParameter,
        MethodParameter? referenceHandlerParameter,
        bool enableReferenceHandling
    )
        : base(method, sourceParameter, referenceHandlerParameter, method.ReturnType.UpgradeNullable())
    {
        _enableReferenceHandling = enableReferenceHandling;
        Method = method;
    }

    public IMethodSymbol Method { get; }

    public INewInstanceMapping? DelegateMapping { get; private set; }

    public void SetDelegateMapping(INewInstanceMapping mapping) => DelegateMapping = mapping;

    public override IEnumerable<StatementSyntax> BuildBody(TypeMappingBuildContext ctx)
    {
        if (DelegateMapping == null)
        {
            return new[] { ctx.SyntaxFactory.ExpressionStatement(ctx.SyntaxFactory.ThrowMappingNotImplementedExceptionStatement()), };
        }

        // if reference handling is enabled and no reference handler parameter is declared
        // the generated mapping method is called with a new reference handler instance
        // otherwise the generated method is embedded
        if (_enableReferenceHandling && ReferenceHandlerParameter == null)
        {
            // new RefHandler();
            var createRefHandler = ctx.SyntaxFactory.CreateInstance(TemplateReference.PreserveReferenceHandler);
            ctx = ctx.WithRefHandler(createRefHandler);
            return new[] { ctx.SyntaxFactory.Return(DelegateMapping.Build(ctx)) };
        }

        if (DelegateMapping is MethodMapping delegateMethodMapping)
            return delegateMethodMapping.BuildBody(ctx);

        return new[] { ctx.SyntaxFactory.Return(DelegateMapping.Build(ctx)) };
    }

    /// <summary>
    /// A <see cref="UserDefinedNewInstanceMethodMapping"/> is callable by other mappings
    /// if either reference handling is not activated, or the user defined a reference handler parameter.
    /// </summary>
    public override bool CallableByOtherMappings => !_enableReferenceHandling || ReferenceHandlerParameter != null;

    internal override void EnableReferenceHandling(INamedTypeSymbol iReferenceHandlerType)
    {
        // the parameters of user defined methods should not be manipulated
        // if the user did not define a parameter a new reference handler is initialized
        if (DelegateMapping is MethodMapping methodMapping)
        {
            methodMapping.EnableReferenceHandling(iReferenceHandlerType);
        }
    }
}
