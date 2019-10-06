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
let getAtCoderDbId () =
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
        eprintfn "[AtCoder or userAtCoder] Now %d %d" page rated
        let res = HtmlDocument.Load((getContestListUrl page rated))
        webSleep()
        let x =
            res.CssSelect "tbody > tr"
                |>Seq.map(
                    fun x->
                        x.Descendants ["td"]|>Seq.toArray
                            |>fun x->
                                let cId = x.[1].Descendants ["a"]
                                        |>Seq.map(fun x->x.TryGetAttribute("href"))
                                        |>Seq.head
                                        |>fun x->
                                            match x with
                                            |None -> ""
                                            |Some x -> 
                                                let arr=x.Value().Split("/")
                                                arr.[Array.length(arr)-1]
                                (DateTime.Parse( x.[0].InnerText()),x.[1].InnerText(),cId)
                )|>Seq.toArray
        match x with
        | [||] ->
            arr
        | x ->
            getContestsFromPage (page+1) rated (Array.append arr x)
    [|1..3|]|>Array.collect(fun x->getContestsFromPage 1 x [||])
let atCoderStatus2Status str =
    match str with
    |"CE" -> SubmissionStatus.CE
    |"MLE" -> SubmissionStatus.MLE
    |"TLE" -> SubmissionStatus.TLE
    |"RE" -> SubmissionStatus.RE
    |"OLE" -> SubmissionStatus.WA
    |"IE" -> SubmissionStatus.WJ
    |"WA" -> SubmissionStatus.WA
    |"AC" -> SubmissionStatus.AC
    |"WJ" -> SubmissionStatus.WJ
    |"WR" -> SubmissionStatus.WJ
    |x -> SubmissionStatus.NaN
let getContestSubmissionUrl contest page handle =
    "https://atcoder.jp/contests/"+contest+"/submissions?lang=ja&f.User="+handle+"&page="+(page.ToString())
let insertSubmissions () =
    let ctx=getDataContext()
    let handleArr = 
        query{
            for wuser in ctx.ContestLog.WatchingUser do
                for cuser in ctx.ContestLog.ContestUsers do 
                    for contestServer in ctx.ContestLog.ContestServer do
                        where (wuser.ContestUsersUserId = cuser.UserId && contestServer.ContestServerId = cuser.ContestServerContestServerId && contestServer.ContestServerName = "AtCoder")
                        select (cuser.ContestUserId,cuser.UserId)
        }|>Seq.toArray
    let contests = getContests()
    contests|>Array.map(fun (_,_,cId)->
        let contestElm = 
            query{
                for contest in ctx.ContestLog.Contest do
                    where (contest.ContestServerContestId = cId)
                    exactlyOne
            }
        let problemSet =
            query{
                for prob in ctx.ContestLog.Problem do
                    where(prob.ContestContestId = contestElm.ContestId)
                    select(prob.ContestServerProblemId,prob.ProblemId)
            }|>Map.ofSeq
        let rec getSubmissionsFromPage (handle,userDbId) page =
            eprintfn "[userAtCoder] Now %s %s %d" cId handle page
            let res = HtmlDocument.Load((getContestSubmissionUrl cId page handle))
            webSleep()
            let x=
                res.CssSelect "tbody > tr"
            if Seq.isEmpty x then
                eprintfn "[userAtCoder] Nothing to do %s %s %d" cId handle page
                ()
            else
                eprintfn "[userAtCoder] collect submissions %s %s %d" cId handle page
                x|>List.map(
                    fun x->
                        eprintfn "[userAtCoder] %s" (x.ToString())
                        x.Descendants["td"]|>Seq.toArray
                            |>fun x->
                                let getLast = 
                                    fun (x:string)->
                                        let y=x.Split("/")
                                        y.[y.Length-1]
                                eprintfn "[userAtCoder] in td %s" (x.[6].Elements().[0].Elements().[0].ToString())
                                let submissionTime = (DateTimeOffset.Parse(x.[0].InnerText())).ToUnixTimeSeconds()
                                eprintfn "[userAtCoder] submissionTime %d" submissionTime
                                let problemId = (Seq.head (x.[1].Descendants ["a"])).TryGetAttribute("href")|>Option.map(fun x-> getLast(x.Value()))
                                eprintfn "[userAtCoder] problemId %s" (problemId.ToString())
                                let serverSubmissionId = (Seq.head(x.[x.Length-1].Descendants["a"])).TryGetAttribute("href")|>Option.map(fun x->getLast(x.Value()))
                                eprintfn "[userAtCoder] serverSubmissionId %s" (serverSubmissionId.ToString())
                                let statusStr = submissionStatusToString (atCoderStatus2Status (x.[6].Elements().[0].Elements().[0].ToString()))
                                eprintfn "[userAtCoder] %s %s" statusStr ((x.[6].InnerText()))
                                match problemId with
                                | Some pId ->
                                    match serverSubmissionId with
                                    | Some sId -> 
                                        eprintfn "[userAtCoder] %d %s %s %s" submissionTime pId sId statusStr
                                        let elm =
                                            let submissionId = int64(sId)
                                            try
                                                Ok(query{
                                                    for submission in ctx.ContestLog.ProblemSubmissions do
                                                        where (submission.ProblemProblemId = (problemSet.[pId]) && submission.ContestServerSubmissionId = (submissionId) && submission.ContestUsersUserId = userDbId)
                                                        exactlyOne
                                                })
                                            with
                                            | :? Exception as e->
                                                eprintfn "[userAtCoder] Error %s" (e.Message)
                                                Error()
                                        match elm with
                                        | Ok x->
                                            eprintfn "[userAtCoder] update %d %d %d %d %s" (int64(sId)) userDbId (problemSet.[pId]) submissionTime statusStr
                                            x.SubmissionStatus <- statusStr
                                            x.SubmissionTime <- submissionTime
                                            ctx.SubmitUpdates()
                                        | Error () ->
                                            eprintfn "[userAtCoder] insert %d %d %d %d %s" (int64(sId)) userDbId (problemSet.[pId]) submissionTime statusStr
                                            ctx.ContestLog.ProblemSubmissions.``Create(contestServerSubmissionId, contest_users_userId, problem_problemId, submission_status, submission_time)``
                                                (int64(sId),userDbId,problemSet.[pId],statusStr,submissionTime)|>ignore
                                            ctx.SubmitUpdates()
                                    | None -> ()
                                | None -> ()
                )|>ignore
                ctx.SubmitUpdates()
                getSubmissionsFromPage (handle,userDbId) (page+1)
        handleArr |> Array.map(fun x->getSubmissionsFromPage x 1) |> ignore
    )|>ignore
        


