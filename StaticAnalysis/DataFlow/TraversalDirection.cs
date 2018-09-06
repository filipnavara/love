using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StaticAnalysis.DataFlow
{
	/// <summary>
	/// Direction of traversal for solving a data-flow problem.
	/// </summary>
	public enum TraversalDirection
	{
		/// <summary>
		/// Traversal from entrypoint towards exitpoint(s).
		/// </summary>
		Forward,
		// Backward
	}
}
