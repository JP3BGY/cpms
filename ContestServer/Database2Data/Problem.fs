module ContestServer.Database2Data.Problem
open System
open ContestServer.Setting
open ContestServer.Types
open Scraper.Submission
open System.Text.RegularExpressions

type ProblemDb =
    {
        ProblemId:int
        ProblemName:string
        ContestServerProblemId:string
        ContestContestId:int
    }
type SubmissionDb =
    {
        ProblemProblemId:int
        ContestUsersUserId:int
        SubmissionTime:int64
        SubmissionStatus:string
        ContestServerSubmissionId:int64
    }
let problem2Url serverName contestId problemId = 
    match serverName with
    | "Codeforces" -> Ok ("https://codeforces.com/contest/"+contestId+"/problem/"+problemId)
    | "AtCoder" -> Ok ("https://atcoder.jp/contests/"+contestId+"/tasks/"+problemId)
    | "TopCoder" -> Error "Not supported"
    | _ -> Error "Not supported"
let (|ParseRegex|_|) regex str =
   let m = Regex(regex).Match(str)
   if m.Success
   then Some (List.tail [ for x in m.Groups -> x.Value ])
   else None
let problemDb2Problem = 
        fun problemDb->
            let ctx = getDataContext()
            let (serverName,contestServerContestId) = 
                query{
                    for server in ctx.ContestLog.ContestServer do
                        for contest in ctx.ContestLog.Contest do
                            where (contest.ContestId = problemDb.ContestContestId && contest.ContestServerContestServerId = server.ContestServerId)
                            select (server.ContestServerName,contest.ContestServerContestId)
                            exactlyOne
                }
            {
                dbId = (problemDb.ProblemId)
                name = (problemDb.ProblemName)
                url = 
                    match problem2Url serverName contestServerContestId (problemDb.ContestServerProblemId) with
                    | Ok x -> x
                    | Error y -> ""
                serverName = serverName
                tags = 
                    query{
                        for tag in ctx.ContestLog.ProblemTag do
                            where (tag.ProblemProblemId = problemDb.ProblemId)
                            select tag.Tag
                    }|>Seq.toArray
                difficulty =
                    query{
                        for diff in ctx.ContestLog.ProblemDifficulty do
                            where (diff.ProblemProblemId = problemDb.ProblemId)
                            select diff.ProblemDifficulty
                    }|> fun x ->
                        if Seq.isEmpty x then
                            None
                        else
                            Some(Seq.head x)
                    |> fun x ->
                        Option.toNullable x
                contestDbId =
                    problemDb.ContestContestId
            }
let getProblemFromProblemId problemId = 
    let ctx = getDataContext()
    query{
        for problem in ctx.ContestLog.Problem do
            where (problem.ProblemId=problemId)
            select problem
            exactlyOne
    }|>fun x->x.MapTo<ProblemDb>()
    |>problemDb2Problem
let getProblemFromContestServerIds contestServerName contestId problemId =
    let ctx=getDataContext()
    query{
        for problem in ctx.ContestLog.Problem do
            for contest in ctx.ContestLog.Contest do
                for cserver in ctx.ContestLog.ContestServer do
                    where (problem.ContestContestId = contest.ContestId
                            && contest.ContestServerContestServerId = cserver.ContestServerId 
                            && cserver.ContestServerName = contestServerName 
                            && problem.ContestServerProblemId = problemId && contest.ContestServerContestId = contestId)
                    select problem
                    exactlyOne
    }|>fun x->x.MapTo<ProblemDb>()
    |>problemDb2Problem
let url2Problem url = 
    match url with
    | ParseRegex "https://atcoder.jp/contests/(.+)/tasks/(.+)" [contestId;problemId] -> Ok(getProblemFromContestServerIds "AtCoder" contestId problemId)
    | ParseRegex "https://(.+).contest.atcoder.jp/tasks/(.+)" [contestId;problemId] -> Ok(getProblemFromContestServerIds "AtCoder" contestId problemId)
    | ParseRegex "https://codeforces.com/contest/(.+)/problem/(.+)" [contestId;problemId] -> Ok(getProblemFromContestServerIds "Codeforces" contestId problemId)
    | ParseRegex "https://codeforces.com/problemset/problem/(.+)/(.+)" [contestId;problemId] -> Ok(getProblemFromContestServerIds "Codeforces" contestId problemId)
    | _ -> Error("Not found")
