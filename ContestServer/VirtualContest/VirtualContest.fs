module ContestServer.VirtualContest
open System.Transactions
open System.Collections.Generic
open System
open ContestServer.Database2Data.Problem
open ContestServer.Setting
open ContestServer.Database2Data.UserInfo
open FSharp.Data.Sql
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
type VContest =
    {
        dbId:Int32
        startTime:Int64
        endTime:Int64
        participants:UserInfo []
        creator:UserInfo
    }
let getProblemsOfVirtualContest dbId (userInfo:UserInfo) =
    let nowUnixTime = DateTimeOffset.Now.ToUnixTimeSeconds()
    let ctx = getDataContext()
    let elm = 
        query{
            for vcon in ctx.ContestLog.VirtualContest do
                where (vcon.IdvirtualContest = dbId && (vcon.CreatedUserUserIduser = userInfo.dbId || vcon.StartTime >= nowUnixTime))
        }
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
        Ok(problems,submissions)

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
    }
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
let createContest (creatorInfo:UserInfo) startTime duration (problems:int[]) = 
    let nowTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
    if nowTime >= startTime+duration then
        Error "This contest already ended."
    else 
        if duration < 5L*60L || startTime+duration - nowTime < 5L*60L then
            Error "There is little time left."
        else
            let ctx = getDataContext()
            let problemSet = Set.ofArray problems
            let servers = 
                query{
                    for problem in ctx.ContestLog.Problem do
                        for contest in ctx.ContestLog.Contest do
                            for contestServer in ctx.ContestLog.ContestServer do
                                where (problemSet.Contains(problem.ProblemId) && problem.ContestContestId = contest.ContestId && contest.ContestServerContestServerId = contestServer.ContestServerId)
                                select (problem.ProblemId,contestServer.ContestServerId)
                }|> Array.ofSeq
            let isNotValid = 
                problemSet.Count = servers.Length
            if isNotValid then
                Error "No such problem is found."
            else
                try
                    let ctx = getDataContext()
                    let transactionopt = TransactionOptions(Timeout=TimeSpan.Zero,IsolationLevel=IsolationLevel.Serializable)
                    use transaction = new TransactionScope(TransactionScopeOption.RequiresNew,
                                                           transactionopt,
                                                           TransactionScopeAsyncFlowOption.Enabled)
                    let contestElm=ctx.ContestLog.VirtualContest.``Create(createdUser_user_iduser, endTime, startTime)``(creatorInfo.dbId,startTime+duration,startTime)
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