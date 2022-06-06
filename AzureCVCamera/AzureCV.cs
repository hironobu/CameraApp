#nullable enable

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;

namespace AzureCVCamera
{
    class AzureCVClient
    {
        public AzureCVClient(string apiKey, string endpoint)
        {
            _subscriptionKey = apiKey;
            _endpoint = endpoint;
        }

        public async Task<string> ProcessFileAsync(string filePath)
        {
            ComputerVisionClient client = Authenticate(_endpoint, _subscriptionKey);

            return await ReadTextFromFileUrl(client, filePath);
        }

        public Task<string> ReadTextFromFileUrl(ComputerVisionClient client, string filePath)
        {
            var imagestream = File.OpenRead(filePath);

            return ReadTextFromStream(client, imagestream);
        }

        public async Task<string> ReadTextFromStream(ComputerVisionClient client, Stream imagestream)
        { 
            var textHeaders = await client.ReadInStreamAsync(imagestream);
            string operationLocation = textHeaders.OperationLocation;
            // await Task.Delay(1000);

            const int numberOfCharsInOperationId = 36;
            string operationId = operationLocation.Substring(operationLocation.Length - numberOfCharsInOperationId);

            ReadOperationResult results;
            do
            {
                results = await client.GetReadResultAsync(Guid.Parse(operationId)).ConfigureAwait(false);
            }
            while ((results.Status == OperationStatusCodes.Running ||
                results.Status == OperationStatusCodes.NotStarted));

            var resultStringBuilder = new StringBuilder();
            var textUrlFileResults = results.AnalyzeResult.ReadResults;
            foreach (ReadResult page in textUrlFileResults)
            {
                foreach (Line line in page.Lines)
                {
                    //var box = string.Join(",", line.BoundingBox);
                    //Console.WriteLine($"[{box}]: {line.Text}");

                    resultStringBuilder.Append(line.Text);
                }
            }

            Console.WriteLine(resultStringBuilder.ToString());

            return resultStringBuilder.ToString();
        }

        private ComputerVisionClient Authenticate(string endpoint, string key)
        {
            ComputerVisionClient client =
              new ComputerVisionClient(new ApiKeyServiceClientCredentials(key))
              { Endpoint = endpoint };
            return client;
        }

        private string _subscriptionKey; // = "74d7f4aec4ce4b97a32bac9636d85717";
        private string _endpoint; // = "https://meuzz-cameraapp.cognitiveservices.azure.com/";
    }
}