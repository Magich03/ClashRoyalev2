namespace ClashRoyale
{
    using System;
    using System.Reflection;
    using System.Threading;
    using ClashRoyale.CmdHandlers;
    using ClashRoyale.Database;
    using ClashRoyale.Handlers;

    using ClashRoyale.Logic.Battle;
    using ClashRoyale.Logic.Collections;
    using ClashRoyale.Logic.Event;
    using ClashRoyale.Logic.Inbox;
    using ClashRoyale.Logic.RoyalTv;

    using ClashRoyale.Network;

    internal static class Program
    {
        internal static bool Initialized
        {
            get;
            set;
        }

        /// <summary>
        /// Defines the entry point of the application.
        /// </summary>
        private static void Main()
        {
            Base.Initialize();
            Test.Initialize();
            Program.Initialized = true;//progres move main in Test.cs

            Thread.Sleep(-1);
        }
    }
}