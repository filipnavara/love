using System;
using System.Collections.Generic;
using Mono.Cecil;
using QuickGraph;
using Mono.Cecil.Cil;
using StaticAnalysis;

namespace StaticAnalysis.ClassHierarchy
{
	/// <summary>
	/// Graph representing class hierarchy of all reachable classes from
	/// a root class.
	/// </summary>
	public class ClassHierarchyGraph
	{
		private readonly IVertexListGraph<TypeDefinition, SEquatableEdge<TypeDefinition>> graph;
		private readonly Dictionary<Tuple<TypeDefinition, MethodReference>, MethodDefinition> overrides;

		/// <summary>
		/// Construct a class hierarchy graph for all reachable types
		/// from a root type.
		/// </summary>
		/// <param name="root">Root type (eg. type corresponding to assembly entrypoint)</param>
		public ClassHierarchyGraph(TypeDefinition root)
		{
			var graph = new AdjacencyGraph<TypeDefinition, SEquatableEdge<TypeDefinition>>();
			Create(graph, (v, w) => new SEquatableEdge<TypeDefinition>(w, v), root);
			this.graph = graph.ToCompressedRowGraph();
			this.overrides = new Dictionary<Tuple<TypeDefinition, MethodReference>, MethodDefinition>();
		}

		/// <summary>
		/// Create a graph representing class hierarchy of all reachable classes
		/// from a root class.
		/// </summary>
		/// <typeparam name="TEdge">Storage type for edges in the graph</typeparam>
		/// <param name="graph">Empty graph to be populated</param>
		/// <param name="edgeFactory">Factory for creating edges</param>
		/// <param name="root">Root class to be analyzed</param>
		private static void Create<TEdge>(
			IMutableVertexAndEdgeListGraph<TypeDefinition, TEdge> graph,
			EdgeFactory<TypeDefinition, TEdge> edgeFactory,
			TypeDefinition root)
			where TEdge : IEdge<TypeDefinition>
		{
			var typesToAnalyze = new Stack<TypeDefinition>();

			typesToAnalyze.Push(root);
			graph.AddVertex(root);

			while (typesToAnalyze.Count > 0)
			{
				var currentType = typesToAnalyze.Pop();

				// Link to parent class and interfaces, if any
				if (IsDerivedType(currentType))
				{
					var edges = new List<TEdge>(1 + currentType.Interfaces.Count);

					if (currentType.BaseType != null)
					{
						var baseType = currentType.BaseType.Resolve();
						if (!graph.ContainsVertex(baseType))
						{
							typesToAnalyze.Push(baseType);
							graph.AddVertex(baseType);
						}
						edges.Add(edgeFactory(currentType, baseType));
					}

					foreach (var interfaceTypeReference in currentType.Interfaces)
					{
						var interfaceType = interfaceTypeReference.Resolve();
						if (!graph.ContainsVertex(interfaceType))
						{
							typesToAnalyze.Push(interfaceType);
							graph.AddVertex(interfaceType);
						}
						edges.Add(edgeFactory(currentType, interfaceType));
					}

					graph.AddEdgeRange(edges);
				}

				// Find referenced classes
				foreach (var method in currentType.Methods)
				{
					if (method.HasBody)
					{
						foreach (var referencedType in GetReferencedTypes(method.Body))
						{
							var referencedTypeDefinition = referencedType.Resolve();
							if (graph.ContainsVertex(referencedTypeDefinition))
								continue;
							typesToAnalyze.Push(referencedTypeDefinition);
							graph.AddVertex(referencedTypeDefinition);
						}
						method.Body = null; // Save memory
					}
				}
			}
		}

		/// <summary>
		/// Get all types that are referenced in given method body.
		/// </summary>
		/// <param name="body">Method body to analyze</param>
		/// <returns>An enumerator that can be used to iterate over the referenced types.</returns>
		private static IEnumerable<TypeReference> GetReferencedTypes(MethodBody body)
		{
			foreach (var instruction in body.Instructions)
			{
				// Newobj, Call, Callvirt
				if (instruction.OpCode.FlowControl == FlowControl.Call &&
					instruction.OpCode.Code != Code.Calli)
					yield return ((MethodReference)instruction.Operand).DeclaringType;
				// Ldftn, Ldvirtftn
				if (instruction.OpCode.Code == Code.Ldftn || instruction.OpCode.Code == Code.Ldvirtftn)
					yield return ((MethodReference)instruction.Operand).DeclaringType;
			}
		}

		/// <summary>
		/// Get all directly or indirectly derived types from a specified type.
		/// </summary>
		/// <param name="type">Base type</param>
		/// <returns>Iterator over all derived types</returns>
		public IEnumerable<TypeDefinition> GetDerivedClasses(TypeDefinition type)
		{
			IEnumerable<SEquatableEdge<TypeDefinition>> edges;
			if (graph.TryGetOutEdges(type, out edges))
			{
				foreach (var derivedType in edges)
				{
					yield return derivedType.Target;
					foreach (var derivedType2 in GetDerivedClasses(derivedType.Target))
						yield return derivedType2;
				}
			}
		}

		private MethodDefinition FindMethodOverride(
			TypeDefinition type,
			MethodReference method)
		{
			var cacheTuple = Tuple.Create(type, method);
			MethodDefinition @override;
			if (overrides.TryGetValue(cacheTuple, out @override))
				return @override;

			foreach (var derivedMethod in type.Methods)
			{
				// FIXME: Explicit overrides (.Overrides)

				if (!derivedMethod.IsVirtual)
					continue;
				if (derivedMethod.Name != method.Name)
					continue;
				if (!derivedMethod.ReturnType.TypeMatch(method.ReturnType))
					continue;
				if (method.Parameters.Count != derivedMethod.Parameters.Count)
					continue;
				for (int i = 0; i < method.Parameters.Count; i++)
					if (!derivedMethod.Parameters[i].ParameterType.TypeMatch(method.Parameters[i].ParameterType))
						continue;
				overrides[cacheTuple] = derivedMethod;
				return derivedMethod;
			}

			overrides[cacheTuple] = null;
			return null;
		}
		
