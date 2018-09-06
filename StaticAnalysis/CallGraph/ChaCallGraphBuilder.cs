using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Mono.Cecil;
using Mono.Cecil.Cil;
using QuickGraph;
using StaticAnalysis.ClassHierarchy;
using StaticAnalysis.ControlFlow;
using System.Diagnostics.Contracts;

namespace StaticAnalysis.CallGraph
{
	/// <summary>
	/// Call graph based on class hierarchy analysis.
	/// 
	/// Virtual method calls are resolved to all non-abstract methods
	/// that override the called method and to the method itself.
	/// 
	/// Delegates are resolved by linking the Invoke method to all
	/// functions that were used as first parameter forr construction of
	/// the specific delegate type.
	/// </summary>
	public class ChaCallGraphBuilder : CallGraphBuilder
	{
		private MethodDefinition rootMethod;


		/// <summary>
		/// Construct a call graph reachable from a given root method.
		/// </summary>
		/// <param name="rootMethod">Root method (eg. assembly entrypoint or thread entrypoint)</param>
		public ChaCallGraphBuilder(MethodDefinition rootMethod)
		{
			Contract.Requires(rootMethod != null);
			this.rootMethod = rootMethod;
		}

		/// <summary>
		/// Build the call graph.
		/// </summary>
		/// <param name="cancellationToken">Cancellation token that allows to interrupt the call graph construction.</param>
		public override CallGraph Build(CancellationToken cancellationToken)
		{
			var classHiearchyGraph = new ClassHierarchyGraph(rootMethod.DeclaringType);
			var callGraph = new BidirectionalGraph<MethodDefinition, CallGraphEdge>(false);
			AnalyzeCalls(callGraph, classHiearchyGraph, rootMethod, cancellationToken);
			AnalyzeDelegates(callGraph, classHiearchyGraph, rootMethod, cancellationToken);
			return new CallGraph(callGraph);
		}

		private static void AnalyzeCalls(
			BidirectionalGraph<MethodDefinition, CallGraphEdge> callGraph,
			ClassHierarchyGraph classHiearchyGraph,
			MethodDefinition rootMethod,
			CancellationToken cancellationToken)
		{
			var methodsToAnalyze = new Stack<MethodDefinition>();
			var calledMethods = new List<Tuple<MethodDefinition, Instruction>>();
			var instantiatedTypes = new HashSet<TypeDefinition>();

			// Start analysis from the root method
			methodsToAnalyze.Push(rootMethod);
			callGraph.AddVertex(rootMethod);
			instantiatedTypes.Add(rootMethod.DeclaringType);

			while (methodsToAnalyze.Count > 0)
			{
				MethodDefinition method = methodsToAnalyze.Pop();

				cancellationToken.ThrowIfCancellationRequested();

				// For each method with body add edges for any called method. If the target
				// method wasn't analyzed yet then add it to queue. Also enqueue all
				// referenced methods (ldftn) since they may later be connected during the
				// delegate invocation analysis.
				if (method.HasBody)
				{
					var controlFlowGraph = new ControlFlowGraph(method.Body);
					controlFlowGraph.Traverse(delegate(Instruction instruction, TypeExecutionContext context)
					{
						if (instruction.OpCode.FlowControl == FlowControl.Call &&
							instruction.OpCode.Code != Code.Calli)
						{
							var callTarget = ((MethodReference)instruction.Operand).Resolve();

							// call, newobj - fixed reference
							// calli - (unsupported for now, COM interop?)
							// callvirt - virtual method resolution
							if (instruction.OpCode.Code == Code.Call ||
								instruction.OpCode.Code == Code.Newobj)
							{
								if (callTarget != null)
								{
									if (instruction.OpCode.Code == Code.Newobj)
										instantiatedTypes.Add(callTarget.DeclaringType);
									calledMethods.Add(Tuple.Create(callTarget, instruction));
								}
							}
							else if (instruction.OpCode.Code == Code.Callvirt)
							{
								// FIXME: Can actually call base type method, if not overriden.
								// FIXME: This looks inefficient, does the reference method always belong
								// to the type that was declared or can it refer to supertype?
								// (eg. System.Object.ToString())
								/*if (!callTarget.IsAbstract)
									calledMethods.Add(Tuple.Create(callTarget, instruction));
								foreach (var overridingMethod in classHiearchyGraph.GetMethodOverrides(callTarget))
									calledMethods.Add(Tuple.Create(overridingMethod, instruction));*/
								// XXX
								var stackSymbol = context.Stack.Skip(callTarget.Parameters.Count).FirstOrDefault();
								if (stackSymbol is FieldReference)
									stackSymbol = ((FieldReference)stackSymbol).FieldType;
								if (stackSymbol is ParameterReference)
									stackSymbol = ((ParameterReference)stackSymbol).ParameterType;
								var variableType = stackSymbol as TypeReference;
								if (variableType != null)
									variableType = variableType.Resolve();
								if (variableType == null)
									variableType = callTarget.DeclaringType;
								foreach (var overridingMethod in classHiearchyGraph.GetMethodImplementations(variableType.Resolve(), callTarget))
									calledMethods.Add(Tuple.Create(overridingMethod, instruction));
							}
						}
						else if (instruction.OpCode.Code == Code.Ldftn ||
							instruction.OpCode.Code == Code.Ldvirtftn)
						{
							// A pointer to function is loaded onto the stack. While this function may
							// not be referenced in the end, we analyze it anyway since it ensures that
							// all possible targets of delegate invocation are in the call graph.
							var target = ((MethodReference)instruction.Operand).Resolve();
							System.Diagnostics.Debug.Assert(target != null);
							if (target != null && !callGraph.ContainsVertex(target))
							{
								methodsToAnalyze.Push(target);
								callGraph.AddVertex(target);
							}
						}

						context.EvaluateInstruction(instruction);
					},
					context => new TypeExecutionContext(context),
					new TypeExecutionContext(method));

					// Add static contructors
					foreach (var type in calledMethods.Select(m => m.Item1.DeclaringType).Distinct())
					{
						foreach (var staticConstructor in type.Methods.Where(m => m.IsConstructor && m.IsStatic))
						{
							if (!callGraph.ContainsVertex(staticConstructor))
							{
								methodsToAnalyze.Push(staticConstructor);
								callGraph.AddVertex(staticConstructor);
							}
						}
					}

					foreach (var target in calledMethods)
					{
						if (!callGraph.ContainsVertex(target.Item1))
						{
							methodsToAnalyze.Push(target.Item1);
							callGraph.AddVertex(target.Item1);
						}
						callGraph.AddEdge(new CallGraphEdge(new ProgramPoint(method, target.Item2), target.Item1));
					}
					
					calledMethods.Clear();
					method.Body = null; // Save memory
				}
			}

			// Prune instance methods of types that were never instantiated
			callGraph.RemoveVertexIf(
				v => v.HasThis &&
				!instantiatedTypes.Contains(v.DeclaringType));
		}

