using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil.Cil;
using System.Collections;
using System.Diagnostics;
using QuickGraph;
using QuickGraph.Algorithms.TopologicalSort;

namespace StaticAnalysis.ControlFlow
{
	/// <summary>
	/// Control flow graph of a single method.
	/// 
	/// Normal control flow is computed and represented separately from the
	/// exception control flow. This makes the construction easier while extracting
	/// enough information for certain analyses to be performed and at the same
	/// time allowing merged control flow graph to be produced.
	/// </summary>
	public class ControlFlowGraph
	{
		private readonly BasicBlock[] basicBlocks;
		private readonly Dictionary<int, BasicBlock> basicBlockMap;
		private readonly ExceptionBlock[] exceptionBlocks;

		/// <summary>
		/// Construct a control flow graph from a method body.
		/// </summary>
		/// <param name="body">Method body</param>
		public ControlFlowGraph(MethodBody body)
		{
			var instructions = body.Instructions;
			var firstInstruction = body.Instructions[0];
			var basicBlocks = new List<BasicBlock>();
			var finallyTarget = new Dictionary<int, Instruction>();
			var basicBlockBarrier = new BitArray(body.CodeSize);
			int blockIndex = 0;

			// Detect branch destination since there has to be a basic block at
			// each of them. The destinations could possibly be between two
			// regular instruction.
			foreach (Instruction i in instructions)
			{
				switch (i.OpCode.FlowControl)
				{
					case FlowControl.Branch:
						basicBlockBarrier[((Instruction)i.Operand).Offset] = true;
						break;

					case FlowControl.Cond_Branch:
						switch (i.OpCode.OperandType)
						{
							case OperandType.InlineSwitch:
								foreach (Instruction operandInstruction in (Instruction[])i.Operand)
									basicBlockBarrier[operandInstruction.Offset] = true;
								break;

							case OperandType.ShortInlineBrTarget:
							case OperandType.InlineBrTarget:
								basicBlockBarrier[((Instruction)i.Operand).Offset] = true;
								break;
						}
						break;
				}
			}

			// Analogously to the branch destinations we also want
			// break the basic blocks at boundaries of exception handler
			// blocks.
			foreach (var handler in body.ExceptionHandlers)
			{
				basicBlockBarrier[handler.TryStart.Offset] = true;
				basicBlockBarrier[handler.HandlerStart.Offset] = true;
				if (handler.HandlerType == ExceptionHandlerType.Filter)
					basicBlockBarrier[handler.FilterStart.Offset] = true;
			}

			foreach (Instruction i in instructions)
			{
				Instruction[] linkedInstructions;

				switch (i.OpCode.FlowControl)
				{
					case FlowControl.Next:
					case FlowControl.Call:
						// FIXME: Call can throw an exception, should it end a BB?
					case FlowControl.Meta: // Meta-information about next instruction (eg. "unaligned")
					case FlowControl.Phi: // Obsolete and not used
					case FlowControl.Break: // Debuging break
					default:
						// Stay in current basic block, move to next instruction
						// except if we hit branch destination.
						if (!basicBlockBarrier.Get(i.Next.Offset))
							continue;
						linkedInstructions = new[] { i.Next };
						break;

					case FlowControl.Branch:
						// End current basic block and link it to branch destination
						linkedInstructions = new[] { (Instruction)i.Operand };
						// Merge finally blocks into regular control flow
						if (i.OpCode.Code == Code.Leave || i.OpCode.Code == Code.Leave_S)
						{
							// Link to the finally block and relink the finally block
							// to jump to the Leave target.
							Instruction lastEndFinally = null;
							foreach (var exceptionHandler in body.ExceptionHandlers)
							{
								if (exceptionHandler.HandlerType == ExceptionHandlerType.Finally ||
									exceptionHandler.HandlerType == ExceptionHandlerType.Fault)
								{
									if (exceptionHandler.TryStart.Offset <= i.Offset &&
										exceptionHandler.TryEnd.Offset > i.Offset)
									{
										if (lastEndFinally != null)
											finallyTarget[lastEndFinally.Offset] = exceptionHandler.HandlerStart;
										else
											linkedInstructions = new[] { exceptionHandler.HandlerStart };
										if (exceptionHandler.HandlerEnd != null)
										{
											lastEndFinally = exceptionHandler.HandlerEnd.Previous;
											finallyTarget[lastEndFinally.Offset] = (Instruction)i.Operand;
										}
									}
								}
							}
						}
						break;

					case FlowControl.Cond_Branch:
						// End current basic block and link it to branch destination
						// and next instruction(s) as well.
						switch (i.OpCode.OperandType)
						{
							case OperandType.InlineSwitch:
								if (i.Next != null)
								{
									var linkedInstructionsTemp = new List<Instruction>((Instruction[])i.Operand);
									linkedInstructionsTemp.Add(i.Next);
									linkedInstructions = linkedInstructionsTemp.ToArray();
								}
								else
									linkedInstructions = (Instruction[])i.Operand;
								break;

							case OperandType.ShortInlineBrTarget:
							case OperandType.InlineBrTarget:
								if (i.Next != null)
									linkedInstructions = new[] { i.Next, (Instruction)i.Operand };
								else
									linkedInstructions = new[] { (Instruction)i.Operand };
								break;

							default:
								Debug.Assert(false, "Unknown operand type");
								throw new NotSupportedException("Unknown operand type");
						}
						break;

					case FlowControl.Throw:
						// FIXME: Link to exception handler?
					case FlowControl.Return:
						// End current basic block and don't link anywhere
						linkedInstructions = new Instruction[0];
						if (i.OpCode.Code == Code.Endfinally && finallyTarget.ContainsKey(i.Offset))
							linkedInstructions = new[] { finallyTarget[i.Offset] };
						break;
				}

				// Create new basic block
				basicBlocks.Add(new BasicBlock(blockIndex++, firstInstruction, i, linkedInstructions));
				firstInstruction = i.Next;
			}

			if (firstInstruction != null)
				basicBlocks.Add(new BasicBlock(blockIndex, firstInstruction, instructions[instructions.Count - 1], new Instruction[0]));

			this.basicBlocks = basicBlocks.ToArray();

			// Reconstruct exception handler blocks
			this.exceptionBlocks = ReconstructExceptionBlocks(body);

			// Create a map that will allow us to quickly resolve basic block
			// links.
			this.basicBlockMap = new Dictionary<int, BasicBlock>();
			foreach (BasicBlock basicBlock in this.BasicBlocks)
				this.basicBlockMap.Add(basicBlock.EntryPoint.Offset, basicBlock);
		}

