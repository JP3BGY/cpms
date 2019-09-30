module Scraper.AtCoder
open Scraper.Setting
open Scraper.Submission
open System
open System.IO
open System.Net
open System.Transactions
open FSharp.Data
open FSharp.Data.JsonExtensions
open FSharp.Data.Runtime.Caching
let rec atcoder () =
    let atcoderDbId = 
        let ctx=getDataContext()
        let elm=
            query{
                for contestServer in ctx.ContestLog.ContestServer do
                    where (contestServer.ContestServerName = "AtCoder")
                    select (contestServer.ContestServerId)
            }
        if Seq.isEmpty elm then
            let newelm=ctx.ContestLog.ContestServer.``Create(contestServerName, contestServerUrl)``("AtCoder","https://atcoder.jp")
            ctx.SubmitUpdates()
            newelm.ContestServerId
        else
            Seq.head elm
    let getContestListUrl (page:int) (rated:int) =
        sprintf "https://atcoder.jp/contests/archive?lang=ja&page=%d&ratedType=%d" page rated
    let getContests () =
        let rec getContestsFromPage page rated arr =
            eprintfn "Now %d %d" page rated
            let res = HtmlDocument.Load((getContestListUrl page rated))
            webSleep()
            let x =
                res.CssSelect "tbody > tr"
                    |>Seq.map(
                        fun x->
                            x.Descendants ["td"]|>Seq.toArray
                                |>fun x->
                                    let url = x.[1].Descendants ["a"]
                                            |>Seq.map(fun x->x.TryGetAttribute("href"))
                                            |>Seq.head
                                            |>fun x->
                                                match x with
                                                |None -> ""
                                                |Some x -> 
                                                    let arr=x.Value().Split("/")
                                                    arr.[Array.length(arr)-1]
                                    (DateTime.Parse( x.[0].InnerText()),x.[1].InnerText(),url)
                    )|>Seq.toArray
            match x with
            | [||] ->
                arr
            | x ->
                getContestsFromPage (page+1) rated (Array.append arr x)
        [|1..3|]|>Array.collect(fun x->getContestsFromPage 1 x [||])

    let getContestResultUrl (id:string) =
        "https://atcoder.jp/contests/"+id+"/standings/json"
    let getOrInsertUser userId = 
        let ctx = getDataContext()
        let elm = 
            query{
                for user in ctx.ContestLog.ContestUsers do
                    where (user.ContestServerContestServerId=atcoderDbId&&user.ContestUserId=userId)
            }
        if Seq.isEmpty elm then
            let ret = ctx.ContestLog.ContestUsers.``Create(contestUserId, contest_server_contestServerId)``(userId,atcoderDbId)
            ctx.SubmitUpdates()
            ret.UserId
        else 
            (Seq.head elm).UserId
    let insertContestResultToDb (startTime:DateTime,name:string,id:string) =
        eprintfn "Start %s" id
        try
            let standings=
                Http.RequestString(getContestResultUrl(id))
                    |>JsonValue.Parse
            webSleep()
            if standings?Fixed.AsBoolean() then
                let ctx=getDataContext()
                use transaction = new TransactionScope(TransactionScopeOption.RequiresNew,
                                                       TransactionOptions(Timeout=TimeSpan.Zero,IsolationLevel=IsolationLevel.Serializable),
                                                       TransactionScopeAsyncFlowOption.Enabled)
                let contestElm=ctx.ContestLog.Contest.``Create(contestName, contestServerContestId, contestStartTime, contest_server_contestServerId)``
                                                (name,id,(DateTimeOffset(startTime)).ToUnixTimeSeconds(),atcoderDbId)
                ctx.SubmitUpdates()
                let problemElms =
                    standings?TaskInfo.AsArray()|>
                        Array.map(
                            fun x->
                                eprintfn "add atcoder problem %s %s" (x?TaskScreenName.AsString()) (x?TaskName.AsString())
                                ctx.ContestLog.Problem.``Create(contestServerProblemId, contest_contestId, problemName)``(x?TaskScreenName.AsString(),contestElm.ContestId,x?TaskName.AsString())
                        )
                ctx.SubmitUpdates()
                standings?StandingsData.AsArray()|>
                    Array.map(
                        fun x->
                            let userDbId = getOrInsertUser (x?UserName.AsString())
                            let rating = x?Rating.AsInteger()     
                            ctx.ContestLog.ContestParticipants.``Create(contest_contestId, rating)``(contestElm.ContestId,rating)|>ignore
                            problemElms|>
                                Array.map(
                                    fun y ->
                                        match x.TryGetProperty(y.ContestServerProblemId) with
                                        |None -> ()
                                        |Some x-> 
                                            ctx.ContestLog.ProblemSolverInContest.``Create(contest_users_userId, problem_problemId, rating)``(userDbId,y.ProblemId,rating)|>ignore
                                            ()
                                )
                    )|>ignore
                ctx.SubmitUpdates()
                transaction.Complete()
                ()
            else
                ()
        with
        | :? WebException as we -> 
            eprintfn "Can't save contest %s %s" id we.Message 
            Console.Error.WriteLine("uri:{0}",we.Response.ResponseUri)
            use data = we.Response.GetResponseStream()
            let streamr=new StreamReader(data)
            eprintfn "%s" (streamr.ReadToEnd())
            GC.Collect()
            ()
        | :? TransactionAbortedException as te ->
            eprintfn "Transaction Error %s %s" id te.Message
            GC.Collect()
            ()

    async{
        eprintfn "AtCoder scraper starts!"
        getContests()
            |>Array.filter(
                fun (startTime,name,id)->
                    let ctx=getDataContext()
                    let elm =
                        query{
                            for contest in ctx.ContestLog.Contest do
                                where (contest.ContestServerContestServerId=atcoderDbId && contest.ContestServerContestId=id)
                        }
                    (Seq.isEmpty elm)
            )
            |>Array.map(insertContestResultToDb)|>ignore
        eprintfn "AtCoder scraper ends!"
        Threading.Thread.Sleep(TimeSpan.FromHours(4.0))
        atcoder()|>Async.RunSynchronously|>ignore
    }