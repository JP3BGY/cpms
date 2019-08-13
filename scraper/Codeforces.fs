module Codeforces
open Setting
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
let codeforces ()=
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
        let problemElm = ctx.ContestLog.Problem.``Create(contestServerProblemId, contest_contestId, problemName)``(problem?name.AsString(),contestId,problem?index.AsString())
        ctx.SubmitUpdates()
        eprintfn "problemDbId %d" problemElm.ProblemId
        match problem.TryGetProperty("rating") with
        | None -> ()
        | Some(x) -> ctx.ContestLog.ProblemDifficulty.``Create``(problem?rating.AsFloat(),problemElm.ProblemId)|>ignore
                     ctx.SubmitUpdates()
        problem?tags.AsArray()|>Array.map(fun x-> ctx.ContestLog.ProblemTag.``Create(is_created, problem_problemId, tag)``(0y,problemElm.ProblemId,x.AsString()))|>ignore
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
                                                                                        ComparisonIdentity.FromFunction(fun x y -> Operators.compare (x?ratingUpdateTimeSeconds.AsInteger()) (y?ratingUpdateTimeSeconds.AsInteger()) )))
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
        try
            eprintfn "contest %d" contestId
            let prDict =  
                let participantsRating=Http.RequestString("https://codeforces.com/api/contest.ratingChanges?contestId="+contestId.ToString(),silentHttpErrors=true)|>JsonValue.Parse
                if participantsRating?status.AsString() = "OK" then participantsRatingDict (participantsRating?result.AsArray()) else Map.empty
            let  problemsAndParticipants =Http.RequestString("https://codeforces.com/api/contest.standings?contestId="+contestId.ToString())|>JsonValue.Parse
            (extractUsers (problemsAndParticipants?result?rows.AsArray()))
                |>fun x ->makeParticipantsRatingCache x prDict|>ignore
            let contestTime = problemsAndParticipants?result?contest?startTimeSeconds.AsInteger()
            eprintfn "prDict %s" (prDict.ToString()) 
            use transaction = new TransactionScope(TransactionScopeOption.RequiresNew,
                                                   TimeSpan.Zero)
            let ctx = getDataContext()

            let contestElm = ctx.ContestLog.Contest.``Create(contestName, contestServerContestId, contest_server_contestServerId)``(problemsAndParticipants?result?contest?name.AsString(),
                                                                                                                                    problemsAndParticipants?result?contest?id.AsInteger().ToString(),
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
    async{
        try
            let contestsRes=Http.RequestString("https://codeforces.com/api/contest.list")
            let  contestsJson=JsonValue.Parse(contestsRes)
            contestsJson?result.AsArray()|>Array.filter(fun contestJson -> contestJson?phase.AsString() = "FINISHED")
                                         |>Array.filter(fun contestJson -> not (isContestInDb (contestJson?id.AsInteger().ToString()))) |>Array.map(fun contestJson -> insertContestAndProblemsAndParticipants (contestJson?id.AsInteger()))|>ignore
            eprintfn "Update Done"
            ()
        with
        | :? WebException as we ->
            eprintfn "API Connection Error %s" we.Message
            ()
    }