module Scraper.Codeforces
open Scraper.Setting
open Scraper.Submission
open System
open System.IO
open System.Net
open System.Transactions
open FSharp.Data
open FSharp.Data.JsonExtensions
open FSharp.Data.Runtime.Caching
open MySql.Data
let cfCachePrefix = "codeforces"
let codeforcesCache = createInternetFileCache (Path.Combine(gCachePrefix,cfCachePrefix)) (TimeSpan.MaxValue)
let cfDeleteAllCache () =
    let cacheFolder =
        if Environment.OSVersion.Platform = PlatformID.Unix
        then Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/.cache/fsharp-data"
        else Environment.GetFolderPath(Environment.SpecialFolder.InternetCache)
    let downloadCache = Path.Combine( cacheFolder, (Path.Combine(gCachePrefix,cfCachePrefix)))
    let dir = DirectoryInfo(downloadCache)
    for fi in dir.GetFiles() do
        fi.Delete()
    for di in dir.GetDirectories() do
        di.Delete()
    ()
let rec codeforces ()=
    eprintfn "Codeforces crawler start!" |> ignore
    let contestServerId=
        let ctx=getDataContext()
        query{
        for contest in ctx.ContestLog.ContestServer do
            where (contest.ContestServerName="Codeforces")
            select contest.ContestServerId
            exactlyOne
        }
    let participantsRatingDict participantsRating =
        participantsRating|>Array.map(fun x-> (x?handle.AsString(),x?newRating.AsInteger()))|>Map.ofArray

    let insertProblem contestId problem =
        let ctx = getDataContext()
        let problemElm = ctx.ContestLog.Problem.``Create(contestServerProblemId, contest_contestId, problemName)``(problem?index.AsString(),contestId,problem?name.AsString())
        ctx.SubmitUpdates()
        eprintfn "problemDbId %d" problemElm.ProblemId
        match problem.TryGetProperty("rating") with
        | None -> ()
        | Some(x) -> ctx.ContestLog.ProblemDifficulty.``Create``(problem?rating.AsFloat(),problemElm.ProblemId)|>ignore
                     ctx.SubmitUpdates()
        problem?tags.AsArray()|>Array.map(fun x-> x.AsString())
            |>(Array.distinctBy (fun x->x.ToUpper()))
            |>Array.map(fun x-> ctx.ContestLog.ProblemTag.``Create(is_created, problem_problemId, tag)``(0y,problemElm.ProblemId,x))|>ignore
        ctx.SubmitUpdates()
        problemElm.ProblemId

    let makeParticipantsRatingCache handles (prDict:Map<string,int>) =
        handles|>Array.filter(fun x ->  not (prDict.ContainsKey x))|>
                 Array.map(fun handle ->
                                match codeforcesCache.TryRetrieve(handle) with
                                | None -> 
                                    let c = Http.RequestString("https://codeforces.com/api/user.rating?handle="+handle)
                                    codeforcesCache.Set (handle,c)
                                    ()
                                | Some(x) ->
                                    ())

    let getRating contestTime handle (prDict:Map<string,int>) = 
        if prDict.ContainsKey(handle) then prDict.Item(handle) 
            else 
                let ratingChanges = match codeforcesCache.TryRetrieve(handle) with
                                    | None -> 
                                            let c = Http.RequestString("https://codeforces.com/api/user.rating?handle="+handle)
                                            codeforcesCache.Set (handle, c)
                                            JsonValue.Parse(c)?result.AsArray()
                                    | Some(x) -> JsonValue.Parse(x)?result.AsArray()
                let idx = ~~~ (System.Array.BinarySearch(ratingChanges,JsonValue.Parse("{\"ratingUpdateTimeSeconds\": "+(contestTime.ToString())+" }"),
                                                                                        ComparisonIdentity.FromFunction(fun x y -> Operators.compare (x?ratingUpdateTimeSeconds.AsInteger64()) (y?ratingUpdateTimeSeconds.AsInteger64()) )))
                if ratingChanges.Length =0 then 1500 else if idx = 0 then ratingChanges.[idx]?newRating.AsInteger() else ratingChanges.[idx-1]?newRating.AsInteger()

    let filterSolver ranking probidx =
        ranking |> Array.filter (fun x -> x?problemResults.AsArray().[probidx]?points.AsFloat() > 0.0)|>Array.collect(fun x -> x?party?members.AsArray())|>Array.map(fun x-> x?handle.AsString())
    
    let extractUsers ranking =
        ranking|> Array.collect(fun x -> x?party?members.AsArray())|>Array.map(fun x -> x?handle.AsString())

    let getOrInsertUser handle = 
        let ctx = getDataContext()
        try
            query{
                for user in ctx.ContestLog.ContestUsers do
                    where (user.ContestUserId = handle && user.ContestServerContestServerId = contestServerId)
                    exactlyOne
            }
        with
        | :? InvalidOperationException as oe ->
            let ret =                                 
                ctx.ContestLog.ContestUsers.``Create(contestUserId, contest_server_contestServerId)``(handle,contestServerId) 
            ctx.SubmitUpdates()
            ret

    let insertSolversInContest contestTime problemDbId handles prDict =
        let ctx = getDataContext()
        eprintfn "insertSolversInContest problemDbId %d" problemDbId
        handles|>Array.map(getOrInsertUser)
               |>Array.map(fun x -> 
                                let ret=ctx.ContestLog.ProblemSolverInContest.``Create(contest_users_userId, problem_problemId, rating)``(x.UserId,problemDbId,(getRating contestTime x.ContestUserId prDict))
                                ctx.SubmitUpdates()
                                ret)
               |>ignore
        ctx.SubmitUpdates()
        ()

    let insertContestAndProblemsAndParticipants contestId =
        let transactionopt = new TransactionOptions(Timeout=TimeSpan.Zero,IsolationLevel=IsolationLevel.Serializable)
        try
            eprintfn "contest %d" contestId
            let prDict =  
                let participantsRating=Http.RequestString("https://codeforces.com/api/contest.ratingChanges?contestId="+contestId.ToString(),silentHttpErrors=true)|>JsonValue.Parse
                if participantsRating?status.AsString() = "OK" then participantsRatingDict (participantsRating?result.AsArray()) else Map.empty
            let  problemsAndParticipants =Http.RequestString("https://codeforces.com/api/contest.standings?contestId="+contestId.ToString())|>JsonValue.Parse
            (extractUsers (problemsAndParticipants?result?rows.AsArray()))
                |>fun x ->makeParticipantsRatingCache x prDict|>ignore
            let contestTime = problemsAndParticipants?result?contest?startTimeSeconds.AsInteger64()
            eprintfn "prDict %s" (prDict.ToString()) 
            use transaction = new TransactionScope(TransactionScopeOption.RequiresNew,
                                                   transactionopt,
                                                   TransactionScopeAsyncFlowOption.Enabled)
            let ctx = getDataContext()

            let contestElm = ctx.ContestLog.Contest.``Create(contestName, contestServerContestId, contestStartTime, contest_server_contestServerId)`` 
                                                           (problemsAndParticipants?result?contest?name.AsString(),
                                                            problemsAndParticipants?result?contest?id.AsInteger().ToString(),
                                                            problemsAndParticipants?result?contest?startTimeSeconds.AsInteger64(),
                                                            contestServerId)
            ctx.SubmitUpdates()
            eprintfn "contestDbId %d" contestElm.ContestId

            let problemIdArr = problemsAndParticipants?result?problems.AsArray()|>Array.map(insertProblem contestElm.ContestId)

            (extractUsers (problemsAndParticipants?result?rows.AsArray()))
                |>Array.map(getOrInsertUser)|>ignore
            (extractUsers (problemsAndParticipants?result?rows.AsArray()))
                |>Array.map(fun x-> getRating contestTime x prDict)
                |>Array.map(fun x->
                                let ret=ctx.ContestLog.ContestParticipants.``Create(contest_contestId, rating)``(contestElm.ContestId,x)
                                ctx.SubmitUpdates()
                                ret)|>ignore
            eprintfn "Participants"

            let problemSolverInContestArr = 
                [|0..problemIdArr.Length-1|]
                    |>Array.map(fun x -> (problemIdArr.[x],
                                          filterSolver (problemsAndParticipants?result?rows.AsArray()) x))
            eprintfn "problemSolverInContestArr"
            problemSolverInContestArr
                |>Array.map(fun (problemDbId,solvers)->insertSolversInContest contestTime problemDbId solvers prDict)|>ignore
            ctx.SubmitUpdates()
            eprintfn "Problem Solver"
            transaction.Complete()
            eprintfn "%d done" contestId
            transaction.Dispose()
            GC.Collect()
            ()
        with
        | :? WebException as we -> 
            eprintfn "Can't save contest %d %s" contestId we.Message 
            Console.Error.WriteLine("uri:{0}",we.Response.ResponseUri)
            use data = we.Response.GetResponseStream()
            let streamr=new StreamReader(data)
            eprintfn "%s" (streamr.ReadToEnd())
            GC.Collect()
            ()
        | :? TransactionAbortedException as te ->
            eprintfn "Transaction Error %d %s" contestId te.Message
            GC.Collect()
            ()
    
    let isContestInDb contestId =
        let ctx = getDataContext()
        let elm = query{
                for contest in ctx.ContestLog.Contest do
                    where ((contest.ContestServerContestId = contestId) && (contest.ContestServerContestServerId = contestServerId))
            }
        not (Seq.isEmpty elm)

    let addDifficulty () =
        let transactionopt = TransactionOptions(Timeout=TimeSpan.Zero,IsolationLevel=IsolationLevel.Serializable)
        try
            use transaction = new TransactionScope(TransactionScopeOption.RequiresNew,
                                                   transactionopt,
                                                   TransactionScopeAsyncFlowOption.Enabled)
            eprintfn "Start addDifficulty"
            let ctx = getDataContext()
            eprintfn "Get Elms"
            let elms = 
                query{
                    for problem in ctx.ContestLog.Problem do
                        join contest in ctx.ContestLog.Contest on (problem.ContestContestId = contest.ContestId)
                        where (not (query{
                            for diff in ctx.ContestLog.ProblemDifficulty do
                                select diff.ProblemProblemId
                                contains problem.ProblemId
                        }))
                        select (contest.ContestId,contest.ContestServerContestId)
                        distinct
                }|>Seq.toArray
            eprintfn "map elms %d" (elms.Length)
            elms|>Array.map(
                fun (dbId,contestId)->
                    eprintfn "add Difficulty of %s" contestId
                    let contestRes = Http.RequestString("https://codeforces.com/api/contest.standings?contestId="+contestId.ToString()+"&from=1&count=1")
                    eprintfn "%s" contestRes
                    let problems = JsonValue.Parse(contestRes)?result?problems.AsArray()
                    problems|>
                        Array.map(
                            fun problem->
                                match problem.TryGetProperty("rating") with
                                | None -> ()
                                | Some(x) -> 
                                    eprintfn "Difficulty %f" (x.AsFloat())
                                    let elm = 
                                        query{
                                            for p in ctx.ContestLog.Problem do
                                                where (p.ContestContestId = dbId && p.ContestServerProblemId = (problem?index.AsString()))
                                                select (p.ProblemId)
                                                exactlyOne
                                        }
                                    ctx.ContestLog.ProblemDifficulty.``Create(problemDifficulty, problem_problemId)``(x.AsFloat(),elm)|>ignore
                                    ctx.SubmitUpdates()
                        )|>ignore
                )|>ignore
            ctx.SubmitUpdates()
            transaction.Complete()
            eprintfn "All complete"
        with
        | :? TransactionAbortedException as te ->
            eprintfn "Transaction Error in addDifficulty %s" te.Message
            GC.Collect()
            ()
    async{
        try
            let contestsRes=Http.RequestString("https://codeforces.com/api/contest.list")
            let  contestsArr=JsonValue.Parse(contestsRes)?result.AsArray()
            contestsArr|>Array.filter(fun contestJson -> contestJson?phase.AsString() <> "FINISHED")
                       |>Array.filter(fun contestJson ->
                                          let ctx = getDataContext()
                                          let cnt = query{
                                              for contestbe in ctx.ContestLog.ContestBeforeEnd do
                                                where (contestbe.ContestServerContestId=contestJson?id.AsInteger().ToString()&&contestbe.ContestServerContestServerId=contestServerId)
                                                count
                                          }
                                          cnt=0)
                       |>Array.map(fun contestJson -> 
                                        eprintfn "before end %s" (contestJson?name.AsString())
                                        let ctx=getDataContext()
                                        ctx.ContestLog.ContestBeforeEnd.``Create(contestEndTime, contestServerContestId, contestServerContestName, contestStartTime, contest_server_contestServerId)``
                                                                               (
                                                                                   contestJson?startTimeSeconds.AsInteger64()+contestJson?durationSeconds.AsInteger64(),
                                                                                   contestJson?id.AsInteger().ToString(),
                                                                                   contestJson?name.AsString(),
                                                                                   contestJson?startTimeSeconds.AsInteger64(),
                                                                                   contestServerId
                                                                               )|>ignore
                                        ctx.SubmitUpdates())
                       |>ignore
            contestsArr|>Array.filter(fun contestJson -> contestJson?phase.AsString() = "FINISHED")
                       |>Array.map(fun x->
                                        let ctx=getDataContext()
                                        query{
                                            for contestbe in ctx.ContestLog.ContestBeforeEnd do
                                                where (contestbe.ContestServerContestServerId=contestServerId&&contestbe.ContestServerContestId=(x?id.AsInteger().ToString()))
                                        }    
                                            |>FSharp.Data.Sql.Seq.``delete all items from single table``  |>Async.RunSynchronously|>ignore
                                        ()
                       )|>ignore
            contestsArr|>Array.filter(fun contestJson -> contestJson?phase.AsString() = "FINISHED")
                       |>Array.filter(fun contestJson -> not (isContestInDb (contestJson?id.AsInteger().ToString()))) |>Array.map(fun contestJson -> insertContestAndProblemsAndParticipants (contestJson?id.AsInteger()))|>ignore
            addDifficulty()
            eprintfn "Update Done"
            cfDeleteAllCache()|>ignore
            eprintfn "Delete all cache"
            let ctx = getDataContext()
            let cnt = query{
                for contestbe in ctx.ContestLog.ContestBeforeEnd do
                    count
            }
            eprintfn "count:%d" cnt
            let nextCrawleTime = if cnt = 0 then TimeSpan.FromHours(3.0) 
                                    else
                                        let nowunixtime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                                        let time=
                                            query{
                                                for contestbe in ctx.ContestLog.ContestBeforeEnd do
                                                    sortBy (contestbe.ContestStartTime)
                                                    select (contestbe.ContestStartTime,contestbe.ContestEndTime)
                                                    take 1
                                            }|>Seq.map(fun (x,y)-> TimeSpan.FromSeconds(float (if x-nowunixtime<0L then y-nowunixtime else x-nowunixtime)))|>Seq.head
                                        min time (TimeSpan.FromDays(1.0))
            Console.WriteLine ("SleepTime {0}",nextCrawleTime)
            Threading.Thread.Sleep(nextCrawleTime)
            codeforces()|>Async.RunSynchronously|>ignore
        with
        | :? WebException as we ->
            eprintfn "API Connection Error %s" we.Message
            Threading.Thread.Sleep(TimeSpan.FromSeconds(1.0))
            codeforces()|>Async.RunSynchronously|>ignore
    }