let rec atcoder () =
    let atcoderDbId =  getAtCoderDbId()
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
        eprintfn "[AtCoder] Start %s" id
        try
            let standings=
                Http.RequestString(getContestResultUrl(id))
                    |>JsonValue.Parse
            webSleep()
            if standings?Fixed.AsBoolean() then
                let ctx=getDataContext()
                use transaction = new TransactionScope(TransactionScopeOption.RequiresNew,
                                                       TransactionOptions(Timeout=TimeSpan.Zero,IsolationLevel=IsolationLevel.RepeatableRead),
                                                       TransactionScopeAsyncFlowOption.Enabled)
                let contestElm=ctx.ContestLog.Contest.``Create(contestName, contestServerContestId, contestStartTime, contest_server_contestServerId)``
                                                (name,id,(DateTimeOffset(startTime)).ToUnixTimeSeconds(),atcoderDbId)
                ctx.SubmitUpdates()
                let problemElms =
                    standings?TaskInfo.AsArray()|>
                        Array.map(
                            fun x->
                                eprintfn "[AtCoder] add atcoder problem %s %s" (x?TaskScreenName.AsString()) (x?TaskName.AsString())
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
            eprintfn "[AtCoder] Can't save contest %s %s" id we.Message 
            Console.Error.WriteLine("uri:{0}",we.Response.ResponseUri)
            use data = we.Response.GetResponseStream()
            let streamr=new StreamReader(data)
            eprintfn "[AtCoder] %s" (streamr.ReadToEnd())
            GC.Collect()
            ()
        | :? TransactionAbortedException as te ->
            eprintfn "[AtCoder] Transaction Error %s %s" id te.Message
            GC.Collect()
            ()

    async{
        eprintfn "[AtCoder] AtCoder scraper starts!"
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
        eprintfn "[AtCoder] AtCoder scraper ends!"
        Threading.Thread.Sleep(TimeSpan.FromHours(4.0))
        atcoder()|>Async.RunSynchronously|>ignore
    }
let rec userAtCoder () =
    async{
        eprintfn "[userAtCoder] userAtCoder start"
        let ctx = getDataContext()
        insertSubmissions()
        Threading.Thread.Sleep(TimeSpan.FromMinutes(1.0))
        userAtCoder()|>Async.RunSynchronously|>ignore
    }