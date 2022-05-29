using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;

namespace CameraApp
{
    class VisionClient
    {
        // Add your Computer Vision subscription key and endpoint
        static string subscriptionKey = "74d7f4aec4ce4b97a32bac9636d85717";
        static string endpoint = "https://meuzz-cameraapp.cognitiveservices.azure.com/";

        // private const string READ_TEXT_URL_IMAGE = "https://raw.githubusercontent.com/Azure-Samples/cognitive-services-sample-data-files/master/ComputerVision/Images/printed_text.jpg";

        public static void ProcessFile(string filePath)
        {
            Console.WriteLine("Azure Cognitive Services Computer Vision - .NET quickstart example");
            Console.WriteLine();

            ComputerVisionClient client = Authenticate(endpoint, subscriptionKey);

            // Extract text (OCR) from a URL image using the Read API
            ReadFileUrl(client, filePath).ConfigureAwait(false).GetAwaiter();
        }

        public static ComputerVisionClient Authenticate(string endpoint, string key)
        {
            ComputerVisionClient client =
              new ComputerVisionClient(new ApiKeyServiceClientCredentials(key))
              { Endpoint = endpoint };
            return client;
        }

        public static async Task ReadFileUrl(ComputerVisionClient client, string filePath)
        {
            Console.WriteLine("----------------------------------------------------------");
            Console.WriteLine("READ FILE FROM URL");
            Console.WriteLine();

            var imagestream = File.OpenRead(filePath);

            // Read text from URL
            var textHeaders = await client.ReadInStreamAsync(imagestream);
            // After the request, get the operation location (operation ID)
            string operationLocation = textHeaders.OperationLocation;
            Thread.Sleep(2000);

            // Retrieve the URI where the extracted text will be stored from the Operation-Location header.
            // We only need the ID and not the full URL
            const int numberOfCharsInOperationId = 36;
            string operationId = operationLocation.Substring(operationLocation.Length - numberOfCharsInOperationId);

            // Extract the text
            ReadOperationResult results;
            Console.WriteLine($"Extracting text from URL file {Path.GetFileName(filePath)}...");
            Console.WriteLine();
            do
            {
                results = await client.GetReadResultAsync(Guid.Parse(operationId));
            }
            while ((results.Status == OperationStatusCodes.Running ||
                results.Status == OperationStatusCodes.NotStarted));

            // Display the found text.
            Console.WriteLine();
            var textUrlFileResults = results.AnalyzeResult.ReadResults;
            foreach (ReadResult page in textUrlFileResults)
            {
                Console.WriteLine($"Width: {page.Width}");
                Console.WriteLine($"Height: {page.Height}");

                foreach (Line line in page.Lines)
                {
                    var box = string.Join(",", line.BoundingBox);
                    Console.WriteLine($"[{box}]: {line.Text}");
                }
            }
            Console.WriteLine();
        }



        public static async Task ReadFileUrl__(ComputerVisionClient client, string filePath)
        {
            Console.WriteLine("----------------------------------------------------------");
            Console.WriteLine("READ FILE FROM URL");
            Console.WriteLine();

            var imagestream = File.OpenRead(filePath);

            // Read text from URL
            var result = await client.RecognizePrintedTextInStreamAsync(true, imagestream);

            foreach (var r in result.Regions)
            {
                foreach (var line in r.Lines)
                {
                    foreach (var w in line.Words)
                    {
                        var box = w.BoundingBox.Select(x => x).ToArray();
                        Console.WriteLine($"{box}: {w.Text}");
                    }
                }

            }

            Console.WriteLine();

            /*

            // After the request, get the operation location (operation ID)
            string operationLocation = textHeaders.OperationLocation;
            Thread.Sleep(2000);

            // Retrieve the URI where the extracted text will be stored from the Operation-Location header.
            // We only need the ID and not the full URL
            const int numberOfCharsInOperationId = 36;
            string operationId = operationLocation.Substring(operationLocation.Length - numberOfCharsInOperationId);

            // Extract the text
            ReadOperationResult results;
            Console.WriteLine($"Extracting text from URL file {Path.GetFileName(filePath)}...");
            Console.WriteLine();
            do
            {
                results = await client.GetReadResultAsync(Guid.Parse(operationId));
            }
            while ((results.Status == OperationStatusCodes.Running ||
                results.Status == OperationStatusCodes.NotStarted));

            // Display the found text.
            Console.WriteLine();
            var textUrlFileResults = results.AnalyzeResult.ReadResults;
            foreach (ReadResult page in textUrlFileResults)
            {
                Console.WriteLine($"Width: {page.Width}");
                Console.WriteLine($"Height: {page.Height}");

                foreach (Line line in page.Lines)
                {
                    var box = line.BoundingBox.Select(x => (double)x).ToArray();
                    Console.WriteLine($"{box}: {line.Text}");
                }
            }
            Console.WriteLine();*/
        }
    }
}