		/// <summary>
		/// Get all overrides of a method.
		/// </summary>
		/// <param name="method">Base virtual method</param>
		/// <returns>Iterator over all method overrides</returns>
		public IEnumerable<MethodDefinition> GetMethodOverrides(
			MethodReference method)
		{
			return GetMethodOverrides(method.DeclaringType.Resolve(), method);
		}

		/// <summary>
		/// Get all overrides of a method for particular declared type.
		/// </summary>
		/// <param name="method">Base virtual method</param>
		/// <param name="type">Declared type of the object the method is called on</param>
		/// <returns>Iterator over all method overrides</returns>
		public IEnumerable<MethodDefinition> GetMethodOverrides(
			TypeDefinition type,
			MethodReference method)
		{
			foreach (var derivedType in GetDerivedClasses(type))
			{
				var derivedMethod = FindMethodOverride(derivedType, method);
				if (derivedMethod != null)
					yield return derivedMethod;
			}
		}

		/// <summary>
		/// Get all implementations of a method. This includes the actual method, if
		/// non-abstract, and all overrides.
		/// </summary>
		/// <param name="method">Base virtual method</param>
		/// <returns>Iterator over all method overrides</returns>
		public IEnumerable<MethodDefinition> GetMethodImplementations(
			MethodReference method)
		{
			return GetMethodImplementations(method.DeclaringType.Resolve(), method);
		}

		/// <summary>
		/// Get all implementations of a method for particular declared type. This includes
		/// the actual method, if non-abstract, and all overrides.
		/// </summary>
		/// <param name="method">Base virtual method</param>
		/// <param name="type">Declared type of the object the method is called on</param>
		/// <returns>Iterator over all method implementations</returns>
		public IEnumerable<MethodDefinition> GetMethodImplementations(
			TypeDefinition type,
			MethodReference method)
		{
			var methodDefinition = method.Resolve();

			if (methodDefinition != null && !methodDefinition.IsVirtual)
			{
				yield return methodDefinition;
				yield break;
			}

			for (var baseType = type; baseType != null; baseType = baseType.BaseType != null ? baseType.BaseType.Resolve() : null)
			{
				var baseMethod = FindMethodOverride(baseType, method);
				if (baseMethod != null)
				{
					yield return baseMethod;
					break;
				}
			}

			foreach (var @override in GetMethodOverrides(type, method))
				yield return @override;
		}

		/// <summary>
		/// Get least common type for all given types in the class hierarchy.
		/// </summary>
		/// <param name="types">List of types to get the least common type for</param>
		/// <returns>Least common type that all types derive from</returns>
		public static TypeDefinition GetLeastCommonAncestor(
			params TypeDefinition[] types)
		{
			var ancestors = new Stack<TypeDefinition>[types.Length];
			TypeDefinition leastCommonAncestor = null;
			int index = 0;

			if (types.Length == 0)
				return null;
			if (types.Length == 1)
				return types[0];

			foreach (var type in types)
			{
				var ancestorStack = new Stack<TypeDefinition>();
				for (var currentType = type; currentType != null; currentType = currentType.BaseType != null ? currentType.BaseType.Resolve() : null)
					ancestorStack.Push(currentType);
				ancestors[index++] = ancestorStack;
			}

			while (ancestors[0].Count > 0)
			{
				var currentType = ancestors[0].Pop();
				for (index = 1; index < types.Length; index++)
				{
					if (ancestors[index].Count == 0 ||
						ancestors[index].Pop() != currentType)
						return leastCommonAncestor;
				}
				leastCommonAncestor = currentType;
			}

			return leastCommonAncestor;
		}	

		#region QuickGraph

		private bool TryGetInEdges(TypeDefinition vertex, out IEnumerable<SEquatableEdge<TypeDefinition>> edges)
		{
			return this.graph.TryGetOutEdges(vertex, out edges);
		}

		private static bool TryGetOutEdges(TypeDefinition vertex, out IEnumerable<SEquatableEdge<TypeDefinition>> edges)
		{
			if (IsDerivedType(vertex))
			{
				var outEdges = new List<SEquatableEdge<TypeDefinition>>();
				if (vertex.BaseType != null)
					outEdges.Add(new SEquatableEdge<TypeDefinition>(vertex, vertex.BaseType.Resolve()));
				foreach (var interfaceTypeReference in vertex.Interfaces)
				{
					var interfaceType = interfaceTypeReference.Resolve();
					outEdges.Add(new SEquatableEdge<TypeDefinition>(vertex, interfaceType));
				}
				edges = outEdges;
				return true;
			}
			edges = null;
			return false;
		}

		/// <summary>
		/// Get a representation of the call graph as QuickGraph object that
		/// can be used for special algorithmic analyses or for vizualization
		/// of the graph.
		/// </summary>
		public IBidirectionalIncidenceGraph<TypeDefinition, SEquatableEdge<TypeDefinition>> QuickGraph
		{
			get
			{
				return new DelegateBidirectionalIncidenceGraph<TypeDefinition, SEquatableEdge<TypeDefinition>>(
					TryGetOutEdges, TryGetInEdges);
			}
		}

		#endregion

		#region TypeDefinition helper routines
		// FIXME: Move all this to Mono.Cecil?

		private static bool IsDerivedType(TypeDefinition type)
		{
			if (type.BaseType != null)
				return true;
			if (type.HasInterfaces)
				return true;
			return false;
		}

		#endregion
	}
}
