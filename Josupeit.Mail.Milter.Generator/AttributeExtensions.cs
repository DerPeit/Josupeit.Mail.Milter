using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Josupeit.Mail.Milter.Generator;

internal static class AttributeExtensions
{
    public static IEnumerable<TAttribute> GetAttributes<TAttribute>(this ISymbol subject) where TAttribute : Attribute
    {
        if (subject == null)
            throw new ArgumentNullException(nameof(subject));

        foreach (var attributeData in subject.GetAttributes())
        {
            var attributeClass = attributeData.AttributeClass;
            if (attributeClass == null)
                continue;

            var isCorrectAttribute = attributeClass.IsAttribute<TAttribute>();
            if (!isCorrectAttribute)
                continue;

            var constructorArguments = attributeData.ConstructorArguments.Select(x => x.Value).ToArray();
            var attribute = (TAttribute)Activator.CreateInstance(typeof(TAttribute), constructorArguments);

            yield return attribute;
        }
    }

    public static bool HasAttribute<TAttribute>(this ISymbol subject) where TAttribute : Attribute
    {
        if (subject == null)
            throw new ArgumentNullException(nameof(subject));

        return subject.GetAttributes().Any(ad => ad.AttributeClass != null && ad.AttributeClass.IsAttribute<TAttribute>());
    }

    public static bool IsAttribute<TAttribute>(this ISymbol attributeCandidate) where TAttribute : Attribute
    {
        if (attributeCandidate == null)
            throw new ArgumentNullException(nameof(attributeCandidate));
        
        if (attributeCandidate is not INamedTypeSymbol namedType)
            return false;

        if (attributeCandidate.ContainingAssembly.ToDisplayString() != typeof(TAttribute).Assembly.FullName)
            return false;

        if (attributeCandidate.ContainingNamespace.ToDisplayString() != typeof(TAttribute).Namespace)
            return false;

        if (attributeCandidate.ToDisplayString() != typeof(TAttribute).FullName)
            return false;

        return true;
    }
}