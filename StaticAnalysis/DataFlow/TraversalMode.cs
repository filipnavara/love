using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StaticAnalysis.DataFlow
{
	/// <summary>
	/// Defines the traversal mode for data-flow analysis.
	/// </summary>
	public enum TraversalMode
	{
		/// <summary>
		/// Iterative traversal mode that recomputes the flow analysis
		/// for every node when its input facts changed.
		/// </summary>
		Iterative,

		/// <summary>
		/// Non-iterative traversal mode that doesn't recompute the flow
		/// analysis when facts for predecessor node changed.
		/// </summary>
		NonIterative
	}
}
