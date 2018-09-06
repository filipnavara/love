using QuickGraph;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Diagnostics.Contracts;

namespace StaticAnalysis.CallGraph
{
	/// <summary>
	/// Edge in call graph that specifies a method call at particular
	/// call site in the source method.
	/// </summary>
	public class CallGraphEdge : IEdge<MethodDefinition>
	{
		private readonly ProgramPoint source;
		private readonly MethodDefinition target;

		/// <summary>
		/// Initializes new instance of call graph edge.
		/// </summary>
		/// <param name="source">Call site that is source of the call</param>
		/// <param name="target">Method that is target of the call</param>
		public CallGraphEdge(
			ProgramPoint source,
			MethodDefinition target)
		{
			Contract.Requires(source != null);
			Contract.Requires(target != null);
			this.source = source;
			this.target = target;
		}

		/// <summary>
		/// Program point of the call instruction in the source method.
		/// </summary>
		public ProgramPoint ProgramPoint
		{
			get { return this.source; }
		}

		/// <summary>
		/// Calling method.
		/// </summary>
		public MethodDefinition Source
		{
			get { return this.source.Method; }
		}

		/// <summary>
		/// Called method.
		/// </summary>
		public MethodDefinition Target
		{
			get { return this.target; }
		}
	}
}
