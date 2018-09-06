// Characteristics of test:
// - Lock on static objects
// - lock (a) lock (b) vs. lock (b) lock (a) pattern on different threads
// - Threads constructed using new Thread(new ThreadStart()) 

using System;
using System.Threading;

public class Deadlock
{
	static readonly object a = new object();
	static readonly object b = new object();
	static Action realFunctionA;

	public static void FunctionA()
	{
		realFunctionA = RealFunctionA;
		realFunctionA();
	}

	public static void RealFunctionA()
	{
		lock (b)
		{
			lock (a)
			{
			}
		}
	}

	public static void FunctionB()
	{
		lock (a)
		{
			lock (b)
			{
			}
		}
	}

	public static void Main()
	{
		Thread thread1 = new Thread(FunctionA);
		Thread thread2 = new Thread(FunctionB);
		thread1.Start();
		thread2.Start();
	}
}
