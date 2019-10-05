module ContestServer.VirtualContest
open System.Transactions
open System.Collections.Generic
open System
open ContestServer.Database2Data.Problem
open ContestServer.Setting
open ContestServer.Database2Data.UserInfo
open FSharp.Data.Sql
open ContestServer.Types

type Difficulty =
    | Easy = 0
    | EasyMiddle = 1
    | Middle = 2
    | MiddleHard = 3
    | Hard = 4
type VContestDb = 
    {
        IdvirtualContest:Int32
        StartTime:Int64
        EndTime:Int64
        CreatedUserUserIduser:Int32
    }
let getVirtualContest vContestDb =
    let ctx = getDataContext()
    {
        dbId = vContestDb.IdvirtualContest
        startTime = vContestDb.StartTime
        endTime = vContestDb.EndTime
        participants = 
            query{
                for vconParticipants in ctx.ContestLog.VirtualContestParticipants do
                    where (vconParticipants.VirtualContestIdvirtualContest = vContestDb.IdvirtualContest)
                    select (vconParticipants.UserIduser)
            }|>Seq.map(getUserInfoFromId)|>Seq.toArray
        creator = 
            getUserInfoFromId (vContestDb.CreatedUserUserIduser)
        name =
            query{
                for vConName in ctx.ContestLog.VirtualContestName do
                    where (vConName.VirtualContestIdvirtualContest = vContestDb.IdvirtualContest)
                    select (vConName.VirtualContestName)
                    exactlyOneOrDefault
            }
    }
let getProblemsOfVirtualContest dbId (userInfo:UserInfo) =
    let nowUnixTime = DateTimeOffset.Now.ToUnixTimeSeconds()
    let ctx = getDataContext()
    let elm = 
        query{
            for vcon in ctx.ContestLog.VirtualContest do
                where (vcon.IdvirtualContest = dbId && (vcon.CreatedUserUserIduser = userInfo.dbId || vcon.StartTime >= nowUnixTime))
        }|>Seq.map(fun x->x.MapTo<VContestDb>())
        |>Seq.map(getVirtualContest)
    if Seq.isEmpty elm then
        Error("No such contest found or you have no permission to see.")
    else 
        let problems = 
            query{
                for vconProblem in ctx.ContestLog.VirtualContestProblems do
                    join problem in ctx.ContestLog.Problem on (vconProblem.ProblemProblemId=problem.ProblemId)
                    where (vconProblem.VirtualContestIdvirtualContest = dbId)
                    select (problem)
            }|>Seq.map(fun x-> problemDb2Problem (x.MapTo<ProblemDb>()))|>Seq.toArray
        let submissions =
            query{
                for vParticipant in ctx.ContestLog.VirtualContestParticipants do
                    for vProblem in ctx.ContestLog.VirtualContestProblems do
                        for submission in ctx.ContestLog.ProblemSubmissions do
                            for wuser in ctx.ContestLog.WatchingUser do
                                where (vParticipant.VirtualContestIdvirtualContest = dbId && vProblem.VirtualContestIdvirtualContest = dbId && wuser.UserIduser = vParticipant.UserIduser && submission.ContestUsersUserId = wuser.ContestUsersUserId && submission.ProblemProblemId = vProblem.ProblemProblemId)
                                select (submission,wuser.UserIduser)
            }|>Seq.map(fun (x,y)-> (x.MapTo<SubmissionDb>(),y))
            |>Seq.map(fun (x,y)->submissionDb2SubmissionWithU x y)|>Seq.toArray
        Ok(Seq.head elm,problems,submissions)

let getVirtualContests()=
    let ctx = getDataContext()
    query{
        for vcontest in ctx.ContestLog.VirtualContest do
            sortByDescending vcontest.EndTime
            select (vcontest)
    }|>Seq.map(fun x->x.MapTo<VContestDb>())
    |>Seq.map(getVirtualContest)

