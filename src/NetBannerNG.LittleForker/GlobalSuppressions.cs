// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Performance", "CA1837:Use 'Environment.ProcessId'", Justification = "<Pending>", Scope = "member", Target = "~M:LittleForker.CooperativeShutdown.Listen(System.Action,Microsoft.Extensions.Logging.ILoggerFactory,System.Action{System.Exception})~System.Threading.Tasks.Task{System.IDisposable}")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:LittleForker.CooperativeShutdown.Listen(System.Action,Microsoft.Extensions.Logging.ILoggerFactory,System.Action{System.Exception})~System.Threading.Tasks.Task{System.IDisposable}")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:LittleForker.CooperativeShutdown.SignalExit(System.Int32,Microsoft.Extensions.Logging.ILoggerFactory)~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:LittleForker.TaskQueue.EnqueueInternal``1(System.Collections.Concurrent.ConcurrentQueue{System.Func{System.Threading.Tasks.Task}},System.Func{System.Threading.CancellationToken,System.Threading.Tasks.Task{``0}})~System.Threading.Tasks.Task{``0}")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:LittleForker.ProcessSupervisor.OnStart")]
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<Pending>", Scope = "member", Target = "~M:LittleForker.ProcessSupervisor.OnStop(System.Nullable{System.TimeSpan})~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Documentation", "CA1200:Avoid using cref tags with a prefix", Justification = "<Pending>", Scope = "member", Target = "~M:LittleForker.InterlockedBoolean.#ctor(System.Boolean)")]