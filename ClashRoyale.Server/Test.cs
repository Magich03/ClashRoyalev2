using System;
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

namespace ClashRoyale
{
    class Test
    {
        internal static bool Initialized
        {
            get;
            set;
        }

        /// <summary>
        /// Defines the entry point of the application.
        /// </summary>
        internal static void Initialize()
        {
            if (Test.Initialized)
            {
                return;
            }

            // Ensure GameDb is initialized first
            GameDb.Initialize();

            // Now you can safely call GetPlayersSeed()
            //int playerSeed = GameDb.GetPlayersSeed();
           // Console.WriteLine(playerSeed);

            Devices.Initialize();
            Players.Initialize();
            Clans.Initialize();


            Battles.Initialize();
            Leaderboards.Initialize();

            InboxManager.Initialize();
            EventManager.Initialize();
            BattleManager.Initialize();
            RoyalTvManager.Initialize();

            HandlerFactory.Initialize();
            NetworkTcp.Initialize();

            Program.Initialized = true;
             //Console.CancelKeyPress += ConsoleOnCancelKeyPress;

            CommandLine.Initialize();
            Test.Initialized = true; ;
        }
    }
}
