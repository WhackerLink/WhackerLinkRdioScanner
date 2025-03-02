/*
* WhackerLink - WhackerLinkRdioScanner
*
* This program is free software: you can redistribute it and/or modify
* it under the terms of the GNU General Public License as published by
* the Free Software Foundation, either version 3 of the License, or
* (at your option) any later version.
*
* This program is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
* GNU General Public License for more details.
*
* You should have received a copy of the GNU General Public License
* along with this program.  If not, see <http://www.gnu.org/licenses/>.
* 
* Copyright (C) 2025 Caleb, K4PHP
* 
*/

using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

using WhackerLinkLib.Models;
using WhackerLinkLib.Interfaces;
using WhackerLinkLib.Network;

namespace WhackerLinkRdioScanner
{
    internal class Program
    {
        public static Config config;

        private static ILogger Logger;
        private static CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        /// <summary>
        /// App entry point
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        static void Main(string[] args)
        {
            Logger = LoggerSetup.CreateLogger(string.Empty);

            Log.Logger = Logger;

            try
            {
                string configPath = "config.yml";
                if (args.Length > 0 && args[0] == "-c" && args.Length > 1)
                {
                    configPath = args[1];
                }

                config = LoadConfig(configPath);
                if (config == null)
                {
                    Logger.Error("Failed to load config.");
                    return;
                }

                string debug = string.Empty;

#if DEBUG
                debug = "DEBUG_PROTO_LABTOOL";
#endif

                Logger.Information("WhackerLink Rdio Scanner - Rdio Scanner Interface");
                Logger.Information("Copyright (C) 2025 Caleb, K4PHP (_php_)");

                Logger.Information("Initializing Peer");

                PeerHandler handler = new PeerHandler(config.Master.Address, config.Master.Port);

                try
                {
                    handler.Start();
                } catch(Exception ex)
                {
                    Log.Logger.Error("Failed to start peer {Ex}", ex);
                }

                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    Logger.Information("Shutting down...");
                    Shutdown();
                };

                while (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    Thread.Sleep(1000);
                }
            }
            catch (IOException ex)
            {
                Logger.Error(ex, "IO Error");
            }
            catch (Exception ex)
            {
                Logger.Fatal(ex, "An unhandled exception occurred.");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        /// <summary>
        /// Helper to load config file
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private static Config LoadConfig(string path)
        {
            try
            {
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                var yaml = File.ReadAllText(path);
                return deserializer.Deserialize<Config>(yaml);
            }
            catch (Exception ex)
            {
                Log.Error("Error loading config: {Message}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Gracefully kill all masters then die
        /// </summary>
        private static void Shutdown()
        {
            cancellationTokenSource.Cancel();
        }
    }
}
