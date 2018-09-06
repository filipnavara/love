// Characteristics of test:
// - Lock on field objects
// - lock (a) lock (b) vs. lock (b) lock (a) pattern on different threads
// - Threads constructed ThreadPool.QueueUserWorkItem(new WaitCallback())

using System;
using System.Threading;

public class Deadlock
{
	readonly object a = new object();
	readonly object b = new object();

	public void FunctionA(object state)
	{
		lock (this)
		{
			lock (a)
			{
			}
		}
	}

	public void FunctionB(object state)
	{
		lock (a)
		{
			lock (this)
			{
			}
		}
	}

	public static void Main()
	{
		Deadlock d = new Deadlock();
		ThreadPool.QueueUserWorkItem(d.FunctionA);
		ThreadPool.QueueUserWorkItem(d.FunctionB);
	}
}