let getRunningVirtualContests() = 
    let nowUnixTime = DateTimeOffset.Now.ToUnixTimeSeconds()
    let ctx = getDataContext()
    query{
        for vcontest in ctx.ContestLog.VirtualContest do
            where (vcontest.EndTime > nowUnixTime)
            sortByDescending vcontest.EndTime
            select (vcontest)
    }|>Seq.map(fun x->x.MapTo<VContestDb>())
    |>Seq.map(getVirtualContest)
let getPastVirtualContests() = 
    let nowUnixTime = DateTimeOffset.Now.ToUnixTimeSeconds()
    let ctx = getDataContext()
    query{
        for vcontest in ctx.ContestLog.VirtualContest do
            where (vcontest.EndTime <= nowUnixTime)
            sortByDescending vcontest.EndTime
            select (vcontest)
    }|>Seq.map(fun x->x.MapTo<VContestDb>())
    |>Seq.map(getVirtualContest)
let deleteContest (creatorInfo:UserInfo) dbId =
    let ctx = getDataContext()
    let elm = 
        query{
            for vcontest in ctx.ContestLog.VirtualContest do
                where (dbId = vcontest.IdvirtualContest && vcontest.CreatedUserUserIduser = creatorInfo.dbId)
        }
    if Seq.isEmpty elm then
        Error("No such Virtual Contest found")
    else
        use transaction = new TransactionScope(TransactionScopeOption.RequiresNew,
                                               TransactionOptions(Timeout=TimeSpan.Zero,IsolationLevel=IsolationLevel.Serializable),
                                               TransactionScopeAsyncFlowOption.Enabled)
        let delname = 
            query{
                for vname in ctx.ContestLog.VirtualContestName do
                    where (dbId = vname.VirtualContestIdvirtualContest)
            }
        let delnameasync = Seq.``delete all items from single table``(delname) 
        let delp = 
            query{
                for p in ctx.ContestLog.VirtualContestParticipants do
                    where (p.VirtualContestIdvirtualContest = dbId)
            }
        let delpasync = Seq.``delete all items from single table`` (delp)
        let delprob =
            query{
                for p in ctx.ContestLog.VirtualContestProblems do
                    where (p.VirtualContestIdvirtualContest = dbId)
            }
        let delprobasync = Seq.``delete all items from single table``(delprob)
        [|delpasync;delprobasync;delnameasync|]|>Async.Parallel|>Async.RunSynchronously|>ignore
        ctx.SubmitUpdates()
        Seq.``delete all items from single table``(elm)|>Async.RunSynchronously|>ignore
        ctx.SubmitUpdates()
        transaction.Complete()
        Ok()
let modifyContest dbId (creatorInfo:UserInfo) startTime duration (problems:int[]) name=
    let ctx = getDataContext()
    let maybeelm = 
        try
            let e=query{
                for vcontest in ctx.ContestLog.VirtualContest do
                    where (dbId = vcontest.IdvirtualContest && vcontest.CreatedUserUserIduser = creatorInfo.dbId)
                    exactlyOne
            }
            Ok(e)
        with
        | :? SystemException as e ->
            Error()
    match maybeelm with 
    | Error  _ ->
        Error("No such Virtual Contest found")
    | Ok elm ->
        let isProbExist = 
            not (Array.exists 
                (fun p ->
                    not (query{
                        for prob in ctx.ContestLog.Problem do
                            select prob.ProblemId
                            contains p
                    }))
                problems
            )
        if isProbExist then
            let nowTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            if startTime + duration - nowTime < 5L*60L then
                Error("already ended or soon will end")
            else 
                use transaction = new TransactionScope(TransactionScopeOption.RequiresNew,
                                                       TransactionOptions(Timeout=TimeSpan.Zero,IsolationLevel=IsolationLevel.Serializable),
                                                       TransactionScopeAsyncFlowOption.Enabled)
                elm.StartTime <- startTime
                elm.EndTime <- startTime+duration
                let nameElms = 
                    query{
                        for name in ctx.ContestLog.VirtualContestName do
                            where (name.VirtualContestIdvirtualContest = dbId)
                    }
                let _ =
                    if Seq.isEmpty nameElms then
                        ctx.ContestLog.VirtualContestName.``Create(virtualContestName, virtual_contest_idvirtual_contest)``(name,dbId)
                    else
                        let nameElm = Seq.head(nameElms)
                        nameElm.VirtualContestName <- name
                        nameElm
                let probElms =
                    query{
                        for vprob in ctx.ContestLog.VirtualContestProblems do
                            where(vprob.VirtualContestIdvirtualContest = dbId)
                    }
                Seq.``delete all items from single table``(probElms)|>Async.RunSynchronously|>ignore
                ctx.SubmitUpdates()
                problems|>Array.map(
                    fun p ->
                        ctx.ContestLog.VirtualContestProblems.``Create(problem_problemId, virtual_contest_idvirtual_contest)``(p,dbId)
                )|>ignore
                ctx.SubmitUpdates()
                transaction.Complete()
                Ok()
        else
            Error("No such problem found")
