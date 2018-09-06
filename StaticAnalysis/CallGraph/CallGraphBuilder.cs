using System.Threading;

namespace StaticAnalysis.CallGraph
{
	/// <summary>
	/// Abstract class for call graph extraction implementations.
	/// </summary>
	public abstract class CallGraphBuilder
	{
		/// <summary>
		/// Build the call graph.
		/// </summary>
		/// <param name="cancellationToken">Cancellation token that allows to interrupt the call graph construction.</param>
		public abstract CallGraph Build(CancellationToken cancellationToken);

		/// <summary>
		/// Build the call graph.
		/// </summary>
		public CallGraph Build()
		{
			return Build(CancellationToken.None);
		}
	}
}