let getProblems () =
    let ctx = getDataContext()
    query{
        for problem in ctx.ContestLog.Problem do
            join contest in ctx.ContestLog.Contest on (problem.ContestContestId = contest.ContestId)
            sortByDescending contest.ContestStartTime
            thenBy problem.ContestServerProblemId
            select problem
    }|>Seq.map(fun x->x.MapTo<ProblemDb>())
    |>Seq.map(
            problemDb2Problem
    )
let getLimitProblems offset n =
    let ctx = getDataContext()
    query{
        for problem in ctx.ContestLog.Problem do
            join contest in ctx.ContestLog.Contest on (problem.ContestContestId = contest.ContestId)
            sortByDescending contest.ContestStartTime
            thenBy problem.ContestServerProblemId
            skip offset
            take n
            select problem
    }|>Seq.map(fun x->x.MapTo<ProblemDb>())
    |>Seq.map(
            problemDb2Problem
    )

let getProblemsOfContest contestId =
    let ctx = getDataContext()
    query{
        for problem in ctx.ContestLog.Problem do
            where (problem.ContestContestId = contestId)
            sortBy problem.ContestServerProblemId
            select problem
    }|>Seq.map(fun x->x.MapTo<ProblemDb>())
    |>Seq.map(
        problemDb2Problem
    )

let submission2Url (submissionId:Int64) contestId contestServerName =
    match contestServerName with
    | "Codeforces" -> 
        "https://codeforces.com/contest/"+(contestId)+"/submission/"+(string submissionId)
    | "AtCoder" ->
        "https://atcoder.jp/contests/"+contestId+"/submissions/"+(string submissionId)
    | _ ->
        ""
let submissionDb2SubmissionWithUSC submissionDb userId serverName contestId= 
    {
        problemId = submissionDb.ProblemProblemId
        userId = userId
        submissionTime = submissionDb.SubmissionTime
        submissionStatus = submissionDb.SubmissionStatus
        url = submission2Url (submissionDb.ContestServerSubmissionId) contestId serverName
    }
let submissionDb2SubmissionWithU submissionDb userId =
    let ctx = getDataContext()
    let contestId = 
        query{
            for problem in ctx.ContestLog.Problem do
                join contest in ctx.ContestLog.Contest on (problem.ContestContestId = contest.ContestId)
                where (problem.ProblemId = submissionDb.ProblemProblemId )
                select (contest)
                exactlyOne
        }
    let serverName =
        query{
            for cserver in ctx.ContestLog.ContestServer do
                where(cserver.ContestServerId = contestId.ContestServerContestServerId)
                select (cserver)
                exactlyOne
        }
    submissionDb2SubmissionWithUSC submissionDb userId (serverName.ContestServerName) (contestId.ContestServerContestId)
let getSolverOfProblem problemId userId = 
    let ctx = getDataContext()
    let elms = 
        query{
            for problem in ctx.ContestLog.Problem do
                join contest in ctx.ContestLog.Contest on (problem.ContestContestId = contest.ContestId)
                join contestServer in ctx.ContestLog.ContestServer on (contest.ContestServerContestServerId = contestServer.ContestServerId)
                select (contestServer.ContestServerName,contest.ContestServerContestId)
        }
    if Seq.isEmpty elms then
        Seq.empty
    else
        let (serverName,contestId) = Seq.head elms
        query{
            for wuser in ctx.ContestLog.WatchingUser do
                for submission in ctx.ContestLog.ProblemSubmissions do
                    where (wuser.UserIduser=userId && wuser.ContestUsersUserId = submission.ContestUsersUserId && submission.ProblemProblemId = problemId)
                    sortByDescending submission.SubmissionTime
                    select (submission)
        }|>Seq.map(fun x->x.MapTo<SubmissionDb>())
        |>Seq.map(fun x -> submissionDb2SubmissionWithUSC x userId serverName contestId)
