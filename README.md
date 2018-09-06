# Project Description

Tool for finding potential deadlocks in .NET programs using static code analysis.

# Motivation

Modern software design includes concurrent programming. It is widely accepted that designing and testing concurrent programs is hard. Two major problems with designing concurrent programs are deadlocks and race conditions.

Deadlock is a condition under which program is halted because each thread in a set is waiting for a resource that is currently held by other thread in the set. Since deadlocks prevent the application from running, it poses a significant problem.

Finding deadlocks using traditional testing techniques, such as unit tests, integration tests and validation testing, proved to be difficult since reaching a deadlock may require a specific interleaving of threads. It is usually infeasible to simulate all thread interleavings of a program, because it has exponential complexity with regard to the number of threads.

Many techniques [Hewitt et al., 1973] and language extensions [Agarwal et al., 2006, Permandla et al., 2007] were developed that aim to reduce or eliminate the potential for deadlocks at the expense of requiring the programmer to learn new methods of how to write programs. This approach has two major drawbacks. Firstly, the learning curve is often very steep and thus the programmer has to spend more time to learn the new techniques. Secondly, there are often large legacy code bases that can't be easily refactored to accompany the new techniques. These drawbacks largely contribute to the fact that these techniques are rarely used outside of the academic community and mission critical systems.

# Objectives

This project aims at introducing a design of a tool for analyzing whole programs written for the .NET Framework for a potential presence of deadlocks in the code.

The tool will operate on programs without actually executing them using a set of techniques known as static program analysis. Benefit of this approach is that the tool can readily be run on existing programs with no modifications required. It will work on legacy code bases and require no changes to the design of  programs or language.

A reference implementation of the design will be provided and evaluated on several test programs and one commercial application.

The objective is to provide a tool that is fast enough to be used on large scale code bases. At the same time the amount of false positives reported by the tool should be minimized to allow for manual inspection of the results.
