namespace ContestServer

open System
open System.Collections.Generic
open System.IO
open System.Linq
open System.Threading.Tasks
open Microsoft.AspNetCore
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Logging
open MySqlConnector.Logging
open FSharp.Data.Sql

module Program =
    //MySqlConnectorLogManager.set_Provider(ConsoleLoggerProvider(MySqlConnectorLogLevel.Trace))
    //Common.QueryEvents.SqlQueryEvent.Add(fun x -> eprintfn "[SQLProvider Events]Query Event Command:%s %s" x.Command (x.ConnectionStringHash.ToString())
    //                                              x.Parameters|>Seq.map(fun (x,y)->eprintfn "    Parameters: %s , %s" x (y.ToString()))|>ignore
    //                                              ())
    let exitCode = 0

    let CreateWebHostBuilder args =
        WebHost
            .CreateDefaultBuilder(args)
            .UseStartup<Startup>();

    [<EntryPoint>]
    let main args =
        CreateWebHostBuilder(args).Build().Run()

        exitCode
