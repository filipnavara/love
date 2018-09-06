using System;
using StaticAnalysis.Collections;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Diagnostics;

namespace StaticAnalysis.ControlFlow
{
	/// <summary>
	/// Basic execution context class that simulates type propagation through
	/// stack.
	/// </summary>
	/// <see cref="ControlFlowGraph.Traverse"/>
	public class TypeExecutionContext
	{
		private MethodDefinition method;
		private IStack<TypeReference> stack;
		private TypeReference[] locals;

		/// <summary>
		/// Initializes a new instance of TypeExecutionContext with initial
		/// context for a given method.
		/// </summary>
		/// <param name="method">Method to generate the context for</param>
		public TypeExecutionContext(
			MethodDefinition method)
		{
			this.method = method;
			this.stack = ImmutableStack<TypeReference>.Empty;
			this.locals = new TypeReference[method.Body.Variables.Count];
		}

		/// <summary>
		/// Initializes a new instance of TypeExecutionContext with a copy
		/// of existing instance.
		/// </summary>
		/// <param name="context">Context with values to be copied</param>
		public TypeExecutionContext(
			TypeExecutionContext context)
		{
			this.method = context.method;
			this.stack = context.stack;
			this.locals = (TypeReference[])context.locals.Clone();
		}

		/// <summary>
		/// Returns the method that the context was created with.
		/// </summary>
		public MethodDefinition Method
		{
			get { return this.method; }
		}

		/// <summary>
		/// Returns types or metadata references that appear on the
		/// stack in the given execution context.
		/// </summary>
		public IStack<TypeReference> Stack
		{
			get { return this.stack; }
		}

		/// <summary>
		/// Returns types or metadata references that appear in the
		/// local variables in the given execution context.
		/// </summary>
		public TypeReference[] Locals
		{
			get { return this.locals; }
		}

		private static bool IsGetTypeFromHandle(MethodReference method)
		{
			return
				method.DeclaringType.FullName.Equals("System.Type") &&
				method.Name.Equals("GetTypeFromHandle");
		}

