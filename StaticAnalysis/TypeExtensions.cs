using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;

namespace StaticAnalysis
{
	/// <summary>
	/// Extension methods for comparison of various Mono.Cecil type definitions
	/// that lack the Equals implementation.
	/// </summary>
	public static class TypeExtensions
	{
		/// <summary>
		/// Compate two TypeReferences for by-value equality.
		/// </summary>
		/// <param name="a">First reference</param>
		/// <param name="b">Second reference</param>
		/// <returns>true if references are equal, false otherwise</returns>
		public static bool TypeMatch(this TypeReference a, TypeReference b)
		{
			if (a is GenericParameter)
				return true;
			if (a is TypeSpecification || b is TypeSpecification)
			{
				if (a.GetType() != b.GetType())
					return false;
				return TypeMatch((TypeSpecification)a, (TypeSpecification)b);
			}
			return a.FullName == b.FullName;
		}

		private static bool TypeMatch(this TypeSpecification a, TypeSpecification b)
		{
			if (a is GenericInstanceType)
				return TypeMatch((GenericInstanceType)a, (GenericInstanceType)b);
			// FIXME: ModifierType
			return TypeMatch(a.ElementType, b.ElementType);
		}

		private static bool TypeMatch(this GenericInstanceType a, GenericInstanceType b)
		{
			if (!TypeMatch(a.ElementType, b.ElementType))
				return false;
			if (a.GenericArguments.Count != b.GenericArguments.Count)
				return false;
			if (a.GenericArguments.Count == 0)
				return true;
			for (var i = 0; i < a.GenericArguments.Count; i++)
				if (!TypeMatch(a.GenericArguments[i], b.GenericArguments[i]))
					return false;
			return true;
		}
	}
}