		private static void AnalyzeDelegates(
			BidirectionalGraph<MethodDefinition, CallGraphEdge> callGraph,
			ClassHierarchyGraph classHiearchyGraph,
			MethodDefinition rootMethod,
			CancellationToken cancellationToken)
		{
			// Now the generated call graph contains overapproximation of the actual call graph
			// sans the delegate invocation edges. To resolve delegates a flow-sensitive analysis
			// is needed. We locate all invocations of delegate constructors, extract the
			// called methods from them and create edges between the delegate Invoke method and
			// the extracted methods.

			// FIXME: Locating the System.Delegate type is probably not entirely correct.
			TypeDefinition systemDelegateType = null;
			foreach (var reference in rootMethod.Module.AssemblyReferences)
			{
				if (reference.Name == "mscorlib")
				{
					systemDelegateType = new TypeReference("System", "MulticastDelegate", rootMethod.Module, reference).Resolve();
					break;
				}
			}
			if (systemDelegateType == null)
				return;

			foreach (var delegateType in classHiearchyGraph.GetDerivedClasses(systemDelegateType))
			{
				MethodDefinition delegateConstructor = null;
				MethodDefinition delegateInvoke = null;

				cancellationToken.ThrowIfCancellationRequested();

				foreach (var delegateMethod in delegateType.Methods)
				{
					if (delegateMethod.IsConstructor)
						delegateConstructor = delegateMethod;
					if (delegateMethod.Name == "Invoke")
						delegateInvoke = delegateMethod;
				}

				System.Diagnostics.Debug.Assert(delegateConstructor != null);
				System.Diagnostics.Debug.Assert(delegateInvoke != null);

				// Check if Invoke was called for the particular delegate
				// FIXME: Sometimes it isn't called because the method is
				// implemented in runtime.
				if (!callGraph.ContainsVertex(delegateInvoke))
					callGraph.AddVertex(delegateInvoke);

				IEnumerable<CallGraphEdge> edges;
				if (callGraph.TryGetInEdges(delegateConstructor, out edges))
				{
					foreach (var callingMethod in edges)
					{
						cancellationToken.ThrowIfCancellationRequested();

						var cfg = new ControlFlowGraph(callingMethod.Source.Body);
						cfg.Traverse(
							(instruction, context) =>
							{
								if (instruction.OpCode.Code == Code.Newobj &&
									((MethodReference)instruction.Operand).Resolve() == delegateConstructor)
								{
									callGraph.AddVerticesAndEdge(new CallGraphEdge(
										new ProgramPoint(delegateInvoke, null),
										((MethodReference)context.Stack.Peek()).Resolve()));
								}
								context.EvaluateInstruction(instruction);
							},
							context => new TypeExecutionContext(context),
							new TypeExecutionContext(callingMethod.Source));
						callingMethod.Source.Body = null; // Save memory
					} 
				}
			}
		}
	}
}
