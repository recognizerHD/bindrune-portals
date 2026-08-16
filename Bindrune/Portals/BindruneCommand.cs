using System;
using Jotunn.Entities;

namespace Bindrune.Portals
{
    /// <summary>
    /// Base for this mod's console commands, which exists for one reason: an exception thrown inside
    /// a console command reaches the Unity log and <em>nothing else</em>. The terminal prints no
    /// output at all, which reads exactly like a command that ran and had nothing to say.
    /// <para>
    /// That cost a debugging round trip once already. Here the failure lands where the person who
    /// typed the command is looking.
    /// </para>
    /// </summary>
    internal abstract class BindruneCommand : ConsoleCommand
    {
        public sealed override void Run(string[] args) => Run(args, Console.instance);

        public sealed override void Run(string[] args, Terminal context)
        {
            if (context == null)
            {
                return;
            }

            try
            {
                Execute(args ?? Array.Empty<string>(), context);
            }
            catch (Exception exception)
            {
                context.AddString($"Bindrune: {Name} failed — {exception.GetType().Name}: {exception.Message}");
                Jotunn.Logger.LogError($"{Name} threw: {exception}");
            }
        }

        /// <summary>The command body. Anything it throws is reported to the terminal.</summary>
        protected abstract void Execute(string[] args, Terminal context);
    }
}