		private static ExceptionBlock[] ReconstructExceptionBlocks(MethodBody body)
		{
			var exceptionBlocks = new List<ExceptionBlock>();

			// Group exception handlers by their Try block
			// FIXME: Possibly broken for two try blocks starting at the same instruction, has to be tested!
			var exceptionHandlersDictionary = new Dictionary<int, List<ExceptionHandler>>();
			foreach (var exceptionHandler in body.ExceptionHandlers)
			{
				List<ExceptionHandler> handlerList;
				if (!exceptionHandlersDictionary.TryGetValue(exceptionHandler.TryStart.Offset, out handlerList))
				{
					handlerList = new List<ExceptionHandler>();
					exceptionHandlersDictionary.Add(exceptionHandler.TryStart.Offset, handlerList);
				}
				handlerList.Add(exceptionHandler);
			}

			foreach (var handlerList in exceptionHandlersDictionary.Values)
			{
				Instruction tryEntryPoint = handlerList[0].TryStart;
				Instruction tryExitPoint = handlerList[0].TryEnd;
				List<CatchBlock> catchBlocks = null;
				Instruction finallyEntryPoint = null;
				Instruction finallyExitPoint = null;
				Instruction faultEntryPoint = null;
				Instruction faultExitPoint = null;

				foreach (var handler in handlerList)
				{
					switch (handler.HandlerType)
					{
						case ExceptionHandlerType.Catch:
							if (catchBlocks == null)
								catchBlocks = new List<CatchBlock>();
							catchBlocks.Add(new CatchBlock(handler.CatchType, handler.HandlerStart, handler.HandlerEnd));
							break;

						case ExceptionHandlerType.Fault:
							faultEntryPoint = handler.HandlerStart;
							faultExitPoint = handler.HandlerEnd;
							break;

						// FIXME
						//case ExceptionHandlerType.Filter:

						case ExceptionHandlerType.Finally:
							finallyEntryPoint = handler.HandlerStart;
							finallyExitPoint = handler.HandlerEnd;
							break;
					}
				}

				exceptionBlocks.Add(new ExceptionBlock(
					tryEntryPoint,
					tryExitPoint,
					catchBlocks == null ? new CatchBlock[0] : catchBlocks.ToArray(),
					finallyEntryPoint,
					finallyExitPoint,
					faultEntryPoint,
					faultExitPoint));
			}

			return exceptionBlocks.ToArray();
		}