let createContest (creatorInfo:UserInfo) startTime duration (problems:int[]) name= 
    let nowTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
    if nowTime >= startTime+duration then
        Error "This contest already ended."
    else 
        if duration < 5L*60L || startTime+duration - nowTime < 5L*60L then
            Error "There is little time left."
        else
            let ctx = getDataContext()
            let problemSet = ( Set.ofArray problems )
            let servers = 
                query{
                    for problem in ctx.ContestLog.Problem do
                        for contest in ctx.ContestLog.Contest do
                            for contestServer in ctx.ContestLog.ContestServer do
                                where ( query{
                                    for prob in problemSet do
                                        contains(problem.ProblemId)
                                }&& problem.ContestContestId = contest.ContestId && contest.ContestServerContestServerId = contestServer.ContestServerId)
                                select (problem.ProblemId,contestServer.ContestServerId)
                                distinct
                }|> Array.ofSeq
            let isNotValid = 
                (problemSet.Count <> servers.Length)
            eprintfn "Problem length %d" (servers.Length)
            if isNotValid then
                Error "No such problem is found."
            else
                try
                    let ctx = getDataContext()
                    use transaction = new TransactionScope(TransactionScopeOption.RequiresNew,
                                                           TransactionOptions(Timeout=TimeSpan.Zero,IsolationLevel=IsolationLevel.Serializable),
                                                           TransactionScopeAsyncFlowOption.Enabled)
                    let contestElm=ctx.ContestLog.VirtualContest.``Create(createdUser_user_iduser, endTime, startTime)``(creatorInfo.dbId,startTime+duration,startTime)
                    ctx.SubmitUpdates()
                    if not (isNull name) &&  name<>"" then
                        ctx.ContestLog.VirtualContestName.``Create(virtualContestName, virtual_contest_idvirtual_contest)``(name,contestElm.IdvirtualContest)|>ignore
                        ctx.SubmitUpdates()
                    ctx.ContestLog.VirtualContestParticipants.``Create(user_iduser, virtual_contest_idvirtual_contest)``(creatorInfo.dbId,contestElm.IdvirtualContest)|>ignore
                    problems
                        |> Array.map(
                            fun problem ->
                                ctx.ContestLog.VirtualContestProblems.``Create(problem_problemId, virtual_contest_idvirtual_contest)``(problem,contestElm.IdvirtualContest)|>ignore
                        )|>ignore
                    ctx.SubmitUpdates()
                    let ret = 
                        query{
                            for vcontest in ctx.ContestLog.VirtualContest do
                                where (vcontest.IdvirtualContest = contestElm.IdvirtualContest)
                                exactlyOne
                        }
                    let user = getUserInfoFromId 
                    transaction.Complete()
                    let vcontest = 
                        {
                            dbId = contestElm.IdvirtualContest
                            name = name
                            startTime = startTime
                            endTime = startTime + duration
                            creator = creatorInfo
                            participants = [|creatorInfo|]
                        }
                    Ok (vcontest)
                with
                | :? TransactionAbortedException -> 
                    Error "Internal Problem occured"
let createContestNow creatorInfo duration problems =
    createContest creatorInfo (DateTimeOffset.UtcNow.ToUnixTimeSeconds()) (duration) problems