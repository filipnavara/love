using System.Diagnostics;
using Mono.Cecil;
using Mono.Cecil.Cil;
using StaticAnalysis;
using StaticAnalysis.DataFlow;

namespace Love.IntraproceduralAnalysis
{
	class VariableAnalysis //: DataFlowProblem<VariableState>
	{
		private AnalysisOptions options;

		public VariableAnalysis(AnalysisOptions options)
		{
			this.options = options;
		}

		private static bool IsGetTypeFromHandle(MethodReference method)
		{
			return
				method.DeclaringType.FullName.Equals("System.Type") &&
				method.Name.Equals("GetTypeFromHandle");
		}

		private static TypeReference StripModifiers(TypeReference type)
		{
			var typeSpecification = type as TypeSpecification;
			if (typeSpecification != null && (typeSpecification.IsOptionalModifier || typeSpecification.IsRequiredModifier))
				return typeSpecification.ElementType;
			return type;
		}

		public void ApplyRules(ProgramPoint programPoint, VariableState state)
		{
			MethodReference reference = null;
			var instruction = programPoint.Instruction;
			var method = programPoint.Method;

			switch (instruction.OpCode.Code)
			{
				case Code.Calli:
					// FIXME: Probably not handled correctly
					state.StackVariables.Pop(); // Pop the entrypoint
					goto case Code.Call;
				case Code.Call:
				case Code.Callvirt:
				case Code.Newobj:
					reference = (MethodReference)instruction.Operand;
					break;

				case Code.Newarr:
					state.StackVariables.Pop(); // Skip size
					state.StackVariables.Push(new ObjectReferenceStackEntry(new HeapObject(
						programPoint,
						new ArrayType((TypeReference)instruction.Operand, 1))));					
					break;

				case Code.Isinst:
				case Code.Castclass:
					var stackObject = state.StackVariables.Pop() as ObjectReferenceStackEntry;
					if (stackObject == null)
						state.StackVariables.Push(stackObject);
					else
						state.StackVariables.Push(new ObjectReferenceStackEntry(
							new HeapObject(stackObject.Value.ProgramPoint, (TypeReference)instruction.Operand)));
					break;

				case Code.Ldfld:
					state.StackVariables.Pop();
					goto case Code.Ldsfld;
				case Code.Ldsfld:
					var fieldReference = (FieldReference)instruction.Operand;
					if ((this.options & AnalysisOptions.NoAliasing) != 0)
						state.StackVariables.Push(new ObjectReferenceStackEntry(new UnaliasedFieldHeapObject(fieldReference.Resolve())));
					else if (fieldReference.Resolve().IsInitOnly/* ||
						fieldReference.Name.EndsWith("lock", System.StringComparison.OrdinalIgnoreCase) ||
						fieldReference.Name.EndsWith("sync", System.StringComparison.OrdinalIgnoreCase) ||
						fieldReference.Name.EndsWith("root", System.StringComparison.OrdinalIgnoreCase)*/)
						state.StackVariables.Push(new ObjectReferenceStackEntry(new UnaliasedFieldHeapObject(fieldReference.Resolve())));
					else
						state.StackVariables.Push(new ObjectReferenceStackEntry(
							new HeapObject(programPoint, ((FieldReference)instruction.Operand).FieldType)));
					break;

				/*case Code.Ldind_Ref:
					var indirectReference = (ByReferenceType)state.StackVariables.Pop().Type;
					state.StackVariables.Push(new ObjectReferenceStackEntry(
						new HeapObject(programPoint, indirectReference.ElementType)));
					break;*/

				case Code.Ldelem_Ref:
					state.StackVariables.Pop(); // Skip the index
					var stackReference = state.StackVariables.Pop() as ObjectReferenceStackEntry;
					if (stackReference != null)
					{
						var arrayType = (ArrayType)StripModifiers(stackReference.Value.Type);
						state.StackVariables.Push(new ObjectReferenceStackEntry(
							new HeapObject(programPoint, arrayType.ElementType)));
					}
					else
					{
						// FIXME: Shouldn't get here
						state.StackVariables.Push(null);
					}
					break;

				case Code.Ldtoken:
					TypeReference typeReference = instruction.Operand as TypeReference;
					if (typeReference != null)
						state.StackVariables.Push(new ObjectReferenceStackEntry(new TypeOfHeapObject(typeReference)));
					else
						state.StackVariables.Push(null);
					break;

				/*case Code.Ldtoken:
				case Code.Ldftn:
					stack = stack.Push(instruction.Operand as IMetadataTokenProvider);
					break;*/

				case Code.Dup:
					state.StackVariables.Push(state.StackVariables.Peek());
					break;

				case Code.Ldarg_0:
				case Code.Ldarg_1:
				case Code.Ldarg_2:
				case Code.Ldarg_3:
					state.StackVariables.Push(new ObjectReferenceStackEntry(
						state.ParameterVariables[instruction.OpCode.Code - Code.Ldarg_0]));
					break;

				case Code.Ldarg_S:
				case Code.Ldarg:
					state.StackVariables.Push(new ObjectReferenceStackEntry(
						state.ParameterVariables[((ParameterReference)instruction.Operand).Index + (method.HasThis ? 1 : 0)]));
					break;

				case Code.Stloc_0:
				case Code.Stloc_1:
				case Code.Stloc_2:
				case Code.Stloc_3:
					var stackEntry = state.StackVariables.Pop() as ObjectReferenceStackEntry;
					state.LocalVariables[instruction.OpCode.Code - Code.Stloc_0] = stackEntry == null ? null : stackEntry.Value;
					break;

				case Code.Stloc_S:
				case Code.Stloc:
					stackEntry = state.StackVariables.Pop() as ObjectReferenceStackEntry;
					state.LocalVariables[((VariableDefinition)instruction.Operand).Index] = stackEntry == null ? null : stackEntry.Value;
					break;

				case Code.Ldloc_0:
				case Code.Ldloc_1:
				case Code.Ldloc_2:
				case Code.Ldloc_3:
					var localVariable = state.LocalVariables[instruction.OpCode.Code - Code.Ldloc_0];
					state.StackVariables.Push(localVariable == null ? (StackEntry)NullStackEntry.Null : new ObjectReferenceStackEntry(localVariable));
					break;

				case Code.Ldloc_S:
				case Code.Ldloc:
					localVariable = state.LocalVariables[((VariableDefinition)instruction.Operand).Index];
					state.StackVariables.Push(localVariable == null ? (StackEntry)NullStackEntry.Null : new ObjectReferenceStackEntry(localVariable));
					break;

				case Code.Ldstr:
					state.StackVariables.Push(new ObjectReferenceStackEntry(new HeapObject(programPoint, method.Module.TypeSystem.String)));
					break;
					 
				/*case Code.Ldnull:
					state.StackVariables.Push(new HeapObject(instruction, method.Module.TypeSystem.Object));
					break;*/

				default:
					switch (instruction.OpCode.StackBehaviourPop)
					{
						case StackBehaviour.Pop1:
						case StackBehaviour.Popi:
						case StackBehaviour.Popref:
							state.StackVariables.Pop();
							break;

						case StackBehaviour.Pop1_pop1:
						case StackBehaviour.Popi_pop1:
						case StackBehaviour.Popi_popi:
						case StackBehaviour.Popi_popi8:
						case StackBehaviour.Popi_popr4:
						case StackBehaviour.Popi_popr8:
						case StackBehaviour.Popref_pop1:
						case StackBehaviour.Popref_popi:
							state.StackVariables.Pop();
							state.StackVariables.Pop();
							break;

						case StackBehaviour.Popi_popi_popi:
						case StackBehaviour.Popref_popi_popi:
						case StackBehaviour.Popref_popi_popi8:
						case StackBehaviour.Popref_popi_popr4:
						case StackBehaviour.Popref_popi_popr8:
						case StackBehaviour.Popref_popi_popref:
							state.StackVariables.Pop();
							state.StackVariables.Pop();
							state.StackVariables.Pop();
							break;

						case StackBehaviour.PopAll:
							state.StackVariables.Clear();
							break;
					}

					if (instruction.OpCode.StackBehaviourPush != StackBehaviour.Push0 &&
						instruction.OpCode.StackBehaviourPush != StackBehaviour.Varpush)
					{
						state.StackVariables.Push(null);
						if (instruction.OpCode.StackBehaviourPush == StackBehaviour.Push1_push1)
							state.StackVariables.Push(null);
					}

					break;
			}

			if (instruction.OpCode.FlowControl == FlowControl.Call)
			{
				// Newobj, Call and Callvirt
				Debug.Assert(reference != null);

				if (instruction.OpCode.Code == Code.Call &&
					IsGetTypeFromHandle(reference))
				{
					// Leave the stack untouched (see ldtoken hack above)
				}
				else
				{
					// Pop method arguments
					for (int i = reference.Parameters.Count; --i >= 0; )
						state.StackVariables.Pop();
					if (instruction.OpCode.Code != Code.Newobj && reference.HasThis)
						state.StackVariables.Pop();

					// Push return value
					if (instruction.OpCode.Code == Code.Newobj)
					{
						state.StackVariables.Push(new ObjectReferenceStackEntry(
							new HeapObject(programPoint, reference.DeclaringType)));
					}
					else
					{
						var returnType = StripModifiers(reference.ReturnType);
						if (returnType.Namespace != "System" || returnType.Name != "Void")
							state.StackVariables.Push(new ObjectReferenceStackEntry(
								new HeapObject(programPoint, returnType)));
					}
				}
			}

			Debug.Assert(state.StackVariables.Count <= method.Body.MaxStackSize);
		}
	}
}