		/// <summary>
		/// Set of interlinked basic blocks that form the body of a function.
		/// </summary>
		public BasicBlock[] BasicBlocks
		{
			get { return this.basicBlocks; }
		}

		/// <summary>
		/// Entry point into the control flow graph.
		/// </summary>
		public BasicBlock EntryPoint
		{
			get { return this.basicBlocks[0]; }
		}

		/// <summary>
		/// Exception handler blocks for given function.
		/// </summary>
		public ExceptionBlock[] ExceptionBlocks
		{
			get { return this.exceptionBlocks; }
		}

		/// <summary>
		/// Get basic block corresponding to particular instruction.
		/// </summary>
		/// <param name="i">Instruction</param>
		/// <returns>Basic block containing the instruction or null, if not found</returns>
		public BasicBlock GetBasicBlockAtInstruction(Instruction i)
		{
			BasicBlock block;
			while (i != null)
			{
				if (basicBlockMap.TryGetValue(i.Offset, out block))
					return block;
				i = i.Previous;
			}
			return null;
		}

		/// <summary>
		/// Traverse the control-flow graph for flow-sensitive analysis.
		/// </summary>
		/// <typeparam name="TContext">Type used to represent the execution context between basic blocks</typeparam>
		/// <param name="f">Callback for each basic block</param>
		/// <param name="cloneContext">Callback for cloning execution context</param>
		/// <param name="initialContext">Initial execution context</param>
		/// <remarks>
		/// TODO: Path-sensitive traversal
		/// TODO: Drop this in favor of full data-flow computation?
		/// </remarks>
		public void Traverse<TContext>(
			Action<Instruction, TContext> f,
			Func<TContext, TContext> cloneContext,
			TContext initialContext)
		{
			var blocksToProcess = new Stack<Tuple<BasicBlock, TContext>>();
			var basicBlockMap = new Dictionary<int, BasicBlock>(this.basicBlockMap);

			blocksToProcess.Push(Tuple.Create(this.EntryPoint, initialContext));
			basicBlockMap.Remove(0);

			while (blocksToProcess.Count > 0)
			{
				var blockToProcess = blocksToProcess.Pop();
				TContext executionContext = blockToProcess.Item2;

				foreach (var instruction in blockToProcess.Item1.Instructions)
					f(instruction, executionContext);

				foreach (Instruction linkedInstruction in blockToProcess.Item1.Successors)
				{
					BasicBlock targetBasicBlock;
					if (basicBlockMap.TryGetValue(linkedInstruction.Offset, out targetBasicBlock))
					{
						blocksToProcess.Push(Tuple.Create(targetBasicBlock, cloneContext(executionContext)));
						basicBlockMap.Remove(linkedInstruction.Offset);
					}
				}
			}
		}

		#region QuickGraph

		private bool TryGetOutEdges(BasicBlock vertex, out IEnumerable<SEquatableEdge<BasicBlock>> edges)
		{
			if (vertex.Successors.Length > 0)
			{
				var outEdges = new List<SEquatableEdge<BasicBlock>>(vertex.Successors.Length);
				foreach (var successor in vertex.Successors)
					outEdges.Add(new SEquatableEdge<BasicBlock>(vertex, basicBlockMap[successor.Offset]));
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
		public IVertexAndEdgeListGraph<BasicBlock, SEquatableEdge<BasicBlock>> QuickGraph
		{
			get
			{
				return new DelegateVertexAndEdgeListGraph<BasicBlock, SEquatableEdge<BasicBlock>>(
					this.BasicBlocks,
					TryGetOutEdges);
			}
		}

		#endregion
	}
}
