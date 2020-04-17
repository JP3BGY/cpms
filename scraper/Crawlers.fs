module Scraper.Crawlers
open System
open System.Transactions
open System.Net
open System.IO
open FSharp.Data.Sql
open Scraper.Codeforces
open Scraper.Setting
open Scraper.AtCoder
let initArray=[|
    ("Codeforces","https://codeforces.com")
|]
let crawlerArray=[|
    codeforces;
    atcoder
|]
(*
let crawler (cServerName,url) getContestsBeforeEnd getContests isContestEnded getProblems getParticipantsAndRatings =
    let ctx = getDataContext()
    let now = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
    let cServer = 
        try
            query{
                for cserver in ctx.ContestLog.ContestServer do
                    where (cserver.ContestServerName = cServerName)
                    exactlyOne
            }
        with
        | :? ArgumentNullException as e ->
            let elm=ctx.ContestLog.ContestServer.``Create(contestServerName, contestServerUrl)``(cServerName,url)
            ctx.SubmitUpdates()
            elm
    try
        let contestsBE = getContestsBeforeEnd()
        use transaction = new TransactionScope(TransactionScopeOption.RequiresNew,
                                               TransactionOptions(Timeout=TimeSpan.Zero,IsolationLevel=IsolationLevel.RepeatableRead),
                                               TransactionScopeAsyncFlowOption.Enabled)
        query{
            for contest in ctx.ContestLog.ContestBeforeEnd do
                where (contest.ContestServerContestServerId = cServer.ContestServerId && contest.ContestEndTime < now)
        }|>Seq.``delete all items from single table``|>ignore
        contestsBE |> Array.map(
            fun (contestName,contestId,startTime,endTime) ->
                ctx.ContestLog.ContestBeforeEnd.``Create(contestEndTime, contestServerContestId, contestServerContestName, contestStartTime, contest_server_contestServerId)``
                    (endTime,contestId,contestName,startTime,cServer.ContestServerId)|>ignore
                ()
        )|>ignore
        ctx.SubmitUpdates()
        transaction.Complete()
        ()
    with
    | :? WebException as we -> 
        eprintfn "[%s] Can't save contestBE  %s" cServerName we.Message 
        Console.Error.WriteLine("uri:{0}",we.Response.ResponseUri)
        use data = we.Response.GetResponseStream()
        let streamr=new StreamReader(data)
        eprintfn "[%s] %s" cServerName (streamr.ReadToEnd())
        GC.Collect()
        ()
    | :? TransactionAbortedException as te ->
        eprintfn "[%s] Transaction Error %s" cServerName te.Message
        GC.Collect()
        ()
    try
        let contests = getContests()
        let insertContest contestId =
            let isInDb = 
                query{
                    for contest in ctx.ContestLog.Contest do
                        contains (contest.ContestServerContestServerId = cServer.ContestServerId && contest.ContestServerContestId = contestId)
                }
            let isEnded = isContestEnded contestId
            if isInDb || not isEnded then
                ()
            else 
                try
                    let participantsAndRating = getParticipantsAndRatings contestId
                    let problems = getProblems contestId
                    use transaction = new TransactionScope(TransactionScopeOption.RequiresNew,
                                                           TransactionOptions(Timeout=TimeSpan.Zero,IsolationLevel=IsolationLevel.RepeatableRead),
                                                           TransactionScopeAsyncFlowOption.Enabled)
                    ctx.SubmitUpdates()
                    transaction.Complete()
                    ()
                with
                | :? WebException as we -> 
                    eprintfn "[%s] Can't save contestBE  %s" cServerName we.Message 
                    Console.Error.WriteLine("uri:{0}",we.Response.ResponseUri)
                    use data = we.Response.GetResponseStream()
                    let streamr=new StreamReader(data)
                    eprintfn "[%s] %s" cServerName (streamr.ReadToEnd())
                    GC.Collect()
                    ()
                | :? TransactionAbortedException as te ->
                    eprintfn "[%s] Transaction Error %s" cServerName te.Message
                    GC.Collect()
                    ()
    with
    | :? WebException as we -> 
        eprintfn "[%s] Can't save contestBE  %s" cServerName we.Message 
        Console.Error.WriteLine("uri:{0}",we.Response.ResponseUri)
        use data = we.Response.GetResponseStream()
        let streamr=new StreamReader(data)
        eprintfn "[%s] %s" cServerName (streamr.ReadToEnd())
        GC.Collect()
        ()
    ()
*)
let rec internalCrawlerLoop ()=
    crawlerArray|>Array.map(fun x-> x())|>Async.Parallel|>Async.RunSynchronously|>ignore

let crawlerLoop =
    async{
        internalCrawlerLoop()
    }