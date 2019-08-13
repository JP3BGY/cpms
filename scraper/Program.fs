module Scraper
open FSharp.Data.Sql
open MySql.Data.MySqlClient
open MySqlConnector.Logging
open Setting
open Crawlers

[<EntryPoint>]
let main argv  = 
    //MySqlConnectorLogManager.set_Provider(new ConsoleLoggerProvider(MySqlConnectorLogLevel.Trace))
    //Common.QueryEvents.SqlQueryEvent.Add(fun x -> eprintfn "[SQLProvider Events]Query Event Command:%s %s" x.Command (x.ConnectionStringHash.ToString())
    //                                              x.Parameters|>Seq.map(fun (x,y)->eprintfn "    Parameters: %s , %s" x (y.ToString()))|>ignore
    //                                              ())
    let ctx = getDataContext()
    initArray|>Array.map(fun (name,url)->
                                        let exists=query{
                                            for x in ctx.ContestLog.ContestServer do
                                                where (x.ContestServerName=name)
                                                select (x.ContestServerName)
                                        }
                                        if Seq.isEmpty exists then 
                                                let emp=ctx.ContestLog.ContestServer.``Create``(name,url)
                                                ()
                                            else
                                                eprintfn "There exist %s in ContestServer table" name
                                                () )|>ignore
    try
        ctx.SubmitUpdates()
    with
        | :? MySqlException as me ->
            eprintfn "MySQL Submit Error"
            eprintfn "%s" me.Message
            exit(1)
    crawlerLoop()
    0
