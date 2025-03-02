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

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace WhackerLinkRdioScanner
{
    /// <summary>
    /// Rdio Scanner HTTP client
    /// </summary>
    public class RdioScannerClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _endpoint;

        /// <summary>
        /// Creates an instance of <see cref="RdioScannerClient"/>
        /// </summary>
        /// <param name="apiKey"></param>
        /// <param name="endpoint"></param>
        public RdioScannerClient(string apiKey, string endpoint)
        {
            _httpClient = new HttpClient();
            _apiKey = apiKey;
            _endpoint = endpoint.TrimEnd('/');
        }

        /// <summary>
        /// Helper to send a new voice call to rdio scanner
        /// </summary>
        /// <param name="talkgroup"></param>
        /// <param name="source"></param>
        /// <param name="audioFilePath"></param>
        /// <param name="systemId"></param>
        /// <param name="systemLabel"></param>
        /// <returns></returns>
        public async Task<bool> SendCall(string talkgroup, string source, string audioFilePath, string systemId = "1", string systemLabel = "WLINK")
        {
            try
            {
                using var formData = new MultipartFormDataContent();
                using var fileStream = new ByteArrayContent(await System.IO.File.ReadAllBytesAsync(audioFilePath));

                fileStream.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/x-wav");

                formData.Add(fileStream, "audio", audioFilePath);
                formData.Add(new StringContent(System.IO.Path.GetFileName(audioFilePath)), "audioName");
                formData.Add(new StringContent("audio/x-wav"), "audioType");
                formData.Add(new StringContent(DateTime.UtcNow.ToString("o")), "dateTime"); // RFC3339 format
                formData.Add(new StringContent(_apiKey), "key");
                formData.Add(new StringContent(talkgroup), "talkgroup");
                formData.Add(new StringContent(source), "source");
                formData.Add(new StringContent(systemId), "system");
                formData.Add(new StringContent(systemLabel), "systemLabel");

                var response = await _httpClient.PostAsync($"{_endpoint}/api/call-upload", formData);
                response.EnsureSuccessStatusCode();

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending call: {ex.Message}");
                return false;
            }
        }
    }
}
