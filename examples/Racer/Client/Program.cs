﻿#region Copyright notice and license

// Copyright 2019 The gRPC Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#endregion

using System.Diagnostics;
using System.Threading;
using Grpc.Core;
using Grpc.Net.Client;
using Race;

//string urls = "http://localhost:5001";
string urls = "http://server:5001";
if (args.Length > 1)
{
    urls = args[1];
}
Thread.Sleep(3000); // wait for server to start
using var channel = GrpcChannel.ForAddress(urls);
var client = new Racer.RacerClient(channel);

Console.WriteLine($"Race duration: {RaceDuration.TotalSeconds} seconds");
//Console.WriteLine("Press any key to start race...");
//Console.ReadKey();

await BidirectionalStreamingExample(client);

Console.WriteLine("Finished");
//Console.WriteLine("Press any key to exit...");
//Console.ReadKey();

static async Task BidirectionalStreamingExample(Racer.RacerClient client)
{
    var headers = new Metadata { new Metadata.Entry("race-duration", RaceDuration.ToString()) };

    Console.WriteLine("Ready, set, go!");
    using var call = client.ReadySetGo(new CallOptions(headers));
    var complete = false;

    // Read incoming messages in a background task
    RaceMessage? lastMessageReceived = null;
    var readTask = Task.Run(async () =>
    {
        await foreach (var message in call.ResponseStream.ReadAllAsync())
        {
            lastMessageReceived = message;
        }
    });

    // Write outgoing messages until timer is complete
    var sw = Stopwatch.StartNew();
    var sent = 0;

    #region Reporting
    // Report requests in realtime
    var reportTask = Task.Run(async () =>
    {
        while (true)
        {
            Console.WriteLine($"Messages sent: {sent:n0}");
            Console.WriteLine($"Messages received: {(lastMessageReceived?.Count ?? 0):n0}");

            if (!complete)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                Console.WriteLine($"Messages sent: {sent:n0}");
                Console.WriteLine($"Messages received: {(lastMessageReceived?.Count ?? 0):n0}");
                //Console.SetCursorPosition(0, Console.CursorTop - 2);
            }
            else
            {
                break;
            }
        }
    });
    #endregion

    while (sw.Elapsed < RaceDuration)
    {
        await call.RequestStream.WriteAsync(new RaceMessage { Count = ++sent , Data =  { Enumerable.Range(0, 1000).Select(_ => (double) _ ) }});
    }

    // Finish call and report results
    await call.RequestStream.CompleteAsync();
    await readTask;

    complete = true;
    await reportTask;
}

public partial class Program
{
    static readonly TimeSpan RaceDuration = TimeSpan.FromSeconds(30);
}
