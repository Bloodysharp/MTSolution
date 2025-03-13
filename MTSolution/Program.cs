using System;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using System.Collections.Generic;
using MTSolution.Services;

class Program
{
    static void Main()
    {
        VMManager vmManager = new VMManager();

        while (true)
        {
            vmManager.GenerateNewVM();
            vmManager.ProcessNewRound();
            Thread.Sleep(1000);
        }
    }
}