let userCodeforces () =
    eprintfn "userCodeforces Start!"
    let contestServerDbId=
        let ctx=getDataContext()
        query{
        for contest in ctx.ContestLog.ContestServer do
            where (contest.ContestServerName="Codeforces")
            select contest.ContestServerId
            exactlyOne
        }
    let convertVerdict2SubmissionStatus verdict =
        match verdict with
        |"OK" -> SubmissionStatus.AC
        |"PARTIAL"->SubmissionStatus.PAC
        |"COMPILATION_ERROR"->SubmissionStatus.CE
        |"RUNTIME_ERROR"->SubmissionStatus.RE
        |"WRONG_ANSWER"->SubmissionStatus.WA
        |"PRESENTATION_ERROR"->SubmissionStatus.PE
        |"TIME_LIMIT_EXCEEDED"->SubmissionStatus.TLE
        |"MEMORY_LIMIT_EXCEEDED"->SubmissionStatus.MLE
        |"IDLENESS_LIMIT_EXCEEDED"->SubmissionStatus.TLE
        |"SECURITY_VIOLATED"->SubmissionStatus.RE
        |"CRASHED"->SubmissionStatus.WJ
        |"INPUT_PREPARATION_CRASHED"->SubmissionStatus.WJ
        |"CHALLENGED"->SubmissionStatus.WA
        |"SKIPPED"->SubmissionStatus.WJ
        |"TESTING"->SubmissionStatus.WJ
        |"REJECTED"->SubmissionStatus.IG
        |x->
            eprintfn "convertVerdict2SubmissionStatus %s" x
            SubmissionStatus.IG
    let getUserSubmissions handle =
        let submissions=Http.RequestString("https://codeforces.com/api/user.status?handle="+handle)
                            |>JsonValue.Parse
        let ret=
            submissions?result.AsArray()
                |>Array.map(fun submission ->
                                (submission?problem?contestId.AsInteger(),
                                 submission?problem?index.AsString(),
                                 submission?creationTimeSeconds.AsInteger64(),
                                 (convertVerdict2SubmissionStatus (submission?verdict.AsString())),
                                 submission?id.AsInteger64()))
                |>Array.filter(fun (_,_,_,ss,_)->(ss<>SubmissionStatus.WJ)&&(ss<>SubmissionStatus.IG))
        (handle,ret)
    let insertUserSubmissions (handle,submissions) =
        let ctx=getDataContext()
        let userDbId = query{
            for user in ctx.ContestLog.ContestUsers do
                where (user.ContestServerContestServerId=contestServerDbId&&user.ContestUserId=handle)
                select user.UserId
                exactlyOne
        }
        let insertUserSubmission (contestId,problemIndex,creationTime,ss,submissionId) =
            let contestDbId = query{
                for contest in ctx.ContestLog.Contest do
                    where ((contest.ContestServerContestServerId=contestServerDbId)&&(contest.ContestServerContestId=contestId.ToString()))
                    select contest.ContestId
                    exactlyOne
            }
            let problemDbId = query{
                for problem in ctx.ContestLog.Problem do
                    where ((problem.ContestContestId=contestDbId)&&(problem.ContestServerProblemId=problemIndex))
                    select problem.ProblemId
                    exactlyOne
            }
            let inDb = 
                query{
                    for submission in ctx.ContestLog.ProblemSubmissions do
                        where (submission.ContestServerSubmissonId = submissionId && submission.ContestUsersUserId=userDbId && submission.ProblemProblemId=problemDbId)
                }
            if Seq.isEmpty inDb then 
                ctx.ContestLog.ProblemSubmissions.``Create(contestServerSubmissonId, contest_users_userId, problem_problemId, submission_status, submission_time)`` (submissionId,userDbId,problemDbId,submissionStatusToString ss,creationTime)
                |>ignore
                ctx.SubmitUpdates()
                ()
            else 
                let head = Seq.head inDb
                if head.SubmissionStatus = (submissionStatusToString ss) then
                    ()
                else 
                    head.SubmissionStatus <- submissionStatusToString ss
                    ()
        submissions|>Array.map(insertUserSubmission)|>ignore
        ()
            
        
    let ctx = getDataContext()
    let handles = 
        query{
            for watchingUser in ctx.ContestLog.WatchingUser do
                for user in ctx.ContestLog.ContestUsers do
                    for server in ctx.ContestLog.ContestServer do
                        where (user.UserId = watchingUser.ContestUsersUserId&&user.ContestServerContestServerId = server.ContestServerId && server.ContestServerName = "Codeforces")
                        select user.ContestUserId
        }|>Seq.toList
    handles|>List.map(getUserSubmissions>>insertUserSubmissions)|>ignore
    eprintfn "userCodeforces ends"
    ()
