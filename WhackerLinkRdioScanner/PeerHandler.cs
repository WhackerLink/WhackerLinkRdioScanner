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

using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Serilog;
using WhackerLinkLib.Interfaces;
using WhackerLinkLib.Models;
using WhackerLinkLib.Models.IOSP;
using WhackerLinkLib.Network;

namespace WhackerLinkRdioScanner
{
    /// <summary>
    /// Class that handles transport of WhackerLink audio to Rdio Scanner
    /// </summary>
    public class PeerHandler
    {
        private IPeer peer;
        private RdioScannerClient rdioScanner;

        private string address;
        private string authKey;
        private int port;

        private Dictionary<string, List<byte>> activeCalls = new();

        /// <summary>
        /// Creates an instance of <see cref="PeerHandler"/>
        /// </summary>
        public PeerHandler(string address, int port, string authKey = "UNAUTH")
        {
            peer = new Peer();
            rdioScanner = new RdioScannerClient(Program.config.ApiKey, Program.config.EndPoint);

            peer.OnOpen += OnOpen;
            peer.OnClose += OnClose;
            peer.OnReconnecting += OnReconnecting;
            peer.OnVoiceChannelRelease += HandleRelease;
            peer.OnAudioData += HandleAudio;

            this.address = address;
            this.port = port;
            this.authKey = authKey;
        }

        /// <summary>
        /// Start the master connection
        /// </summary>
        public void Start()
        {
            peer.Connect(address, port, authKey);
        }

        /// <summary>
        /// Peer connection succesful
        /// </summary>
        public void OnOpen()
        {
            Log.Logger.Information("Connection to master succesful");

            // send fake aff's
            foreach (string talkgroup in Program.config.Talkgroups)
            {
                GRP_AFF_REQ affReq = new GRP_AFF_REQ
                {
                    SrcId = Program.config.Master.RadioId,
                    DstId = talkgroup
                };

                peer.SendMessage(affReq.GetData());
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void OnClose()
        {
            Log.Logger.Warning("Connection to master lost");
        }

        /// <summary>
        /// 
        /// </summary>
        public void OnReconnecting()
        {
            Log.Logger.Warning("Attempting master reconnection");
        }

        /// <summary>
        /// Helper to handle channel releases from the Master
        /// </summary>
        /// <param name="release"></param>
        private void HandleRelease(GRP_VCH_RLS release)
        {
            string dstId = release.DstId;
            string srcId = release.SrcId;
            string frequency = release.Channel;

            string callKey = $"{srcId}-{dstId}";

            if (activeCalls.ContainsKey(callKey))
            {
                Log.Logger.Information($"Call ended for srcId: {srcId}, dstId: {dstId}, channel: {frequency}");
                Task.Run(() => FinalizeCall(callKey, srcId, dstId, frequency));
            }
            else
            {
                Log.Logger.Warning($"Received call release for unknown call: srcId: {srcId}, dstId: {dstId}, channel: {frequency}");
            }
        }

        /// <summary>
        /// Helper to handle audio traffic from the master
        /// </summary>
        /// <param name="audioPacket"></param>
        private void HandleAudio(AudioPacket audioPacket)
        {
            byte[] pcm = audioPacket.Data;
            string srcId = audioPacket.VoiceChannel.SrcId;
            string dstId = audioPacket.VoiceChannel.DstId;
            string frequency = audioPacket.VoiceChannel.Frequency;

            string callKey = $"{srcId}-{dstId}";

            if (!activeCalls.ContainsKey(callKey))
            {
                activeCalls[callKey] = new List<byte>();
            }

            activeCalls[callKey].AddRange(pcm);

            Log.Logger.Information($"Voice transmission, srcId: {srcId}, dstId: {dstId}, channel: {frequency}");
        }

        /// <summary>
        /// Helper to end a call then send to Rdio
        /// </summary>
        /// <param name="callKey"></param>
        /// <param name="srcId"></param>
        /// <param name="dstId"></param>
        /// <returns></returns>
        private async Task FinalizeCall(string callKey, string srcId, string dstId, string freq)
        {
            if (!activeCalls.ContainsKey(callKey))
                return;

            string filePath = $"call_{callKey}_{DateTime.UtcNow:yyyyMMddHHmmss}.wav";
            WriteWavFile(filePath, activeCalls[callKey]);

            Log.Logger.Information($"Call ended. Sending to API: {filePath}");

            bool success = await rdioScanner.SendCall(dstId, srcId, filePath, Program.config.SystemId, freq);

            if (success)
                Log.Logger.Information($"Call {filePath} uploaded successfully.");
            else
                Log.Logger.Error($"Call {filePath} failed to upload.");

            activeCalls.Remove(callKey);
        }

        /// <summary>
        /// Helper to write PCM to wave file
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="pcmData"></param>
        private void WriteWavFile(string filePath, List<byte> pcmData)
        {
            using var fileStream = new FileStream(filePath, FileMode.Create);
            using var writer = new BinaryWriter(fileStream);

            int sampleRate = 8000;
            int bitsPerSample = 16;
            int channels = 1;
            int byteRate = sampleRate * channels * (bitsPerSample / 8);
            int blockAlign = channels * (bitsPerSample / 8);
            int dataSize = pcmData.Count;
            int fileSize = 36 + dataSize;

            // WAV Header
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(fileSize);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);       // PCM header length
            writer.Write((short)1); // PCM format
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write((short)blockAlign);
            writer.Write((short)bitsPerSample);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);
            writer.Write(pcmData.ToArray());
        }
    }
}