		/// <summary>
		/// Evaluate a single CIL instruction and modify the context
		/// accordingly.
		/// </summary>
		/// <param name="instruction">Instruction to evaluate</param>
		public void EvaluateInstruction(Instruction instruction)
		{
			MethodReference reference = null;

			switch (instruction.OpCode.Code)
			{
				case Code.Call:
				case Code.Callvirt:
					reference = (MethodReference)instruction.Operand;
					break;

				case Code.Calli:
					// FIXME: Probably not handled correctly
					reference = (MethodReference)instruction.Operand;
					stack = stack.Pop(); // Pop the entrypoint
					break;

				case Code.Newobj:
					reference = (MethodReference)instruction.Operand;
					break;

				case Code.Isinst:
				case Code.Castclass:
					stack = stack.Pop().Push((TypeReference)instruction.Operand);
					break;

				case Code.Ldfld:
					stack = stack.Pop();
					goto case Code.Ldsfld;
				case Code.Ldsfld:
					stack = stack.Push(((FieldReference)instruction.Operand).FieldType);
					break;

				//case Code.Ldtoken:
				/*case Code.Ldftn:
				case Code.Ldvirtftn:
					stack = stack.Push(((MethodReference)instruction.Operand).);
					break;*/

				case Code.Dup:
					stack = stack.Push(stack.Peek());
					break;

				case Code.Ldarg_0:
					// FIXME: Is this correct?
					if (method.IsStatic)
						stack = stack.Push(method.Parameters[0]);
					else
						stack = stack.Push(method.DeclaringType);
					break;

				case Code.Ldarg_1:
				case Code.Ldarg_2:
				case Code.Ldarg_3:
					if (method.IsStatic)
						stack = stack.Push(method.Parameters[instruction.OpCode.Code - Code.Ldarg_0]);
					else
						stack = stack.Push(method.Parameters[instruction.OpCode.Code - Code.Ldarg_1]);
					break;

				case Code.Ldarg_S:
				case Code.Ldarg:
					stack = stack.Push(method.Parameters[((ParameterReference)instruction.Operand).Index]);
					break;

				case Code.Stloc_0:
				case Code.Stloc_1:
				case Code.Stloc_2:
				case Code.Stloc_3:
					locals[instruction.OpCode.Code - Code.Stloc_0] = stack.Peek();
					stack = stack.Pop();
					break;

				case Code.Stloc_S:
				case Code.Stloc:
					locals[((VariableDefinition)instruction.Operand).Index] = stack.Peek();
					stack = stack.Pop();
					break;

				case Code.Ldloc_0:
				case Code.Ldloc_1:
				case Code.Ldloc_2:
				case Code.Ldloc_3:
					if (locals[instruction.OpCode.Code - Code.Ldloc_0] != null)
						stack = stack.Push(locals[instruction.OpCode.Code - Code.Ldloc_0]);
					else
						stack = stack.Push(method.Body.Variables[instruction.OpCode.Code - Code.Ldloc_0].VariableType);
					break;

				case Code.Ldloc_S:
				case Code.Ldloc:
					if (locals[((VariableDefinition)instruction.Operand).Index] != null)
						stack = stack.Push(locals[((VariableDefinition)instruction.Operand).Index]);
					else
						stack = stack.Push(((VariableDefinition)instruction.Operand).VariableType);
					break;

				default:
					if (instruction.OpCode.StackBehaviourPush != StackBehaviour.Push0 &&
						instruction.OpCode.StackBehaviourPush != StackBehaviour.Varpush)
					{
						stack = stack.Push(null);
						if (instruction.OpCode.StackBehaviourPush == StackBehaviour.Push1_push1)
							stack = stack.Push(null);
					}

					switch (instruction.OpCode.StackBehaviourPop)
					{
						case StackBehaviour.Pop1:
						case StackBehaviour.Popi:
						case StackBehaviour.Popref:
							stack = stack.Pop();
							break;

						case StackBehaviour.Pop1_pop1:
						case StackBehaviour.Popi_pop1:
						case StackBehaviour.Popi_popi:
						case StackBehaviour.Popi_popi8:
						case StackBehaviour.Popi_popr4:
						case StackBehaviour.Popi_popr8:
						case StackBehaviour.Popref_pop1:
						case StackBehaviour.Popref_popi:
							stack = stack.Pop().Pop();
							break;

						case StackBehaviour.Popi_popi_popi:
						case StackBehaviour.Popref_popi_popi:
						case StackBehaviour.Popref_popi_popi8:
						case StackBehaviour.Popref_popi_popr4:
						case StackBehaviour.Popref_popi_popr8:
						case StackBehaviour.Popref_popi_popref:
							stack = stack.Pop().Pop().Pop();
							break;

						case StackBehaviour.PopAll:
							stack = ImmutableStack<IMetadataTokenProvider>.Empty;
							break;
					}

					break;
			}

			if (instruction.OpCode.FlowControl == FlowControl.Call)
			{
				// Newobj, Call and Callvirt
				Debug.Assert(reference != null);

				// Pop method arguments
				for (int i = reference.Parameters.Count; --i >= 0; )
					stack = stack.Pop();
				if (instruction.OpCode.Code != Code.Newobj && reference.HasThis)
					stack = stack.Pop();

				// Push return value
				if (instruction.OpCode.Code == Code.Newobj)
				{
					stack = stack.Push(reference.DeclaringType);
				}
				else
				{
					var returnType = reference.ReturnType;
					var returnTypeSpecification = reference.ReturnType as TypeSpecification;
					if (returnTypeSpecification != null && returnTypeSpecification.IsOptionalModifier)
						returnType = returnTypeSpecification.ElementType;
					if (!returnType.FullName.Equals("System.Void"))
						stack = stack.Push(reference.ReturnType);
				}
			}
		}
	}
}
