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
        eprintfn "[AtCoder] Now %d %d" page rated
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
                                let cName = x.[1].Descendants["a"]|>Seq.head|>fun x->x.InnerText()
                                (DateTime.Parse( x.[0].InnerText()),cName,cId)
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
let getOldContestSubmissionUrl contest =
    "https://"+contest+".contest.atcoder.jp/submissions/all/json"
let insertSubmissions () =
    try
        let ctx=getDataContext()
        let handleMap = 
            query{
                for wuser in ctx.ContestLog.WatchingUser do
                    for cuser in ctx.ContestLog.ContestUsers do 
                        for contestServer in ctx.ContestLog.ContestServer do
                            where (wuser.ContestUsersUserId = cuser.UserId && contestServer.ContestServerId = cuser.ContestServerContestServerId && contestServer.ContestServerName = "AtCoder")
                            select (cuser.ContestUserId,cuser.UserId)
            }|>Map.ofSeq
        eprintfn "[UserAtcoder] handles %s" (handleMap.ToString())
        let contests = 
                query{
                    for contest in ctx.ContestLog.Contest do
                        join cserver in ctx.ContestLog.ContestServer on (contest.ContestServerContestServerId = cserver.ContestServerId)
                        where (cserver.ContestServerName = "AtCoder")
                        select contest.ContestServerContestId
                }|>Array.ofSeq
        eprintfn "[UserAtCoder] contest %s" (contests.ToString())
        contests|>Array.map(fun cId->
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
            let submissionsJson = JsonValue.Load(getOldContestSubmissionUrl cId)
            webSleep()
            submissionsJson?response.AsArray()|>Array.filter(
                    fun x->
                        handleMap.ContainsKey(x?user_name.AsString())&&((x?is_accepted.AsInteger())=1)
                )|>Array.map(
                    fun x->
                        let problemName = x?task_screen_name.AsString()
                        eprintfn "[UserAtCoder] problemName %s" problemName
                        let problemDbId = 
                            query{
                                for problem in ctx.ContestLog.Problem do
                                    join contest in ctx.ContestLog.Contest on (problem.ContestContestId = contest.ContestId)
                                    where (problem.ContestServerProblemId = problemName&&contest.ContestServerContestId = cId)
                                    select (problem.ProblemId)
                                    exactlyOne
                            }

                        let cuserDbId = 
                            match handleMap.TryFind(x?user_name.AsString()) with
                            | Some(x) -> x
                            | None -> -1
                        let cserverSubmissionId = x?submission_id.AsInteger64()
                        let submissionTime = DateTimeOffset(DateTime.SpecifyKind(DateTime.Parse(x?created.AsString()),DateTimeKind.Local))
                        let isInDb=
                            query{
                                for submission in ctx.ContestLog.ProblemSubmissions do
                                    exists (submission.ContestServerSubmissionId = cserverSubmissionId&& submission.ProblemProblemId = problemDbId && submission.ContestUsersUserId = cuserDbId)
                            }
                        if not isInDb then
                            eprintfn "[UserAtCoder] insert submissions (%d,%d,%d,%s,%d)" cserverSubmissionId cuserDbId problemDbId (submissionStatusToString(atCoderStatus2Status(x?status.AsString()))) (submissionTime.ToUnixTimeSeconds())
                            ctx.ContestLog.ProblemSubmissions.``Create(contestServerSubmissionId, contest_users_userId, problem_problemId, submission_status, submission_time)``
                                (cserverSubmissionId,cuserDbId,problemDbId,submissionStatusToString( atCoderStatus2Status(x?status.AsString())),submissionTime.ToUnixTimeSeconds())|>ignore
                        else
                            ()
                )|>ignore
            ctx.SubmitUpdates()
            ()
        )|>ignore
    with 
    | :? Exception as e ->
        eprintfn "[UserAtCoder] Error %s" e.Message
        ()
        

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
        insertSubmissions()
        Threading.Thread.Sleep(TimeSpan.FromMinutes(1.0))
        userAtCoder()|>Async.RunSynchronously|>ignore
    }